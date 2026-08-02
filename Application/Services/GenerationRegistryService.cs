using System.Collections.Concurrent;
using Application.Contracts.Features.Chats;
using Application.Contracts.Features.Chats.Responses;
using Application.Contracts.Models;
using Application.Contracts.Services;
using Application.Features.Agents;
using Application.Features.Chats;
using Assistant.Contracts;
using Assistant.Contracts.Models;
using Domain.Entities.Conversations;
using Domain.Entities.Conversations.Parameters;
using Domain.Entities.Messages;
using Domain.Enums;
using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Contracts;

namespace Application.Services;

/// <summary>
/// Держит реестр активных генераций и гоняет сам пайплайн ответа ассистента: достаёт историю чата,
/// стримит ответ агента в <see cref="IChatNotifier"/>, сохраняет итоговое сообщение и, если это
/// первый ответ в чате, просит агента придумать название.
///
/// Живёт синглтоном и работает вне HTTP-запроса, поэтому берёт свой <see cref="IDbContext"/>
/// у фабрики, а не из scope.
/// </summary>
internal sealed class GenerationRegistryService(
    IDbContextFactory dbFactory,
    IAssistantAgent assistantAgent,
    IChatNotifier chatNotifier,
    ILogger<GenerationRegistryService> logger) : IGenerationRegistryService
{
    private const string GenerationFailedMessage = "Не удалось получить ответ ассистента.";
    private const string EmptyAnswerMessage = "Модель вернула пустой ответ.";
    private const string GenerationStoppedMessage = "Генерация остановлена.";

    private readonly ConcurrentDictionary<Guid, ActiveGeneration> _generations = new();

    public ActiveGeneration? GetGeneration(Guid conversationId)
    {
        return _generations.GetValueOrDefault(conversationId);
    }

    public bool IsGenerationActive(Guid conversationId)
    {
        return _generations.ContainsKey(conversationId);
    }

    public bool StopGeneration(Guid conversationId)
    {
        var generation = GetGeneration(conversationId);
        if (generation is null)
            return false;

        logger.LogInformation("Генерация для чата {ConversationId} остановлена пользователем.", conversationId);

        // Сам пайплайн ловит отмену, сохраняет уже сгенерированный кусок и убирает себя из реестра.
        generation.CancellationTokenSource.Cancel();

        return true;
    }

    public async Task StartGeneration(StartGenerationParameters parameters)
    {
        if (!AddGeneration(parameters))
            return;

        var conversationId = parameters.ConversationId;
        var activeGeneration = GetGeneration(conversationId)!;

        IDbContext? context = null;

        try
        {
            context = await dbFactory.CreateDbContextAsync();
            var conversation = await GetConversationAsync(conversationId, context);

            await GenerateAsync(conversation, context, activeGeneration);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Произошла ошибка при генерации сообщения для чата {ConversationId}.",
                conversationId);
        }
        finally
        {
            RemoveGeneration(conversationId);
            activeGeneration.CancellationTokenSource.Dispose();

            await DisposeContextAsync(context);
        }
    }

    /// <summary>
    /// Один проход генерации: стрим ответа в клиент, сохранение сообщения, название чата.
    /// </summary>
    private async Task GenerateAsync(
        Conversation conversation,
        IDbContext context,
        ActiveGeneration activeGeneration)
    {
        var conversationId = conversation.Id;
        var cancellationToken = activeGeneration.CancellationTokenSource.Token;

        var messages = conversation.Messages.OrderBy(message => message.CreatedAt).ToArray();
        var lastUserMessage = messages.LastOrDefault(message => message.RoleEnum == MessageRoleEnum.User);

        if (lastUserMessage is null)
        {
            logger.LogWarning(
                "Генерация для чата {ConversationId} запущена, но сообщений пользователя в нём нет.",
                conversationId);
            return;
        }

        // Последнее сообщение пользователя уходит агенту отдельным параметром — история идёт до него.
        var history = messages
            .Where(message => message.Id != lastUserMessage.Id)
            .Select(ToAssistantMessage)
            .ToArray();

        var isFirstAnswer = messages.All(message => message.RoleEnum != MessageRoleEnum.Assistant);

        // Id ответа генерируем заранее: фронт ждёт его в messageStarted, чтобы понимать,
        // к какому сообщению клеить дельты.
        var messageId = Guid.CreateVersion7();
        await chatNotifier.MessageStartedAsync(conversationId, messageId, CancellationToken.None);

        var wasCancelled = false;

        // Запоминаем до прогона: в catch поле уже могло быть перезаписано новым состоянием.
        var hadAgentState = !string.IsNullOrEmpty(conversation.AgentState);

        // Считаем, что именно прислал агент. Нужно ровно для одного случая: если текста не пришло,
        // без этой раскладки непонятно, ответила модель рассуждениями, вызовом инструмента
        // или вообще ничем.
        var updateKinds = new Dictionary<AssistantUpdateKindEnum, int>();
        var reasoningLength = 0;

        try
        {
            // Состояние сессии агента (сжатая история, todo-лист, режим, подтверждения) уезжает
            // в агента и возвращается обновлённым последним событием стрима. История из БД —
            // запасной вариант на первый ход и на случай, если состояние не прочиталось.
            var request = new AssistantRunRequest
            {
                UserText = lastUserMessage.Text,
                History = history,
                SessionState = conversation.AgentState,
                Agent = await GetAgentDefinitionAsync(conversation, context),
                UserId = activeGeneration.UserId,
                ConversationId = conversationId,
            };

            var updates = assistantAgent.RunStreamingAsync(request, cancellationToken);

            await foreach (var update in updates.WithCancellation(cancellationToken))
            {
                updateKinds[update.Kind] = updateKinds.GetValueOrDefault(update.Kind) + 1;

                if (update.Kind == AssistantUpdateKindEnum.Reasoning)
                    reasoningLength += update.Text?.Length ?? 0;

                if (update.Kind == AssistantUpdateKindEnum.SessionState)
                {
                    conversation.SaveAgentState(update.Text);
                    continue;
                }

                await HandleUpdateAsync(update, conversationId, messageId, activeGeneration);
            }
        }
        catch (OperationCanceledException)
        {
            // Пользователь нажал «стоп» — это не ошибка, сохраняем то, что успели сгенерировать.
            wasCancelled = true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Агент упал при генерации ответа для чата {ConversationId}.", conversationId);

            // Сбрасываем состояние сессии агента. Если упало именно на нём — например, модель
            // не приняла восстановленную историю — то без сброса чат превращается в кирпич:
            // каждая следующая попытка подсовывала бы агенту то же самое состояние и падала так же.
            // Ценой потери сжатия и todo-листа следующий заход соберёт сессию из истории в БД.
            if (hadAgentState)
            {
                logger.LogWarning(
                    "Сбрасываем состояние сессии агента для чата {ConversationId} — следующая попытка "
                    + "начнётся с истории переписки.",
                    conversationId);

                conversation.ResetAgentState();
            }

            await FailAsync(conversation, context, messageId, GenerationFailedMessage);
            return;
        }

        var text = activeGeneration.Message.ToString().Trim();

        if (text.Length == 0)
        {
            if (wasCancelled)
            {
                // Остановили до первого токена — сообщение сохранять нечего, но состояние сессии
                // агента уже могло измениться, и его терять не хочется.
                await context.SaveChangesAsync(CancellationToken.None);

                await chatNotifier.MessageFailedAsync(
                    conversationId,
                    messageId,
                    GenerationStoppedMessage,
                    CancellationToken.None);
                return;
            }

            logger.LogWarning(
                "Агент вернул пустой ответ для чата {ConversationId}. Пришло из стрима: {Updates}; "
                + "рассуждений — {ReasoningLength} символов; вопрос пользователя: {UserText}.",
                conversationId,
                updateKinds.Count == 0
                    ? "ничего"
                    : string.Join(", ", updateKinds.Select(pair => $"{pair.Key} x{pair.Value}")),
                reasoningLength,
                lastUserMessage.Text);

            await FailAsync(conversation, context, messageId, EmptyAnswerMessage);
            return;
        }

        var message = conversation.AddMessage(new AddMessageParameter
        {
            Id = messageId,
            RoleEnum = MessageRoleEnum.Assistant,
            Text = text,
        });

        await context.SaveChangesAsync(CancellationToken.None);
        await chatNotifier.MessageCompletedAsync(conversationId, message.ToResponse(), CancellationToken.None);

        if (isFirstAnswer && !wasCancelled)
            StartTitleGeneration(conversationId, lastUserMessage.Text);
    }

    private async Task HandleUpdateAsync(
        AssistantStreamUpdate update,
        Guid conversationId,
        Guid messageId,
        ActiveGeneration activeGeneration)
    {
        switch (update.Kind)
        {
            case AssistantUpdateKindEnum.Text when !string.IsNullOrEmpty(update.Text):
                activeGeneration.AddStreamMessageChunk(update.Text);

                // Токен отмены сюда не передаём: дельту надо доставить, даже если генерацию уже гасят.
                await chatNotifier.MessageDeltaAsync(conversationId, messageId, update.Text, CancellationToken.None);
                break;

            case AssistantUpdateKindEnum.ToolCall when update is { ToolName: not null, CallId: not null }:
                await NotifyToolCallStartedAsync(update, conversationId, messageId, activeGeneration);
                break;

            case AssistantUpdateKindEnum.ToolResult when update.CallId is not null:
                await NotifyToolCallCompletedAsync(update, conversationId, messageId, activeGeneration);
                break;

            case AssistantUpdateKindEnum.ApprovalRequired when update.ToolName is not null:
                // Агент упёрся в инструмент, требующий подтверждения, и дальше не пойдёт, пока
                // ему не вернут ToolApprovalResponseContent. Пробрасывания ответа через UI пока нет,
                // поэтому здесь только запись в лог — включать Assistant:Approvals без него не нужно.
                logger.LogWarning(
                    "Чат {ConversationId}: агент ждёт подтверждения на вызов инструмента {ToolName}, "
                    + "но отвечать на подтверждения приложение пока не умеет.",
                    conversationId,
                    update.ToolName);
                break;

            case AssistantUpdateKindEnum.Error when update.Error is not null:
                logger.LogWarning(
                    "Агент прислал ошибку при генерации для чата {ConversationId}: {Error}.",
                    conversationId,
                    update.Error);
                break;

            case AssistantUpdateKindEnum.Usage when update.Usage is not null:
                logger.LogInformation(
                    "Чат {ConversationId}: потрачено токенов — вход {InputTokens}, выход {OutputTokens}.",
                    conversationId,
                    update.Usage.InputTokens,
                    update.Usage.OutputTokens);
                break;

            case AssistantUpdateKindEnum.Reasoning:
            default:
                // Рассуждения модели пользователю пока не показываем.
                break;
        }
    }

    /// <summary>
    /// Сообщает клиенту, что агент полез в инструмент. Событие идёт до того, как инструмент
    /// отработает, — именно эта пауза и выглядит в UI зависанием.
    /// </summary>
    private async Task NotifyToolCallStartedAsync(
        AssistantStreamUpdate update,
        Guid conversationId,
        Guid messageId,
        ActiveGeneration activeGeneration)
    {
        var toolCall = new PipelineCallingInformation
        {
            Id = Guid.CreateVersion7(),
            CallId = update.CallId!,
            CallingName = update.ToolName!,
            CallingInformation = update.ToolArguments,
        };

        // Модель может прислать один и тот же вызов несколькими кусками — дублировать событие не надо.
        if (!activeGeneration.TryAddToolCall(toolCall))
            return;

        logger.LogInformation(
            "Чат {ConversationId}: агент вызывает инструмент {ToolName}.",
            conversationId,
            toolCall.CallingName);

        await chatNotifier.ToolCallStartedAsync(
            conversationId,
            messageId,
            new ToolCallResponse
            {
                Id = toolCall.Id,
                Name = toolCall.CallingName,
                Arguments = toolCall.CallingInformation,
            },
            CancellationToken.None);
    }

    private async Task NotifyToolCallCompletedAsync(
        AssistantStreamUpdate update,
        Guid conversationId,
        Guid messageId,
        ActiveGeneration activeGeneration)
    {
        // Результат без начала — например, вызов пришёл ещё до того, как клиент подписался.
        // Показывать нечего.
        var toolCall = activeGeneration.FindToolCall(update.CallId!);
        if (toolCall is null)
            return;

        if (update.Error is not null)
        {
            logger.LogWarning(
                "Чат {ConversationId}: инструмент {ToolName} вернул ошибку: {Error}.",
                conversationId,
                toolCall.CallingName,
                update.Error);
        }

        await chatNotifier.ToolCallCompletedAsync(
            conversationId,
            messageId,
            toolCall.Id,
            update.Error,
            CancellationToken.None);
    }

    /// <summary>
    /// Помечает чат ошибкой и сообщает клиенту, что ответа не будет.
    /// </summary>
    private async Task FailAsync(Conversation conversation, IDbContext context, Guid messageId, string error)
    {
        conversation.SetError(error);

        await context.SaveChangesAsync(CancellationToken.None);
        await chatNotifier.MessageFailedAsync(conversation.Id, messageId, error, CancellationToken.None);
    }

    /// <summary>
    /// Название чата придумывается уже после того, как ответ доехал до пользователя, и в отрыве от
    /// генерации: на локальной модели это ещё десятки секунд, и всё это время чат был бы занят.
    /// </summary>
    private void StartTitleGeneration(Guid conversationId, string userText)
    {
        _ = Task.Run(
            async () =>
            {
                IDbContext? context = null;

                try
                {
                    logger.LogInformation("Придумываем название для чата {ConversationId}.", conversationId);

                    var title = await assistantAgent.GenerateTitleAsync(userText);

                    context = await dbFactory.CreateDbContextAsync();
                    var conversation = await GetConversationAsync(conversationId, context);

                    conversation.Rename(title);

                    await context.SaveChangesAsync(CancellationToken.None);
                    await chatNotifier.ChatRenamedAsync(conversationId, conversation.Title, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // Название необязательное: чат просто останется с тем, что дал пользователь.
                    logger.LogWarning(
                        exception,
                        "Не удалось сгенерировать название для чата {ConversationId}.",
                        conversationId);
                }
                finally
                {
                    await DisposeContextAsync(context);
                }
            },
            CancellationToken.None);
    }

    private bool AddGeneration(StartGenerationParameters parameters)
    {
        return _generations.TryAdd(parameters.ConversationId,
            new ActiveGeneration(parameters.ConversationId, parameters.UserId));
    }

    private bool RemoveGeneration(Guid conversationId)
    {
        return _generations.TryRemove(conversationId, out _);
    }

    private static AssistantMessage ToAssistantMessage(Message message) => new()
    {
        RoleEnum = message.RoleEnum switch
        {
            MessageRoleEnum.System => AssistantRoleEnum.System,
            MessageRoleEnum.User => AssistantRoleEnum.User,
            MessageRoleEnum.Assistant => AssistantRoleEnum.Assistant,
            MessageRoleEnum.Tool => AssistantRoleEnum.Tool,
            _ => AssistantRoleEnum.User,
        },
        Text = message.Text,
    };

    /// <summary>
    /// Достаёт настройки агента, назначенного чату. Пусто — отвечает встроенный агент.
    /// Агента могли удалить, пока чат жил: внешний ключ обнулится сам, но перестраховываемся.
    /// </summary>
    private async Task<AssistantAgentDefinition?> GetAgentDefinitionAsync(
        Conversation conversation,
        IDbContext context)
    {
        if (conversation.AgentId is not { } agentId)
            return null;

        var agent = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == agentId, CancellationToken.None);

        if (agent is not null)
            return agent.ToDefinition();

        logger.LogWarning(
            "Чат {ConversationId} ссылается на несуществующего агента {AgentId} — отвечает встроенный.",
            conversation.Id,
            agentId);

        return null;
    }

    private static async Task<Conversation> GetConversationAsync(Guid conversationId, IDbContext context)
    {
        return await context.Conversations
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.Id == conversationId)
            .SingleAsync(CancellationToken.None);
    }

    private static async Task DisposeContextAsync(IDbContext? context)
    {
        switch (context)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}

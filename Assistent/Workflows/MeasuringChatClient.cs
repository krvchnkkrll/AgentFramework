using Microsoft.Extensions.AI;

namespace Assistant.Workflows;

/// <summary>
/// Прослойка над <see cref="IChatClient"/>, которая засекает каждое обращение к модели.
///
/// Без неё в отчёте видно только «подагент отработал за 80 секунд», и непонятно, это один
/// долгий запрос или три быстрых. А ответ на вопрос «почему так медленно» почти всегда
/// звучит как «модель дёрнули N раз», и N важнее длительности каждого вызова.
/// </summary>
internal sealed class MeasuringChatClient(IChatClient innerClient, WorkflowTrace trace, string agentName)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = trace.ElapsedMs;

        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);

            trace.Add(
                $"{agentName}: запрос к модели",
                WorkflowStageKinds.Model,
                startedAt,
                Describe(response));

            return response;
        }
        catch (Exception exception)
        {
            trace.Add($"{agentName}: запрос к модели", WorkflowStageKinds.Model, startedAt, error: exception.Message);
            throw;
        }
    }

    /// <summary>
    /// Описание ответа для отчёта. Вызовы инструментов важнее текста: именно по ним видно,
    /// выдала модель оба вызова за один ход (тогда они уйдут параллельно) или по одному.
    /// </summary>
    private static string Describe(ChatResponse response)
    {
        var toolCalls = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .Select(call => call.Name)
            .ToList();

        var text = response.Text?.Length ?? 0;

        return toolCalls.Count > 0
            ? $"вызовы инструментов: {string.Join(", ", toolCalls)}; текста {text} симв."
            : $"текста {text} симв.";
    }
}

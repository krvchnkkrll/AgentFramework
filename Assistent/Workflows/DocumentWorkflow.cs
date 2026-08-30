using Assistant.Contracts.Workflows;
using Assistant.Options;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Assistant.Workflows;

/// <summary>
/// Стенд для замера мультиагентного сценария. Один и тот же результат добывается тремя способами,
/// и разница между ними — это и есть то, что мы меряем.
///
/// <list type="number">
/// <item><b>DirectApi</b> — ни одного обращения к модели. Нижняя граница: два параллельных вызова
/// «внешних сервисов» и шаблонная склейка. Всё, что медленнее, — плата за участие модели.</item>
/// <item><b>AgentTools</b> — главный агент получает подагентов инструментами. Параллельность
/// возможна, но не гарантирована: она случится, только если модель выдаст оба вызова за один ход.
/// Это ровно тот сценарий, который обычно и называют мультиагентностью.</item>
/// <item><b>Workflow</b> — граф с fan-out и fan-in барьером. Параллельность записана в топологию,
/// решение модели на неё не влияет. Зато и гибкости никакой: кого звать, решено заранее.</item>
/// </list>
/// </summary>
public sealed class DocumentWorkflow(
    IChatClient chatClient,
    CorporateApi api,
    IOptions<AssistantOptions> options,
    ILoggerFactory loggerFactory) : IDocumentWorkflow
{
    private readonly AssistantOptions _options = options.Value;
    private readonly ILogger<DocumentWorkflow> _logger = loggerFactory.CreateLogger<DocumentWorkflow>();

    public async Task<DocumentWorkflowResult> RunAsync(
        DocumentWorkflowRequest request,
        DocumentWorkflowModeEnum mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trace = new WorkflowTrace();
        var agents = new DocumentWorkflowAgents(chatClient, api, _options, loggerFactory, trace);

        var text = string.Empty;
        string? error = null;

        _logger.LogInformation("Стенд документов: старт прогона в режиме {Mode}.", mode);

        try
        {
            text = mode switch
            {
                DocumentWorkflowModeEnum.DirectApi => await RunDirectAsync(request, trace, cancellationToken),
                DocumentWorkflowModeEnum.AgentTools => await RunWithAgentToolsAsync(request, agents, cancellationToken),
                DocumentWorkflowModeEnum.Workflow => await RunWithWorkflowAsync(request, agents, trace, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Неизвестный режим прогона."),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Прогон упал — но замеры этапов, которые успели отработать, всё равно ценны:
            // по ним видно, на чём именно всё встало.
            _logger.LogError(exception, "Стенд документов: прогон в режиме {Mode} упал.", mode);
            error = exception.Message;
        }

        var result = new DocumentWorkflowResult
        {
            Mode = mode,
            TotalMs = Math.Round(trace.ElapsedMs, 1),
            ModelCalls = trace.CountOf(WorkflowStageKinds.Model),
            ModelMs = trace.TotalOf(WorkflowStageKinds.Model),
            ApiMs = trace.TotalOf(WorkflowStageKinds.Api),
            ApiParallelSavingMs = trace.ParallelSaving(WorkflowStageKinds.Api),
            Stages = trace.Stages,
            Text = text,
            Error = error,
        };

        _logger.LogInformation(
            "Стенд документов: режим {Mode} отработал за {Total} мс, обращений к модели {Calls} "
            + "на {ModelMs} мс, к API {ApiMs} мс (параллельность сэкономила {Saving} мс).",
            mode,
            result.TotalMs,
            result.ModelCalls,
            result.ModelMs,
            result.ApiMs,
            result.ApiParallelSavingMs);

        return result;
    }

    public string GetDiagram() => WorkflowVisualizer.ToMermaidString(BuildWorkflow(agents: null, trace: null));

    // -------------------------------------------------------------------------
    // Режим 1: без модели
    // -------------------------------------------------------------------------

    /// <summary>
    /// Оба сервиса дёргаются одновременно, поэтому весь прогон стоит одной задержки, а не двух.
    /// Это тот минимум, ниже которого не опустится ни один из остальных режимов.
    /// </summary>
    private async Task<string> RunDirectAsync(
        DocumentWorkflowRequest request,
        WorkflowTrace trace,
        CancellationToken cancellationToken)
    {
        var employeesTask = trace.MeasureAsync(
            "Оргструктура: поиск сотрудников",
            WorkflowStageKinds.Api,
            token => api.FindEmployeesAsync(request.EmployeeNames, token),
            result => $"{result.Length} симв.",
            cancellationToken);

        var templateTask = trace.MeasureAsync(
            "ЭДО: шаблон документа",
            WorkflowStageKinds.Api,
            token => api.GetTemplateAsync(request.DocumentType, token),
            result => $"{result.Length} симв.",
            cancellationToken);

        await Task.WhenAll(employeesTask, templateTask);

        return string.Join(
            "\n\n",
            $"Тип документа: {request.DocumentType}",
            $"Тема: {request.Subject ?? "не указана"}",
            await employeesTask,
            await templateTask);
    }

    // -------------------------------------------------------------------------
    // Режим 2: подагенты как инструменты
    // -------------------------------------------------------------------------

    private async Task<string> RunWithAgentToolsAsync(
        DocumentWorkflowRequest request,
        DocumentWorkflowAgents agents,
        CancellationToken cancellationToken)
    {
        var orchestrator = agents.CreateOrchestratorAgent(
            agents.CreateEmployeeAgent(),
            agents.CreateTemplateAgent());

        var response = await orchestrator.RunAsync(
            BuildOrchestratorPrompt(request),
            session: null,
            options: null,
            cancellationToken);

        return response.Text ?? string.Empty;
    }

    // -------------------------------------------------------------------------
    // Режим 3: граф
    // -------------------------------------------------------------------------

    private async Task<string> RunWithWorkflowAsync(
        DocumentWorkflowRequest request,
        DocumentWorkflowAgents agents,
        WorkflowTrace trace,
        CancellationToken cancellationToken)
    {
        var workflow = BuildWorkflow(agents, trace);
        var run = await InProcessExecution.Default.RunAsync(workflow, request, cancellationToken: cancellationToken);

        var failure = run.OutgoingEvents.OfType<WorkflowErrorEvent>().FirstOrDefault();

        if (failure is not null)
            throw new InvalidOperationException($"Граф упал: {failure.Data}");

        var output = run.OutgoingEvents
            .OfType<WorkflowOutputEvent>()
            .Select(item => item.Data as string)
            .LastOrDefault(item => !string.IsNullOrWhiteSpace(item));

        return output ?? string.Empty;
    }

    /// <summary>
    /// Топология: приёмщик заявки раздаёт её обоим подагентам сразу (fan-out), барьер придерживает
    /// их ответы, пока не придут оба, и отдаёт составителю (fan-in).
    ///
    /// Барьер отдаёт сообщения по одному, а не пачкой, поэтому составитель копит их сам —
    /// см. <see cref="ComposeExecutor"/>.
    ///
    /// <paramref name="agents"/> и <paramref name="trace"/> опциональны: без них собирается
    /// та же топология с пустыми обработчиками — этого достаточно, чтобы нарисовать схему.
    /// </summary>
    private Workflow BuildWorkflow(DocumentWorkflowAgents? agents, WorkflowTrace? trace)
    {
        var dispatch = new FunctionExecutor<DocumentWorkflowRequest>(
            "Приём заявки",
            async (input, context, token) => await context.SendMessageAsync(input, cancellationToken: token),
            null,
            [typeof(DocumentWorkflowRequest)]);

        var employees = CreateStageExecutor(
            "Поиск сотрудников",
            agents?.CreateEmployeeAgent(),
            trace,
            request => $"Найди сотрудников: {string.Join(", ", request.EmployeeNames)}.");

        var template = CreateStageExecutor(
            "Поиск шаблона",
            agents?.CreateTemplateAgent(),
            trace,
            request => $"Дай шаблон документа типа «{request.DocumentType}».");

        var compose = new ComposeExecutor(
            "Составление документа",
            agents?.CreateComposerAgent(),
            trace,
            expectedStages: 2);

        return new WorkflowBuilder(dispatch)
            .AddFanOutEdge(dispatch, [employees, template], "заявка")
            .AddFanInBarrierEdge([employees, template], compose, "собранные данные")
            .WithOutputFrom(compose)
            .Build();
    }

    /// <summary>Узел графа, за которым стоит один подагент.</summary>
    private static FunctionExecutor<DocumentWorkflowRequest> CreateStageExecutor(
        string name,
        AIAgent? agent,
        WorkflowTrace? trace,
        Func<DocumentWorkflowRequest, string> buildPrompt) =>
        new(
            name,
            async (request, context, token) =>
            {
                if (agent is null || trace is null)
                    return;

                var text = await trace.MeasureAsync(
                    $"Подагент «{name}»",
                    WorkflowStageKinds.Agent,
                    async innerToken =>
                    {
                        var response = await agent.RunAsync(buildPrompt(request), session: null, options: null, innerToken);

                        return response.Text ?? string.Empty;
                    },
                    result => $"{result.Length} симв.",
                    token);

                await context.SendMessageAsync(new WorkflowStageMessage(name, text, request), cancellationToken: token);
            },
            null,
            [typeof(WorkflowStageMessage)]);

    // -------------------------------------------------------------------------
    // Промпты
    // -------------------------------------------------------------------------

    private static string BuildOrchestratorPrompt(DocumentWorkflowRequest request) => string.Join(
        "\n",
        $"Подготовь документ типа «{request.DocumentType}».",
        $"Тема: {request.Subject ?? "не указана"}.",
        $"В документе должны фигурировать сотрудники: {string.Join(", ", request.EmployeeNames)}.");

    internal static string BuildComposerPrompt(
        DocumentWorkflowRequest request,
        IReadOnlyList<WorkflowStageMessage> stages) => string.Join(
        "\n\n",
        [
            $"Тип документа: {request.DocumentType}.",
            $"Тема: {request.Subject ?? "не указана"}.",
            .. stages.Select(stage => $"### {stage.Stage}\n{stage.Text}"),
            "Собери готовый текст документа.",
        ]);
}

/// <summary>Ответ одного подагента, который едет по графу к составителю.</summary>
public sealed record WorkflowStageMessage(string Stage, string Text, DocumentWorkflowRequest Request);

/// <summary>
/// Составитель на выходе графа. Барьер отдаёт ответы подагентов по одному, поэтому узел копит их
/// сам и запускает модель, только когда собрал все.
///
/// Копим в состоянии прогона, а не в поле объекта: экземпляр узла один на все прогоны графа,
/// а состояние прогона у каждого своё — иначе два одновременных прогона склеили бы свои данные.
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class ComposeExecutor(string id, AIAgent? agent, WorkflowTrace? trace, int expectedStages)
    : Executor<WorkflowStageMessage>(id)
{
    private const string StateKey = "stages";

    public override async ValueTask HandleAsync(
        WorkflowStageMessage message,
        IWorkflowContext context,
        CancellationToken cancellationToken)
    {
        var collected = await context.ReadStateAsync<List<WorkflowStageMessage>>(StateKey, cancellationToken) ?? [];

        collected.Add(message);

        await context.QueueStateUpdateAsync(StateKey, collected, cancellationToken);

        if (collected.Count < expectedStages || agent is null || trace is null)
            return;

        var prompt = DocumentWorkflow.BuildComposerPrompt(message.Request, collected);

        var text = await trace.MeasureAsync(
            "Агент «составление документа»",
            WorkflowStageKinds.Agent,
            async token =>
            {
                var response = await agent.RunAsync(prompt, session: null, options: null, token);

                return response.Text ?? string.Empty;
            },
            result => $"{result.Length} симв.",
            cancellationToken);

        await context.YieldOutputAsync(text, cancellationToken);
    }
}

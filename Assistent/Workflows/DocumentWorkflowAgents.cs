using System.ComponentModel;
using Assistant.Agents;
using Assistant.Contracts.Workflows;
using Assistant.Options;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Assistant.Workflows;

/// <summary>
/// Собирает трёх агентов стенда. Экземпляр создаётся на каждый прогон, потому что все инструменты
/// замыкаются на <see cref="WorkflowTrace"/> этого прогона — иначе замеры двух одновременных
/// прогонов перемешались бы. Сборка агента стоит доли миллисекунды, кэшировать нечего.
///
/// Агенты:
/// 1. поиск сотрудников — умеет один инструмент, ходящий в «оргструктуру»;
/// 2. поиск шаблона — умеет один инструмент, ходящий в «ЭДО»;
/// 3. составитель — инструментов не имеет вовсе, только пишет текст по тому, что ему принесли.
/// </summary>
internal sealed class DocumentWorkflowAgents(
    IChatClient chatClient,
    CorporateApi api,
    AssistantOptions options,
    ILoggerFactory loggerFactory,
    WorkflowTrace trace)
{
    private WorkflowOptions Workflow => options.Workflow;

    // -------------------------------------------------------------------------
    // Подагенты
    // -------------------------------------------------------------------------

    public AIAgent CreateEmployeeAgent() => CreateAgent(
        id: "workflow-employee-finder",
        name: "EmployeeFinder",
        description: "Ищет сотрудников в организационной структуре компании.",
        instructions: """
            Ты агент поиска сотрудников в организационной структуре компании.
            Тебе дают список имён или фамилий. Найди каждого инструментом directory_find_employees.
            Всегда вызывай инструмент: собственных данных о сотрудниках у тебя нет, выдумывать их запрещено.
            Ответ — только список найденных: ФИО, должность, подразделение, почта, табельный номер.
            Никаких вступлений, пояснений и выводов.
            """,
        maxOutputTokens: Workflow.SubAgentMaxOutputTokens,
        tools: [CreateDirectoryTool()]);

    public AIAgent CreateTemplateAgent() => CreateAgent(
        id: "workflow-template-finder",
        name: "TemplateFinder",
        description: "Забирает шаблон документа нужного типа из системы электронного документооборота.",
        instructions: """
            Ты агент, который забирает шаблоны документов из системы электронного документооборота.
            Тебе дают тип документа. Получи его шаблон инструментом edo_get_template.
            Всегда вызывай инструмент: шаблонов ты не знаешь, придумывать их запрещено.
            Ответ — только структура шаблона, как её вернул инструмент.
            Никаких вступлений, пояснений и выводов.
            """,
        maxOutputTokens: Workflow.SubAgentMaxOutputTokens,
        tools: [CreateTemplateTool()]);

    /// <summary>Составитель для режима <see cref="DocumentWorkflowModeEnum.Workflow"/>: данные ему приносит граф.</summary>
    public AIAgent CreateComposerAgent() => CreateAgent(
        id: "workflow-composer",
        name: "DocumentComposer",
        description: "Собирает готовый текст документа из шаблона и данных о сотрудниках.",
        instructions: """
            Ты агент-составитель документов.
            Тебе дают тип документа, тему, данные сотрудников из оргструктуры и структуру шаблона из ЭДО.
            Собери готовый текст документа по этой структуре, подставив реальные ФИО и должности.
            Стиль официально-деловой. Не выдумывай ни сотрудников, ни разделы, которых нет во входных данных.
            Ответ — только текст документа.
            """,
        maxOutputTokens: Workflow.ComposerMaxOutputTokens,
        tools: []);

    /// <summary>
    /// Главный агент режима <see cref="DocumentWorkflowModeEnum.AgentTools"/>: те же два подагента,
    /// но отданные ему инструментами. Кого и когда звать, решает модель — в этом весь смысл режима
    /// и вся его непредсказуемость по времени.
    /// </summary>
    public AIAgent CreateOrchestratorAgent(AIAgent employeeAgent, AIAgent templateAgent) => CreateAgent(
        id: "workflow-orchestrator",
        name: "DocumentOrchestrator",
        description: "Готовит документ, делегируя сбор данных двум помощникам.",
        instructions: """
            Ты агент подготовки документов.
            Данных о сотрудниках и шаблонах у тебя нет — их приносят два помощника:
            - ask_employee_agent: ищет сотрудников в оргструктуре по списку имён;
            - ask_template_agent: приносит структуру шаблона документа из ЭДО.

            Порядок работы: вызови обоих помощников одним ходом. Они работают независимо друг от друга,
            и одновременный вызов экономит время. Дождись обоих ответов и только потом пиши документ.

            Собери готовый текст документа по структуре шаблона, подставив реальные ФИО и должности.
            Стиль официально-деловой. Не выдумывай ни сотрудников, ни разделы шаблона.
            Ответ — только текст документа.
            """,
        maxOutputTokens: Workflow.ComposerMaxOutputTokens,
        tools:
        [
            CreateSubAgentTool(
                employeeAgent,
                "ask_employee_agent",
                "Спрашивает агента оргструктуры о сотрудниках. Аргумент request — перечисление имён "
                + "или фамилий через запятую.",
                "поиск сотрудников"),
            CreateSubAgentTool(
                templateAgent,
                "ask_template_agent",
                "Спрашивает агента ЭДО о шаблоне документа. Аргумент request — тип документа, "
                + "например «служебная записка».",
                "поиск шаблона"),
        ]);

    // -------------------------------------------------------------------------
    // Инструменты
    // -------------------------------------------------------------------------

    /// <summary>Инструмент, ходящий в «оргструктуру». Замер идёт по времени самого обращения.</summary>
    private AIFunction CreateDirectoryTool() => AIFunctionFactory.Create(
        ([Description("Имена или фамилии сотрудников через запятую.")] string names,
            CancellationToken cancellationToken) =>
            trace.MeasureAsync(
                "Оргструктура: поиск сотрудников",
                WorkflowStageKinds.Api,
                token => api.FindEmployeesAsync(SplitNames(names), token),
                result => $"{result.Length} симв.",
                cancellationToken),
        name: "directory_find_employees",
        description: "Ищет сотрудников в организационной структуре компании по имени или фамилии.");

    /// <summary>Инструмент, ходящий в «ЭДО».</summary>
    private AIFunction CreateTemplateTool() => AIFunctionFactory.Create(
        ([Description("Тип документа: служебная записка, приказ, доверенность, договор.")] string documentType,
            CancellationToken cancellationToken) =>
            trace.MeasureAsync(
                "ЭДО: шаблон документа",
                WorkflowStageKinds.Api,
                token => api.GetTemplateAsync(documentType, token),
                result => $"{result.Length} симв.",
                cancellationToken),
        name: "edo_get_template",
        description: "Возвращает структуру шаблона документа указанного типа из системы ЭДО.");

    /// <summary>
    /// Оборачивает целого агента в инструмент. Для вызывающей модели это обычная функция,
    /// а внутри — полноценный прогон другого агента со своими обращениями к модели.
    /// </summary>
    private AIFunction CreateSubAgentTool(AIAgent agent, string name, string description, string stageName) =>
        AIFunctionFactory.Create(
            ([Description("Задача для помощника, одной строкой.")] string request,
                CancellationToken cancellationToken) =>
                trace.MeasureAsync(
                    $"Подагент «{stageName}»",
                    WorkflowStageKinds.Agent,
                    async token =>
                    {
                        var response = await agent.RunAsync(request, session: null, options: null, token);

                        return response.Text ?? string.Empty;
                    },
                    result => $"{result.Length} симв.",
                    cancellationToken),
            name: name,
            description: description);

    // -------------------------------------------------------------------------
    // Общая сборка
    // -------------------------------------------------------------------------

    private ChatClientAgent CreateAgent(
        string id,
        string name,
        string description,
        string instructions,
        int maxOutputTokens,
        IList<AITool> tools)
    {
        // Замеряющая прослойка идёт под вызовом инструментов, а не над ним: так в отчёт попадает
        // каждый round-trip к модели по отдельности, а не один общий «прогон агента».
        var measured = new MeasuringChatClient(chatClient, trace, name);

        // Пайплайн собираем сами, чтобы добраться до AllowConcurrentInvocation: у ChatClientAgent
        // своего способа это настроить нет, а именно от этого флага зависит, выполнятся ли два
        // выданных за один ход вызова одновременно или встанут в очередь.
        var hasTools = tools.Count > 0;

        var client = hasTools
            ? measured.AsBuilder()
                .UseFunctionInvocation(
                    loggerFactory,
                    invocation => invocation.AllowConcurrentInvocation = Workflow.AllowConcurrentToolCalls)
                .Build()
            : measured;

        return new ChatClientAgent(
            client,
            new ChatClientAgentOptions
            {
                Id = id,
                Name = name,
                Description = description,

                // Пайплайн уже собран вручную, второй FunctionInvokingChatClient поверх не нужен.
                UseProvidedChatClientAsIs = hasTools,

                ChatOptions = new ChatOptions
                {
                    ModelId = options.Model,
                    Instructions = instructions,
                    Temperature = Workflow.Temperature,
                    MaxOutputTokens = maxOutputTokens,
                    ToolMode = tools.Count > 0 ? ChatToolMode.Auto : ChatToolMode.None,
                    AllowMultipleToolCalls = true,

                    // Рассуждения на стенде только мешают: они дают сотни лишних токенов до первого
                    // символа ответа и делают прогоны несравнимыми между собой.
                    Reasoning = AgentRuntimeFactory.BuildReasoning(ReasoningEffortOption.None),
                    Tools = tools,
                },
            },
            loggerFactory);
    }

    private static string[] SplitNames(string names) =>
        [
            .. (names ?? string.Empty)
                .Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        ];
}

using Assistant.Contracts.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Стенд для замера мультиагентного сценария «подготовь документ».
///
/// Намеренно мимо MediatR и слоя Application: это не функциональность продукта, а измерительный
/// прибор. Тащить ради него команду, обработчик и контракт в Application значило бы закрепить
/// в архитектуре то, что живёт до первого ответа на вопрос «а сколько это вообще стоит по времени».
///
/// Прогон с моделью идёт минутами: каждое обращение к локальной модели — это десятки секунд,
/// а их в сценарии несколько. Клиенту нужен таймаут соответствующий.
/// </summary>
[ApiController]
[Authorize]
[Route("api/workflows/document")]
public sealed class WorkflowController(IDocumentWorkflow workflow) : ControllerBase
{
    /// <summary>Один прогон в одном режиме.</summary>
    [HttpPost("run")]
    [ProducesResponseType<DocumentWorkflowResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Run(
        [FromBody] DocumentWorkflowRunRequest request,
        CancellationToken cancellationToken)
    {
        var result = await workflow.RunAsync(ToRequest(request), request.Mode, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Прогон одного и того же сценария несколькими режимами подряд — то, ради чего стенд и нужен.
    /// Именно подряд, а не параллельно: локальная модель обслуживает запросы по очереди,
    /// и одновременный запуск смазал бы все замеры.
    /// </summary>
    [HttpPost("benchmark")]
    [ProducesResponseType<IReadOnlyList<DocumentWorkflowResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Benchmark(
        [FromBody] DocumentWorkflowBenchmarkRequest request,
        CancellationToken cancellationToken)
    {
        var modes = request.Modes is { Count: > 0 }
            ? request.Modes
            : [DocumentWorkflowModeEnum.DirectApi, DocumentWorkflowModeEnum.Workflow, DocumentWorkflowModeEnum.AgentTools];

        var scenario = ToRequest(request);
        var results = new List<DocumentWorkflowResult>(modes.Count);

        foreach (var mode in modes)
            results.Add(await workflow.RunAsync(scenario, mode, cancellationToken));

        return Ok(results);
    }

    /// <summary>Схема графа режима Workflow в формате mermaid.</summary>
    [HttpGet("diagram")]
    [AllowAnonymous]
    [Produces("text/plain")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Diagram() => Content(workflow.GetDiagram(), "text/plain; charset=utf-8");

    private static DocumentWorkflowRequest ToRequest(DocumentWorkflowScenario scenario) => new()
    {
        DocumentType = string.IsNullOrWhiteSpace(scenario.DocumentType)
            ? "служебная записка"
            : scenario.DocumentType,
        EmployeeNames = scenario.EmployeeNames is { Count: > 0 }
            ? scenario.EmployeeNames
            : ["Иванов", "Петрова"],
        Subject = scenario.Subject,
    };
}

/// <summary>Общая часть запросов: что за документ готовим.</summary>
public abstract record DocumentWorkflowScenario
{
    /// <summary>Тип документа. Пусто — берётся служебная записка.</summary>
    public string? DocumentType { get; init; }

    /// <summary>Фамилии сотрудников. Пусто — берутся Иванов и Петрова.</summary>
    public IReadOnlyList<string>? EmployeeNames { get; init; }

    public string? Subject { get; init; }
}

public sealed record DocumentWorkflowRunRequest : DocumentWorkflowScenario
{
    public DocumentWorkflowModeEnum Mode { get; init; } = DocumentWorkflowModeEnum.Workflow;
}

public sealed record DocumentWorkflowBenchmarkRequest : DocumentWorkflowScenario
{
    /// <summary>Режимы по порядку. Пусто — все три, от самого дешёвого к самому дорогому.</summary>
    public IReadOnlyList<DocumentWorkflowModeEnum>? Modes { get; init; }
}

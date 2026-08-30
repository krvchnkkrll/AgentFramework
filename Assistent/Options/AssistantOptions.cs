namespace Assistant.Options;

/// <summary>
/// Корень конфигурации ассистента (секция "Assistant" в appsettings).
/// Всё, что раньше было хардкодом в DefaultAgent, теперь живёт здесь.
/// </summary>
public sealed class AssistantOptions
{
    /// <summary>
    /// Адрес сервера без /v1 на конце. Для локальной LM Studio — http://192.168.0.12:1234,
    /// для Yandex AI Studio — https://ai.api.cloud.yandex.net.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Идентификатор модели. У локального сервера это просто имя (google/gemma-4-12b-qat),
    /// у Yandex — URI вида gpt://{FolderId}/qwen3-235b-a22b-fp8/latest.
    /// </summary>
    public required string Model { get; init; }

    /// <summary>Параметры генерации основного агента.</summary>
    public GenerationOptions Generation { get; init; } = new();

    /// <summary>Параметры служебного агента, который придумывает название чата.</summary>
    public TitleGenerationOptions Title { get; init; } = new();

    /// <summary>Сжатие истории диалога перед каждым вызовом модели.</summary>
    public CompactionOptions Compaction { get; init; } = new();

    /// <summary>Скиллы: папки со SKILL.md, которые агент подгружает по требованию.</summary>
    public SkillsOptions Skills { get; init; } = new();

    /// <summary>Встроенные инструменты агента.</summary>
    public ToolsOptions Tools { get; init; } = new();

    /// <summary>Todo-лист агента для длинных задач.</summary>
    public TodoOptions Todo { get; init; } = new();

    /// <summary>Режимы работы агента (plan / execute).</summary>
    public ModesOptions Modes { get; init; } = new();

    /// <summary>Файловые инструменты агента.</summary>
    public FilesOptions Files { get; init; } = new();

    /// <summary>Долговременная файловая память агента.</summary>
    public MemoryOptions Memory { get; init; } = new();

    /// <summary>Подтверждение вызовов инструментов пользователем.</summary>
    public ApprovalsOptions Approvals { get; init; } = new();

    /// <summary>RAG поверх OpenSearch.</summary>
    public SearchOptions Search { get; init; } = new();

    /// <summary>Стенд мультиагентного сценария «подготовь документ» с замером времени.</summary>
    public WorkflowOptions Workflow { get; init; } = new();

    /// <summary>Телеметрия OpenTelemetry для прогонов агента.</summary>
    public bool EnableOpenTelemetry { get; init; }
}

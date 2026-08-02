namespace Assistant.Contracts.Models;

/// <summary>
/// Тип события в стриме агента.
/// </summary>
public enum AssistantUpdateKindEnum
{
    /// <summary>Кусок обычного текста ответа — его и надо слать в UI.</summary>
    Text,

    /// <summary>Кусок «размышлений» модели (reasoning). В UI обычно не показывается.</summary>
    Reasoning,

    /// <summary>Агент решил вызвать инструмент.</summary>
    ToolCall,

    /// <summary>Инструмент отработал и вернул результат.</summary>
    ToolResult,

    /// <summary>Статистика по токенам, приходит в конце.</summary>
    Usage,

    /// <summary>Модель или инструмент вернули ошибку.</summary>
    Error
}

/// <summary>
/// Одно событие стрима. Заполнены только поля, относящиеся к <see cref="Kind"/>.
/// </summary>
public sealed record AssistantStreamUpdate
{
    public required AssistantUpdateKindEnum Kind { get; init; }

    public string? Text { get; init; }

    public string? CallId { get; init; }

    public string? ToolName { get; init; }

    public string? ToolArguments { get; init; }

    public string? ToolResult { get; init; }

    public AssistantUsage? Usage { get; init; }

    public string? Error { get; init; }

    public static AssistantStreamUpdate ForText(string text) =>
        new() { Kind = AssistantUpdateKindEnum.Text, Text = text };

    public static AssistantStreamUpdate ForReasoning(string text) =>
        new() { Kind = AssistantUpdateKindEnum.Reasoning, Text = text };

    public static AssistantStreamUpdate ForToolCall(string callId, string toolName, string? arguments) =>
        new()
        {
            Kind = AssistantUpdateKindEnum.ToolCall,
            CallId = callId,
            ToolName = toolName,
            ToolArguments = arguments,
        };

    public static AssistantStreamUpdate ForToolResult(string callId, string? result, string? error) =>
        new()
        {
            Kind = AssistantUpdateKindEnum.ToolResult,
            CallId = callId,
            ToolResult = result,
            Error = error,
        };

    public static AssistantStreamUpdate ForUsage(AssistantUsage usage) =>
        new() { Kind = AssistantUpdateKindEnum.Usage, Usage = usage };

    public static AssistantStreamUpdate ForError(string error) =>
        new() { Kind = AssistantUpdateKindEnum.Error, Error = error };
}

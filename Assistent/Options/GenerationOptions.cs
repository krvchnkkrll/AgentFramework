namespace Assistant.Options;

/// <summary>
/// Насколько усердно модель «думает» перед ответом. Понимает не всякий сервер и не всякая модель:
/// если параметр проигнорируют, модель просто продолжит думать столько, сколько считает нужным.
/// </summary>
public enum ReasoningEffortOption
{
    /// <summary>Параметр не отправляется вовсе — как решит сервер.</summary>
    Default,

    /// <summary>reasoning_effort=none. Именно генерация рассуждений съедает основное время ответа.</summary>
    None,

    Low,
    Medium,
    High,
}

/// <summary>Параметры генерации основного агента.</summary>
public sealed class GenerationOptions
{
    public float Temperature { get; init; } = 0.7f;

    public float TopP { get; init; } = 0.95f;

    public int TopK { get; init; } = 40;

    public int MaxOutputTokens { get; init; } = 8192;

    public float FrequencyPenalty { get; init; }

    public float PresencePenalty { get; init; }

    public ReasoningEffortOption ReasoningEffort { get; init; } = ReasoningEffortOption.None;

    /// <summary>Разрешить модели вызывать несколько инструментов за один ход.</summary>
    public bool AllowMultipleToolCalls { get; init; } = true;

    /// <summary>
    /// Системный промпт. Если не задан — берётся встроенный из <see cref="Prompts.DefaultPrompts"/>.
    /// </summary>
    public string? SystemPrompt { get; init; }
}

/// <summary>Параметры служебного агента, который придумывает название чата.</summary>
public sealed class TitleGenerationOptions
{
    public float Temperature { get; init; } = 0.2f;

    /// <summary>
    /// Бюджет большой не потому, что заголовок длинный, а потому, что рассуждающая модель
    /// сначала тратит сотни токенов на reasoning. С маленьким лимитом ответ обрывается
    /// на рассуждениях и текст приходит пустым.
    /// </summary>
    public int MaxOutputTokens { get; init; } = 1024;

    public int MaxLength { get; init; } = 50;

    public string? Prompt { get; init; }
}

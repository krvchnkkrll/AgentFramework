namespace Assistant.Options;

/// <summary>Как именно собирается стратегия сжатия истории.</summary>
public enum CompactionModeOption
{
    /// <summary>Сжатия нет вообще — вся история уезжает в модель как есть.</summary>
    Off,

    /// <summary>
    /// Готовая двухфазная стратегия фреймворка: считает бюджет входа как
    /// (контекстное окно − максимум выхода) и по достижении порогов сначала выкидывает результаты
    /// инструментов, потом обрезает старые группы сообщений. Ничего не спрашивает у модели.
    /// </summary>
    ContextWindow,

    /// <summary>
    /// Своя цепочка из включённых ниже стратегий: результаты инструментов → саммаризация →
    /// скользящее окно → обрезание. Порядок фиксирован, состав настраивается.
    /// </summary>
    Pipeline,
}

/// <summary>
/// Сжатие истории диалога. Работает как AIContextProvider: срабатывает перед каждым вызовом модели,
/// смотрит на накопленный список сообщений и укорачивает его. Прогресс сжатия лежит в состоянии
/// сессии, поэтому саммаризация не пересчитывается на каждый ход, если состояние сессии сохраняется.
/// </summary>
public sealed class CompactionOptions
{
    public bool Enabled { get; init; } = true;

    public CompactionModeOption Mode { get; init; } = CompactionModeOption.Pipeline;

    /// <summary>
    /// Размер контекстного окна модели в токенах. Для gemma-4-12b в LM Studio это то, что выставлено
    /// при загрузке модели. Используется режимом <see cref="CompactionModeOption.ContextWindow"/>.
    /// </summary>
    public int MaxContextWindowTokens { get; init; } = 32_768;

    /// <summary>Доля бюджета входа, на которой выкидываются результаты инструментов (0..1].</summary>
    public double ToolEvictionThreshold { get; init; } = 0.5;

    /// <summary>Доля бюджета входа, на которой начинается обрезание (0..1]. Не меньше предыдущей.</summary>
    public double TruncationThreshold { get; init; } = 0.8;

    /// <summary>Схлопывание старых вызовов инструментов в одну строку-описание.</summary>
    public ToolResultCompactionOptions ToolResults { get; init; } = new();

    /// <summary>Саммаризация старой части диалога отдельным вызовом модели.</summary>
    public SummarizationCompactionOptions Summarization { get; init; } = new();

    /// <summary>Скользящее окно по ходам диалога.</summary>
    public SlidingWindowCompactionOptions SlidingWindow { get; init; } = new();

    /// <summary>Грубое обрезание самых старых групп сообщений — страховка на случай, если остальное не помогло.</summary>
    public TruncationCompactionOptions Truncation { get; init; } = new();
}

/// <summary>
/// Схлопывает старые группы вызовов инструментов в короткое сообщение вида «вызывали get_current_time».
/// Самая дешёвая стратегия: тела результатов часто занимают больше места, чем весь остальной диалог,
/// а модели после нескольких ходов они уже не нужны.
/// </summary>
public sealed class ToolResultCompactionOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Срабатывает, когда сообщений в контексте больше этого числа.</summary>
    public int TriggerMessages { get; init; } = 20;

    /// <summary>Сколько последних групп сообщений трогать нельзя ни при каких условиях.</summary>
    public int MinimumPreservedGroups { get; init; } = 3;
}

/// <summary>
/// Просит модель пересказать старую часть диалога и заменяет её одним сообщением-саммари.
/// Стоит одного лишнего запроса к модели — на локальной gemma это десятки секунд, поэтому по умолчанию
/// выключено. Включать имеет смысл вместе с сохранением состояния сессии, иначе пересказ будет
/// пересчитываться на каждый ход.
/// </summary>
public sealed class SummarizationCompactionOptions
{
    public bool Enabled { get; init; }

    /// <summary>Срабатывает, когда токенов в контексте больше этого числа.</summary>
    public int TriggerTokens { get; init; } = 12_000;

    /// <summary>Сколько последних групп сообщений остаётся дословно.</summary>
    public int MinimumPreservedGroups { get; init; } = 6;

    /// <summary>
    /// Свой промпт для пересказа. ВАЖНО: результат пересказа НАВСЕГДА заменяет исходные сообщения
    /// в контексте модели, так что промпт — это часть периметра безопасности.
    /// </summary>
    public string? Prompt { get; init; }
}

/// <summary>Выкидывает самые старые ходы диалога целиком, держа длину переписки в рамках.</summary>
public sealed class SlidingWindowCompactionOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Срабатывает, когда ходов пользователя в контексте больше этого числа.</summary>
    public int TriggerTurns { get; init; } = 20;

    /// <summary>Сколько последних ходов остаётся всегда.</summary>
    public int MinimumPreservedTurns { get; init; } = 4;
}

/// <summary>Последний рубеж: режет старые группы сообщений по счётчику токенов.</summary>
public sealed class TruncationCompactionOptions
{
    public bool Enabled { get; init; } = true;

    /// <summary>Срабатывает, когда токенов в контексте больше этого числа.</summary>
    public int TriggerTokens { get; init; } = 24_000;

    /// <summary>Сколько последних групп сообщений остаётся всегда.</summary>
    public int MinimumPreservedGroups { get; init; } = 4;
}

using Assistant.Options;
using Assistant.Prompts;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace Assistant.Compaction;

/// <summary>
/// Собирает стратегию сжатия истории из конфигурации.
///
/// Зачем это вообще нужно: у модели фиксированное контекстное окно. Пока диалог короткий, в модель
/// уезжает вся переписка целиком; после нескольких десятков сообщений она перестаёт помещаться,
/// и сервер либо отрезает начало сам (как попало), либо возвращает ошибку. Сжатие решает, что именно
/// выкинуть, и делает это осмысленно.
///
/// Порядок в цепочке важен и идёт от самого дешёвого к самому разрушительному:
/// 1. результаты инструментов — выкидываем тела, оставляем факт вызова (модель ничего не теряет);
/// 2. саммаризация — старая часть диалога заменяется пересказом (теряются детали, но не смысл);
/// 3. скользящее окно — самые старые ходы выкидываются целиком;
/// 4. обрезание — последний рубеж, когда всё остальное не помогло.
/// </summary>
internal static class CompactionStrategyFactory
{
    /// <summary>
    /// Возвращает стратегию сжатия или null, если сжатие выключено или в цепочке не осталось шагов.
    /// </summary>
    /// <param name="options">Настройки сжатия.</param>
    /// <param name="maxOutputTokens">Максимум токенов на ответ — из него считается бюджет входа.</param>
    /// <param name="summarizationChatClient">Клиент для саммаризации. Обычно тот же, что и у агента.</param>
    public static CompactionStrategy? Create(
        CompactionOptions options,
        int maxOutputTokens,
        IChatClient summarizationChatClient)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(summarizationChatClient);

        if (!options.Enabled)
            return null;

        return options.Mode switch
        {
            CompactionModeOption.Off => null,
            CompactionModeOption.ContextWindow => CreateContextWindow(options, maxOutputTokens),
            CompactionModeOption.Pipeline => CreatePipeline(options, summarizationChatClient),
            _ => CreatePipeline(options, summarizationChatClient),
        };
    }

    /// <summary>
    /// Готовая стратегия фреймворка. Сама считает бюджет входа как (окно − максимум выхода)
    /// и по достижении долей этого бюджета сначала выкидывает результаты инструментов,
    /// потом обрезает старые группы. Ни одного лишнего запроса к модели.
    /// </summary>
    private static CompactionStrategy CreateContextWindow(CompactionOptions options, int maxOutputTokens) =>
        new ContextWindowCompactionStrategy(
            options.MaxContextWindowTokens,
            maxOutputTokens,
            options.ToolEvictionThreshold,
            options.TruncationThreshold);

    private static CompactionStrategy? CreatePipeline(CompactionOptions options, IChatClient chatClient)
    {
        var strategies = new List<CompactionStrategy>();

        if (options.ToolResults.Enabled)
        {
            strategies.Add(new ToolResultCompactionStrategy(
                CompactionTriggers.MessagesExceed(options.ToolResults.TriggerMessages),
                options.ToolResults.MinimumPreservedGroups));
        }

        if (options.Summarization.Enabled)
        {
            strategies.Add(new SummarizationCompactionStrategy(
                chatClient,
                CompactionTriggers.TokensExceed(options.Summarization.TriggerTokens),
                options.Summarization.MinimumPreservedGroups,
                options.Summarization.Prompt ?? DefaultPrompts.Summarization));
        }

        if (options.SlidingWindow.Enabled)
        {
            strategies.Add(new SlidingWindowCompactionStrategy(
                CompactionTriggers.TurnsExceed(options.SlidingWindow.TriggerTurns),
                options.SlidingWindow.MinimumPreservedTurns));
        }

        if (options.Truncation.Enabled)
        {
            strategies.Add(new TruncationCompactionStrategy(
                CompactionTriggers.TokensExceed(options.Truncation.TriggerTokens),
                options.Truncation.MinimumPreservedGroups));
        }

        return strategies.Count switch
        {
            0 => null,
            1 => strategies[0],
            _ => new PipelineCompactionStrategy(strategies),
        };
    }

    /// <summary>Человекочитаемое описание собранной цепочки — уходит в лог при старте агента.</summary>
    public static string Describe(CompactionOptions options)
    {
        if (!options.Enabled || options.Mode == CompactionModeOption.Off)
            return "выключено";

        if (options.Mode == CompactionModeOption.ContextWindow)
        {
            return $"ContextWindow(окно {options.MaxContextWindowTokens} т., "
                + $"инструменты с {options.ToolEvictionThreshold:P0}, обрезание с {options.TruncationThreshold:P0})";
        }

        var steps = new List<string>();

        if (options.ToolResults.Enabled)
            steps.Add($"ToolResult(>{options.ToolResults.TriggerMessages} сообщ.)");

        if (options.Summarization.Enabled)
            steps.Add($"Summarization(>{options.Summarization.TriggerTokens} т.)");

        if (options.SlidingWindow.Enabled)
            steps.Add($"SlidingWindow(>{options.SlidingWindow.TriggerTurns} ходов)");

        if (options.Truncation.Enabled)
            steps.Add($"Truncation(>{options.Truncation.TriggerTokens} т.)");

        return steps.Count == 0 ? "выключено" : string.Join(" → ", steps);
    }
}

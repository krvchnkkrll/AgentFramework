using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.AI;

namespace Assistant.Tools;

/// <summary>
/// Инструменты агента, не требующие ни одной внешней интеграции. Всё без побочных эффектов —
/// специально, чтобы агент был работоспособен на голом окружении.
/// Всё, что ходит наружу (файлы, поиск, память), приезжает отдельными провайдерами.
/// </summary>
public static class BuiltInTools
{
    /// <summary>
    /// Искусственная задержка перед ответом инструмента. Нужна только для демонстраций:
    /// встроенные инструменты отвечают мгновенно, и увидеть индикацию вызова в интерфейсе
    /// на них невозможно — плашка успевает мигнуть и исчезнуть.
    ///
    /// Задаётся через Assistant:Tools:SimulatedDelaySeconds, по умолчанию выключена.
    /// </summary>
    private static TimeSpan _simulatedDelay = TimeSpan.Zero;

    /// <summary>Собирает список инструментов для регистрации в агенте.</summary>
    /// <param name="simulatedDelay">Задержка для демонстрации. <see cref="TimeSpan.Zero"/> — без задержки.</param>
    public static IReadOnlyList<AIFunction> Create(TimeSpan simulatedDelay = default)
    {
        _simulatedDelay = simulatedDelay;

        return
        [
            AIFunctionFactory.Create(GetCurrentTime, "get_current_time"),
            AIFunctionFactory.Create(GetDaysBetween, "get_days_between"),
            AIFunctionFactory.Create(GetRandomNumber, "get_random_number"),
            AIFunctionFactory.Create(GetTextStatistics, "get_text_statistics"),
            AIFunctionFactory.Create(NewGuid, "new_guid"),
        ];
    }

    /// <summary>
    /// Ждёт настроенную задержку, слушая токен отмены. Без токена нажатие «стоп» не прерывало бы
    /// генерацию до конца задержки — а именно её мы и растягиваем.
    /// </summary>
    private static Task DelayAsync(CancellationToken cancellationToken) =>
        _simulatedDelay > TimeSpan.Zero ? Task.Delay(_simulatedDelay, cancellationToken) : Task.CompletedTask;

    [Description("Возвращает текущие дату и время. Единственный достоверный источник времени для агента.")]
    public static async Task<string> GetCurrentTime(
        [Description("Часовой пояс в формате IANA, например Europe/Moscow или Asia/Novosibirsk. Если не указан — UTC.")]
        string? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(timeZone))
            return utcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            var local = TimeZoneInfo.ConvertTime(utcNow, zone);
            
            return $"{local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} "
                + $"({timeZone}, UTC{local.Offset.Hours:+00;-00}:{Math.Abs(local.Offset.Minutes):00})";
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return $"Неизвестный часовой пояс '{timeZone}'. Используй идентификаторы вида Europe/Moscow.";
        }
    }

    [Description("Считает количество дней между двумя датами.")]
    public static async Task<string> GetDaysBetween(
        [Description("Дата начала в формате ГГГГ-ММ-ДД.")]
        string fromDate,
        [Description("Дата окончания в формате ГГГГ-ММ-ДД.")]
        string toDate,
        CancellationToken cancellationToken = default)
    {
        await DelayAsync(cancellationToken);

        if (!DateOnly.TryParse(fromDate, CultureInfo.InvariantCulture, out var from))
            return $"Не удалось разобрать дату '{fromDate}'. Формат: ГГГГ-ММ-ДД.";

        if (!DateOnly.TryParse(toDate, CultureInfo.InvariantCulture, out var to))
            return $"Не удалось разобрать дату '{toDate}'. Формат: ГГГГ-ММ-ДД.";

        var days = to.DayNumber - from.DayNumber;

        return $"{days} дн. (с {from:yyyy-MM-dd} по {to:yyyy-MM-dd})";
    }

    [Description("Возвращает случайное целое число в заданном диапазоне, границы включаются.")]
    public static long GetRandomNumber(
        [Description("Минимальное значение.")] int min,
        [Description("Максимальное значение.")] int max)
    {
        var (low, high) = min <= max ? (min, max) : (max, min);

        return Random.Shared.NextInt64(low, high + 1L);
    }

    [Description("Считает статистику по тексту: символы, слова, строки.")]
    public static TextStatistics GetTextStatistics(
        [Description("Текст для анализа.")] string text)
    {
        text ??= string.Empty;

        return new TextStatistics
        {
            Characters = text.Length,
            CharactersWithoutSpaces = text.Count(symbol => !char.IsWhiteSpace(symbol)),
            Words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
            Lines = text.Length == 0 ? 0 : text.Split('\n').Length,
        };
    }

    [Description("Генерирует новый уникальный идентификатор (GUID).")]
    public static string NewGuid() => Guid.CreateVersion7().ToString();

    /// <summary>Результат инструмента get_text_statistics. Сериализуется в JSON и уезжает модели.</summary>
    public sealed record TextStatistics
    {
        public required int Characters { get; init; }

        public required int CharactersWithoutSpaces { get; init; }

        public required int Words { get; init; }

        public required int Lines { get; init; }
    }
}

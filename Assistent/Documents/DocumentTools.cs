using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Assistant.Documents;

/// <summary>
/// Инструменты работы с приложенными документами.
///
/// Здесь лежит ответ на вопрос «что делать, если документ не влезает в контекст»: его туда
/// и не кладут. Документ живёт в сторе, а модель получает не текст, а способ по нему ходить —
/// посмотреть, что вообще приложено, найти нужное место поиском и прочитать вокруг него
/// несколько десятков строк. Ровно так человек работает с толстой книгой: не заучивает её
/// целиком, а пользуется оглавлением и указателем.
///
/// Три правила, без которых схема разваливается:
/// 1. У каждого ответа жёсткий потолок объёма — иначе одно чтение «с 1 по 100000 строку»
///    сведёт на нет всю затею.
/// 2. В ответе всегда есть номера строк — иначе модели некуда двигаться дальше.
/// 3. В ответе всегда сказано, сколько осталось за кадром — иначе модель решит, что прочла всё.
///
/// Инструменты привязаны к конкретному чату: за его пределы они не видят.
/// </summary>
internal sealed class DocumentTools(InMemoryDocumentStore store, Guid conversationId)
{
    /// <summary>Потолок одного ответа в символах. Примерно 2–3 тысячи токенов на русском.</summary>
    private const int MaxResponseCharacters = 8_000;

    /// <summary>Сколько строк отдаём, если модель не указала иное.</summary>
    private const int DefaultLineCount = 80;

    private const int MaxLineCount = 400;

    private const int MaxMatches = 40;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(2);

    public IReadOnlyList<AIFunction> Create() =>
    [
        AIFunctionFactory.Create(ListDocuments, "document_list"),
        AIFunctionFactory.Create(ReadDocument, "document_read"),
        AIFunctionFactory.Create(SearchDocument, "document_search"),
    ];

    [Description("Показывает документы, приложенные к этому разговору: имя, размер и число строк.")]
    private string ListDocuments()
    {
        var documents = store.GetForConversation(conversationId);

        if (documents.Count == 0)
            return "К этому разговору не приложено ни одного документа.";

        var builder = new StringBuilder("Приложенные документы:");

        foreach (var document in documents)
        {
            builder.AppendLine()
                .Append("- ").Append(document.FileName)
                .Append(": строк ").Append(document.LineCount)
                .Append(", символов ").Append(document.CharacterCount);
        }

        return builder.ToString();
    }

    [Description(
        "Читает кусок документа по номерам строк. Документ может быть большим, поэтому читай "
        + "частями: сначала найди нужное место через document_search, потом прочитай вокруг него.")]
    private string ReadDocument(
        [Description("Имя файла, как его показал document_list.")]
        string fileName,
        [Description("С какой строки читать. Нумерация с 1. По умолчанию с начала.")]
        int fromLine = 1,
        [Description("Сколько строк прочитать. По умолчанию 80, максимум 400.")]
        int lineCount = DefaultLineCount)
    {
        var document = store.Find(conversationId, fileName);

        if (document is null)
            return NotFound(fileName);

        var lines = document.Lines;
        var start = Math.Clamp(fromLine, 1, lines.Length) - 1;
        var count = Math.Clamp(lineCount, 1, MaxLineCount);
        var end = Math.Min(start + count, lines.Length);

        var builder = new StringBuilder();
        var truncatedAtLine = -1;

        for (var index = start; index < end; index++)
        {
            // Обрываемся по объёму, а не только по числу строк: строки бывают очень длинные.
            if (builder.Length >= MaxResponseCharacters)
            {
                truncatedAtLine = index;
                break;
            }

            builder.Append(index + 1).Append('\t').AppendLine(lines[index]);
        }

        var lastRead = truncatedAtLine == -1 ? end : truncatedAtLine;

        return builder
            .AppendLine()
            .Append(Footer(document.Info.FileName, lastRead, lines.Length))
            .ToString();
    }

    [Description(
        "Ищет в документе по регулярному выражению и возвращает совпадения с номерами строк. "
        + "С этого начинают работу с большим документом: сначала найти, потом прочитать вокруг.")]
    private string SearchDocument(
        [Description("Имя файла, как его показал document_list.")]
        string fileName,
        [Description("Регулярное выражение или просто слово. Регистр не учитывается.")]
        string pattern,
        [Description("Сколько строк показывать вокруг каждого совпадения. По умолчанию 1.")]
        int contextLines = 1)
    {
        var document = store.Find(conversationId, fileName);

        if (document is null)
            return NotFound(fileName);

        Regex regex;

        try
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase, RegexTimeout);
        }
        catch (ArgumentException exception)
        {
            return $"Не удалось разобрать регулярное выражение: {exception.Message}";
        }

        var lines = document.Lines;
        var context = Math.Clamp(contextLines, 0, 10);
        var builder = new StringBuilder();
        var matches = 0;
        var lastPrinted = 0;

        try
        {
            for (var index = 0; index < lines.Length && matches < MaxMatches; index++)
            {
                if (!regex.IsMatch(lines[index]))
                    continue;

                matches++;

                var from = Math.Max(index - context, 0);
                var to = Math.Min(index + context, lines.Length - 1);

                // Разрыв между блоками помечаем, чтобы модель не считала строки соседними.
                if (from > lastPrinted && builder.Length > 0)
                    builder.AppendLine("…");

                for (var line = Math.Max(from, lastPrinted); line <= to; line++)
                    builder.Append(line + 1).Append('\t').AppendLine(lines[line]);

                lastPrinted = to + 1;

                if (builder.Length >= MaxResponseCharacters)
                    break;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return "Поиск занял слишком много времени. Упрости регулярное выражение.";
        }

        if (matches == 0)
            return $"В документе {document.Info.FileName} совпадений с '{pattern}' не найдено.";

        return builder
            .AppendLine()
            .Append($"Найдено совпадений: {matches}")
            .Append(matches >= MaxMatches ? $" (показаны первые {MaxMatches}, уточни запрос)." : ".")
            .Append($" Всего строк в документе: {lines.Length}. Читай нужные места через document_read.")
            .ToString();
    }

    private string NotFound(string fileName)
    {
        var available = store.GetForConversation(conversationId);

        return available.Count == 0
            ? "К этому разговору не приложено ни одного документа."
            : $"Документа '{fileName}' нет. Доступны: {string.Join(", ", available.Select(d => d.FileName))}.";
    }

    /// <summary>
    /// Хвост ответа на чтение. Модель обязана понимать, что документ не кончился —
    /// иначе она сделает вывод по первым восьмидесяти строкам и уверенно ошибётся.
    /// </summary>
    private static string Footer(string fileName, int lastReadLine, int totalLines) =>
        lastReadLine >= totalLines
            ? $"[Конец документа {fileName}. Всего строк: {totalLines}.]"
            : $"[Прочитаны строки по {lastReadLine} из {totalLines}. "
                + $"Дальше — document_read(\"{fileName}\", {lastReadLine + 1}).]";
}

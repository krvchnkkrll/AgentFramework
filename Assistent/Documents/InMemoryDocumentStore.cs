using System.Collections.Concurrent;
using System.Text;
using Assistant.Contracts.Documents;
using Microsoft.Extensions.Logging;

namespace Assistant.Documents;

/// <summary>
/// Документ вместе с разбором на строки. Строки считаем один раз при загрузке: инструменты
/// читают документ диапазонами строк, и пересобирать массив на каждый вызов было бы расточительно.
/// </summary>
internal sealed record StoredDocument
{
    public required DocumentInfo Info { get; init; }

    public required Guid UserId { get; init; }

    public required string[] Lines { get; init; }
}

/// <summary>
/// Хранилище документов в памяти процесса.
///
/// Это осознанно временное решение под эксперимент: ни БД, ни S3 не задействованы, всё
/// пропадает при перезапуске и не переживает вторую реплику. Когда дойдут руки до настоящего
/// хранилища — меняется только этот класс, интерфейс и инструменты останутся прежними.
/// </summary>
public sealed class InMemoryDocumentStore(ILogger<InMemoryDocumentStore> logger) : IDocumentStore
{
    private readonly ConcurrentDictionary<Guid, StoredDocument> _documents = new();

    /// <summary>Какие документы приписаны к какому чату.</summary>
    private readonly ConcurrentDictionary<Guid, HashSet<Guid>> _byConversation = new();

    public DocumentInfo Add(Guid userId, string fileName, string contentType, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(text);

        // Нормализуем переводы строк: иначе номера строк разъедутся между Windows- и Unix-файлами.
        var lines = text.ReplaceLineEndings("\n").Split('\n');

        var info = new DocumentInfo
        {
            Id = Guid.CreateVersion7(),
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = Encoding.UTF8.GetByteCount(text),
            LineCount = lines.Length,
            CharacterCount = text.Length,
        };

        _documents[info.Id] = new StoredDocument { Info = info, UserId = userId, Lines = lines };

        logger.LogInformation(
            "Документ {FileName} загружен: {Lines} строк, {Characters} символов.",
            info.FileName,
            info.LineCount,
            info.CharacterCount);

        return info;
    }

    public void AttachToConversation(Guid conversationId, Guid userId, IReadOnlyCollection<Guid> documentIds)
    {
        ArgumentNullException.ThrowIfNull(documentIds);

        if (documentIds.Count == 0)
            return;

        // Чужой документ к своему чату не привяжешь, даже зная его идентификатор.
        var own = documentIds
            .Where(id => _documents.TryGetValue(id, out var document) && document.UserId == userId)
            .ToArray();

        if (own.Length == 0)
            return;

        _byConversation.AddOrUpdate(
            conversationId,
            _ => [.. own],
            (_, existing) =>
            {
                lock (existing)
                {
                    foreach (var id in own)
                        existing.Add(id);
                }

                return existing;
            });

        logger.LogInformation(
            "К чату {ConversationId} привязано документов: {Count}.",
            conversationId,
            own.Length);
    }

    public IReadOnlyList<DocumentInfo> GetForConversation(Guid conversationId)
    {
        if (!_byConversation.TryGetValue(conversationId, out var ids))
            return [];

        Guid[] snapshot;

        lock (ids)
            snapshot = [.. ids];

        return
        [
            .. snapshot
                .Select(id => _documents.GetValueOrDefault(id))
                .Where(document => document is not null)
                .Select(document => document!.Info)
                .OrderBy(info => info.FileName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    public bool Remove(Guid documentId, Guid userId)
    {
        if (!_documents.TryGetValue(documentId, out var document) || document.UserId != userId)
            return false;

        return _documents.TryRemove(documentId, out _);
    }

    /// <summary>
    /// Достаёт документ чата по имени файла. Инструменты обращаются к документу по имени,
    /// а не по идентификатору: модели проще оперировать «отчёт.txt», чем гуидом.
    /// </summary>
    internal StoredDocument? Find(Guid conversationId, string fileName)
    {
        if (!_byConversation.TryGetValue(conversationId, out var ids))
            return null;

        Guid[] snapshot;

        lock (ids)
            snapshot = [.. ids];

        return snapshot
            .Select(id => _documents.GetValueOrDefault(id))
            .FirstOrDefault(document => document is not null
                && string.Equals(document.Info.FileName, fileName, StringComparison.OrdinalIgnoreCase));
    }
}

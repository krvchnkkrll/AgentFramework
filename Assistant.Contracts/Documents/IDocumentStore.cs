namespace Assistant.Contracts.Documents;

/// <summary>Загруженный документ — то, что видно снаружи слоя ассистента.</summary>
public sealed record DocumentInfo
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    /// <summary>Размер исходного файла в байтах.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>Число строк. По ним модель и ходит: читает диапазонами, а не целиком.</summary>
    public required int LineCount { get; init; }

    /// <summary>Число символов текста — грубая, но понятная оценка «влезет ли в контекст».</summary>
    public required int CharacterCount { get; init; }
}

/// <summary>
/// Хранилище загруженных документов.
///
/// Реализация намеренно живёт в памяти процесса: это эксперимент, ни БД, ни объектное
/// хранилище не подключены. После перезапуска приложения документы пропадают, и это
/// ожидаемое поведение, а не недоделка.
/// </summary>
public interface IDocumentStore
{
    /// <summary>Кладёт текст документа и возвращает его карточку.</summary>
    DocumentInfo Add(Guid userId, string fileName, string contentType, string text);

    /// <summary>
    /// Привязывает ранее загруженные документы к чату. До этого момента документ ничей:
    /// файл загружают до отправки сообщения, а чата может ещё не существовать.
    /// </summary>
    void AttachToConversation(Guid conversationId, Guid userId, IReadOnlyCollection<Guid> documentIds);

    /// <summary>Документы, доступные агенту в этом чате.</summary>
    IReadOnlyList<DocumentInfo> GetForConversation(Guid conversationId);

    /// <summary>Убирает документ. Нужен, когда вложение сняли из черновика.</summary>
    bool Remove(Guid documentId, Guid userId);
}

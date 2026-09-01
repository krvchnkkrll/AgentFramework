namespace Persistence.Contracts.Storage;

/// <summary>Что хранилище знает о файле помимо его содержимого.</summary>
public sealed record StoredFileInfo
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }

    /// <summary>SHA-256 содержимого в hex. Считается хранилищем при записи.</summary>
    public required string ContentHash { get; init; }
}

/// <summary>
/// Файловое хранилище: большие файлы, которым не место в базе.
///
/// Интерфейс намеренно узкий — положить, прочитать, удалить, — чтобы за ним могло стоять
/// что угодно: папка на диске, S3, MinIO. Реализация по умолчанию пишет в локальную папку;
/// менять её на объектное хранилище нужно здесь и только здесь.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Записывает файл и возвращает его карточку с новым идентификатором.
    /// Хэш содержимого считается по ходу записи — второй раз файл ради этого не читается.
    /// </summary>
    Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Открывает файл на чтение. null — файла нет.</summary>
    Task<Stream?> OpenReadAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Удаляет файл. false — удалять было нечего.</summary>
    Task<bool> DeleteAsync(Guid fileId, CancellationToken cancellationToken = default);
}

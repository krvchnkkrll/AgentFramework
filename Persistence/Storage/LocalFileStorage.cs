using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Persistence.Contracts.Storage;

namespace Persistence.Storage;

/// <summary>
/// Файловое хранилище поверх обычной папки на диске.
///
/// Это временная реализация под один узел: для нескольких экземпляров приложения нужна
/// либо общая сетевая папка, либо объектное хранилище. Менять на S3/MinIO нужно здесь —
/// весь остальной код видит только <see cref="IFileStorage"/>.
///
/// Раскладка: {root}/{первые два символа id}/{id}.bin. Подпапки нужны не для красоты —
/// каталог с сотней тысяч файлов в одной директории заметно тормозит на большинстве ФС.
/// </summary>
internal sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _root = Path.GetFullPath(options.Value.RootDirectory);
        Directory.CreateDirectory(_root);
    }

    public async Task<StoredFileInfo> SaveAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var id = Guid.CreateVersion7();
        var destination = GetPath(id);

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        // Пишем во временный файл и переименовываем: иначе оборванная запись оставила бы
        // в хранилище файл, который выглядит целым.
        var temporary = destination + ".tmp";

        long size;
        byte[] hash;

        try
        {
            await using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                size = 0;

                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken);

                    if (read == 0)
                        break;

                    hasher.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    size += read;
                }

                hash = hasher.GetCurrentHash();
            }

            File.Move(temporary, destination, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        return new StoredFileInfo
        {
            Id = id,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = size,
            ContentHash = Convert.ToHexStringLower(hash),
        };
    }

    public Task<Stream?> OpenReadAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var path = GetPath(fileId);

        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        return Task.FromResult<Stream?>(stream);
    }

    public Task<bool> DeleteAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryDelete(GetPath(fileId)));

    private string GetPath(Guid fileId)
    {
        var name = fileId.ToString("N");

        return Path.Combine(_root, name[..2], name + ".bin");
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;

            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

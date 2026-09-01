namespace Persistence.Storage;

/// <summary>Настройки файлового хранилища (секция "FileStorage").</summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Корневая папка локального хранилища. Относительный путь считается от рабочей
    /// директории процесса.
    /// </summary>
    public string RootDirectory { get; init; } = "storage";
}

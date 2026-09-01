using Assistant.Contracts.Skills;
using Persistence.Contracts.Storage;

namespace Application.Services;

/// <summary>
/// Отдаёт слою ассистента архивы скиллов из файлового хранилища.
///
/// Вся суть — в направлении зависимости: ассистент объявляет, что ему нужен поток по
/// идентификатору файла, а знание о том, где этот файл лежит, остаётся в приложении.
/// </summary>
internal sealed class SkillPackageSource(IFileStorage fileStorage) : ISkillPackageSource
{
    public Task<Stream?> OpenAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        fileStorage.OpenReadAsync(fileId, cancellationToken);
}

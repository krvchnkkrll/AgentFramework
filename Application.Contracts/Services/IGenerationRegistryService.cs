using Application.Contracts.Models;
using Domain.Models;

namespace Application.Contracts.Services;

public interface IGenerationRegistryService
{
    ActiveGeneration? GetGeneration(Guid conversationId);

    bool IsGenerationActive(Guid conversationId);

    Task StartGeneration(StartGenerationParameters parameters);

    /// <summary>
    /// Останавливает генерацию по требованию пользователя. Возвращает false, если генерации не было.
    /// То, что модель успела сгенерировать, сохраняется как обычное сообщение.
    /// </summary>
    bool StopGeneration(Guid conversationId);
}
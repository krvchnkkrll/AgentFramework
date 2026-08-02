using Application.Contracts.Models;
using Domain.Models;

namespace Application.Contracts.Services;

public interface IGenerationRegistryService
{
    ActiveGeneration? GetGeneration(Guid conversationId);

    bool IsGenerationActive(Guid conversationId);

    Task StartGeneration(StartGenerationParameters parameters);
}
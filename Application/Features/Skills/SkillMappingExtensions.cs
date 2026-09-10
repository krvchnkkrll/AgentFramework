using Application.Contracts.Features.Skills.Responses;
using Assistant.Contracts.Models;
using Domain.Entities.Skills;

namespace Application.Features.Skills;

internal static class SkillMappingExtensions
{
    public static SkillResponse ToResponse(this Skill skill) => new()
    {
        Id = skill.Id,
        Name = skill.Name,
        Description = skill.Description,
        SizeBytes = skill.SizeBytes,
        HasScripts = skill.HasScripts,
        Shared = skill.UserId is null,
        CreatedAt = skill.CreatedAt,
        UpdatedAt = skill.UpdatedAt,
    };

    /// <summary>
    /// Переводит скилл в ссылку для слоя ассистента: имя и описание для системного промпта
    /// и идентификатор файла, по которому ассистент скачает текст, когда модель его запросит.
    /// </summary>
    public static AssistantSkillReference ToReference(this Skill skill) => new()
    {
        Id = skill.Id,
        Name = skill.Name,
        Description = skill.Description,
        FileId = skill.FileId,
        ContentHash = skill.ContentHash,
    };
}

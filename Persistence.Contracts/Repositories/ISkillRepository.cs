using Domain.Entities.Skills;

namespace Persistence.Contracts.Repositories;

public interface ISkillRepository
{
    Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Скиллы пользователя вместе с общими (у которых нет владельца).</summary>
    Task<IReadOnlyCollection<Skill>> GetAvailableAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Скиллы по списку идентификаторов — только те, что пользователю доступны.
    /// Так конструктор агента не сможет прицепить чужой скилл, подставив его id.
    /// </summary>
    Task<IReadOnlyCollection<Skill>> GetAvailableByIdsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>Есть ли у пользователя скилл с таким именем. Имена уникальны в пределах владельца.</summary>
    Task<Skill?> FindByNameAsync(Guid userId, string name, CancellationToken cancellationToken = default);

    /// <summary>Агенты, которым этот скилл выдан. Нужны, чтобы сбросить их кэш после правки.</summary>
    Task<IReadOnlyCollection<Guid>> GetAgentIdsAsync(Guid skillId, CancellationToken cancellationToken = default);

    Task AddAsync(Skill skill, CancellationToken cancellationToken = default);

    void Remove(Skill skill);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using Application.Contracts.Features.Skills.Responses;
using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Skills;

/// <summary>Скиллы, доступные пользователю: свои плюс общие.</summary>
public sealed record GetSkillsQuery : IRequest<Result<IReadOnlyCollection<SkillResponse>>>;

/// <summary>
/// Загрузка нового скилла. Архив приходит потоком: имя и описание вычитываются из
/// SKILL.md внутри, задавать их отдельно нельзя.
/// </summary>
public sealed record UploadSkillCommand(string FileName, Stream Content)
    : IRequest<Result<SkillResponse>>;

/// <summary>
/// Перезалив содержимого существующего скилла. Имя и описание при этом могут поменяться —
/// правда лежит во frontmatter нового архива.
/// </summary>
public sealed record UpdateSkillCommand(Guid SkillId, string FileName, Stream Content)
    : IRequest<Result<SkillResponse>>;

public sealed record DeleteSkillCommand(Guid SkillId) : IRequest<Result>;

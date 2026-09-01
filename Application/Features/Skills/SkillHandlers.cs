using Application.Contracts.Features.Skills;
using Application.Contracts.Features.Skills.Responses;
using Assistant.Contracts;
using Assistant.Contracts.Skills;
using Domain.Common;
using Domain.Entities.Skills;
using Domain.Entities.Skills.Parameters;
using MediatR;
using Microsoft.Extensions.Logging;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;
using Persistence.Contracts.Storage;

namespace Application.Features.Skills;

/// <summary>Скиллы, доступные пользователю: свои плюс общие.</summary>
file sealed class GetSkillsQueryHandler(
    ICurrentUserService currentUserService,
    ISkillRepository skillRepository)
    : IRequestHandler<GetSkillsQuery, Result<IReadOnlyCollection<SkillResponse>>>
{
    public async Task<Result<IReadOnlyCollection<SkillResponse>>> Handle(
        GetSkillsQuery request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<IReadOnlyCollection<SkillResponse>>(userIdResult.Error);

        var skills = await skillRepository.GetAvailableAsync(userIdResult.Value, cancellationToken);

        return Result.Success<IReadOnlyCollection<SkillResponse>>(
            [.. skills.Select(skill => skill.ToResponse())]);
    }
}

file sealed class UploadSkillCommandHandler(
    ICurrentUserService currentUserService,
    ISkillRepository skillRepository,
    IFileStorage fileStorage)
    : IRequestHandler<UploadSkillCommand, Result<SkillResponse>>
{
    public async Task<Result<SkillResponse>> Handle(UploadSkillCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<SkillResponse>(userIdResult.Error);

        var read = await SkillArchive.ReadAsync(request.Content, cancellationToken);
        if (read.IsFailure)
            return Result.Failure<SkillResponse>(read.Error);

        var (archive, package) = read.Value;

        await using (archive)
        {
            // Имя скилла — ключ, по которому провайдер отбирает скиллы агента, поэтому
            // одноимённых у одного владельца быть не должно.
            var existing = await skillRepository.FindByNameAsync(userIdResult.Value, package.Name, cancellationToken);

            if (existing is not null)
            {
                return Result.Failure<SkillResponse>(Error.Conflict(
                    "Skill.NameTaken",
                    $"Скилл «{package.Name}» уже загружен. Обновите его вместо создания нового."));
            }

            archive.Seek(0, SeekOrigin.Begin);

            var file = await fileStorage.SaveAsync(
                archive,
                request.FileName,
                "application/zip",
                cancellationToken);

            var skill = Skill.Create(new CreateSkillParameter
            {
                UserId = userIdResult.Value,
                Content = SkillArchive.ToContent(package, file),
            });

            await skillRepository.AddAsync(skill, cancellationToken);
            await skillRepository.SaveChangesAsync(cancellationToken);

            return skill.ToResponse();
        }
    }
}

/// <summary>
/// Перезалив содержимого скилла. Агентам, которым он выдан, сбрасывается кэш: иначе они
/// продолжили бы работать с прошлой версией до перезапуска приложения.
/// </summary>
file sealed class UpdateSkillCommandHandler(
    ICurrentUserService currentUserService,
    ISkillRepository skillRepository,
    IFileStorage fileStorage,
    IAssistantAgent assistantAgent,
    ILogger<UpdateSkillCommand> logger)
    : IRequestHandler<UpdateSkillCommand, Result<SkillResponse>>
{
    public async Task<Result<SkillResponse>> Handle(UpdateSkillCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure<SkillResponse>(userIdResult.Error);

        var skill = await skillRepository.GetByIdAsync(request.SkillId, cancellationToken);

        // Общий скилл (UserId == null) через интерфейс не правится — только своё.
        if (skill is null || skill.UserId != userIdResult.Value)
            return Result.Failure<SkillResponse>(Error.NotFound("Skill.NotFound", "Скилл не найден."));

        var read = await SkillArchive.ReadAsync(request.Content, cancellationToken);
        if (read.IsFailure)
            return Result.Failure<SkillResponse>(read.Error);

        var (archive, package) = read.Value;

        await using (archive)
        {
            var duplicate = await skillRepository.FindByNameAsync(userIdResult.Value, package.Name, cancellationToken);

            if (duplicate is not null && duplicate.Id != skill.Id)
            {
                return Result.Failure<SkillResponse>(Error.Conflict(
                    "Skill.NameTaken",
                    $"Скилл «{package.Name}» уже загружен под другим идентификатором."));
            }

            archive.Seek(0, SeekOrigin.Begin);

            var previousFileId = skill.FileId;

            var file = await fileStorage.SaveAsync(
                archive,
                request.FileName,
                "application/zip",
                cancellationToken);

            var agentIds = await skillRepository.GetAgentIdsAsync(skill.Id, cancellationToken);

            skill.UpdateContent(SkillArchive.ToContent(package, file));

            await skillRepository.SaveChangesAsync(cancellationToken);

            // Старый архив удаляем уже после того, как новый доехал до базы: упади сохранение
            // раньше — скилл остался бы записью без файла.
            if (previousFileId != file.Id && !await fileStorage.DeleteAsync(previousFileId, cancellationToken))
                logger.LogWarning("Старый архив {FileId} скилла {SkillId} удалить не удалось.", previousFileId, skill.Id);

            foreach (var agentId in agentIds)
                assistantAgent.EvictAgent(agentId);

            return skill.ToResponse();
        }
    }
}

file sealed class DeleteSkillCommandHandler(
    ICurrentUserService currentUserService,
    ISkillRepository skillRepository,
    IFileStorage fileStorage,
    IAssistantAgent assistantAgent,
    ILogger<DeleteSkillCommand> logger)
    : IRequestHandler<DeleteSkillCommand, Result>
{
    public async Task<Result> Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure(userIdResult.Error);

        var skill = await skillRepository.GetByIdAsync(request.SkillId, cancellationToken);

        if (skill is null || skill.UserId != userIdResult.Value)
            return Result.Failure(Error.NotFound("Skill.NotFound", "Скилл не найден."));

        var agentIds = await skillRepository.GetAgentIdsAsync(skill.Id, cancellationToken);
        var fileId = skill.FileId;

        skillRepository.Remove(skill);

        // Связи с агентами уходят каскадом — сами агенты остаются, просто без этого скилла.
        await skillRepository.SaveChangesAsync(cancellationToken);

        if (!await fileStorage.DeleteAsync(fileId, cancellationToken))
            logger.LogWarning("Архив {FileId} удалённого скилла {SkillId} остался в хранилище.", fileId, skill.Id);

        foreach (var agentId in agentIds)
            assistantAgent.EvictAgent(agentId);

        return Result.Success();
    }
}

/// <summary>
/// Общая для загрузки и перезалива часть: вычитать архив в память и разобрать его.
///
/// В память — потому что архив нужно прочитать дважды (разбор, потом запись в хранилище),
/// а поток из HTTP-запроса не перемотать. Размер при этом ограничен, так что в память
/// попадает не больше нескольких мегабайт.
/// </summary>
file static class SkillArchive
{
    public static async Task<Result<(MemoryStream Archive, SkillPackageInfo Package)>> ReadAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();

        try
        {
            await content.CopyToAsync(buffer, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await buffer.DisposeAsync();
            return Result.Failure<(MemoryStream, SkillPackageInfo)>(
                Error.Validation("Skill.ReadFailed", "Не удалось прочитать загруженный файл."));
        }

        if (buffer.Length == 0)
        {
            await buffer.DisposeAsync();
            return Result.Failure<(MemoryStream, SkillPackageInfo)>(
                Error.Validation("Skill.Empty", "Файл пустой."));
        }

        if (buffer.Length > SkillPackageLimits.MaxArchiveBytes)
        {
            await buffer.DisposeAsync();
            return Result.Failure<(MemoryStream, SkillPackageInfo)>(Error.Validation(
                "Skill.TooLarge",
                $"Архив больше {SkillPackageLimits.MaxArchiveBytes / 1024 / 1024} МБ."));
        }

        buffer.Seek(0, SeekOrigin.Begin);

        var result = SkillPackageReader.Read(buffer);

        if (!result.IsSuccess)
        {
            await buffer.DisposeAsync();
            return Result.Failure<(MemoryStream, SkillPackageInfo)>(
                Error.Validation("Skill.InvalidPackage", result.Error!));
        }

        return Result.Success((buffer, result.Package!));
    }

    public static SkillContentParameter ToContent(SkillPackageInfo package, StoredFileInfo file) => new()
    {
        Name = package.Name,
        Description = package.Description,
        FileId = file.Id,
        ContentHash = file.ContentHash,
        SizeBytes = file.SizeBytes,
        HasScripts = package.HasScripts,
    };
}

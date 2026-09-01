using Application.Contracts.Features.Skills;
using Application.Contracts.Features.Skills.Responses;
using Assistant.Contracts.Skills;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Скиллы — папки со SKILL.md, упакованные в zip. Карточка скилла лежит в базе, сам архив —
/// в файловом хранилище, а перед работой агента разворачивается в локальный кэш.
///
/// Имя и описание задаются не здесь, а во frontmatter внутри SKILL.md: по имени слой
/// ассистента отбирает скиллы агента, по описанию модель решает, брать скилл в работу.
/// </summary>
[Authorize]
[Route("api/skills")]
public sealed class SkillController(ISender sender) : AppController(sender)
{
    /// <summary>Скиллы, доступные пользователю: загруженные им самим плюс общие.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<SkillResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSkills(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetSkillsQuery(), cancellationToken);

        return HandleResult(result);
    }

    [HttpPost]
    [RequestSizeLimit(SkillPackageLimits.MaxArchiveBytes)]
    [ProducesResponseType<SkillResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryValidate(file, out var problem))
            return BadRequest(problem);

        await using var stream = file.OpenReadStream();

        var result = await Sender.Send(new UploadSkillCommand(file.FileName, stream), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Заливает в существующий скилл новое содержимое. Имя и описание после этого могут
    /// поменяться — они берутся из нового архива.
    /// </summary>
    [HttpPut("{id:guid}")]
    [RequestSizeLimit(SkillPackageLimits.MaxArchiveBytes)]
    [ProducesResponseType<SkillResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, IFormFile file, CancellationToken cancellationToken)
    {
        if (!TryValidate(file, out var problem))
            return BadRequest(problem);

        await using var stream = file.OpenReadStream();

        var result = await Sender.Send(new UpdateSkillCommand(id, file.FileName, stream), cancellationToken);

        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteSkillCommand(id), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Отсекает заведомо не тот файл до того, как он поедет в слой Application. Содержимое
    /// проверяется дальше, при разборе архива, — здесь только размер и расширение.
    /// </summary>
    private static bool TryValidate(IFormFile? file, out ProblemDetails problem)
    {
        if (file is null || file.Length == 0)
        {
            problem = new ProblemDetails { Title = "Файл не передан или пустой." };
            return false;
        }

        if (file.Length > SkillPackageLimits.MaxArchiveBytes)
        {
            problem = new ProblemDetails
            {
                Title = $"Архив больше {SkillPackageLimits.MaxArchiveBytes / 1024 / 1024} МБ.",
            };

            return false;
        }

        if (!Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            problem = new ProblemDetails { Title = "Скилл загружается zip-архивом с файлом SKILL.md внутри." };
            return false;
        }

        problem = null!;
        return true;
    }
}

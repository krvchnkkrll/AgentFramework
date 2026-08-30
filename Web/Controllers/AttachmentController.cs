using System.Text;
using Application.Contracts.Features.Attachments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Загрузка документов, с которыми будет работать агент.
///
/// Пока принимаются только текстовые файлы и только в память процесса — ни БД, ни объектного
/// хранилища за этим нет. Двоичные форматы (pdf, docx) потребуют извлечения текста, а хранение
/// между перезапусками — настоящего хранилища; и то и другое пока не подключено.
/// </summary>
[Authorize]
[Route("api/attachments")]
public sealed class AttachmentController(ISender sender) : AppController(sender)
{
    /// <summary>Потолок размера. Файл целиком читается в память, поэтому он нарочно скромный.</summary>
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".txt", ".md", ".csv", ".log", ".json", ".xml", ".yaml", ".yml"];

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType<AttachmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "Файл не передан или пустой." });

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new ProblemDetails
            {
                Title = $"Файл больше {MaxFileSizeBytes / 1024 / 1024} МБ.",
            });
        }

        var extension = Path.GetExtension(file.FileName);

        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = $"Поддерживаются только текстовые файлы: {string.Join(", ", AllowedExtensions)}.",
            });
        }

        await using var stream = file.OpenReadStream();

        // detectEncodingFromByteOrderMarks: файл из Windows вполне может приехать с BOM.
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var result = await Sender.Send(
            new UploadAttachmentCommand(file.FileName, file.ContentType, text),
            cancellationToken);

        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteAttachmentCommand(id), cancellationToken);

        return HandleResult(result);
    }
}

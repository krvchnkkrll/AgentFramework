using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Attachments;

/// <summary>Загруженный документ в том виде, в каком его видит фронт.</summary>
public sealed record AttachmentResponse
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long Size { get; init; }

    /// <summary>Ссылки на скачивание нет: документ живёт в памяти процесса, отдавать нечего.</summary>
    public string? Url { get; init; }

    /// <summary>Строк в документе — фронт показывает это в плашке вложения.</summary>
    public required int LineCount { get; init; }
}

/// <summary>
/// Загрузка документа. Текст читается контроллером и приходит сюда уже строкой:
/// слой Application про HTTP и IFormFile не знает.
/// </summary>
public sealed record UploadAttachmentCommand(string FileName, string ContentType, string Text)
    : IRequest<Result<AttachmentResponse>>;

public sealed record DeleteAttachmentCommand(Guid AttachmentId) : IRequest<Result>;

using Application.Contracts.Features.Attachments;
using Assistant.Contracts.Documents;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Services;

namespace Application.Features.Attachments;

file sealed class UploadAttachmentCommandHandler(
    ICurrentUserService currentUserService,
    IDocumentStore documentStore)
    : IRequestHandler<UploadAttachmentCommand, Result<AttachmentResponse>>
{
    public Task<Result<AttachmentResponse>> Handle(
        UploadAttachmentCommand request,
        CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Task.FromResult(Result.Failure<AttachmentResponse>(userIdResult.Error));

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Task.FromResult(Result.Failure<AttachmentResponse>(
                Error.Validation("Attachment.Empty", "Файл пустой.")));
        }

        var document = documentStore.Add(
            userIdResult.Value,
            request.FileName,
            request.ContentType,
            request.Text);

        return Task.FromResult(Result.Success(new AttachmentResponse
        {
            Id = document.Id,
            FileName = document.FileName,
            ContentType = document.ContentType,
            Size = document.SizeBytes,
            LineCount = document.LineCount,
        }));
    }
}

file sealed class DeleteAttachmentCommandHandler(
    ICurrentUserService currentUserService,
    IDocumentStore documentStore)
    : IRequestHandler<DeleteAttachmentCommand, Result>
{
    public Task<Result> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Task.FromResult(Result.Failure(userIdResult.Error));

        // Документа могло уже не быть — снятие вложения из черновика не обязано быть идемпотентным
        // с точки зрения клиента, но и ошибкой это считать незачем.
        documentStore.Remove(request.AttachmentId, userIdResult.Value);

        return Task.FromResult(Result.Success());
    }
}

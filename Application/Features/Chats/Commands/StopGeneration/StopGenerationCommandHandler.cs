using Application.Contracts.Features.Chats.Commands.StopGeneration;
using Application.Contracts.Services;
using Domain.Common;
using MediatR;
using Persistence.Contracts.Repositories;
using Persistence.Contracts.Services;

namespace Application.Features.Chats.Commands.StopGeneration;

file sealed class StopGenerationCommandHandler(
    ICurrentUserService currentUserService,
    IConversationRepository conversationRepository,
    IGenerationRegistryService generationRegistryService)
    : IRequestHandler<StopGenerationCommand, Result>
{
    public async Task<Result> Handle(StopGenerationCommand request, CancellationToken cancellationToken)
    {
        var userIdResult = currentUserService.GetUserId();
        if (userIdResult.IsFailure)
            return Result.Failure(userIdResult.Error);

        var conversation = await conversationRepository.GetByIdAsync(request.Body.ChatId, cancellationToken);

        if (conversation is null || conversation.UserId != userIdResult.Value)
            return Result.Failure(Error.NotFound("Chat.NotFound", "Chat was not found."));

        // Останавливать нечего — это не ошибка: пользователь мог нажать «стоп» уже после ответа.
        generationRegistryService.StopGeneration(conversation.Id);

        return Result.Success();
    }
}

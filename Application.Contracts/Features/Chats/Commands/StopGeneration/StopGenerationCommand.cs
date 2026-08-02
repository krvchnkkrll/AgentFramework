using Domain.Common;
using MediatR;

namespace Application.Contracts.Features.Chats.Commands.StopGeneration;

public sealed record StopGenerationCommand(StopGenerationRequest Body) : IRequest<Result>;

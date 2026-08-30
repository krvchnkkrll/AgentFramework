using Application.Contracts.Features.Chats.Commands.CreateChat;
using Application.Contracts.Features.Chats.Commands.DeleteChat;
using Application.Contracts.Features.Chats.Commands.PinChat;
using Application.Contracts.Features.Chats.Commands.RenameChat;
using Application.Contracts.Features.Chats.Commands.SendMessage;
using Application.Contracts.Features.Chats.Commands.SetChatAgent;
using Application.Contracts.Features.Chats.Commands.StopGeneration;
using Application.Contracts.Features.Chats.Commands.UnpinChat;
using Application.Contracts.Features.Chats.Queries.GetChat;
using Application.Contracts.Features.Chats.Queries.GetChatMessages;
using Application.Contracts.Features.Chats.Queries.GetChats;
using Application.Contracts.Features.Chats.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

[Authorize]
[Route("api/chats")]
public sealed class ConversationController(ISender sender) : AppController(sender)
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ChatSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetChats(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetChatsQuery(), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChat(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetChatQuery(new GetChatRequest { Id = id }), cancellationToken);

        return HandleResult(result);
    }

    [HttpPost]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateChat(CreateChatRequest body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateChatCommand(body), cancellationToken);

        return HandleResult(result);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameChat(Guid id, RenameChatHttpBody body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new RenameChatCommand(new RenameChatRequest { ChatId = id, Title = body.Title }),
            cancellationToken);

        return HandleResult(result);
    }

    [HttpPost("{id:guid}/pin")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PinChat(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new PinChatCommand(new PinChatRequest { ChatId = id }), cancellationToken);

        return HandleResult(result);
    }

    [HttpPost("{id:guid}/unpin")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnpinChat(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UnpinChatCommand(new UnpinChatRequest { ChatId = id }), cancellationToken);

        return HandleResult(result);
    }

    /// <summary>
    /// Меняет агента чата. Состояние сессии при этом сбрасывается — контекст, собранный
    /// прошлым агентом, новому не подходит.
    /// </summary>
    [HttpPut("{id:guid}/agent")]
    [ProducesResponseType<ChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetChatAgent(Guid id, SetChatAgentHttpBody body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SetChatAgentCommand(new SetChatAgentRequest { ChatId = id, AgentId = body.AgentId }),
            cancellationToken);

        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChat(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteChatCommand(new DeleteChatRequest { ChatId = id }), cancellationToken);

        return HandleResult(result);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType<IReadOnlyCollection<MessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatMessages(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new GetChatMessagesQuery(new GetChatMessagesRequest { ChatId = id }),
            cancellationToken);

        return HandleResult(result);
    }

    [HttpPost("{id:guid}/stop")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StopGeneration(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new StopGenerationCommand(new StopGenerationRequest { ChatId = id }),
            cancellationToken);

        return HandleResult(result);
    }

    [HttpPost("{id:guid}/messages")]
    [ProducesResponseType<MessageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(Guid id, SendMessageHttpBody body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(
            new SendMessageCommand(new SendMessageRequest
            {
                ChatId = id,
                Text = body.Text,
                AttachmentIds = body.AttachmentIds ?? [],
            }),
            cancellationToken);

        return HandleResult(result);
    }
}

public sealed record RenameChatHttpBody(string Title);

public sealed record SendMessageHttpBody(string Text, IReadOnlyList<Guid>? AttachmentIds);

public sealed record SetChatAgentHttpBody(Guid? AgentId);

using Application.Contracts.Features.Agents;
using Application.Contracts.Features.Agents.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Web.Controllers;

/// <summary>
/// Конструктор агентов. Агенты принадлежат пользователю; встроенного агента здесь нет —
/// он живёт в конфигурации приложения и отвечает в любом чате без явно выбранного агента.
/// </summary>
[Authorize]
[Route("api/agents")]
public sealed class AgentController(ISender sender) : AppController(sender)
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<AgentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAgents(CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new GetAgentsQuery(), cancellationToken);

        return HandleResult(result);
    }

    [HttpPost]
    [ProducesResponseType<AgentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAgent(SaveAgentRequest body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateAgentCommand(body), cancellationToken);

        return HandleResult(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AgentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAgent(Guid id, SaveAgentRequest body, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new UpdateAgentCommand(id, body), cancellationToken);

        return HandleResult(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAgent(Guid id, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new DeleteAgentCommand(id), cancellationToken);

        return HandleResult(result);
    }
}

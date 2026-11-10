using System.Security.Claims;
using HelpDesk.Api.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelpDesk.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tickets")]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class TicketsController(TicketService service) : ControllerBase
{
    private Actor Actor => new(User.FindFirstValue(ClaimTypes.NameIdentifier)!, User.IsInRole(Roles.Support));

    /// <summary>List own tickets, or all tickets for Support. Stable sorting and pagination.</summary>
    [HttpGet]
    public Task<PagedResult<TicketDto>> List([FromQuery] TicketQuery query, CancellationToken ct) =>
        service.ListAsync(Actor, query, ct);

    /// <summary>Get one visible ticket. Other users' tickets return 404.</summary>
    [HttpGet("{id:guid}")]
    public Task<TicketDto> Get(Guid id, CancellationToken ct) => service.GetAsync(id, Actor, ct);

    /// <summary>Create a ticket owned by the current user.</summary>
    [HttpPost]
    [ProducesResponseType<TicketDto>(201)]
    public async Task<ActionResult<TicketDto>> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = await service.CreateAsync(request, Actor, ct);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, ticket);
    }

    /// <summary>Edit title, description and priority. Supply the current version.</summary>
    [HttpPut("{id:guid}")]
    public Task<TicketDto> Edit(Guid id, EditTicketRequest request, CancellationToken ct) =>
        service.EditAsync(id, request, Actor, ct);

    /// <summary>Support: assign an open ticket to yourself.</summary>
    [Authorize(Roles = Roles.Support)]
    [HttpPost("{id:guid}/take")]
    [ProducesResponseType<ProblemDetails>(403)]
    public Task<TicketDto> Take(Guid id, VersionRequest request, CancellationToken ct) =>
        service.TakeAsync(id, request.Version, Actor, ct);

    /// <summary>Assigned Support agent: close a ticket in progress.</summary>
    [Authorize(Roles = Roles.Support)]
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType<ProblemDetails>(403)]
    public Task<TicketDto> Close(Guid id, VersionRequest request, CancellationToken ct) =>
        service.CloseAsync(id, request.Version, Actor, ct);

    /// <summary>Owner or Support: reopen a closed ticket and clear assignment.</summary>
    [HttpPost("{id:guid}/reopen")]
    public Task<TicketDto> Reopen(Guid id, VersionRequest request, CancellationToken ct) =>
        service.ReopenAsync(id, request.Version, Actor, ct);

    /// <summary>Read comments after checking ticket access.</summary>
    [HttpGet("{id:guid}/comments")]
    public Task<PagedResult<CommentDto>> Comments(Guid id, [FromQuery] PageQuery query, CancellationToken ct) =>
        service.CommentsAsync(id, query, Actor, ct);

    /// <summary>Add a comment; returns the ticket with its new version. Closed tickets are read-only.</summary>
    [HttpPost("{id:guid}/comments")]
    public Task<TicketDto> Comment(Guid id, CommentRequest request, CancellationToken ct) =>
        service.CommentAsync(id, request, Actor, ct);
}

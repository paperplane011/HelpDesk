using HelpDesk.Api.Domain;

namespace HelpDesk.Api.Application;

public sealed class TicketService(ITicketStore store, TimeProvider clock)
{
    public Task<PagedResult<TicketDto>> ListAsync(Actor actor, TicketQuery query, CancellationToken ct) =>
        store.ListAsync(actor, query, ct);

    public async Task<TicketDto> GetAsync(Guid id, Actor actor, CancellationToken ct) =>
        TicketDto.From(await FindAsync(id, actor, ct));

    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, Actor actor, CancellationToken ct)
    {
        var ticket = Ticket.Create(request.Title, request.Description, request.Priority, actor.Id, Now);
        store.Add(ticket);
        await store.SaveAsync(ct);
        return TicketDto.From(ticket);
    }

    public async Task<TicketDto> EditAsync(Guid id, EditTicketRequest request, Actor actor, CancellationToken ct)
    {
        var ticket = await FindAsync(id, actor, ct);
        CheckVersion(ticket, request.Version);
        ticket.Edit(request.Title, request.Description, request.Priority, Now);
        return await SaveAsync(ticket, ct);
    }

    public async Task<TicketDto> TakeAsync(Guid id, Guid version, Actor actor, CancellationToken ct)
    {
        RequireSupport(actor);
        var ticket = await FindAsync(id, actor, ct);
        CheckVersion(ticket, version);
        ticket.Take(actor.Id, Now);
        return await SaveAsync(ticket, ct);
    }

    public async Task<TicketDto> CloseAsync(Guid id, Guid version, Actor actor, CancellationToken ct)
    {
        RequireSupport(actor);
        var ticket = await FindAsync(id, actor, ct);
        CheckVersion(ticket, version);
        ticket.Close(actor.Id, Now);
        return await SaveAsync(ticket, ct);
    }

    public async Task<TicketDto> ReopenAsync(Guid id, Guid version, Actor actor, CancellationToken ct)
    {
        var ticket = await FindAsync(id, actor, ct);
        CheckVersion(ticket, version);
        ticket.Reopen(Now);
        return await SaveAsync(ticket, ct);
    }

    public async Task<PagedResult<CommentDto>> CommentsAsync(Guid id, PageQuery query, Actor actor, CancellationToken ct)
    {
        _ = await FindAsync(id, actor, ct); // Authorization precedes every comment read.
        return await store.CommentsAsync(id, query, ct);
    }

    public async Task<TicketDto> CommentAsync(Guid id, CommentRequest request, Actor actor, CancellationToken ct)
    {
        var ticket = await FindAsync(id, actor, ct);
        CheckVersion(ticket, request.Version);
        store.Add(ticket.AddComment(request.Body, actor.Id, Now));
        return await SaveAsync(ticket, ct);
    }

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<Ticket> FindAsync(Guid id, Actor actor, CancellationToken ct) =>
        await store.FindAsync(id, actor, ct) ?? throw new MissingTicketException();

    private async Task<TicketDto> SaveAsync(Ticket ticket, CancellationToken ct)
    {
        await store.SaveAsync(ct);
        return TicketDto.From(ticket);
    }

    private static void CheckVersion(Ticket ticket, Guid version)
    {
        if (version == Guid.Empty || version != ticket.Version)
            throw new ConflictException("Ticket has changed. Reload it and use the latest version.");
    }

    private static void RequireSupport(Actor actor)
    {
        if (!actor.IsSupport) throw new UnauthorizedAccessException("Support role required.");
    }
}

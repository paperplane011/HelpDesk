using HelpDesk.Api.Application;
using HelpDesk.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Infrastructure;

public sealed class EfTicketStore(HelpDeskDbContext db) : ITicketStore
{
    private IQueryable<Ticket> Visible(Actor actor) => actor.IsSupport
        ? db.Tickets : db.Tickets.Where(t => t.OwnerId == actor.Id);

    public Task<Ticket?> FindAsync(Guid id, Actor actor, CancellationToken ct) =>
        Visible(actor).SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<PagedResult<TicketDto>> ListAsync(Actor actor, TicketQuery query, CancellationToken ct)
    {
        var source = Visible(actor).AsNoTracking();
        if (query.Status is { } status) source = source.Where(t => t.Status == status);
        if (query.Priority is { } priority) source = source.Where(t => t.Priority == priority);
        var count = await source.CountAsync(ct);
        var ordered = (query.SortBy, query.Descending) switch
        {
            (TicketSort.UpdatedAt, true) => source.OrderByDescending(t => t.UpdatedAt),
            (TicketSort.UpdatedAt, false) => source.OrderBy(t => t.UpdatedAt),
            (TicketSort.Priority, true) => source.OrderByDescending(t => t.Priority),
            (TicketSort.Priority, false) => source.OrderBy(t => t.Priority),
            (_, false) => source.OrderBy(t => t.CreatedAt),
            _ => source.OrderByDescending(t => t.CreatedAt)
        };
        var items = await ordered.ThenBy(t => t.Id).Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(TicketDto.From).ToArray(), count, query.Page, query.PageSize);
    }

    public async Task<PagedResult<CommentDto>> CommentsAsync(Guid ticketId, PageQuery query, CancellationToken ct)
    {
        var source = db.Comments.AsNoTracking().Where(c => c.TicketId == ticketId);
        var count = await source.CountAsync(ct);
        var items = await source.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        return new(items.Select(CommentDto.From).ToArray(), count, query.Page, query.PageSize);
    }

    public void Add(Ticket ticket) => db.Tickets.Add(ticket);
    public void Add(TicketComment comment) => db.Comments.Add(comment);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

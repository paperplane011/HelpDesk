using HelpDesk.Api.Domain;

namespace HelpDesk.Api.Application;

public interface ITicketStore
{
    Task<Ticket?> FindAsync(Guid id, Actor actor, CancellationToken ct);
    Task<PagedResult<TicketDto>> ListAsync(Actor actor, TicketQuery query, CancellationToken ct);
    Task<PagedResult<CommentDto>> CommentsAsync(Guid ticketId, PageQuery query, CancellationToken ct);
    void Add(Ticket ticket);
    void Add(TicketComment comment);
    Task SaveAsync(CancellationToken ct);
}

public sealed class MissingTicketException() : Exception("Ticket not found.");
public sealed class ConflictException(string message) : Exception(message);

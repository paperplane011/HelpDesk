namespace HelpDesk.Api.Domain;

public sealed class TicketComment
{
    private TicketComment() { }

    internal TicketComment(Guid id, Guid ticketId, string authorId, string body, DateTime createdAt)
    {
        Id = id;
        TicketId = ticketId;
        AuthorId = authorId;
        Body = body;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string AuthorId { get; private set; } = "";
    public string Body { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
}

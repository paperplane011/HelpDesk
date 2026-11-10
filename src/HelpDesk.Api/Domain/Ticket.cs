namespace HelpDesk.Api.Domain;

public enum TicketPriority { Low, Normal, High, Critical }
public enum TicketStatus { Open, InProgress, Closed }

// All changes go through these methods, including changes outside HTTP.
public sealed class Ticket
{
    private Ticket() { } // EF Core materialization

    public Guid Id { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public string OwnerId { get; private set; } = "";
    public string? AssigneeId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid Version { get; private set; }

    public static Ticket Create(string title, string description, TicketPriority priority,
        string ownerId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(ownerId)) throw new RuleException("Owner is required.");
        var ticket = new Ticket { Id = Guid.NewGuid(), OwnerId = ownerId, CreatedAt = now };
        ticket.Edit(title, description, priority, now);
        return ticket;
    }

    public void Edit(string title, string description, TicketPriority priority, DateTime now)
    {
        EnsureOpenForChanges();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new RuleException("Title must contain 1 to 200 non-whitespace characters.");
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 10000)
            throw new RuleException("Description must contain 1 to 10000 characters.");
        if (!Enum.IsDefined(priority)) throw new RuleException("Unknown priority.");
        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
        Touch(now);
    }

    public void Take(string supportId, DateTime now)
    {
        if (Status != TicketStatus.Open) throw new RuleException("Only an open ticket can be taken.");
        if (string.IsNullOrWhiteSpace(supportId)) throw new RuleException("Assignee is required.");
        AssigneeId = supportId;
        Status = TicketStatus.InProgress;
        Touch(now);
    }

    public void Close(string supportId, DateTime now)
    {
        if (Status != TicketStatus.InProgress || AssigneeId != supportId)
            throw new RuleException("Only the assigned support agent can close a ticket in progress.");
        Status = TicketStatus.Closed;
        ClosedAt = now;
        Touch(now);
    }

    public void Reopen(DateTime now)
    {
        if (Status != TicketStatus.Closed) throw new RuleException("Only a closed ticket can be reopened.");
        Status = TicketStatus.Open;
        AssigneeId = null;
        ClosedAt = null;
        Touch(now);
    }

    public TicketComment AddComment(string body, string authorId, DateTime now)
    {
        EnsureOpenForChanges();
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > 4000)
            throw new RuleException("Comment must contain 1 to 4000 characters.");
        Touch(now); // Also protects a comment racing with ticket closure.
        return new TicketComment(Guid.NewGuid(), Id, authorId, body.Trim(), now);
    }

    private void EnsureOpenForChanges()
    {
        if (Status == TicketStatus.Closed)
            throw new RuleException("Reopen the closed ticket before editing or commenting.");
    }

    private void Touch(DateTime now)
    {
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}

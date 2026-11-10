using System.ComponentModel.DataAnnotations;
using HelpDesk.Api.Domain;

namespace HelpDesk.Api.Application;

public static class Roles
{
    public const string Support = "Support";
}

public sealed record Actor(string Id, bool IsSupport);

public sealed class CreateTicketRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = "";
    [Required, StringLength(10000, MinimumLength = 1)]
    public string Description { get; init; } = "";
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; } = TicketPriority.Normal;
}

public sealed class EditTicketRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; init; } = "";
    [Required, StringLength(10000, MinimumLength = 1)]
    public string Description { get; init; } = "";
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; }
    public Guid Version { get; init; }
}

public record VersionRequest(Guid Version);
public sealed record CommentRequest([Required, StringLength(4000, MinimumLength = 1)] string Body, Guid Version);

public enum TicketSort { CreatedAt, UpdatedAt, Priority }

public sealed class TicketQuery
{
    [EnumDataType(typeof(TicketStatus))]
    public TicketStatus? Status { get; init; }
    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority? Priority { get; init; }
    [EnumDataType(typeof(TicketSort))]
    public TicketSort SortBy { get; init; } = TicketSort.CreatedAt;
    public bool Descending { get; init; } = true;
    [Range(1, 1000000)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed class PageQuery
{
    [Range(1, 1000000)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public sealed record TicketDto(Guid Id, string Title, string Description, TicketPriority Priority,
    TicketStatus Status, string OwnerId, string? AssigneeId, DateTime CreatedAt, DateTime UpdatedAt,
    DateTime? ClosedAt, Guid Version)
{
    public static TicketDto From(Ticket t) => new(t.Id, t.Title, t.Description, t.Priority, t.Status,
        t.OwnerId, t.AssigneeId, t.CreatedAt, t.UpdatedAt, t.ClosedAt, t.Version);
}

public sealed record CommentDto(Guid Id, Guid TicketId, string AuthorId, string Body, DateTime CreatedAt)
{
    public static CommentDto From(TicketComment c) => new(c.Id, c.TicketId, c.AuthorId, c.Body, c.CreatedAt);
}

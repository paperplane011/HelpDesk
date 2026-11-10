using HelpDesk.Api.Domain;

namespace HelpDesk.Api.Tests;

public sealed class TicketTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static Ticket NewTicket() => Ticket.Create(" VPN ", " Details ", TicketPriority.High, "owner", Now);

    [Fact]
    public void Create_SetsDefaultsAndTrimsText()
    {
        var ticket = NewTicket();
        Assert.Equal("VPN", ticket.Title);
        Assert.Equal("Details", ticket.Description);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ClosedAt);
        Assert.Equal(Now, ticket.CreatedAt);
        Assert.NotEqual(Guid.Empty, ticket.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    public void Create_RejectsBlankTitle(string title) =>
        Assert.Throws<RuleException>(() => Ticket.Create(title, "Details", TicketPriority.Normal, "owner", Now));

    [Fact]
    public void Create_RejectsTooLongTitle() =>
        Assert.Throws<RuleException>(() => Ticket.Create(new string('a', 201), "Details", TicketPriority.Normal, "owner", Now));

    [Fact]
    public void Create_RejectsUnknownPriority() =>
        Assert.Throws<RuleException>(() => Ticket.Create("Title", "Details", (TicketPriority)99, "owner", Now));

    [Fact]
    public void ClosedTicket_RejectsEditsAndComments()
    {
        var ticket = NewTicket();
        ticket.Take("agent", Now);
        ticket.Close("agent", Now);
        Assert.Throws<RuleException>(() => ticket.Edit("Changed", "Details", TicketPriority.Low, Now));
        Assert.Throws<RuleException>(() => ticket.AddComment("Text", "owner", Now));
    }

    [Fact]
    public void Close_RequiresAssignedAgentAndInProgress()
    {
        var ticket = NewTicket();
        Assert.Throws<RuleException>(() => ticket.Close("agent", Now));
        ticket.Take("agent", Now);
        Assert.Throws<RuleException>(() => ticket.Close("another-agent", Now));
        ticket.Close("agent", Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), ticket.ClosedAt);
    }

    [Fact]
    public void Reopen_ClearsAssignmentAndAllowsEditing()
    {
        var ticket = NewTicket();
        ticket.Take("agent", Now);
        ticket.Close("agent", Now);
        var version = ticket.Version;
        ticket.Reopen(Now.AddMinutes(1));
        ticket.Edit("New title", "Details", TicketPriority.Normal, Now.AddMinutes(2));
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ClosedAt);
        Assert.NotEqual(version, ticket.Version);
        Assert.Equal(Now, ticket.CreatedAt);
    }

    [Fact]
    public void Take_TwiceFails()
    {
        var ticket = NewTicket();
        ticket.Take("first", Now);
        Assert.Throws<RuleException>(() => ticket.Take("second", Now));
        Assert.Equal("first", ticket.AssigneeId);
    }

    [Fact]
    public void Comment_ChangesTicketVersion()
    {
        var ticket = NewTicket();
        var version = ticket.Version;
        var comment = ticket.AddComment(" Hello ", "owner", Now.AddMinutes(1));
        Assert.Equal("Hello", comment.Body);
        Assert.Equal(ticket.Id, comment.TicketId);
        Assert.NotEqual(version, ticket.Version);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Comment_RejectsBlankText(string body) =>
        Assert.Throws<RuleException>(() => NewTicket().AddComment(body, "owner", Now));
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HelpDesk.Api.Application;
using HelpDesk.Api.Domain;
using HelpDesk.Api.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace HelpDesk.Api.Tests;

public sealed class ApiTests : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly ApiFactory factory = new();
    private HttpClient anonymous = null!;
    private readonly string password = "Aa1!" + Guid.NewGuid().ToString("N");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async ValueTask InitializeAsync()
    {
        anonymous = NewClient();
        await factory.InitializeDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        anonymous.Dispose();
        await factory.DisposeAsync();
    }

    [Fact]
    public async Task AnonymousTicketRequest_Returns401Problem()
    {
        var response = await anonymous.GetAsync("/api/tickets", cancellationToken: Ct);
        await AssertProblem(response, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegistrationLoginRefresh_UseRealIdentity()
    {
        var client = await RegisterAsync("alice");
        var response = await client.GetAsync("/api/auth/manage/info", cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("alice@example.test", await response.Content.ReadAsStringAsync(cancellationToken: Ct));
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password }, cancellationToken: Ct);
        var tokens = await login.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
        var refreshed = await anonymous.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = tokens.GetProperty("refreshToken").GetString() }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var wrong = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "wrong" }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    [Fact]
    public async Task DuplicateEmailAndWeakPassword_Return400()
    {
        _ = await RegisterAsync("alice");
        var duplicate = await anonymous.PostAsJsonAsync("/api/auth/register", new { email = "alice@example.test", password }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        var weak = await anonymous.PostAsJsonAsync("/api/auth/register", new { email = "weak@example.test", password = "short" }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
    }

    [Fact]
    public async Task Create_Returns201LocationAndOwnerFromToken()
    {
        var alice = await RegisterAsync("alice");
        var response = await alice.PostAsJsonAsync("/api/tickets", new { title = "VPN", description = "Details", priority = "High", ownerId = "attacker", status = "Closed" }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var ticket = await ReadTicket(response);
        Assert.NotEqual("attacker", ticket.OwnerId);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(HttpStatusCode.OK, (await alice.GetAsync(response.Headers.Location, cancellationToken: Ct)).StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidTitle_Returns400(string title)
    {
        var alice = await RegisterAsync("alice");
        await AssertProblem(await alice.PostAsJsonAsync("/api/tickets", new { title, description = "Details", priority = "Normal" }, cancellationToken: Ct), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task InvalidEnumsAndPagination_Return400()
    {
        var alice = await RegisterAsync("alice");
        await AssertProblem(await alice.PostAsJsonAsync("/api/tickets", new { title = "Title", description = "Details", priority = "Urgent" }, cancellationToken: Ct), HttpStatusCode.BadRequest);
        await AssertProblem(await alice.PostAsJsonAsync("/api/tickets", new { title = "Title", description = "Details", priority = 99 }, cancellationToken: Ct), HttpStatusCode.BadRequest);
        foreach (var query in new[] { "page=0", "pageSize=101", "status=999", "sortBy=Unknown", "priority=Unknown", "page=2147483647" })
            await AssertProblem(await alice.GetAsync("/api/tickets?" + query, cancellationToken: Ct), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("edit")]
    [InlineData("comments")]
    [InlineData("comment")]
    [InlineData("reopen")]
    public async Task OtherUsersTicket_IsHiddenOnEveryAccessibleRoute(string operation)
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var ticket = await Create(alice);
        var url = "/api/tickets/" + ticket.Id;
        var response = operation switch
        {
            "get" => await bob.GetAsync(url, cancellationToken: Ct),
            "edit" => await bob.PutAsJsonAsync(url, Edit(ticket), cancellationToken: Ct),
            "comments" => await bob.GetAsync(url + "/comments", cancellationToken: Ct),
            "comment" => await bob.PostAsJsonAsync(url + "/comments", new { body = "Intrusion", ticket.Version }, cancellationToken: Ct),
            _ => await bob.PostAsJsonAsync(url + "/reopen", new { ticket.Version }, cancellationToken: Ct)
        };
        await AssertProblem(response, HttpStatusCode.NotFound);
        var list = await bob.GetFromJsonAsync<PagedResult<TicketDto>>("/api/tickets", Json, cancellationToken: Ct);
        Assert.Empty(list!.Items);
    }

    [Theory]
    [InlineData("take")]
    [InlineData("close")]
    public async Task RegularUser_CannotCallSupportOperations(string operation)
    {
        var alice = await RegisterAsync("alice");
        var ticket = await Create(alice);
        await AssertProblem(await alice.PostAsJsonAsync($"/api/tickets/{ticket.Id}/{operation}", new { ticket.Version }, cancellationToken: Ct), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Registration_CannotSelfAssignSupportRole()
    {
        var response = await anonymous.PostAsJsonAsync("/api/auth/register", new { email = "mallory@example.test", password, role = "Support", roles = new[] { "Support" } }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mallory = await LoginAsync("mallory");
        var ticket = await Create(mallory);
        await AssertProblem(await mallory.PostAsJsonAsync($"/api/tickets/{ticket.Id}/take", new { ticket.Version }, cancellationToken: Ct), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Support_SeesAllTickets()
    {
        var alice = await RegisterAsync("alice");
        var bob = await RegisterAsync("bob");
        var agent = await RegisterAsync("agent", true);
        _ = await Create(alice);
        var bobs = await Create(bob);
        var list = await agent.GetFromJsonAsync<PagedResult<TicketDto>>("/api/tickets", Json, cancellationToken: Ct);
        Assert.Equal(2, list!.TotalCount);
        Assert.Equal(HttpStatusCode.OK, (await agent.GetAsync("/api/tickets/" + bobs.Id, cancellationToken: Ct)).StatusCode);
    }

    [Fact]
    public async Task FullWorkflow_EnforcesClosedReadOnlyAndReopen()
    {
        var alice = await RegisterAsync("alice");
        var agent = await RegisterAsync("agent", true);
        var ticket = await Create(alice);
        ticket = await ReadTicket(await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/take", new { ticket.Version }, cancellationToken: Ct));
        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        ticket = await ReadTicket(await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { body = "Fixed", ticket.Version }, cancellationToken: Ct));
        var comments = await alice.GetFromJsonAsync<PagedResult<CommentDto>>($"/api/tickets/{ticket.Id}/comments", Json, cancellationToken: Ct);
        Assert.Equal("Fixed", Assert.Single(comments!.Items).Body);
        ticket = await ReadTicket(await agent.PostAsJsonAsync($"/api/tickets/{ticket.Id}/close", new { ticket.Version }, cancellationToken: Ct));
        Assert.Equal(TicketStatus.Closed, ticket.Status);
        Assert.NotNull(ticket.ClosedAt);
        await AssertProblem(await alice.PutAsJsonAsync("/api/tickets/" + ticket.Id, Edit(ticket), cancellationToken: Ct), HttpStatusCode.Conflict);
        await AssertProblem(await alice.PostAsJsonAsync($"/api/tickets/{ticket.Id}/comments", new { body = "Late", ticket.Version }, cancellationToken: Ct), HttpStatusCode.Conflict);
        ticket = await ReadTicket(await alice.PostAsJsonAsync($"/api/tickets/{ticket.Id}/reopen", new { ticket.Version }, cancellationToken: Ct));
        Assert.Null(ticket.AssigneeId);
        Assert.Null(ticket.ClosedAt);
        var edited = await ReadTicket(await alice.PutAsJsonAsync("/api/tickets/" + ticket.Id, Edit(ticket), cancellationToken: Ct));
        Assert.Equal("Updated", edited.Title);
    }

    [Fact]
    public async Task DifferentSupportAgent_CannotCloseAssignedTicket()
    {
        var alice = await RegisterAsync("alice");
        var first = await RegisterAsync("first", true);
        var second = await RegisterAsync("second", true);
        var ticket = await Create(alice);
        ticket = await ReadTicket(await first.PostAsJsonAsync($"/api/tickets/{ticket.Id}/take", new { ticket.Version }, cancellationToken: Ct));
        await AssertProblem(await second.PostAsJsonAsync($"/api/tickets/{ticket.Id}/close", new { ticket.Version }, cancellationToken: Ct), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StaleVersion_Returns409AndPreservesLatestData()
    {
        var alice = await RegisterAsync("alice");
        var ticket = await Create(alice);
        var edited = await ReadTicket(await alice.PutAsJsonAsync("/api/tickets/" + ticket.Id, Edit(ticket), cancellationToken: Ct));
        await AssertProblem(await alice.PutAsJsonAsync("/api/tickets/" + ticket.Id, Edit(ticket), cancellationToken: Ct), HttpStatusCode.Conflict);
        var current = await alice.GetFromJsonAsync<TicketDto>("/api/tickets/" + ticket.Id, Json, cancellationToken: Ct);
        Assert.Equal(edited.Version, current!.Version);
        Assert.Equal("Updated", current.Title);
    }

    [Fact]
    public async Task DatabaseConcurrency_RejectsSecondWriter()
    {
        var alice = await RegisterAsync("alice");
        var ticket = await Create(alice);
        using var scope1 = factory.Services.CreateScope();
        using var scope2 = factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        var first = await db1.Tickets.SingleAsync(t => t.Id == ticket.Id, cancellationToken: Ct);
        var second = await db2.Tickets.SingleAsync(t => t.Id == ticket.Id, cancellationToken: Ct);
        first.Edit("First", "Details", TicketPriority.Normal, DateTime.UtcNow);
        second.Edit("Second", "Details", TicketPriority.High, DateTime.UtcNow);
        await db1.SaveChangesAsync(cancellationToken: Ct);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () => await db2.SaveChangesAsync(cancellationToken: Ct));
    }

    [Fact]
    public async Task FilteringSortingPagination_AreAppliedInDatabase()
    {
        var alice = await RegisterAsync("alice");
        _ = await Create(alice, "Low", "First");
        _ = await Create(alice, "High", "Second");
        _ = await Create(alice, "Critical", "Third");
        var filtered = await alice.GetFromJsonAsync<PagedResult<TicketDto>>("/api/tickets?status=Open&priority=High", Json, cancellationToken: Ct);
        Assert.Equal("Second", Assert.Single(filtered!.Items).Title);
        var page = await alice.GetFromJsonAsync<PagedResult<TicketDto>>("/api/tickets?sortBy=Priority&descending=true&page=2&pageSize=1", Json, cancellationToken: Ct);
        Assert.Equal(3, page!.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(TicketPriority.High, Assert.Single(page.Items).Priority);
        var empty = await alice.GetFromJsonAsync<PagedResult<TicketDto>>("/api/tickets?page=99", Json, cancellationToken: Ct);
        Assert.Empty(empty!.Items);
    }

    [Fact]
    public async Task MissingTicket_Returns404()
    {
        var alice = await RegisterAsync("alice");
        await AssertProblem(await alice.GetAsync("/api/tickets/" + Guid.NewGuid(), cancellationToken: Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SwaggerAndReadiness_AreAvailable()
    {
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health/ready", cancellationToken: Ct)).StatusCode);
        var response = await anonymous.GetAsync("/swagger/v1/swagger.json", cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var spec = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
        Assert.True(spec.GetProperty("paths").TryGetProperty("/api/tickets/{id}/take", out _));
        Assert.True(spec.GetProperty("paths").TryGetProperty("/api/auth/login", out _));
        Assert.Contains("swagger-ui", await anonymous.GetStringAsync("/swagger/index.html", cancellationToken: Ct));
    }

    [Fact]
    public async Task DemoSeed_IsRepeatableAndCreatesDifferentOwners()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HelpDesk:SeedDemo"] = "true", ["HelpDesk:DemoPassword"] = password
        }).Build();
        await DatabaseSetup.InitializeAsync(factory.Services, config);
        await DatabaseSetup.InitializeAsync(factory.Services, config);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        Assert.Equal(3, await db.Tickets.CountAsync(Ct));
        Assert.Equal(3, await db.Users.CountAsync(Ct));
        Assert.Equal(1, await db.Comments.CountAsync(Ct));
        Assert.Equal(2, await db.Tickets.Select(t => t.OwnerId).Distinct().CountAsync(Ct));
    }

    [Fact]
    public async Task DatabaseRead_SerializesDatesWithUtcSuffix()
    {
        var alice = await RegisterAsync("alice");
        var ticket = await Create(alice);
        var payload = await alice.GetFromJsonAsync<JsonElement>("/api/tickets/" + ticket.Id, cancellationToken: Ct);
        Assert.EndsWith("Z", payload.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", payload.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task MissingVersion_IsRejected()
    {
        var alice = await RegisterAsync("alice");
        var ticket = await Create(alice);
        await AssertProblem(await alice.PutAsJsonAsync("/api/tickets/" + ticket.Id,
            new { title = "No version", description = "Details", priority = "Normal" }, cancellationToken: Ct), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RepeatedFailedLogins_LockTheAccount()
    {
        _ = await RegisterAsync("alice");
        for (var i = 0; i < 5; i++)
        {
            var failed = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password = "wrong" }, cancellationToken: Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        }
        var correct = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "alice@example.test", password }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
    }

    [Fact]
    public async Task AuthenticationRateLimit_Returns429()
    {
        for (var i = 0; i < 30; i++)
            _ = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password }, cancellationToken: Ct);
        await AssertProblem(await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = "missing@example.test", password }, cancellationToken: Ct), HttpStatusCode.TooManyRequests);
    }

    private HttpClient NewClient() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private async Task<HttpClient> RegisterAsync(string name, bool support = false)
    {
        var response = await anonymous.PostAsJsonAsync("/api/auth/register", new { email = name + "@example.test", password }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (support) await factory.GrantSupportAsync(name + "@example.test");
        return await LoginAsync(name);
    }

    private async Task<HttpClient> LoginAsync(string name)
    {
        var response = await anonymous.PostAsJsonAsync("/api/auth/login?useCookies=false", new { email = name + "@example.test", password }, cancellationToken: Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
        var client = NewClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<TicketDto> Create(HttpClient client, string priority = "Normal", string title = "VPN") =>
        await ReadTicket(await client.PostAsJsonAsync("/api/tickets", new { title, description = "Details", priority }, cancellationToken: Ct));

    private static object Edit(TicketDto ticket) => new { title = "Updated", description = "Details", priority = "High", ticket.Version };

    private static async Task<TicketDto> ReadTicket(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(cancellationToken: Ct));
        return (await response.Content.ReadFromJsonAsync<TicketDto>(Json, cancellationToken: Ct))!;
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: Ct);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.True(problem.TryGetProperty("traceId", out _));
    }
}

using HelpDesk.Api.Application;
using HelpDesk.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api.Infrastructure;

public static class DatabaseSetup
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        if (configuration.GetValue<bool>("HelpDesk:MigrateOnStartup")) await db.Database.MigrateAsync();
        if (!configuration.GetValue<bool>("HelpDesk:SeedDemo")) return;
        var password = configuration["HelpDesk:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set HelpDesk__DemoPassword before enabling demo seed.");

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(Roles.Support)) Check(await roles.CreateAsync(new(Roles.Support)));
        var alice = await EnsureUserAsync(users, "alice@example.test", password);
        var bob = await EnsureUserAsync(users, "bob@example.test", password);
        var agent = await EnsureUserAsync(users, "support@example.test", password);
        if (!await users.IsInRoleAsync(agent, Roles.Support)) Check(await users.AddToRoleAsync(agent, Roles.Support));

        // Seed only an empty ticket database; do not duplicate tickets on a restart.
        if (await db.Tickets.AnyAsync()) return;
        var now = DateTime.UtcNow;
        var open = Ticket.Create("VPN не подключается", "После смены пароля не удаётся подключиться к VPN.", TicketPriority.High, alice.Id, now.AddHours(-3));
        var active = Ticket.Create("Нет доступа к принтеру", "Принтер в переговорной недоступен.", TicketPriority.Normal, bob.Id, now.AddHours(-2));
        active.Take(agent.Id, now.AddHours(-1));
        var closed = Ticket.Create("Установка редактора", "Нужен редактор кода для учебного проекта.", TicketPriority.Low, alice.Id, now.AddDays(-1));
        closed.Take(agent.Id, now.AddHours(-6));
        var comment = closed.AddComment("Редактор установлен, запуск проверен.", agent.Id, now.AddHours(-5));
        closed.Close(agent.Id, now.AddHours(-4));
        db.Tickets.AddRange(open, active, closed);
        db.Comments.Add(comment);
        await db.SaveChangesAsync();
    }

    public static async Task GrantSupportAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var user = await users.FindByEmailAsync(email) ?? throw new InvalidOperationException("Register this user first.");
        if (!await roles.RoleExistsAsync(Roles.Support)) Check(await roles.CreateAsync(new(Roles.Support)));
        Check(await users.AddToRoleAsync(user, Roles.Support));
        await users.UpdateSecurityStampAsync(user);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(UserManager<ApplicationUser> users, string email, string password)
    {
        var existing = await users.FindByEmailAsync(email);
        if (existing is not null) return existing;
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        Check(await users.CreateAsync(user, password));
        return user;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}

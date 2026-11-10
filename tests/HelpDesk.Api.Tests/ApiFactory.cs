using HelpDesk.Api.Application;
using HelpDesk.Api.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace HelpDesk.Api.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection sqlite = new("Data Source=:memory:");
    private readonly string? sqlConnection;

    public ApiFactory()
    {
        var template = Environment.GetEnvironmentVariable("HELPDESK_TEST_SQLSERVER");
        if (!string.IsNullOrWhiteSpace(template))
        {
            var cs = new SqlConnectionStringBuilder(template) { InitialCatalog = "HelpDeskTests_" + Guid.NewGuid().ToString("N") };
            sqlConnection = cs.ConnectionString;
        }
        else sqlite.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["HelpDesk:MigrateOnStartup"] = "false",
            ["HelpDesk:SeedDemo"] = "false",
            ["HelpDesk:EnableSwagger"] = "true",
            ["Logging:LogLevel:Default"] = "Warning"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<HelpDeskDbContext>();
            services.RemoveAll<DbContextOptions<HelpDeskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<HelpDeskDbContext>>();
            services.AddDbContext<HelpDeskDbContext>(o =>
            {
                if (sqlConnection is null) o.UseSqlite(sqlite);
                else o.UseSqlServer(sqlConnection);
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>();
        if (sqlConnection is null) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();
    }

    public async Task GrantSupportAsync(string email)
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(Roles.Support))
            Assert.True((await roles.CreateAsync(new(Roles.Support))).Succeeded);
        var user = await users.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.True((await users.AddToRoleAsync(user, Roles.Support)).Succeeded);
    }

    public override async ValueTask DisposeAsync()
    {
        if (sqlConnection is not null)
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<HelpDeskDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
        await sqlite.DisposeAsync();
    }
}

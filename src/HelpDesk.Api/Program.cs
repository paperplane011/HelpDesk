using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HelpDesk.Api;
using HelpDesk.Api.Application;
using HelpDesk.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<HelpDeskDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("HelpDesk"), sql => sql.EnableRetryOnFailure()));
builder.Services.AddScoped<ITicketStore, EfTicketStore>();
builder.Services.AddScoped<TicketService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 12;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
}).AddRoles<IdentityRole>().AddEntityFrameworkStores<HelpDeskDbContext>();
builder.Services.Configure<Microsoft.AspNetCore.Authentication.BearerToken.BearerTokenOptions>(
    IdentityConstants.BearerScheme, options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromMinutes(30);
        options.RefreshTokenExpiration = TimeSpan.FromDays(7);
    });
var protection = builder.Services.AddDataProtection().SetApplicationName("HelpDesk.Api");
if (builder.Configuration["HelpDesk:DataProtectionPath"] is { Length: > 0 } keyPath)
    protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddAuthorization();
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    c.ProblemDetails.Extensions["traceId"] = c.HttpContext.TraceIdentifier;
    c.ProblemDetails.Instance = c.HttpContext.Request.Path;
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
        }));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HelpDesk API", Version = "v1",
        Description = "Register/login at /api/auth. Use the accessToken in Authorize. Identity tokens are opaque, not JWT. Mutations require the latest ticket version."
    });
    o.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "HelpDesk.Api.xml"));
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer",
        Description = "Paste accessToken only (without the Bearer prefix)."
    });
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("HelpDesk:EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "HelpDesk v1"));
}
app.MapControllers();
app.MapGroup("/api/auth").WithTags("Identity").RequireRateLimiting("auth").MapIdentityApi<ApplicationUser>();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" })).AllowAnonymous();
app.MapGet("/health/ready", async (HelpDeskDbContext db, CancellationToken ct) =>
{
    try
    {
        // Also verifies that the application schema exists, not just the SQL login.
        _ = await db.Tickets.Select(t => t.Id).Take(1).ToListAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) when (!ct.IsCancellationRequested)
    {
        return Results.StatusCode(503);
    }
}).AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

var grantIndex = Array.IndexOf(args, "--grant-support");
if (grantIndex >= 0)
{
    if (grantIndex + 1 >= args.Length) throw new ArgumentException("Provide an email after --grant-support.");
    await DatabaseSetup.GrantSupportAsync(app.Services, args[grantIndex + 1]);
    return;
}
await DatabaseSetup.InitializeAsync(app.Services, app.Configuration);
app.Run();

// WebApplicationFactory needs a public entry point.
public partial class Program;

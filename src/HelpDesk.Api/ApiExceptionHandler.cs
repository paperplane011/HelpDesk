using HelpDesk.Api.Application;
using HelpDesk.Api.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            MissingTicketException => (404, "Ticket not found", exception.Message),
            ConflictException => (409, "Concurrent change", exception.Message),
            DbUpdateConcurrencyException => (409, "Concurrent change", "Ticket has changed. Reload and retry."),
            RuleException => (409, "Business rule violation", exception.Message),
            UnauthorizedAccessException => (403, "Forbidden", "Support role required."),
            _ => (500, "Unexpected error", "An unexpected error occurred. Use traceId when contacting support.")
        };
        if (status == 500) logger.LogError(exception, "Unhandled request error. TraceId: {TraceId}", context.TraceIdentifier);
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Middleware;

public class ExceptionMiddleware
{
    // SQL Server error numbers for unique index / unique constraint violations.
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _log;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _log.LogWarning(ex, "Concurrency conflict");
            await WriteError(ctx, StatusCodes.Status409Conflict,
                "The record was modified by another request. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _log.LogWarning(ex, "Unique constraint violation");
            await WriteError(ctx, StatusCodes.Status409Conflict,
                "The change conflicts with existing data. Refresh and try again.");
        }
        catch (DbUpdateException ex)
        {
            // Constraint or data problem that validation should have caught; not an outage.
            _log.LogError(ex, "Database update failed");
            await WriteError(ctx, StatusCodes.Status500InternalServerError,
                "The change could not be saved. Please check the data and try again.");
        }
        catch (SqlException ex)
        {
            // Connection failures, timeouts, deadlocks, etc. (not the caller's fault).
            _log.LogError(ex, "Database unavailable or failed");
            await WriteError(ctx, StatusCodes.Status503ServiceUnavailable,
                "The database is currently unavailable. Please try again shortly.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled exception");
            await WriteError(ctx, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred. Please try again.");
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };

    private static Task WriteError(HttpContext ctx, int statusCode, string message)
    {
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(
            new { error = message, errors = new[] { message } }));
    }
}
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Common;

namespace Zatyshok.Api.Common.Errors;

/// <summary>
/// Turns the expected exceptions of the services into ProblemDetails responses (technical decisions
/// of the API: 400 for a value only a service can check, 404, 409 for a conflict of state). It is an
/// MVC filter, not the exception handler middleware, because that middleware logs every exception as
/// an error, and a forbidden transition is a normal answer, not a failure. Any other exception reaches
/// UseExceptionHandler and gives 500.
/// </summary>
public sealed class KnownExceptionFilter(ILogger<KnownExceptionFilter> logger) : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        // A value that only the service could check, because the rule needs data (e.g. the hotel's current
        // date): the same 400 as for a validator error, with the message under the name of the field.
        if (context.Exception is InvalidRequestException invalid)
        {
            logger.LogInformation("Request rejected with 400: {Field}: {Detail}", invalid.Field, invalid.Message);
            context.ModelState.AddModelError(invalid.Field, invalid.Message);
            context.Result = ValidationProblems.CreateResult(context);
            context.ExceptionHandled = true;
            return;
        }

        if (Map(context.Exception) is not { } mapped)
        {
            return;
        }

        var (status, detail) = mapped;
        logger.LogInformation("Request rejected with {StatusCode}: {Detail}", status, detail);
        context.Result = ProblemDetailsSetup.Result(context.HttpContext, status, detail);
        context.ExceptionHandled = true;
    }

    /// <summary>Status code and Ukrainian detail for an expected exception, or null for an unexpected one.</summary>
    public static (int Status, string Detail)? Map(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
        HotelScopeException => (StatusCodes.Status403Forbidden, exception.Message),

        // A broken domain rule, including a forbidden transition (InvalidTransitionException, FR-16).
        DomainException => (StatusCodes.Status409Conflict, exception.Message),

        // The xmin token (12.1.3): another request changed the row after it was read.
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
            "Запис щойно змінив інший користувач. Оновіть дані та повторіть дію."),

        // Safety net: services translate the constraints they expect into precise messages,
        // so these generic ones appear only for a constraint a service did not foresee.
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }
            => (StatusCodes.Status409Conflict, "Запис із такими даними вже існує."),
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } }
            => (StatusCodes.Status409Conflict, "Дію не виконано: запис пов'язаний з іншими даними."),

        _ => null,
    };
}

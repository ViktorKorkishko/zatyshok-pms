using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Zatyshok.Api.Common.Logging;

namespace Zatyshok.Api.Common.Errors;

/// <summary>
/// Every error of the API is a ProblemDetails (RFC 9457) in Ukrainian (NFR-13): the title names the
/// kind of error by its status code, the detail says what exactly went wrong.
/// </summary>
public static class ProblemDetailsSetup
{
    // Titles by status code. ASP.NET Core writes English ones ("Not Found"); they are replaced here,
    // so the title is the same for an error from a service, from validation or from routing.
    private static readonly Dictionary<int, string> Titles = new()
    {
        [StatusCodes.Status400BadRequest] = "Запит містить помилки",
        [StatusCodes.Status401Unauthorized] = "Потрібно увійти в систему",
        [StatusCodes.Status403Forbidden] = "Доступ заборонено",
        [StatusCodes.Status404NotFound] = "Не знайдено",
        [StatusCodes.Status405MethodNotAllowed] = "Метод не підтримується",
        [StatusCodes.Status406NotAcceptable] = "Формат відповіді не підтримується",
        [StatusCodes.Status409Conflict] = "Конфлікт із поточним станом даних",
        [StatusCodes.Status415UnsupportedMediaType] = "Формат тіла запиту не підтримується",
        [StatusCodes.Status500InternalServerError] = "Внутрішня помилка сервера",
    };

    /// <summary>
    /// Registers the ProblemDetails service used by the exception handler, by status code pages
    /// (e.g. 404 for an unknown path) and by MVC (validation, NotFound()).
    /// </summary>
    public static IServiceCollection AddUkrainianProblemDetails(this IServiceCollection services)
        => services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            if (problem.Status is { } status && Titles.TryGetValue(status, out var title))
            {
                problem.Title = title;
            }

            // The same id as in the X-Correlation-Id header and in the log, so a user can report it.
            if (CorrelationIdMiddleware.Get(context.HttpContext) is { } correlationId)
            {
                problem.Extensions["correlationId"] = correlationId;
            }
        });

    /// <summary>A ProblemDetails result for the MVC filters; the title and the correlation id are added by the factory.</summary>
    public static ObjectResult Result(HttpContext httpContext, int status, string detail)
    {
        var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateProblemDetails(httpContext, status, detail: detail);
        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
    }
}

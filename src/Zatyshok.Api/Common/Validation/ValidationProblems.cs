using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// The 400 response for both kinds of invalid input: a value MVC could not read (malformed JSON, a
/// query value of the wrong type) and a rule of a FluentValidation validator. Both give the same
/// ValidationProblemDetails, whose <c>errors</c> are keyed by the JSON names of the fields.
/// </summary>
public static class ValidationProblems
{
    /// <summary>The detail of every 400 response; the errors themselves are in <c>errors</c>.</summary>
    public const string Detail = "Перевірте значення полів, перелічених в errors.";

    /// <summary>Used as <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> and by <see cref="ValidationFilter"/>.</summary>
    public static IActionResult CreateResult(ActionContext context)
    {
        // MVC names errors by the C# property ("Name"), FluentValidation too; the client sends "name".
        var modelState = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                // An error added from an exception may have no text; its English message is not shown.
                var message = error.ErrorMessage.Length > 0 ? error.ErrorMessage : "Некоректне значення.";
                modelState.AddModelError(ToJsonName(key), message);
            }
        }

        var factory = context.HttpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = factory.CreateValidationProblemDetails(
            context.HttpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            detail: Detail);

        return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
    }

    /// <summary>"CheckInDate" → "checkInDate", "Items[0].Name" → "items[0].name"; JSON paths such as "$.stars" stay as they are.</summary>
    public static string ToJsonName(string key)
        => key.StartsWith('$')
            ? key
            : string.Join('.', key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// The 400 response for both kinds of invalid input: a value MVC could not read (malformed JSON, a
/// query value of the wrong type) and a rule of a FluentValidation validator. Both give the same
/// ValidationProblemDetails, whose <c>errors</c> are keyed by the JSON names of the fields, or by
/// <see cref="BodyKey"/> for the body as a whole.
/// </summary>
public static class ValidationProblems
{
    /// <summary>The detail of every 400 response; the errors themselves are in <c>errors</c>.</summary>
    public const string Detail = "Перевірте значення полів, перелічених в errors.";

    /// <summary>Key of an error of the whole body: empty, not JSON, or not an object.</summary>
    public const string BodyKey = "body";

    /// <summary>Used as <c>ApiBehaviorOptions.InvalidModelStateResponseFactory</c> and by <see cref="ValidationFilter"/>.</summary>
    public static IActionResult CreateResult(ActionContext context)
    {
        // MVC names errors by the C# property ("Name"), FluentValidation too; the client sends "name".
        var modelState = new ModelStateDictionary();
        foreach (var (key, entry) in context.ModelState)
        {
            foreach (var error in entry.Errors)
            {
                // An error of the JSON reader has no text (AllowInputFormatterExceptionMessages = false in
                // Program): its English message would show C# types. It gets a Ukrainian one instead.
                var name = ToJsonName(key);
                var message = error.ErrorMessage.Length > 0 ? error.ErrorMessage
                    : name == BodyKey ? "Тіло запиту не є коректним JSON-об'єктом."
                    : "Значення має інший тип або формат: числа — без лапок, дати — РРРР-ММ-ДД, час — ГГ:ХХ:СС.";
                modelState.AddModelError(name, message);
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

    /// <summary>
    /// The JSON name of a field for the key of an error: "CheckInDate" → "checkInDate", "Items[0].Name" →
    /// "items[0].name". The JSON reader names a field by its path in the body ("$.stars" → "stars"), and the
    /// body as a whole by "$" or, when it is empty, by "" (both → <see cref="BodyKey"/>).
    /// </summary>
    public static string ToJsonName(string key)
    {
        if (key is "" or "$")
        {
            return BodyKey;
        }

        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        return string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}

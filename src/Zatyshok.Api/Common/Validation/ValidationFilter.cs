using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// Runs the FluentValidation validator of every action argument that has one (a request body or a
/// [FromQuery] object) before the action. Validators are found in DI by their type
/// <c>IValidator&lt;T&gt;</c>: Program registers all validators of this assembly. On any error the
/// action is not called and the client gets 400 (<see cref="ValidationProblems"/>).
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            foreach (var error in result.Errors)
            {
                context.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }
        }

        if (!context.ModelState.IsValid)
        {
            context.Result = ValidationProblems.CreateResult(context);
            return;
        }

        await next();
    }
}

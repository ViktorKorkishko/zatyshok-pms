using FluentValidation;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// Base class of the validators of requests and list queries. A rule of a field stops at its first
/// broken check, so the client gets one clear message per field: for an empty time zone only
/// "Вкажіть часовий пояс", not also "Невідомий часовий пояс".
/// </summary>
public abstract class RequestValidator<T> : AbstractValidator<T>
{
    /// <summary>Sets the cascade mode for all rules of the derived validator.</summary>
    protected RequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;
    }
}

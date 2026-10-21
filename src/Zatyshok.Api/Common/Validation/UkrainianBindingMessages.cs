using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// Ukrainian texts of the errors MVC adds itself when it cannot read a value: "abc" for a number in
/// the query string, an empty body, malformed JSON (NFR-13). The rules of the requests (required
/// fields, ranges, dates) are checked later by the FluentValidation validators.
/// </summary>
public static class UkrainianBindingMessages
{
    /// <summary>Replaces the English texts of <paramref name="messages"/> (MvcOptions.ModelBindingMessageProvider).</summary>
    public static void Apply(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"Значення «{value}» не підходить для поля {field}.");
        messages.SetMissingBindRequiredValueAccessor(field => $"Не вказано значення для поля {field}.");
        messages.SetMissingKeyOrValueAccessor(() => "Потрібно вказати значення.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "Тіло запиту не може бути порожнім.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Значення «{value}» некоректне.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Некоректне значення або формат JSON.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Значення має бути числом.");
        messages.SetUnknownValueIsInvalidAccessor(field => $"Некоректне значення поля {field}.");
        messages.SetValueIsInvalidAccessor(value => $"Значення «{value}» некоректне.");
        messages.SetValueMustBeANumberAccessor(field => $"Поле {field} має бути числом.");
        messages.SetValueMustNotBeNullAccessor(value => $"Значення «{value}» некоректне.");
    }
}

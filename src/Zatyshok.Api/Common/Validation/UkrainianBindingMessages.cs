using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// Ukrainian texts of the errors MVC adds itself when it cannot read a value: "abc" for a number in
/// the query string, an empty body, malformed JSON (NFR-13). The rules of the requests (required
/// fields, ranges, dates) are checked later by the FluentValidation validators. The texts do not name
/// the field: MVC would put the C# name there ("Page"), while the key of the error in <c>errors</c>
/// is already the JSON name ("page").
/// </summary>
public static class UkrainianBindingMessages
{
    /// <summary>Replaces the English texts of <paramref name="messages"/> (MvcOptions.ModelBindingMessageProvider).</summary>
    public static void Apply(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((value, _) => $"Значення «{value}» не підходить для цього поля.");
        messages.SetMissingBindRequiredValueAccessor(_ => "Не вказано значення поля.");
        messages.SetMissingKeyOrValueAccessor(() => "Потрібно вказати значення.");
        messages.SetMissingRequestBodyRequiredValueAccessor(() => "Тіло запиту не може бути порожнім.");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"Значення «{value}» некоректне.");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "Некоректне значення або формат JSON.");
        messages.SetNonPropertyValueMustBeANumberAccessor(() => "Значення має бути числом.");
        messages.SetUnknownValueIsInvalidAccessor(_ => "Некоректне значення поля.");
        messages.SetValueIsInvalidAccessor(value => $"Значення «{value}» некоректне.");
        messages.SetValueMustBeANumberAccessor(_ => "Значення має бути числом.");
        messages.SetValueMustNotBeNullAccessor(value => $"Значення «{value}» некоректне.");
    }
}

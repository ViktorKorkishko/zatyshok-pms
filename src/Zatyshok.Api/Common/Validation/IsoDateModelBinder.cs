using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Zatyshok.Api.Common.Validation;

/// <summary>
/// Reads a <see cref="DateOnly"/> from the query string or the route only in the ISO format yyyy-MM-dd,
/// the format Swagger shows ("format: date") and JSON bodies already use.
/// <para>
/// Why: MVC's own binder parses a date with the invariant culture and accepts many formats. "01.06.2027"
/// (1 June as a Ukrainian user writes it) became 6 January, and the search silently answered for other
/// dates. A value in another format is now a 400 error under the name of the field.
/// </para>
/// </summary>
public sealed class IsoDateModelBinder : IModelBinder
{
    /// <summary>The only accepted format.</summary>
    public const string Format = "yyyy-MM-dd";

    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        var text = value.FirstValue;

        // Not given: the property keeps its default (null), and the validator says "Вкажіть …".
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, value);
        if (DateOnly.TryParseExact(text, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            bindingContext.Result = ModelBindingResult.Success(date);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName, $"Значення «{text}» не є датою у форматі РРРР-ММ-ДД (наприклад, 2027-06-01).");
        }

        return Task.CompletedTask;
    }
}

/// <summary>Gives <see cref="IsoDateModelBinder"/> to every <c>DateOnly</c> and <c>DateOnly?</c> outside a JSON body.</summary>
public sealed class IsoDateModelBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
        => context.Metadata.UnderlyingOrModelType == typeof(DateOnly) ? new IsoDateModelBinder() : null;
}

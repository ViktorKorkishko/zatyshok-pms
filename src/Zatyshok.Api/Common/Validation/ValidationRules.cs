using FluentValidation;

namespace Zatyshok.Api.Common.Validation;

/// <summary>Rules repeated in several validators.</summary>
public static class ValidationRules
{
    /// <summary>Largest amount of a numeric(12,2) money column: 10 digits before the point and 2 after.</summary>
    public const decimal MaxMoneyAmount = 9_999_999_999.99m;

    /// <summary>
    /// An amount in UAH exact to the kopiyka that fits the numeric(12,2) columns (NFR-20). Checked
    /// here so that the client gets 400 with the field name; PostgreSQL would otherwise round the
    /// amount on save or reject it as out of range.
    /// </summary>
    /// <param name="rule">Rule of a money property.</param>
    /// <param name="what">The amount as the start of a sentence, e.g. "Ціна за ніч".</param>
    public static IRuleBuilderOptions<T, decimal> MoneyAmount<T>(this IRuleBuilder<T, decimal> rule, string what)
        => rule
            .Must(amount => decimal.Round(amount, 2) == amount)
            .WithMessage($"{what} має бути з точністю до копійки (не більше двох знаків після коми).")
            .LessThanOrEqualTo(MaxMoneyAmount)
            .WithMessage($"{what} не може перевищувати 9 999 999 999,99 грн.");
}

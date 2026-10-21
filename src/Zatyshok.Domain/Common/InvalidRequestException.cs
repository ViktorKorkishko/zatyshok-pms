namespace Zatyshok.Domain.Common;

/// <summary>
/// A value of the request breaks a rule that only a service can check, because the rule needs data from
/// the database: for example, a check-in before the current date of the hotel or more guests than the
/// category holds (FR-10). The API answers 400 in the same format as for an error of a validator, with the
/// message under the name of the field. Not a <see cref="DomainException"/>: that one means a conflict with
/// the state of the data and gives 409. Messages are written in Ukrainian for the end user.
/// </summary>
/// <param name="field">Name of the request field as in C#, e.g. "CheckIn"; the API writes it in camelCase.</param>
public class InvalidRequestException(string field, string message) : Exception(message)
{
    /// <summary>Name of the request field with the wrong value.</summary>
    public string Field { get; } = field;
}

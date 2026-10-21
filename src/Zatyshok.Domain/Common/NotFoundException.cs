namespace Zatyshok.Domain.Common;

/// <summary>
/// The requested record does not exist, or it belongs to a hotel outside the user's scope: such a
/// record is reported as missing so that its existence is not revealed (9.6). The API answers 404
/// with this message, so messages are written in Ukrainian, e.g. "Номер не знайдено.".
/// Not a <see cref="DomainException"/>: that one means a broken rule and gives 409.
/// </summary>
public class NotFoundException(string message) : Exception(message);

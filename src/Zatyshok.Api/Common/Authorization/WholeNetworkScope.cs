using Zatyshok.Domain.Common;

namespace Zatyshok.Api.Common.Authorization;

/// <summary>
/// The hotel scope of every request until sign-in is added: the whole network, because nobody is signed in yet.
/// Sign-in will replace this registration with a scope read from the hotel_id claim of the user (FR-02); the
/// services already filter by <see cref="IHotelScope"/>, so they need no change then.
/// </summary>
public sealed class WholeNetworkScope : IHotelScope
{
    /// <inheritdoc />
    public Guid? HotelId => null;
}

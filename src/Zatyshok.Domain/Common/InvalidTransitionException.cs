namespace Zatyshok.Domain.Common;

/// <summary>
/// A transition that tables 12.25–12.28 forbid in the current state, for example cancelling a
/// completed booking (FR-16). It is a <see cref="DomainException"/>, so the API answers 409 as
/// for any other broken rule; the separate type lets the transition tests check that it was the
/// state check that rejected the call, not another rule of the same method.
/// </summary>
public class InvalidTransitionException(string message) : DomainException(message);

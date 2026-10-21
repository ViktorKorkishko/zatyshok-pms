using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>
/// The lock of 12.8.2 (listing 12.1, step 1). Every transaction that reduces the availability of a
/// pair «hotel – category» first locks the rate row of the pair with SELECT … FOR UPDATE, so a
/// booking being created for the pair waits for it and then counts the rooms again. In this module
/// that is a room put out of order, deactivated, deleted or moved to another category.
/// </summary>
internal static class RateLock
{
    /// <summary>
    /// Starts the transaction the lock lives in. READ COMMITTED: every command sees the rows
    /// committed before it started, so a count made after the lock sees the other transaction's work.
    /// </summary>
    public static Task<IDbContextTransaction> BeginAsync(ZatyshokDbContext db, CancellationToken cancellationToken)
        => db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

    /// <summary>
    /// Locks the rate row of the pair until the transaction ends; waits while another transaction
    /// holds it. A pair without a rate has no row to lock, but it has no bookings either: a booking
    /// needs the price of every night (FR-08).
    /// </summary>
    public static async Task LockAsync(ZatyshokDbContext db, Guid hotelId, Guid roomCategoryId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            // Outside a transaction PostgreSQL would release the lock right after the command.
            throw new InvalidOperationException("The rate lock needs a transaction; start one with RateLock.BeginAsync.");
        }

        await db.Database.ExecuteSqlAsync(
            $"""SELECT 1 FROM rooms."Rates" WHERE "HotelId" = {hotelId} AND "RoomCategoryId" = {roomCategoryId} FOR UPDATE""",
            cancellationToken);
    }
}

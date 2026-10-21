using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Editable fields of a seasonal period (FR-08); both dates are nights of the period.</summary>
public record SeasonRateData(string Name, DateOnly StartDate, DateOnly EndDate, decimal PricePerNight);

/// <summary>Filters of the list of seasonal periods; null means "any".</summary>
public record SeasonRateFilter(Guid? RateId, Guid? HotelId);

/// <summary>
/// Seasonal periods of a rate (FR-08): a price per night that overrides the base price on the
/// nights of the period. Periods of one rate must not overlap, and no database constraint checks
/// it (SeasonRateConfiguration). Two requests could each check before the other saves, so every
/// change of the periods of a rate runs under the rate lock (12.8.2), the same lock under which a
/// booking reads the prices of its nights. A period is deleted physically: bookings copy the price
/// of every night (FR-31), so nothing refers to it. Limited by the hotel scope (FR-02, 9.6).
/// </summary>
public class SeasonRateService(ZatyshokDbContext db, IHotelScope scope)
{
    public Task<PagedList<SeasonRate>> ListAsync(SeasonRateFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = Scoped(db.SeasonRates.AsNoTracking().Include(s => s.Rate));
        if (filter.HotelId is { } hotelId)
        {
            scope.EnsureIncludes(hotelId);
            query = query.Where(s => s.Rate.HotelId == hotelId);
        }

        if (filter.RateId is { } rateId)
        {
            query = query.Where(s => s.RateId == rateId);
        }

        return query.OrderBy(s => s.StartDate).ThenBy(s => s.Id).ToPagedListAsync(page, cancellationToken);
    }

    public async Task<SeasonRate> GetAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(db.SeasonRates.AsNoTracking().Include(s => s.Rate)).SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw NotFound();

    public async Task<SeasonRate> CreateAsync(Guid rateId, SeasonRateData data, CancellationToken cancellationToken)
    {
        // A rate of another hotel is "not found", like any record found by its id (9.6).
        var rate = await db.Rates.AsNoTracking().SingleOrDefaultAsync(r => r.Id == rateId, cancellationToken);
        if (rate is null || !scope.Includes(rate.HotelId))
        {
            throw new NotFoundException("Тариф не знайдено.");
        }

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        await RateLock.LockAsync(db, rate.Id, cancellationToken);
        await EnsureNoOverlapAsync(rate.Id, data, exceptId: null, cancellationToken);

        var season = new SeasonRate { RateId = rate.Id, Name = data.Name };
        Apply(season, data);
        db.SeasonRates.Add(season);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(season.Id, cancellationToken);
    }

    /// <summary>Replaces the fields of the period; it stays in its rate.</summary>
    public async Task<SeasonRate> UpdateAsync(Guid id, SeasonRateData data, CancellationToken cancellationToken)
    {
        var season = await FindAsync(id, cancellationToken);

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        await RateLock.LockAsync(db, season.RateId, cancellationToken);
        await EnsureNoOverlapAsync(season.RateId, data, exceptId: season.Id, cancellationToken);

        Apply(season, data);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(season.Id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var season = await FindAsync(id, cancellationToken);

        await using var transaction = await RateLock.BeginAsync(db, cancellationToken);
        await RateLock.LockAsync(db, season.RateId, cancellationToken);
        db.SeasonRates.Remove(season);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Called under the rate lock, so no other request adds a period of the rate meanwhile.</summary>
    private async Task EnsureNoOverlapAsync(Guid rateId, SeasonRateData data, Guid? exceptId, CancellationToken cancellationToken)
    {
        // A rate has a few periods a year, so they are compared in memory with the domain rule.
        var others = await db.SeasonRates.AsNoTracking()
            .Where(s => s.RateId == rateId && s.Id != exceptId)
            .ToListAsync(cancellationToken);

        var overlapping = others.OrderBy(s => s.StartDate).FirstOrDefault(s => s.Overlaps(data.StartDate, data.EndDate));
        if (overlapping is not null)
        {
            throw new DomainException(
                $"Період перетинається із сезоном «{overlapping.Name}» ({overlapping.StartDate:dd.MM.yyyy}–{overlapping.EndDate:dd.MM.yyyy}).");
        }
    }

    // The hotel scope (9.6), through the rate of the period.
    private IQueryable<SeasonRate> Scoped(IQueryable<SeasonRate> seasons)
        => scope.HotelId is { } hotelId ? seasons.Where(s => s.Rate.HotelId == hotelId) : seasons;

    private async Task<SeasonRate> FindAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(db.SeasonRates).SingleOrDefaultAsync(s => s.Id == id, cancellationToken) ?? throw NotFound();

    private static void Apply(SeasonRate season, SeasonRateData data)
    {
        season.Name = data.Name.Trim();
        season.StartDate = data.StartDate;
        season.EndDate = data.EndDate;
        season.PricePerNight = data.PricePerNight;
    }

    private static NotFoundException NotFound() => new("Сезонний період не знайдено.");
}

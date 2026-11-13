using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Filters of the rate list; null means "any".</summary>
public record RateFilter(Guid? HotelId, Guid? RoomCategoryId);

/// <summary>
/// Base prices per night for the pairs «hotel – category» (FR-08). A rate is never deleted: it is
/// the price every booking of the pair starts from and the row of the availability lock (12.8.2);
/// its price is changed instead. Limited by the hotel scope (FR-02, 9.6).
/// </summary>
public class RateService(ZatyshokDbContext db, IHotelScope scope)
{
    public Task<PagedList<Rate>> ListAsync(RateFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var query = Scoped(db.Rates.AsNoTracking().Include(r => r.Category));
        if (filter.HotelId is { } hotelId)
        {
            scope.EnsureIncludes(hotelId);
            query = query.Where(r => r.HotelId == hotelId);
        }

        if (filter.RoomCategoryId is { } categoryId)
        {
            query = query.Where(r => r.RoomCategoryId == categoryId);
        }

        return query
            .OrderBy(r => r.Hotel.Name).ThenBy(r => r.HotelId).ThenBy(r => r.Category.Name)
            .ToPagedListAsync(page, cancellationToken);
    }

    public async Task<Rate> GetAsync(Guid id, CancellationToken cancellationToken)
        => await Scoped(db.Rates.AsNoTracking().Include(r => r.Category)).SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
           ?? throw NotFound();

    /// <summary>The rate of a pair that has none yet; the hotel and the category must be active.</summary>
    public async Task<Rate> CreateAsync(Guid hotelId, Guid roomCategoryId, decimal basePricePerNight, CancellationToken cancellationToken)
    {
        scope.EnsureIncludes(hotelId);
        var hotel = await db.Hotels.AsNoTracking().SingleOrDefaultAsync(h => h.Id == hotelId, cancellationToken)
            ?? throw new NotFoundException("Готель не знайдено.");
        var category = await db.RoomCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == roomCategoryId, cancellationToken)
            ?? throw new NotFoundException("Категорію номерів не знайдено.");
        if (!hotel.IsActive)
        {
            throw new DomainException("Готель деактивовано. Щоб задати тариф, спершу активуйте готель.");
        }

        if (!category.IsActive)
        {
            throw new DomainException($"Категорію «{category.Name}» деактивовано. Оберіть діючу категорію.");
        }

        // FR-08: exactly one rate per pair. A usual duplicate is found by the query first (409 without a failed
        // command in the log); the unique index catches two requests at the same moment.
        var duplicate = new DomainException($"Тариф категорії «{category.Name}» у цьому готелі вже є. Змініть його ціну.");
        if (await db.Rates.AnyAsync(r => r.HotelId == hotelId && r.RoomCategoryId == roomCategoryId, cancellationToken))
        {
            throw duplicate;
        }

        var rate = new Rate { HotelId = hotelId, RoomCategoryId = roomCategoryId, BasePricePerNight = basePricePerNight };
        db.Rates.Add(rate);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_Rates_HotelId_RoomCategoryId"))
        {
            throw duplicate;
        }

        return await GetAsync(rate.Id, cancellationToken);
    }

    /// <summary>
    /// Changes the base price. Bookings keep the prices of their nights fixed when they were made
    /// (FR-08, FR-31), so the new price applies to new bookings only.
    /// </summary>
    public async Task<Rate> UpdateAsync(Guid id, decimal basePricePerNight, CancellationToken cancellationToken)
    {
        var rate = await Scoped(db.Rates).SingleOrDefaultAsync(r => r.Id == id, cancellationToken) ?? throw NotFound();
        rate.BasePricePerNight = basePricePerNight;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(rate.Id, cancellationToken);
    }

    // The hotel scope (9.6).
    private IQueryable<Rate> Scoped(IQueryable<Rate> rates)
        => scope.HotelId is { } hotelId ? rates.Where(r => r.HotelId == hotelId) : rates;

    private static NotFoundException NotFound() => new("Тариф не знайдено.");
}

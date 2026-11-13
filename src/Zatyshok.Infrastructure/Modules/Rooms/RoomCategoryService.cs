using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Editable fields of a room category (FR-05).</summary>
public record RoomCategoryData(string Name, string? Description, int Capacity);

/// <summary>
/// Room categories: a dictionary of the whole network (FR-05, UC-11), so not limited by the hotel
/// scope. Prices of a category are set per hotel by rates.
/// </summary>
public class RoomCategoryService(ZatyshokDbContext db)
{
    /// <summary>Active or deactivated categories (the API asks for active ones by default).</summary>
    public Task<PagedList<RoomCategory>> ListAsync(bool isActive, PageRequest page, CancellationToken cancellationToken)
        => db.RoomCategories.AsNoTracking()
            .Where(c => c.IsActive == isActive)
            .OrderBy(c => c.Name)
            .ToPagedListAsync(page, cancellationToken);

    public async Task<RoomCategory> GetAsync(Guid id, CancellationToken cancellationToken)
        => await db.RoomCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
           ?? throw NotFound();

    public async Task<RoomCategory> CreateAsync(RoomCategoryData data, CancellationToken cancellationToken)
    {
        var category = new RoomCategory { Name = data.Name };
        Apply(category, data);

        db.RoomCategories.Add(category);
        await SaveAsync(category, cancellationToken);
        return category;
    }

    /// <summary>
    /// Replaces the fields. The base capacity is what the search offers for the category and the default for
    /// new rooms; a room may hold more, but not less (RoomService), so the base cannot grow above a room of
    /// the category, active or not (a deactivated room may come back).
    /// </summary>
    public async Task<RoomCategory> UpdateAsync(Guid id, RoomCategoryData data, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        var smallerRooms = await db.Rooms
            .Where(r => r.RoomCategoryId == id && r.Capacity < data.Capacity)
            .Select(r => r.Capacity)
            .ToListAsync(cancellationToken);
        if (smallerRooms.Count > 0)
        {
            throw new DomainException(
                $"Базову місткість не можна збільшити до {data.Capacity}: номерів категорії, що вміщують менше гостей, — " +
                $"{smallerRooms.Count} (найменша місткість — {smallerRooms.Min()}). Спершу збільште місткість цих номерів.");
        }

        Apply(category, data);
        await SaveAsync(category, cancellationToken);
        return category;
    }

    /// <summary>A category in use is deactivated instead of deleted (UC-11): new rooms and rates cannot use it.</summary>
    public async Task<RoomCategory> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        category.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return category;
    }

    /// <summary>
    /// Deletes a category nothing refers to (UC-11). Rooms, rates and bookings refer to it with
    /// ON DELETE RESTRICT, so PostgreSQL rejects the deletion of a category in use (table 12.12).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await FindAsync(id, cancellationToken);
        db.RoomCategories.Remove(category);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw new DomainException(
                "Категорію мають номери, тарифи або бронювання, тому її не можна видалити. Деактивуйте її.");
        }
    }

    /// <summary>
    /// Categories are shared by the network, so the name is unique (RoomCategoryConfiguration). A usual
    /// duplicate is found by the query first and gives 409 without a failed command in the log; the unique
    /// index still catches two requests at the same moment, which both pass the query.
    /// </summary>
    private async Task SaveAsync(RoomCategory category, CancellationToken cancellationToken)
    {
        if (await db.RoomCategories.AnyAsync(c => c.Name == category.Name && c.Id != category.Id, cancellationToken))
        {
            throw Duplicate(category);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_RoomCategories_Name"))
        {
            throw Duplicate(category);
        }
    }

    private static DomainException Duplicate(RoomCategory category) => new($"Категорія «{category.Name}» уже існує.");

    private async Task<RoomCategory> FindAsync(Guid id, CancellationToken cancellationToken)
        => await db.RoomCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw NotFound();

    private static void Apply(RoomCategory category, RoomCategoryData data)
    {
        category.Name = data.Name.Trim();
        category.Description = Text.OrNull(data.Description);
        category.Capacity = data.Capacity;
    }

    private static NotFoundException NotFound() => new("Категорію номерів не знайдено.");
}

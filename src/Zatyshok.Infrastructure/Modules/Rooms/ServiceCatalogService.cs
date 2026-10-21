using Microsoft.EntityFrameworkCore;
using Zatyshok.Domain.Common;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Editable fields of an extra service (FR-05).</summary>
public record ServiceData(string Name, string? Description, decimal Price);

/// <summary>
/// Catalog of extra services: breakfast, parking, transfer (FR-05, FR-32). Shared by the network,
/// so not limited by the hotel scope. An invoice line copies the name and the price, so a change of
/// the catalog does not alter issued invoices (12.1.2).
/// </summary>
public class ServiceCatalogService(ZatyshokDbContext db)
{
    public Task<PagedList<Service>> ListAsync(bool? isActive, PageRequest page, CancellationToken cancellationToken)
    {
        var query = db.Services.AsNoTracking();
        if (isActive is { } active)
        {
            query = query.Where(s => s.IsActive == active);
        }

        return query.OrderBy(s => s.Name).ToPagedListAsync(page, cancellationToken);
    }

    public async Task<Service> GetAsync(Guid id, CancellationToken cancellationToken)
        => await db.Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
           ?? throw NotFound();

    public async Task<Service> CreateAsync(ServiceData data, CancellationToken cancellationToken)
    {
        var service = new Service { Name = data.Name };
        Apply(service, data);

        db.Services.Add(service);
        await SaveAsync(service, cancellationToken);
        return service;
    }

    public async Task<Service> UpdateAsync(Guid id, ServiceData data, CancellationToken cancellationToken)
    {
        var service = await FindAsync(id, cancellationToken);
        Apply(service, data);
        await SaveAsync(service, cancellationToken);
        return service;
    }

    /// <summary>A service used in invoices is deactivated instead of deleted (UC-11).</summary>
    public async Task<Service> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var service = await FindAsync(id, cancellationToken);
        service.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        return service;
    }

    /// <summary>
    /// Deletes a service no invoice line refers to (UC-11). The lines refer to it with
    /// ON DELETE RESTRICT, so PostgreSQL rejects the deletion of a used service (table 12.12).
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var service = await FindAsync(id, cancellationToken);
        db.Services.Remove(service);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw new DomainException("Послугу вже додано до рахунків, тому її не можна видалити. Деактивуйте її.");
        }
    }

    private async Task SaveAsync(Service service, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation("IX_Services_Name"))
        {
            throw new DomainException($"Послуга «{service.Name}» уже існує.");
        }
    }

    private async Task<Service> FindAsync(Guid id, CancellationToken cancellationToken)
        => await db.Services.SingleOrDefaultAsync(s => s.Id == id, cancellationToken) ?? throw NotFound();

    private static void Apply(Service service, ServiceData data)
    {
        service.Name = data.Name.Trim();
        service.Description = Text.OrNull(data.Description);
        service.Price = data.Price;
    }

    private static NotFoundException NotFound() => new("Послугу не знайдено.");
}

using Microsoft.Extensions.DependencyInjection;
using Zatyshok.Domain.Modules.Rooms.Contracts;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>
/// Services of the module «Номерний фонд і тарифи»: the application services for the controllers of
/// Api/Modules/Rooms and the public contracts for other modules (table 9.6).
/// </summary>
public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(this IServiceCollection services)
    {
        // Scoped: one ZatyshokDbContext per request, shared by the services of the request.
        services.AddScoped<HotelService>();
        services.AddScoped<RoomCategoryService>();
        services.AddScoped<ServiceCatalogService>();
        services.AddScoped<RoomService>();
        services.AddScoped<RateService>();
        services.AddScoped<SeasonRateService>();

        // Contracts of table 9.6: other modules see the rooms only through these interfaces.
        services.AddScoped<IRoomInventory, RoomInventory>();
        services.AddScoped<IRoomCatalog, RoomCatalog>();
        return services;
    }
}

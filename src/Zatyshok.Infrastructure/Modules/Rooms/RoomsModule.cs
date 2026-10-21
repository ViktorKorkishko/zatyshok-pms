using Microsoft.Extensions.DependencyInjection;

namespace Zatyshok.Infrastructure.Modules.Rooms;

/// <summary>Services of the module «Номерний фонд і тарифи» (table 9.6) for the controllers of Api/Modules/Rooms.</summary>
public static class RoomsModule
{
    public static IServiceCollection AddRoomsModule(this IServiceCollection services)
    {
        // Scoped: one ZatyshokDbContext per request, shared by the services of the request.
        services.AddScoped<HotelService>();
        services.AddScoped<RoomCategoryService>();
        services.AddScoped<ServiceCatalogService>();
        services.AddScoped<RoomService>();
        return services;
    }
}

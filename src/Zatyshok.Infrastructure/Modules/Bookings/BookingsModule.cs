using Microsoft.Extensions.DependencyInjection;
using Zatyshok.Domain.Modules.Bookings.Contracts;

namespace Zatyshok.Infrastructure.Modules.Bookings;

/// <summary>Services of the module «Бронювання» (table 9.6): the availability search now, the bookings in T5.</summary>
public static class BookingsModule
{
    public static IServiceCollection AddBookingsModule(this IServiceCollection services)
    {
        // Scoped: one ZatyshokDbContext per request, shared by the services of the request.
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        return services;
    }
}

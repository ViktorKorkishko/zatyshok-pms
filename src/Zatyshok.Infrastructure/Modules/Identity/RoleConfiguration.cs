using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zatyshok.Domain.Modules.Identity;
using Zatyshok.Infrastructure.Persistence;

namespace Zatyshok.Infrastructure.Modules.Identity;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", Schemas.Identity, t =>
            t.HasCheckConstraint("CK_Roles_HotelBoundIsStaff", "NOT \"IsHotelBound\" OR \"IsStaff\""));

        builder.Property(r => r.Code).HasMaxLength(32);
        builder.Property(r => r.Name).HasMaxLength(100);

        builder.HasIndex(r => r.Code).IsUnique();

        // The nine roles of table 4.1 are reference data for every environment, not demo data:
        // authorization policies depend on them. HasData puts them into the migration with fixed
        // ids 66666666-…-6666666666NN (12.8.5). Hotel-bound roles work within one hotel (UC-10 A1).
        builder.HasData(
            CreateRole(1, RoleCodes.Guest, "Гість", isStaff: false, isHotelBound: false),
            CreateRole(2, RoleCodes.Receptionist, "Оператор рецепції", isStaff: true, isHotelBound: true),
            CreateRole(3, RoleCodes.ReservationAgent, "Оператор відділу бронювання", isStaff: true, isHotelBound: false),
            CreateRole(4, RoleCodes.Housekeeper, "Покоївка", isStaff: true, isHotelBound: true),
            CreateRole(5, RoleCodes.HeadHousekeeper, "Старша покоївка", isStaff: true, isHotelBound: true),
            CreateRole(6, RoleCodes.HotelManager, "Менеджер готелю", isStaff: true, isHotelBound: true),
            CreateRole(7, RoleCodes.Accountant, "Бухгалтер", isStaff: true, isHotelBound: false),
            CreateRole(8, RoleCodes.Executive, "Керівництво мережі", isStaff: true, isHotelBound: false),
            CreateRole(9, RoleCodes.Administrator, "Адміністратор системи", isStaff: true, isHotelBound: false));
    }

    private static Role CreateRole(int number, string code, string name, bool isStaff, bool isHotelBound) => new()
    {
        Id = Guid.Parse($"66666666-6666-6666-6666-6666666666{number:D2}"),
        Code = code,
        Name = name,
        IsStaff = isStaff,
        IsHotelBound = isHotelBound,
    };
}

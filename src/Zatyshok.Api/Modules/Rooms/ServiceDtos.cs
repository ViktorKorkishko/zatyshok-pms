using FluentValidation;
using Zatyshok.Api.Common.Paging;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Modules.Rooms;
using Zatyshok.Infrastructure.Modules.Rooms;

namespace Zatyshok.Api.Modules.Rooms;

/// <summary>Додаткова послуга довідника для створення або повної зміни (FR-05, FR-32).</summary>
public sealed class ServiceRequest
{
    /// <summary>Назва послуги, унікальна в мережі, до 200 символів.</summary>
    /// <example>Пральня</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Опис, до 1000 символів; необов'язковий.</summary>
    /// <example>Прання і прасування одягу гостя, за кілограм</example>
    public string? Description { get; init; }

    /// <summary>Ціна в гривнях, від 0, до копійки; обов'язкова.</summary>
    /// <example>150.00</example>
    public decimal? Price { get; init; }

    // The validator has checked that the price is given.
    internal ServiceData ToData() => new(Name, Description, Price!.Value);
}

/// <summary>Додаткова послуга довідника.</summary>
public sealed class ServiceResponse
{
    /// <summary>Ідентифікатор послуги.</summary>
    /// <example>88888888-8888-8888-8888-888888888801</example>
    public Guid Id { get; init; }

    /// <summary>Назва послуги.</summary>
    /// <example>Сніданок</example>
    public required string Name { get; init; }

    /// <summary>Опис.</summary>
    /// <example>Сніданок «шведський стіл» для одного гостя</example>
    public string? Description { get; init; }

    /// <summary>Ціна в гривнях за довідником; у рахунку її можна змінити (FR-32).</summary>
    /// <example>350.00</example>
    public decimal Price { get; init; }

    /// <summary>Чи діє послуга; деактивовану не можна додати до рахунку.</summary>
    /// <example>true</example>
    public bool IsActive { get; init; }

    internal static ServiceResponse From(Service service) => new()
    {
        Id = service.Id,
        Name = service.Name,
        Description = service.Description,
        Price = service.Price,
        IsActive = service.IsActive,
    };
}

/// <summary>Фільтри списку послуг.</summary>
public sealed class ServiceListQuery : PageQuery
{
    /// <summary>true (типово) — лише діючі послуги, false — лише деактивовані, наприклад щоб знову активувати послугу.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>Rules of <see cref="ServiceRequest"/>; the lengths and the price are those of rooms.Services.</summary>
public sealed class ServiceRequestValidator : RequestValidator<ServiceRequest>
{
    /// <summary>Creates the rules.</summary>
    public ServiceRequestValidator()
    {
        RuleFor(s => s.Name)
            .NotEmpty().WithMessage("Вкажіть назву послуги.")
            .MaximumLength(200).WithMessage("Назва послуги має бути не довшою за 200 символів.");
        RuleFor(s => s.Description)
            .MaximumLength(1000).WithMessage("Опис має бути не довшим за 1000 символів.");
        // Nullable, because 0 is a valid price: a missing price would otherwise become 0 without an error.
        RuleFor(s => s.Price)
            .NotNull().WithMessage("Вкажіть ціну послуги.")
            .GreaterThanOrEqualTo(0).WithMessage("Ціна не може бути від'ємною.")
            .MoneyAmount("Ціна");
    }
}

/// <summary>Rules of <see cref="ServiceListQuery"/>.</summary>
public sealed class ServiceListQueryValidator : RequestValidator<ServiceListQuery>
{
    /// <summary>Creates the rules.</summary>
    public ServiceListQueryValidator()
    {
        Include(new PageQueryValidator());
    }
}

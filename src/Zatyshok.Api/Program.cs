using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Errors;
using Zatyshok.Api.Common.Logging;
using Zatyshok.Api.Common.Swagger;
using Zatyshok.Api.Common.Validation;
using Zatyshok.Domain.Common;
using Zatyshok.Infrastructure;
using Zatyshok.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// User-secrets are loaded in every environment (not only Development) so that
// `dotnet ef database update` picks up DB_PASSWORD without extra variables.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services
    .AddControllers(options =>
    {
        // FluentValidation validators run before every action (400 on errors).
        options.Filters.Add<ValidationFilter>();

        // Expected exceptions of the services: 404, 403, 409.
        options.Filters.Add<KnownExceptionFilter>();

        // Required fields are checked by the validators with Ukrainian messages; without this MVC
        // would add its own English "The Name field is required." for every non-nullable string.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
        UkrainianBindingMessages.Apply(options.ModelBindingMessageProvider);
    })
    .AddJsonOptions(options =>
    {
        // Enums as names ("OutOfOrder"), not numbers, in requests, responses and Swagger.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // The System.Text.Json text of a malformed body is English and shows C# types; the client
        // gets the Ukrainian message of UkrainianBindingMessages instead.
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidationProblems.CreateResult);

builder.Services.AddUkrainianProblemDetails();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Messages of rules without an explicit WithMessage, so that no English text reaches the client.
ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("uk");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddZatyshokSwagger();

builder.Services.AddZatyshokInfrastructure(builder.Configuration);

// Hotel scope of the request (FR-02); the whole network until sign-in comes in T4.
builder.Services.AddScoped<IHotelScope, WholeNetworkScope>();

// GET /health returns Healthy only when the database answers (NFR-19).
builder.Services.AddHealthChecks().AddDbContextCheck<ZatyshokDbContext>("database");

var app = builder.Build();

// First, so that the correlation id is in every log record, the error handler's included.
app.UseMiddleware<CorrelationIdMiddleware>();

// An unexpected exception gives 500 with a ProblemDetails body and no internal details; an empty
// error response (e.g. 404 for an unknown path) gets a ProblemDetails body as well.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // The database itself is created by `dotnet ef database update` (see README);
    // here we only fill an empty database with demo data.
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ZatyshokDbContext>();
    await DevelopmentDataSeeder.SeedAsync(db, app.Logger);
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

/// <summary>Entry point of the API; public so that the integration tests can start it with WebApplicationFactory.</summary>
public partial class Program;

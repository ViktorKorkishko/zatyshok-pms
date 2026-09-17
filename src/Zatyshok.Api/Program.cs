using Zatyshok.Infrastructure;
using Zatyshok.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// User-secrets are loaded in every environment (not only Development) so that
// `dotnet ef database update` picks up DB_PASSWORD without extra variables.
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddZatyshokInfrastructure(builder.Configuration);

var app = builder.Build();

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

app.Run();

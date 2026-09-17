using Zatyshok.Infrastructure;

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
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

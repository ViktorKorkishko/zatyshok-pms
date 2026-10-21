using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Zatyshok.Api.Common.Authorization;

namespace Zatyshok.Api.Common.Swagger;

/// <summary>Swagger (OpenAPI) document of the API, built from the XML comments of controllers and DTOs (NFR-21).</summary>
public static class SwaggerSetup
{
    /// <summary>Registers the Swagger generator.</summary>
    public static IServiceCollection AddZatyshokSwagger(this IServiceCollection services)
        => services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Затишок API",
                Version = "v1",
                Description = "REST API системи мережі готелів «Затишок». Помилки повертаються у форматі "
                    + "ProblemDetails українською: 400 — некоректний запит, 404 — запис не знайдено, "
                    + "409 — конфлікт зі станом даних. Вхід і перевірка ролей з'являться в T4; ролі, "
                    + "указані в описі ендпоінта, поки не перевіряються.",
            });

            // <summary>, <remarks>, <response> and <example> of the controllers and DTOs. The file is
            // produced by GenerateDocumentationFile in Zatyshok.Api.csproj.
            var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);

            // Query parameters as the client writes them (?hotelId=…), not as C# properties (HotelId).
            options.DescribeAllParametersInCamelCase();

            // Non-nullable reference properties (string Name) are shown as not nullable.
            options.SupportNonNullableReferenceTypes();

            options.OperationFilter<AllowedRolesOperationFilter>();
            options.OperationFilter<MediaTypesOperationFilter>();
        });
}

/// <summary>
/// Leaves in the document the media types the API actually uses: application/json for request bodies
/// and data, application/problem+json for errors. Without it Swagger lists every type the formatters
/// know (text/plain, text/json, application/*+json). The controllers do not use [Produces] for this:
/// [Produces("application/json")] would also rewrite the type of a 400 ProblemDetails returned by a
/// filter to application/json.
/// </summary>
public sealed class MediaTypesOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody is { } body)
        {
            KeepOne(body.Content, "application/json");
        }

        foreach (var (statusCode, response) in operation.Responses)
        {
            KeepOne(response.Content, statusCode.StartsWith('2') ? "application/json" : "application/problem+json");
        }
    }

    private static void KeepOne(IDictionary<string, OpenApiMediaType> content, string mediaType)
    {
        if (content.Count == 0)
        {
            return;
        }

        // All the types share one schema, with the examples of the XML comments in it.
        var schema = content.Values.First().Schema;
        content.Clear();
        content[mediaType] = new OpenApiMediaType { Schema = schema };
    }
}

/// <summary>Adds the planned roles of <see cref="AllowedRolesAttribute"/> to the description of an endpoint.</summary>
public sealed class AllowedRolesOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attribute = context.MethodInfo.GetCustomAttribute<AllowedRolesAttribute>()
            ?? context.MethodInfo.DeclaringType?.GetCustomAttribute<AllowedRolesAttribute>();
        if (attribute is null)
        {
            return;
        }

        var names = string.Join(", ", attribute.Codes.Select(code => Roles.Names.GetValueOrDefault(code, code)));
        var line = $"**Ролі (перевірка з T4):** {names}.";
        operation.Description = string.IsNullOrEmpty(operation.Description) ? line : $"{operation.Description}\n\n{line}";
    }
}

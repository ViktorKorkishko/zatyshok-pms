using System.Reflection;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Zatyshok.Api.Common.Authorization;
using Zatyshok.Api.Common.Errors;
using Zatyshok.Api.Common.Validation;

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

            // After MediaTypesOperationFilter: that filter replaces the media types of a response and
            // would drop an example added before it.
            options.OperationFilter<ProblemExamplesOperationFilter>();
        });
}

/// <summary>
/// Gives every error response an example in the format the API returns: the
/// Ukrainian title of the status code, the detail and the correlation id. Without it Swagger shows
/// "string" in every field. The detail of the example repeats the description of the response, while
/// a real response names the exact reason; type and traceId, which ASP.NET Core adds itself, are left out.
/// </summary>
public sealed class ProblemExamplesOperationFilter : IOperationFilter
{
    /// <inheritdoc />
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var (statusCode, response) in operation.Responses)
        {
            if (statusCode.StartsWith('2') || !int.TryParse(statusCode, out var status) || response.Content.Count == 0)
            {
                continue;
            }

            var example = new OpenApiObject
            {
                ["title"] = new OpenApiString(ProblemDetailsSetup.TitleOf(status) ?? response.Description),
                ["status"] = new OpenApiInteger(status),
            };
            if (status == StatusCodes.Status400BadRequest)
            {
                // A 400 lists the errors by field; the example names one field of this request.
                example["detail"] = new OpenApiString(ValidationProblems.Detail);
                example["errors"] = new OpenApiObject
                {
                    [FirstField(operation, context)] = new OpenApiArray { new OpenApiString("Некоректне значення.") },
                };
            }
            else
            {
                example["detail"] = new OpenApiString(response.Description.ReplaceLineEndings(" "));
            }

            example["correlationId"] = new OpenApiString("3ab598cb-5b99-4c5c-80cc-1344700e7472");
            foreach (var mediaType in response.Content.Values)
            {
                mediaType.Example = example;
            }
        }
    }

    // The first query parameter (?hotelId=…), otherwise the first field of the request body.
    private static string FirstField(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.Parameters.FirstOrDefault(p => p.In == ParameterLocation.Query) is { } parameter)
        {
            return parameter.Name;
        }

        var schema = operation.RequestBody?.Content.Values.FirstOrDefault()?.Schema;
        if (schema?.Reference is { } reference
            && context.SchemaRepository.Schemas.TryGetValue(reference.Id, out var body)
            && body.Properties.Count > 0)
        {
            return body.Properties.Keys.First();
        }

        return "id";
    }
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

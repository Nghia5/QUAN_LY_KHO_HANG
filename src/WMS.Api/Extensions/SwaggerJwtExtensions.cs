using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WMS.Api.Extensions;

public static class SwaggerJwtExtensions
{
    private const string SchemeName = "Bearer";

    /// <summary>Adds the "Authorize" button (Bearer JWT) to Swagger UI.</summary>
    public static void AddJwtBearerSecurity(this SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(SchemeName, new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the token only (no 'Bearer ' prefix). In Development, get one from POST /api/dev/token."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });
    }
}

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
            Description = "Use POST /api/auth/login, then paste the accessToken without the 'Bearer ' prefix."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = []
        });
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WMS.Api.Constants;

namespace WMS.Api.Controllers;

/// <summary>
/// DEVELOPMENT ONLY: issues test JWTs signed with the local `dotnet user-jwts` key so endpoints
/// can be tried from Swagger before the real login (User module of Người 3) exists.
/// Returns 404 in any other environment.
/// </summary>
[ApiController]
[Route("api/dev/token")]
[AllowAnonymous]
[Produces("application/json")]
public sealed class DevTokenController(
    IWebHostEnvironment environment,
    IOptionsMonitor<JwtBearerOptions> jwtOptions,
    TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Creates a 8-hour test token. Empty permissions = all warehouse permissions.</summary>
    [HttpPost]
    [ProducesResponseType<DevTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<DevTokenResponse> Create(DevTokenRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        var parameters = jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme).TokenValidationParameters;
        var signingKey = parameters.IssuerSigningKeys?.FirstOrDefault() ?? parameters.IssuerSigningKey;
        if (signingKey is null || parameters.ValidIssuer is null)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "JWT signing key is not configured",
                detail: "Run once in src/WMS.Api: dotnet user-jwts create");
        }

        var permissions = request.Permissions is { Count: > 0 } ? request.Permissions : Permissions.All;
        var unknown = permissions.Except(Permissions.All).ToList();
        if (unknown.Count > 0)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown permission",
                detail: $"Unknown: {string.Join(", ", unknown)}. Valid: {string.Join(", ", Permissions.All)}");
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, $"dev-user-{request.UserId}")
        };
        claims.AddRange(permissions.Select(permission => new Claim(Permissions.ClaimType, permission)));

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddHours(8);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = parameters.ValidIssuer,
            Audience = parameters.ValidAudiences?.FirstOrDefault() ?? parameters.ValidAudience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
        });

        return Ok(new DevTokenResponse(token, expires, permissions));
    }
}

public sealed record DevTokenRequest
{
    public int UserId { get; init; } = 1;

    /// <summary>Leave empty for all permissions, e.g. ["INVENTORY_VIEW"] to test a 403.</summary>
    public IReadOnlyList<string>? Permissions { get; init; }
}

public sealed record DevTokenResponse(string Token, DateTime ExpiresAt, IReadOnlyList<string> Permissions);

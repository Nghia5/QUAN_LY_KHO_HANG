using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Authorization;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Extensions;
using WMS.Api.Middleware;
using WMS.Api.Repositories;
using WMS.Api.Services;

var initializeUserPlatform = args.Contains("--initialize-user-platform", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(
    args.Where(arg => arg != "--initialize-user-platform").ToArray());

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Authentication & Authorization with permission policies
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var key = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(key))
    {
        return;
    }

    var issuer = builder.Configuration["Jwt:Issuer"];
    var audience = builder.Configuration["Jwt:Audience"];
    var keyBytes = Encoding.UTF8.GetBytes(key);
    if (keyBytes.Length < 32 || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
    {
        throw new InvalidOperationException("Configure Jwt:Key (at least 32 bytes), Jwt:Issuer and Jwt:Audience.");
    }

    options.TokenValidationParameters = new TokenValidationParameters
    {
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidIssuer = issuer,
        ValidAudience = audience,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPermissionPolicies()
    .AddPolicy(ActiveAdminRequirement.PolicyName, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new ActiveAdminRequirement()));
builder.Services.AddScoped<IAuthorizationHandler, ActiveAdminHandler>();

// Exception handling
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();

// User & Platform module
builder.Services.AddUserPlatformModule();
builder.Services.AddScoped<WarehouseAccess>();
builder.Services.AddWarehouseModule();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddExceptionHandler<ProductSkuConflictExceptionHandler>();
builder.Services.AddExceptionHandler<ProductInUseExceptionHandler>();

// OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

if (initializeUserPlatform)
{
    await UserPlatformInitializer.InitializeAsync(app.Services, app.Configuration, CancellationToken.None);
    return;
}

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

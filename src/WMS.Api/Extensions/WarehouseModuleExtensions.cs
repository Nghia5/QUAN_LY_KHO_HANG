using FluentValidation;
using WMS.Api.Authorization;
using WMS.Api.Common;
using WMS.Api.Middleware;
using WMS.Api.Repositories;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.Locations;
using WMS.Api.Services.Warehouses;

namespace WMS.Api.Extensions;

/// <summary>All registrations of the warehouse module, kept out of Program.cs to limit merge conflicts.</summary>
public static class WarehouseModuleExtensions
{
    public static IServiceCollection AddWarehouseModule(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddScoped<ValidationFilter>();
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
            options.Filters.AddService<ValidationFilter>());

        services.AddExceptionHandler<AppExceptionHandler>();
        services.AddProblemDetails();

        services.AddAuthorizationBuilder().AddPermissionPolicies();

        // Enums (e.g. LocationType) are exchanged as names: "Zone", "Rack", ...
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

        services.AddScoped<IWarehouseRepository, WarehouseRepository>();
        services.AddScoped<IWarehouseService, WarehouseService>();

        services.AddScoped<ILocationRepository, LocationRepository>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IWarehouseDependencyChecker, LocationWarehouseDependencyChecker>();

        services.AddScoped<IWarehouseDependencyChecker, InventoryWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, InventoryLocationDependencyChecker>();

        return services;
    }
}

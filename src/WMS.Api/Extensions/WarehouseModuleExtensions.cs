using FluentValidation;
using WMS.Api.Authorization;
using Microsoft.EntityFrameworkCore;
using WMS.Api.Common;
using WMS.Api.Data;
using WMS.Api.Data.Interceptors;
using WMS.Api.Middleware;
using WMS.Api.Repositories;
using WMS.Api.Services.Audit;
using WMS.Api.Services.Documents;
using WMS.Api.Services.GoodsIssues;
using WMS.Api.Services.GoodsReceipts;
using WMS.Api.Services.Inventories;
using WMS.Api.Services.Locations;
using WMS.Api.Services.StockTransfers;
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

        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IWarehouseDependencyChecker, InventoryWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, InventoryLocationDependencyChecker>();

        // Stock ledger (W-06): movements are append-only, guarded in EF and by a DB trigger.
        services.ConfigureDbContext<WmsDbContext>(options =>
            options.AddInterceptors(new StockMovementImmutabilityInterceptor()));
        services.AddScoped<IStockLedger, StockLedger>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IWarehouseDependencyChecker, StockMovementWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, StockMovementLocationDependencyChecker>();

        // Documents (W-07+): shared workflow, numbering and audit trail.
        services.AddOptions<WarehouseOptions>().BindConfiguration(WarehouseOptions.SectionName);
        services.AddScoped<DocumentWorkflow>();
        services.AddScoped<IDocumentNumberGenerator, DocumentNumberGenerator>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        services.AddScoped<IGoodsReceiptRepository, GoodsReceiptRepository>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<IWarehouseDependencyChecker, GoodsReceiptWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, GoodsReceiptLocationDependencyChecker>();

        services.AddScoped<IStockReservationService, StockReservationService>();
        services.AddScoped<IGoodsIssueRepository, GoodsIssueRepository>();
        services.AddScoped<IGoodsIssueService, GoodsIssueService>();
        services.AddScoped<IWarehouseDependencyChecker, GoodsIssueWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, GoodsIssueLocationDependencyChecker>();

        services.AddScoped<IStockTransferRepository, StockTransferRepository>();
        services.AddScoped<IStockTransferService, StockTransferService>();
        services.AddScoped<IWarehouseDependencyChecker, StockTransferWarehouseDependencyChecker>();
        services.AddScoped<ILocationDependencyChecker, StockTransferLocationDependencyChecker>();

        return services;
    }
}

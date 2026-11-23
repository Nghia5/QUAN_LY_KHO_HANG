using Microsoft.EntityFrameworkCore;
using WMS.Api.Data;
using WMS.Api.Extensions;
using WMS.Api.Middleware;
using WMS.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT bearer: settings bind from "Authentication:Schemes:Bearer" (see `dotnet user-jwts` for local tokens).
builder.Services.AddAuthentication().AddJwtBearer();

// Product module
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddExceptionHandler<ProductSkuConflictExceptionHandler>();

// Warehouse module
builder.Services.AddWarehouseModule();

builder.Services.AddProblemDetails();
builder.Services.AddSwaggerGen();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WMS API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

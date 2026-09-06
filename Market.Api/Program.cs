using System;
using System.Threading;

using Market.Core;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<ProductRepository>(_ => new ProductRepository("products.json"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/products", async (ProductRepository repo, CancellationToken ct) =>
{
    var products = await repo.GetAllAsync(ct);
    return Results.Ok(products);
})
.WithName("GetAllProducts");

app.MapGet("/products/{id:guid}", async (Guid id, ProductRepository repo, CancellationToken ct) =>
{
    var product = await repo.GetByIdAsync(id, ct);
    return product is not null ? Results.Ok(product) : Results.NotFound();
})
.WithName("GetProductById");

app.MapPost("/products", async (Product product, ProductRepository repo, CancellationToken ct) =>
{
    try
    {
        var created = await repo.AddAsync(product, ct);
        return Results.Created($"/products/{created.Guid}", created);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
})
.WithName("AddProduct");

app.MapPut("/products/{id:guid}", async (Guid id, Product updatedProduct, ProductRepository repo, CancellationToken ct) =>
{
    if (id != updatedProduct.Guid)
        return Results.BadRequest(new { error = "ID in URL does not match product GUID." });

    var result = await repo.UpdateAsync(updatedProduct, ct);
    return result is not null ? Results.Ok(result) : Results.NotFound();
})
.WithName("UpdateProduct");

app.MapDelete("/products/{id:guid}", async (Guid id, ProductRepository repo, CancellationToken ct) =>
{
    var deleted = await repo.DeleteAsync(id, ct);
    return deleted ? Results.NoContent() : Results.NotFound();
})
.WithName("DeleteProduct");

app.MapGet("/", () => Results.Ok());

app.Run();

public partial class Program { }
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace Market.Core;

public record Product
{
    public Guid Guid { get; init; } = Guid.CreateVersion7();
    public required string Name { get; init; }
    public required decimal Price { get; init; }

    #region Save & Get products

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    public static async Task SaveProductsAsync(IEnumerable<Product> products, string path = "products.json", CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await JsonSerializer.SerializeAsync(stream, products, _jsonOptions, cancellationToken);
    }

    public static async Task<IEnumerable<Product>?> GetProductsAsync(string path = "products.json", CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return await JsonSerializer.DeserializeAsync<IEnumerable<Product>>(stream, _jsonOptions, cancellationToken);
    }

    #endregion
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Market.Core;

using Xunit;

namespace Market.Core.Tests
{
    // ===== Юнит-тесты (логика объекта) =====
    public class ProductUnitTests
    {
        [Fact]
        public void Product_Created_ShouldHaveGuidAndProperties()
        {
            var product = new Product
            {
                Name = "Test",
                Price = 10.5m
            };

            Assert.NotEqual(Guid.Empty, product.Guid);
            Assert.Equal("Test", product.Name);
            Assert.Equal(10.5m, product.Price);
        }
    }

    // ===== Интеграционные тесты (работа с файлами) =====
    public class ProductIntegrationTests : IDisposable
    {
        private readonly string _testFilePath;

        public ProductIntegrationTests()
        {
            // Уникальный временный файл
            _testFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        [Fact]
        public async Task SaveAndGetProducts_ShouldWork()
        {
            var products = new List<Product>
            {
                new() { Name = "Product1", Price = 10 },
                new() { Name = "Product2", Price = 20 }
            };

            await Product.SaveProductsAsync(products, _testFilePath);
            Assert.True(File.Exists(_testFilePath));

            var loaded = await Product.GetProductsAsync(_testFilePath);
            Assert.NotNull(loaded);

            var list = loaded.ToList();
            Assert.Equal(products, list);
        }

        [Fact]
        public async Task SaveAndGet_WithEmptyCollection()
        {
            var products = new List<Product>();
            await Product.SaveProductsAsync(products, _testFilePath);
            Assert.True(File.Exists(_testFilePath));

            var loaded = await Product.GetProductsAsync(_testFilePath);
            Assert.NotNull(loaded);
            Assert.Empty(loaded);
        }

        [Fact]
        public async Task SaveAndGet_WithCyrillic()
        {
            var products = new List<Product>
            {
                new() { Name = "Молоко", Price = 55.5m },
                new() { Name = "Хлеб", Price = 30.0m }
            };
            await Product.SaveProductsAsync(products, _testFilePath);
            var loaded = await Product.GetProductsAsync(_testFilePath);
            Assert.NotNull(loaded);

            var list = loaded.ToList();
            Assert.Multiple(
                () => Assert.Equal("Молоко", list[0].Name),
                () => Assert.Equal("Хлеб", list[1].Name)
            );
        }

        [Fact]
        public async Task GetProducts_FileNotFound_ThrowsFileNotFoundException()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => Product.GetProductsAsync(_testFilePath));
        }

        [Fact]
        public async Task GetProducts_WithInvalidJson_ThrowsJsonException()
        {
            await File.WriteAllTextAsync(_testFilePath, "This is not JSON");
            await Assert.ThrowsAsync<JsonException>(
                () => Product.GetProductsAsync(_testFilePath));
        }
    }
}
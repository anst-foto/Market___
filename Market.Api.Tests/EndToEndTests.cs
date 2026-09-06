using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

using Market.Core;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace Market.Api.Tests
{
    public class EndToEndTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private readonly string _testFilePath;

        public EndToEndTests(WebApplicationFactory<Program> factory)
        {
            _testFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
            _factory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ProductRepository));
                    if (descriptor != null)
                        services.Remove(descriptor);
                    services.AddScoped<ProductRepository>(_ => new ProductRepository(_testFilePath));
                });
            });
            _client = _factory.CreateClient();
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
                File.Delete(_testFilePath);
        }

        [Fact]
        public async Task GetAllProducts_Empty_ReturnsEmptyArray()
        {
            var response = await _client.GetAsync("/products");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
            Assert.NotNull(content);
            Assert.Empty(content);
        }

        [Fact]
        public async Task GetAllProducts_WithData_ReturnsProducts()
        {
            var products = new List<Product>
            {
                new() { Name = "Product1", Price = 10 },
                new() { Name = "Product2", Price = 20 }
            };
            foreach (var p in products)
            {
                var addResponse = await _client.PostAsJsonAsync("/products", p);
                Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            }

            var response = await _client.GetAsync("/products");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<IEnumerable<Product>>();
            Assert.NotNull(result);
            var list = result.ToList();
            Assert.Equal(products, list);
        }

        [Fact]
        public async Task GetProductById_Existing_ReturnsProduct()
        {
            var product = new Product { Name = "Test", Price = 99 };
            var addResponse = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            var created = await addResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(created);

            var response = await _client.GetAsync($"/products/{created.Guid}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(result);
            Assert.Equal(created.Guid, result.Guid);
            Assert.Equal("Test", result.Name);
            Assert.Equal(99, result.Price);
        }

        [Fact]
        public async Task GetProductById_NonExisting_ReturnsNotFound()
        {
            var id = Guid.NewGuid();
            var response = await _client.GetAsync($"/products/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PostProduct_Valid_CreatesAndReturnsCreated()
        {
            var product = new Product { Name = "New", Price = 123 };
            var response = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var location = response.Headers.Location;
            Assert.NotNull(location);

            var created = await response.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(created);
            Assert.Equal("New", created.Name);
            Assert.Equal(123, created.Price);
            Assert.NotEqual(Guid.Empty, created.Guid);

            var getResponse = await _client.GetAsync(location);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(fetched);
            Assert.Equal(created.Guid, fetched.Guid);
        }

        [Fact]
        public async Task PostProduct_DuplicateGuid_ReturnsBadRequest()
        {
            var product = new Product { Name = "Dup", Price = 1 };
            var response1 = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
            var created = await response1.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(created);
            var duplicate = new Product { Guid = created.Guid, Name = "Dup2", Price = 2 };
            var response2 = await _client.PostAsJsonAsync("/products", duplicate);
            Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
            var error = await response2.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(error.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task PutProduct_Existing_UpdatesAndReturnsOk()
        {
            var product = new Product { Name = "Before", Price = 50 };
            var addResponse = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            var existing = await addResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(existing);

            var updated = new Product { Guid = existing.Guid, Name = "After", Price = 75 };
            var putResponse = await _client.PutAsJsonAsync($"/products/{existing.Guid}", updated);
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
            var result = await putResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(result);
            Assert.Equal(existing.Guid, result.Guid);
            Assert.Equal("After", result.Name);
            Assert.Equal(75, result.Price);

            var getResponse = await _client.GetAsync($"/products/{existing.Guid}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(fetched);
            Assert.Equal("After", fetched.Name);
            Assert.Equal(75, fetched.Price);
        }

        [Fact]
        public async Task PutProduct_NonExisting_ReturnsNotFound()
        {
            var id = Guid.NewGuid();
            var product = new Product { Guid = id, Name = "None", Price = 0 };
            var response = await _client.PutAsJsonAsync($"/products/{id}", product);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PutProduct_MismatchedId_ReturnsBadRequest()
        {
            var product = new Product { Name = "Test", Price = 10 };
            var addResponse = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            var created = await addResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(created);

            var wrongId = Guid.NewGuid();
            var updated = new Product { Guid = created.Guid, Name = "Changed", Price = 20 };
            var response = await _client.PutAsJsonAsync($"/products/{wrongId}", updated);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(error.TryGetProperty("error", out _));
        }

        [Fact]
        public async Task DeleteProduct_Existing_ReturnsNoContent()
        {
            var product = new Product { Name = "DeleteMe", Price = 5 };
            var addResponse = await _client.PostAsJsonAsync("/products", product);
            Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
            var created = await addResponse.Content.ReadFromJsonAsync<Product>();
            Assert.NotNull(created);

            var deleteResponse = await _client.DeleteAsync($"/products/{created.Guid}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            var getResponse = await _client.GetAsync($"/products/{created.Guid}");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task DeleteProduct_NonExisting_ReturnsNotFound()
        {
            var id = Guid.NewGuid();
            var response = await _client.DeleteAsync($"/products/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetRootEndpoint_ReturnsOk()
        {
            var response = await _client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
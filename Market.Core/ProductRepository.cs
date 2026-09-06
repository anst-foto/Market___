using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Market.Core;

public class ProductRepository
{
    private List<Product> _products;
    private bool _isLoaded;
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public ProductRepository(string filePath = "products.json")
    {
        _filePath = filePath;
        _products = new List<Product>();
        _isLoaded = false;
    }

    private async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_filePath))
        {
            var loaded = await Product.GetProductsAsync(_filePath, cancellationToken);
            if (loaded != null)
            {
                _products = loaded.ToList();
                return;
            }
        }
        _products = new List<Product>();
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_isLoaded)
            return;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (!_isLoaded)
            {
                await LoadAsync(cancellationToken);
                _isLoaded = true;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await Product.SaveProductsAsync(_products, _filePath, cancellationToken);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _products.ToList();
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _products.FirstOrDefault(p => p.Guid == id);
    }

    public async Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        await EnsureLoadedAsync(cancellationToken); // загрузка без блокировки на запись

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_products.Any(p => p.Guid == product.Guid))
                throw new InvalidOperationException($"Продукт с Guid {product.Guid} уже существует.");

            _products.Add(product);
            await SaveAsync(cancellationToken);
            return product;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Product?> UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        if (product == null)
            throw new ArgumentNullException(nameof(product));

        await EnsureLoadedAsync(cancellationToken);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var existing = _products.FirstOrDefault(p => p.Guid == product.Guid);
            if (existing == null)
                return null;

            var index = _products.IndexOf(existing);
            _products[index] = product with { };
            await SaveAsync(cancellationToken);
            return product;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var existing = _products.FirstOrDefault(p => p.Guid == id);
            if (existing == null)
                return false;

            _products.Remove(existing);
            await SaveAsync(cancellationToken);
            return true;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
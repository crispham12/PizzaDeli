using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Sản phẩm / Menu - dùng cho Admin và Customer</summary>
public class ProductService
{
    private readonly ApplicationDbContext _db;
    public ProductService(ApplicationDbContext db) => _db = db;

    // ---- Lấy danh sách ----
    public async Task<List<Product>> GetAllAsync()
        => await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsAvailable)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

    public async Task<List<PizzaComponent>> GetAvailableComponentsAsync()
        => await _db.PizzaComponents
                    .Where(p => p.IsAvailable)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync();

    public async Task<List<Product>> GetProductsWithEmbeddingsAsync()
        => await _db.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductToppings).ThenInclude(pt => pt.Topping)
                    .Where(p => p.IsAvailable && !string.IsNullOrEmpty(p.ImageEmbedding))
                    .ToListAsync();
    public async Task<List<Product>> GetByCategoryAsync(int categoryId)
        => await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.CategoryId == categoryId && p.IsAvailable)
                    .ToListAsync();

    public async Task<List<Product>> SearchAsync(string keyword)
        => await _db.Products
                    .Include(p => p.Category)
                    .Where(p => p.IsAvailable &&
                               (p.Name.Contains(keyword) || (p.Description != null && p.Description.Contains(keyword))))
                    .ToListAsync();

    // ---- Chi tiết ----
    public async Task<Product?> GetByIdAsync(string id)
        => await _db.Products
                    .Include(p => p.Category)
                    .Include(p => p.Reviews).ThenInclude(r => r.User)
                    .FirstOrDefaultAsync(p => p.Id == id);

    // ---- Admin CRUD ----
    public async Task<Product> CreateAsync(Product product)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    public async Task<bool> UpdateAsync(Product product)
    {
        var existing = await _db.Products.FindAsync(product.Id);
        if (existing == null) return false;
        existing.Name        = product.Name;
        existing.Description = product.Description;
        existing.Price       = product.Price;
        existing.ImageUrl    = product.ImageUrl;
        existing.CategoryId  = product.CategoryId;
        existing.IsAvailable = product.IsAvailable;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return false;
        product.IsAvailable = false;   // Soft delete
        await _db.SaveChangesAsync();
        return true;
    }
}

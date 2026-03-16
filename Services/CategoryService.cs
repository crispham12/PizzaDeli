using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Danh mục - CRUD cho Admin</summary>
public class CategoryService
{
    private readonly ApplicationDbContext _db;
    public CategoryService(ApplicationDbContext db) => _db = db;

    public async Task<List<Category>> GetAllAsync()
        => await _db.Categories.Include(c => c.Products).ToListAsync();

    public async Task<Category?> GetByIdAsync(int id)
        => await _db.Categories.Include(c => c.Products).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<Category> CreateAsync(Category category)
    {
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category;
    }

    public async Task<bool> UpdateAsync(Category category)
    {
        var existing = await _db.Categories.FindAsync(category.Id);
        if (existing == null) return false;
        existing.Name        = category.Name;
        existing.Description = category.Description;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return false;
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return true;
    }
}

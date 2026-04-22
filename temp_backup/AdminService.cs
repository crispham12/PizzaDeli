using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using System.IO;

namespace PizzaDeli.Services;

public class AdminService
{
    private readonly ApplicationDbContext _db;
    public AdminService(ApplicationDbContext db) => _db = db;

    public async Task<(List<Product> Products, int TotalProducts, List<Category> Categories)> GetProductsPagedAsync(string searchQuery, int? categoryId, int page, int pageSize)
    {
        var query = _db.Products.Include(p => p.Category).Where(p => p.Category != null && p.Category.IsActive).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);

        int total = await query.CountAsync();
        var products = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var categories = await _db.Categories.Where(c => c.IsActive).ToListAsync();
        return (products, total, categories);
    }

    public async Task<(List<Topping> Toppings, int Total)> GetToppingsPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Toppings.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (status == "active") query = query.Where(p => p.IsAvailable);
        else if (status == "inactive") query = query.Where(p => !p.IsAvailable);

        int total = await query.CountAsync();
        var toppings = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (toppings, total);
    }

    public async Task<(List<PizzaComponent> Components, int Total)> GetComponentsPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.PizzaComponents.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(p => p.Name.Contains(searchQuery) || p.Id.Contains(searchQuery));
        if (status == "active") query = query.Where(p => p.IsAvailable);
        else if (status == "inactive") query = query.Where(p => !p.IsAvailable);

        int total = await query.CountAsync();
        var components = await query.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (components, total);
    }

    public async Task<(List<Order> Orders, int Total)> GetOrdersPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Orders.Include(o => o.User).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(o => o.Id.Contains(searchQuery) || (o.User != null && o.User.FullName.Contains(searchQuery)));
        if (status != "all") query = query.Where(o => o.Status == status);

        int total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.OrderDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (orders, total);
    }

    public async Task<(List<User> Users, int Total)> GetAccountsPagedAsync(string searchQuery, string status, string role, int page, int pageSize)
    {
        var query = _db.Users.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(u => u.FullName.Contains(searchQuery) || u.Email.Contains(searchQuery) || u.Id.Contains(searchQuery));
        
        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "locked") query = query.Where(u => !u.IsActive);
        
        if (role != "all" && Enum.TryParse<UserRole>(role, out var parsedRole)) query = query.Where(u => u.Role == parsedRole);

        int total = await query.CountAsync();
        var users = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (users, total);
    }

    public async Task<(List<User> Staff, int Total, int Working, int Leave)> GetStaffPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Users.Where(u => u.Role != UserRole.Customer).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(u => u.FullName.Contains(searchQuery) || u.Id.Contains(searchQuery));
        
        if (status == "active") query = query.Where(u => u.IsActive);
        if (status == "inactive") query = query.Where(u => !u.IsActive);

        int total = await query.CountAsync();
        var staff = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        
        int working = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && u.IsActive);
        int leave = await _db.Users.CountAsync(u => u.Role != UserRole.Customer && !u.IsActive);
        
        return (staff, total, working, leave);
    }

    public async Task<(List<Voucher> Vouchers, int Total)> GetVouchersPagedAsync(string searchQuery, string status, int page, int pageSize)
    {
        var query = _db.Vouchers.AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery))
            query = query.Where(v => v.Code.Contains(searchQuery) || v.Name.Contains(searchQuery));

        var now = DateTime.Now;
        if (status == "active") query = query.Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate >= now));
        else if (status == "expired") query = query.Where(v => !v.IsActive || (v.ExpiryDate.HasValue && v.ExpiryDate < now));

        int total = await query.CountAsync();
        var vouchers = await query.OrderByDescending(v => v.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (vouchers, total);
    }

    public async Task<List<Category>> GetCategoriesAsync(string searchQuery, string status)
    {
        var query = _db.Categories.Include(c => c.Products).AsQueryable();
        if (!string.IsNullOrEmpty(searchQuery)) query = query.Where(c => c.Name.Contains(searchQuery));
        if (status == "active") query = query.Where(c => c.IsActive);
        else if (status == "inactive") query = query.Where(c => !c.IsActive);
        return await query.ToListAsync();
    }

    public async Task<List<Product>> GetProductsForSyncAsync(string? productId)
    {
        var query = _db.Products.Where(p => p.IsAvailable && !string.IsNullOrEmpty(p.ImageUrl));
        if (!string.IsNullOrEmpty(productId)) query = query.Where(p => p.Id == productId);
        else query = query.Where(p => string.IsNullOrEmpty(p.ImageEmbedding));
        return await query.ToListAsync();
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    // Pass-through helpers for simple find/add/remove
    public async Task<T?> FindAsync<T>(params object[] keyValues) where T : class => await _db.Set<T>().FindAsync(keyValues);
    public void Add<T>(T entity) where T : class => _db.Set<T>().Add(entity);
    public void Update<T>(T entity) where T : class => _db.Set<T>().Update(entity);
    public void Remove<T>(T entity) where T : class => _db.Set<T>().Remove(entity);
    public void RemoveRange<T>(IEnumerable<T> entities) where T : class => _db.Set<T>().RemoveRange(entities);

    public async Task<Product?> GetProductWithToppingsAsync(string id)
        => await _db.Products.Include(p => p.ProductToppings).FirstOrDefaultAsync(p => p.Id == id);
        
    public async Task<Category?> GetCategoryWithProductsAsync(int id)
        => await _db.Categories.Include(c => c.Products).ThenInclude(p => p.ProductToppings)
                               .Include(c => c.Products).ThenInclude(p => p.Reviews)
                               .Include(c => c.Products).ThenInclude(p => p.OrderDetails)
                               .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<ProductTopping>> GetProductToppingsAsync(string productId)
        => await _db.ProductToppings.Where(pt => pt.ProductId == productId).ToListAsync();
        
    public async Task<List<Order>> GetOrdersByVoucherIdAsync(int voucherId)
        => await _db.Orders.Where(o => o.VoucherId == voucherId).ToListAsync();

    public async Task<List<Category>> GetActiveCategoriesAsync() => await _db.Categories.Where(c => c.IsActive).ToListAsync();
    public async Task<List<Topping>> GetActiveToppingsAsync() => await _db.Toppings.Where(t => t.IsAvailable).ToListAsync();
}

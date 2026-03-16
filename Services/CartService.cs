using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Giỏ hàng của Customer</summary>
public class CartService
{
    private readonly ApplicationDbContext _db;
    public CartService(ApplicationDbContext db) => _db = db;

    // ---- Lấy giỏ hàng ----
    public async Task<List<CartItem>> GetCartAsync(string userId)
        => await _db.CartItems
                    .Include(c => c.Product).ThenInclude(p => p!.Category)
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

    // ---- Thêm vào giỏ ----
    public async Task AddToCartAsync(string userId, string productId, int quantity = 1)
    {
        var existing = await _db.CartItems
                                .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            _db.CartItems.Add(new CartItem
            {
                UserId    = userId,
                ProductId = productId,
                Quantity  = quantity
            });
        }
        await _db.SaveChangesAsync();
    }

    // ---- Cập nhật số lượng ----
    public async Task<bool> UpdateQuantityAsync(string userId, string productId, int quantity)
    {
        var item = await _db.CartItems
                            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        if (item == null) return false;
        if (quantity <= 0)
            _db.CartItems.Remove(item);
        else
            item.Quantity = quantity;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Xóa khỏi giỏ ----
    public async Task<bool> RemoveFromCartAsync(string userId, string productId)
    {
        var item = await _db.CartItems
                            .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == productId);
        if (item == null) return false;
        _db.CartItems.Remove(item);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Xóa toàn bộ giỏ (sau khi đặt hàng) ----
    public async Task ClearCartAsync(string userId)
    {
        var items = await _db.CartItems.Where(c => c.UserId == userId).ToListAsync();
        _db.CartItems.RemoveRange(items);
        await _db.SaveChangesAsync();
    }

    // ---- Tổng tiền giỏ hàng ----
    public async Task<decimal> GetTotalAsync(string userId)
    {
        return await _db.CartItems
                        .Where(c => c.UserId == userId)
                        .SumAsync(c => c.Quantity * c.Product!.Price);
    }
}

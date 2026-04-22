using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Khuyến mãi / Voucher - Admin tạo, Customer áp dụng</summary>
public class VoucherService
{
    private readonly ApplicationDbContext _db;
    public VoucherService(ApplicationDbContext db) => _db = db;

    // ---- Lấy tất cả voucher (Admin) ----
    public async Task<List<Voucher>> GetAllAsync()
        => await _db.Vouchers.OrderByDescending(v => v.Id).ToListAsync();

    public async Task<List<Voucher>> GetActiveVouchersAsync()
        => await _db.Vouchers
                    .Where(v => v.IsActive)
                    .OrderBy(v => v.ExpiryDate)
                    .ToListAsync();

    public async Task<List<Voucher>> GetValidVouchersAsync()
        => await _db.Vouchers
            .Where(v => v.IsActive && 
                        (!v.StartDate.HasValue || v.StartDate <= DateTime.Now) && 
                        (!v.ExpiryDate.HasValue || v.ExpiryDate >= DateTime.Now))
            .ToListAsync();

    // ---- Kiểm tra và áp dụng voucher (Customer) ----
    public async Task<Voucher?> ValidateAsync(string code, decimal orderTotal)
    {
        var voucher = await _db.Vouchers
                               .FirstOrDefaultAsync(v => v.Code == code && v.IsActive);
        if (voucher == null) return null;
        if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate < DateTime.Now) return null;
        if (orderTotal < voucher.MinOrderValue) return null;
        return voucher;
    }

    // ---- Admin CRUD ----
    public async Task<Voucher> CreateAsync(Voucher voucher)
    {
        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync();
        return voucher;
    }

    public async Task<bool> UpdateAsync(Voucher voucher)
    {
        var existing = await _db.Vouchers.FindAsync(voucher.Id);
        if (existing == null) return false;
        existing.Code           = voucher.Code;
        existing.DiscountAmount = voucher.DiscountAmount;
        existing.MinOrderValue  = voucher.MinOrderValue;
        existing.ExpiryDate     = voucher.ExpiryDate;
        existing.IsActive       = voucher.IsActive;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return false;
        voucher.IsActive = false;   // Vô hiệu hóa thay vì xóa
        await _db.SaveChangesAsync();
        return true;
    }
}

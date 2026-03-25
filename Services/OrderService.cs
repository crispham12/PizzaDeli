using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Đơn hàng - Customer đặt hàng, Staff/Admin quản lý</summary>
public class OrderService
{
    private readonly ApplicationDbContext _db;
    private readonly VoucherService _voucher;

    public OrderService(ApplicationDbContext db, VoucherService voucher)
    {
        _db      = db;
        _voucher = voucher;
    }

    // ---- Customer: Xem lịch sử đơn hàng của mình ----
    public async Task<List<Order>> GetByUserAsync(string userId)
        => await _db.Orders
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .Include(o => o.Voucher)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

    // ---- Staff/Admin: Lấy tất cả đơn hàng ----
    public async Task<List<Order>> GetAllAsync()
        => await _db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

    // ---- Xem chi tiết 1 đơn hàng ----
    public async Task<Order?> GetByIdAsync(string id)
        => await _db.Orders
                    .Include(o => o.User)
                    .Include(o => o.Voucher)
                    .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

    // ---- Staff/Admin: Cập nhật trạng thái đơn hàng ----
    public async Task<bool> UpdateStatusAsync(string orderId, string newStatus)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order == null) return false;
        order.Status = newStatus;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Admin: Thống kê tổng đơn hàng ----
    public async Task<int> CountTotalAsync() => await _db.Orders.CountAsync();

    // ---- Admin: Doanh thu theo ngày ----
    public async Task<decimal> GetRevenueByDateAsync(DateTime date)
        => await _db.Orders
                    .Where(o => o.OrderDate.Date == date.Date && o.Status == "Completed")
                    .SumAsync(o => o.FinalAmount);
}

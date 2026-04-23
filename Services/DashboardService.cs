using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using System.Text.Json;

namespace PizzaDeli.Services;

public class DashboardService
{
    private readonly ApplicationDbContext _db;

    public DashboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<dynamic> GetDashboardStatsAsync()
    {
        var today = DateTime.Today;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        var now = DateTime.UtcNow;

        var totalOrders = await _db.Orders.CountAsync();
        var revenueToday = await _db.Orders
            .Where(o => o.OrderDate.Date == today && o.Status == "Completed")
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        var totalProducts = await _db.Products.CountAsync();
        var totalCustomers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer);

        var ordersThisMonth = await _db.Orders.CountAsync(o => o.OrderDate >= thisMonth);
        var revenueThisMonth = await _db.Orders
            .Where(o => o.OrderDate >= thisMonth && o.Status == "Completed")
            .SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

        var ordersPending = await _db.Orders.CountAsync(o => o.Status == "Pending");
        var ordersDelivering = await _db.Orders.CountAsync(o => o.Status == "Delivering");
        var ordersCompleted = await _db.Orders.CountAsync(o => o.Status == "Completed");

        var categories = await _db.Categories
            .Where(c => c.IsActive)
            .Select(c => new { c.Name, Count = c.Products.Count })
            .Take(3)
            .ToListAsync();

        var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
        var staffCount = await _db.Users.CountAsync(u => u.Role == UserRole.Staff);

        var activeVouchers = await _db.Vouchers
            .Where(v => v.IsActive && (v.ExpiryDate == null || v.ExpiryDate >= now))
            .OrderByDescending(v => v.Id)
            .ToListAsync();

        return new {
            totalOrders, revenueToday, totalProducts, totalCustomers,
            ordersThisMonth, revenueThisMonth,
            ordersPending, ordersDelivering, ordersCompleted,
            categories, adminCount, staffCount, activeVouchers,
            activeVoucher = activeVouchers.FirstOrDefault()
        };
    }

    public async Task<dynamic> GetStatisticsAsync()
    {
        var today = DateTime.Today;
        var thisMonthStart = new DateTime(today.Year, today.Month, 1);
        var lastMonthStart = thisMonthStart.AddMonths(-1);
        var lastMonthEnd = thisMonthStart;

        static decimal CalcPct(decimal current, decimal prev)
            => prev == 0 ? (current > 0 ? 100m : 0m) : Math.Round((current - prev) / prev * 100, 1);

        decimal revThis = await _db.Orders.Where(o => o.Status == "Completed" && o.OrderDate >= thisMonthStart).SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        decimal revLast = await _db.Orders.Where(o => o.Status == "Completed" && o.OrderDate >= lastMonthStart && o.OrderDate < lastMonthEnd).SumAsync(o => (decimal?)o.FinalAmount) ?? 0;
        decimal revTotal = await _db.Orders.Where(o => o.Status == "Completed").SumAsync(o => (decimal?)o.FinalAmount) ?? 0;

        int ordThis = await _db.Orders.CountAsync(o => o.OrderDate >= thisMonthStart);
        int ordLast = await _db.Orders.CountAsync(o => o.OrderDate >= lastMonthStart && o.OrderDate < lastMonthEnd);
        int ordTotal = await _db.Orders.CountAsync();

        decimal aovThis = ordThis > 0 ? (await _db.Orders.Where(o => o.Status == "Completed" && o.OrderDate >= thisMonthStart).SumAsync(o => (decimal?)o.FinalAmount) ?? 0) / ordThis : 0;
        decimal aovLast = ordLast > 0 ? revLast / ordLast : 0;
        decimal aovTotal = ordTotal > 0 ? (revTotal / ordTotal) : 0;

        int cusThis = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= thisMonthStart);
        int cusLast = await _db.Users.CountAsync(u => u.Role == UserRole.Customer && u.CreatedAt >= lastMonthStart && u.CreatedAt < lastMonthEnd);
        int cusTotal = await _db.Users.CountAsync(u => u.Role == UserRole.Customer);

        var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-6 + i)).ToList();
        var revenueRaw = await _db.Orders
            .Where(o => o.Status == "Completed" && o.OrderDate.Date >= today.AddDays(-6))
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(o => o.FinalAmount) })
            .ToListAsync();
            
        var revenueByDay = last7Days.Select(d => new {
            Label = d.DayOfWeek == DayOfWeek.Sunday ? "CN" : "Thứ " + ((int)d.DayOfWeek + 1),
            Value = revenueRaw.FirstOrDefault(r => r.Date == d)?.Total ?? 0
        }).ToList();

        var revenueDetails = await _db.OrderDetails
            .Where(od => od.Order!.Status == "Completed")
            .Select(od => new
            {
                od.ProductId,
                CategoryName = od.Product != null && od.Product.Category != null ? od.Product.Category.Name : null,
                Revenue = od.UnitPrice * od.Quantity
            }).ToListAsync();

        var catRevenue = revenueDetails
            .GroupBy(x => !string.IsNullOrWhiteSpace(x.CategoryName) ? x.CategoryName! : ((x.ProductId != null && x.ProductId.StartsWith("CTM", StringComparison.OrdinalIgnoreCase)) ? "Pizza" : "Khác"))
            .Select(g => new { Name = g.Key, Total = g.Sum(x => x.Revenue) })
            .OrderByDescending(x => x.Total)
            .Take(5).ToList();

        var topProducts = await _db.OrderDetails
            .Where(od => od.Order!.Status == "Completed")
            .GroupBy(od => new { od.ProductId, ProductName = od.Product!.Name, od.Product.ImageUrl, CategoryName = od.Product.Category!.Name })
            .Select(g => new {
                ProductId = g.Key.ProductId,
                Name = g.Key.ProductName,
                ImageUrl = g.Key.ImageUrl,
                Category = g.Key.CategoryName,
                Sold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.UnitPrice * x.Quantity)
            }).OrderByDescending(x => x.Sold).Take(5).ToListAsync();

        return new {
            StatsRevenue = revTotal,
            StatRevenuePct = CalcPct(revThis, revLast),
            StatsOrders = ordTotal,
            StatOrdersPct = CalcPct(ordThis, ordLast),
            StatsAOV = aovTotal,
            StatAOVPct = CalcPct(aovThis, aovLast),
            StatsCustomers = cusTotal,
            StatCustomersPct = CalcPct(cusThis, cusLast),
            ChartLabels = JsonSerializer.Serialize(revenueByDay.Select(r => r.Label).ToList()),
            ChartValues = JsonSerializer.Serialize(revenueByDay.Select(r => r.Value).ToList()),
            DonutLabels = JsonSerializer.Serialize(catRevenue.Select(c => c.Name).ToList()),
            DonutValues = JsonSerializer.Serialize(catRevenue.Select(c => c.Total).ToList()),
            TopProducts = topProducts
        };
    }
}

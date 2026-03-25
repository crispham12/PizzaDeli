using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Controllers;

/// <summary>Nhân viên: Quản lý đơn hàng, Xử lý giao hàng, Hỗ trợ KH, Quản lý bình luận</summary>
public class StaffController : BaseController
{
    private readonly ApplicationDbContext _db;

    public StaffController(ApplicationDbContext db)
    {
        _db = db;
    }

    private IActionResult? Guard() => RequireRole("Staff", "Admin");


    // ---- Quản lý đơn hàng ----
    public async Task<IActionResult> Orders(int page = 1)
    {
        var g = Guard(); if (g != null) return g;

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // Stats
        ViewBag.TotalToday = await _db.Orders.CountAsync(o => o.OrderDate >= today && o.OrderDate < tomorrow);
        ViewBag.PendingOrders = await _db.Orders.CountAsync(o => o.Status == "Pending" || o.Status == "Chờ xử lý");
        ViewBag.DeliveringOrders = await _db.Orders.CountAsync(o => o.Status == "Shipping" || o.Status == "Đang giao hàng");

        // Fetch Orders
        int pageSize = 15;
        var query = _db.Orders.Include(o => o.User).OrderByDescending(o => o.OrderDate);
        var orders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        ViewBag.TotalPages = (int)Math.Ceiling(await query.CountAsync() / (double)pageSize);
        ViewBag.CurrentPage = page;

        return View(orders);
    }
    
    public async Task<IActionResult> OrderDetail(string id) 
    { 
        var g = Guard(); if (g != null) return g; 
        
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        
        var order = await _db.Orders.FindAsync(id);
        if (order != null)
        {
            order.Status = status;
            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật đơn hàng #{id.Substring(0, 8).ToUpper()} → {status}";
        }
        else
        {
            TempData["ErrorMessage"] = "Không tìm thấy đơn hàng.";
        }
        return RedirectToAction("Orders");
    }

    // ---- Xử lý giao hàng ----
    public IActionResult Deliveries()           { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult UpdateDeliveryStatus(string id, string status)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = $"Đã cập nhật giao hàng #{id} → {status}";
        return RedirectToAction("Deliveries");
    }

    // ---- Hỗ trợ khách hàng ----
    public IActionResult Customers()            { var g = Guard(); if (g != null) return g; return View(); }
    public IActionResult CustomerDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    // ---- Quản lý bình luận ----
    public IActionResult Comments()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult HideComment(string id)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = "Đã ẩn bình luận vi phạm.";
        return RedirectToAction("Comments");
    }

    [HttpPost]
    public IActionResult ReplyComment(string id, string reply)
    {
        var g = Guard(); if (g != null) return g;
        TempData["Success"] = "Đã trả lời bình luận.";
        return RedirectToAction("Comments");
    }
}

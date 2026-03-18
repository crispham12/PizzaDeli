using Microsoft.AspNetCore.Mvc;
using PizzaDeli.Data;
using System.Linq;
using System;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;

namespace PizzaDeli.Controllers;

/// <summary>Customer: Xem menu, Đặt hàng, Giỏ hàng, Thông tin cá nhân, Voucher, Bình luận</summary>
public class CustomerController : BaseController
{
    private readonly ApplicationDbContext _context;

    public CustomerController(ApplicationDbContext context)
    {
        _context = context;
    }

    private IActionResult? Guard() => RequireLogin();

    // ---- Thông tin cá nhân ----
    public IActionResult Profile()
    {
        var g = Guard(); if (g != null) return g;
        var user = _context.Users.Find(CurrentUserId);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Profile(string fullName, string phone, DateTime? dateOfBirth, string gender, string address)
    {
        var g = Guard(); if (g != null) return g;
        
        var user = _context.Users.Find(CurrentUserId);
        if (user == null) return NotFound();

        user.FullName = fullName;
        user.Phone = phone;
        user.DateOfBirth = dateOfBirth;
        user.Gender = gender;
        if (!string.IsNullOrEmpty(address)) {
            user.Address = address;
        }

        _context.SaveChanges();
        
        // Cập nhật lại session name
        HttpContext.Session.SetString("USER_NAME", fullName);

        TempData["Success"] = "Cập nhật thông tin thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangeEmail(string newEmail, string verificationCode)
    {
        var g = Guard(); if (g != null) return g;
        
        var user = _context.Users.Find(CurrentUserId);
        if (user == null) return NotFound();

        var sessionCode = HttpContext.Session.GetString("VERIFICATION_CODE");
        var targetEmail = HttpContext.Session.GetString("TARGET_EMAIL");

        if (string.IsNullOrEmpty(sessionCode) || verificationCode != sessionCode || newEmail != targetEmail)
        {
            TempData["Error"] = "Mã xác nhận không hợp lệ hoặc không khớp Email!";
            return RedirectToAction("Profile");
        }

        user.Email = newEmail;
        _context.SaveChanges();

        // Cập nhật session
        HttpContext.Session.SetString("USER_EMAIL", newEmail);
        TempData["Success"] = "Đổi email thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public IActionResult RequestVerificationCode(string newEmail)
    {
        var g = Guard(); if (g != null) return Json(new { success = false, message = "Chưa đăng nhập!" });
        
        if (string.IsNullOrWhiteSpace(newEmail))
            return Json(new { success = false, message = "Email không hợp lệ!" });

        var user = _context.Users.Find(CurrentUserId);
        if (user == null) return Json(new { success = false, message = "Không tìm thấy người dùng." });

        if (user.Email == newEmail)
            return Json(new { success = false, message = "Email mới phải khác email hiện tại!"});

        // Tạo mã ngẫu nhiên 4 số
        Random rnd = new Random();
        string code = rnd.Next(1000, 9999).ToString();

        // Lưu session chuẩn bị xác thực
        HttpContext.Session.SetString("VERIFICATION_CODE", code);
        HttpContext.Session.SetString("TARGET_EMAIL", newEmail);

        try 
        {
            // Thiết lập SMTP gửi mail thực tế 
            // Demo dùng SMTP Của Google, lưu ý phải cung cấp App Password chuẩn bảo mật, ở đây tài khoản thật dùng để gửi
            var client = new SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential("phamanhkhoa56789@gmail.com", "ejff yemk uhwd wdtv") // Tránh vi phạm chính sách gửi mail spam, pass thật app smtp
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("pizzadelidemo1@gmail.com", "PizzaDeli Security"),
                Subject = "PizzaDeli - Xác nhận đổi Email",
                Body = $"<h2>Yêu cầu thay đổi địa chỉ email của tài khoản {user.FullName}</h2><hr/>" +
                       $"<p>Chào bạn,</p>" +
                       $"<p>Đây là mã xác nhận thay đổi địa chỉ email (OTP) gồm 4 chữ số của bạn:</p>" +
                       $"<h1 style='color: #16a34a; letter-spacing: 0.1em;'>{code}</h1>" +
                       $"<p>Vui lòng tuyệt đối không tiết lộ mã này cho bất kỳ ai. Mã này sẽ áp dụng trói buộc cho email <b>{newEmail}</b>.</p>",
                IsBodyHtml = true,
            };
            mailMessage.To.Add(newEmail);
            client.Send(mailMessage);

            return Json(new { success = true, message = "Mã xác nhận đã được gửi về email mới của bạn!" });
        }
        catch (Exception ex)
        {
            // Nếu lỗi do mạng hay SMTP thì sẽ fallback về cảnh báo
            return Json(new { success = false, message = $"Gặp lỗi hệ thống gửi mail: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ChangePassword(string oldPassword, string newPassword)
    {
        var g = Guard(); if (g != null) return g;
        
        var user = _context.Users.Find(CurrentUserId);
        if (user == null) return NotFound();

        if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
        {
            TempData["Error"] = "Mật khẩu hiện tại không đúng!";
            return RedirectToAction("Profile");
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        _context.SaveChanges();

        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Profile");
    }

    // ---- Giỏ hàng ----
    public IActionResult Cart()                 { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult AddToCart(string productId, int qty = 1)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API thêm vào giỏ
        return Json(new { success = true, message = "Đã thêm vào giỏ hàng!" });
    }

    [HttpPost]
    public IActionResult RemoveFromCart(string cartItemId)
    {
        var g = Guard(); if (g != null) return g;
        // TODO: gọi API xóa khỏi giỏ
        TempData["Success"] = "Đã xóa món ăn khỏi giỏ hàng.";
        return RedirectToAction("Cart");
    }

    // ---- Đặt hàng ----
    public IActionResult Checkout()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult PlaceOrder(string paymentMethod, string? voucherCode, string? cartJson, string? shippingAddress)
    {
        if (!IsLoggedIn)
            return Json(new { success = false, message = "Vui lòng đăng nhập để đặt hàng." });

        // Parse giỏ hàng từ JSON gửi lên
        if (string.IsNullOrWhiteSpace(cartJson))
        {
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });
        }

        List<CartItemDto>? items;
        try
        {
            items = System.Text.Json.JsonSerializer.Deserialize<List<CartItemDto>>(cartJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return Json(new { success = false, message = "Dữ liệu giỏ hàng không hợp lệ." });
        }

        if (items == null || items.Count == 0)
        {
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });
        }

        // Tính tổng tiền
        decimal totalAmount = 0;
        foreach (var item in items)
            totalAmount += item.Price * item.Quantity;

        // Tính giảm giá từ voucher (nếu có)
        decimal discountAmount = 0;
        int? voucherId = null;
        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == voucherCode && v.IsActive);
            if (voucher != null)
            {
                voucherId = voucher.Id;
                if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount > 0)
                    discountAmount = voucher.DiscountAmount.Value;
                else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent > 0)
                    discountAmount = totalAmount * (voucher.DiscountPercent.Value / 100m);
            }
        }

        decimal finalAmount = Math.Max(0, totalAmount - discountAmount);

        // Tạo đơn hàng
        var order = new PizzaDeli.Models.Order
        {
            UserId        = CurrentUserId!,
            OrderDate     = DateTime.Now,
            TotalAmount   = totalAmount,
            DiscountAmount= discountAmount,
            FinalAmount   = finalAmount,
            PaymentMethod = paymentMethod ?? "COD",
            Status        = "Pending",
            ShippingAddress = shippingAddress ?? "Chưa cập nhật địa chỉ",
            VoucherId     = voucherId
        };

        _context.Orders.Add(order);
        _context.SaveChanges(); // Lưu để lấy order.Id

        // Thêm chi tiết đơn hàng
        foreach (var item in items)
        {
            // Trích xuất ID gốc (với Pizza tuỳ chỉnh ID có dạng P001-Mỏng-Cheddar-Nấm)
            var realId = item.Id.Contains("-") ? item.Id.Split('-')[0] : item.Id;

            // Kiểm tra sản phẩm tồn tại trong DB
            var productExists = _context.Products.Any(p => p.Id == realId);
            if (!productExists) continue;

            _context.OrderDetails.Add(new PizzaDeli.Models.OrderDetail
            {
                OrderId   = order.Id,
                ProductId = realId,
                Quantity  = item.Quantity,
                UnitPrice = item.Price
            });
        }

        _context.SaveChanges();

        return Json(new { success = true, orderId = order.Id.Substring(0, 8).ToUpper() });
    }

    [HttpGet]
    public IActionResult GetLatestOrder()
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Vui lòng đăng nhập" });

        var latestOrder = _context.Orders
            .Include(o => o.OrderDetails!)
                .ThenInclude(d => d.Product)
            .Where(o => o.UserId == CurrentUserId)
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefault();

        if (latestOrder == null)
            return Json(new { success = false, message = "Bạn chưa có đơn hàng nào." });

        if (latestOrder.OrderDetails == null || latestOrder.OrderDetails.Count == 0)
            return Json(new { success = false, message = "Đơn hàng rỗng." });

        var items = latestOrder.OrderDetails.Select(od => new
        {
            id = od.Product?.Id ?? "",
            name = od.Product?.Name ?? "",
            price = od.UnitPrice,
            quantity = od.Quantity,
            image = od.Product?.ImageUrl ?? "/images/placeholder.png"
        }).ToList();

        return Json(new { success = true, items = items });
    }

    // ---- Lịch sử đơn hàng ----
    public IActionResult Orders()
    {
        var g = Guard(); if (g != null) return g;
        var uid = CurrentUserId;
        var orders = _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
            .Where(o => o.UserId == uid)
            .OrderByDescending(o => o.OrderDate)
            .ToList();
        return View(orders);
    }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelOrder(string id)
    {
        var g = Guard(); if (g != null) return g;
        var order = _context.Orders.FirstOrDefault(o => o.Id == id && o.UserId == CurrentUserId);
        if (order == null || order.Status != "Pending")
        {
            TempData["Error"] = "Không thể hủy đơn hàng này.";
            return RedirectToAction("Orders");
        }
        order.Status = "Cancelled";
        _context.SaveChanges();
        TempData["Success"] = "Đã hủy đơn hàng thành công.";
        return RedirectToAction("Orders");
    }

    // ---- Voucher ----
    public IActionResult Vouchers()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public IActionResult ApplyVoucher(string code, decimal currentSubtotal)
    {
        var g = Guard(); if (g != null) return g;
        
        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Vui lòng nhập mã." });

        var voucher = _context.Vouchers.FirstOrDefault(v => v.Code == code && v.IsActive);
        if (voucher == null)
            return Json(new { success = false, message = "Mã giảm giá không tồn tại hoặc đã bị khóa." });

        // Check date
        if (voucher.StartDate.HasValue && voucher.StartDate > DateTime.Now)
            return Json(new { success = false, message = "Mã này chưa đến thời gian áp dụng." });

        if (voucher.ExpiryDate.HasValue && voucher.ExpiryDate < DateTime.Now)
            return Json(new { success = false, message = "Mã giảm giá đã quá hạn." });

        // Check MinOrderValue
        if (voucher.MinOrderValue > currentSubtotal)
            return Json(new { success = false, message = $"Đơn hàng phải từ {voucher.MinOrderValue:N0} ₫ để dùng mã này." });

        // Tính toán discount
        decimal discountAmount = 0;
        if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount > 0)
        {
            discountAmount = voucher.DiscountAmount.Value;
        }
        else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent > 0)
        {
            // Trừ theo phần trăm
            discountAmount = currentSubtotal * (voucher.DiscountPercent.Value / 100);
        }

        return Json(new { success = true, discount = discountAmount, message = "Áp dụng mã giảm giá thành công!" });
    }

    public class ReviewDto
    {
        public string ProductId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    [HttpPost]
    public IActionResult AddComment(string productId, string content, int rating)
    {
        var g = Guard(); if (g != null) return g;
        
        var realId = productId.Contains("-") ? productId.Split('-')[0] : productId;

        var review = new PizzaDeli.Models.Review
        {
            UserId = CurrentUserId!,
            ProductId = realId,
            Rating = rating,
            Comment = content ?? "",
            CreatedAt = DateTime.Now,
            IsHidden = false
        };
        _context.Reviews.Add(review);
        _context.SaveChanges();

        return Json(new { success = true });
    }

    [HttpPost]
    public IActionResult SubmitMultipleReviews([FromBody] List<ReviewDto> reviews)
    {
        var g = Guard(); if (g != null) return g;

        if (reviews == null || !reviews.Any()) 
            return Json(new { success = false });

        foreach (var r in reviews)
        {
            var realId = r.ProductId.Contains("-") ? r.ProductId.Split('-')[0] : r.ProductId;
            
            if (r.Rating < 1 || r.Rating > 5) continue;

            var review = new PizzaDeli.Models.Review
            {
                UserId = CurrentUserId!,
                ProductId = realId,
                Rating = r.Rating,
                Comment = r.Content ?? "",
                CreatedAt = DateTime.Now,
                IsHidden = false
            };
            _context.Reviews.Add(review);
        }

        _context.SaveChanges();
        return Json(new { success = true, message = "Cảm ơn bạn đã đánh giá!" });
    }
    
    [HttpGet]
    public IActionResult ProductReviews()
    {
        var g = Guard(); if (g != null) return g;
        
        var uid = CurrentUserId;
        var orders = _context.Orders
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
            .Where(o => o.UserId == uid)
            .OrderByDescending(o => o.OrderDate)
            .ToList();

        return View(orders);
    }

    [HttpGet]
    public IActionResult ReviewDetail(string id)
    {
        var product = _context.Products.Find(id);
        if (product == null) return NotFound();

        var reviews = _context.Reviews
            .Include(r => r.User)
            .Where(r => r.ProductId == id && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        ViewBag.Product = product;
        return View(reviews);
    }

    /// <summary>API: Lấy điểm đánh giá trung bình + số lượng đánh giá của sản phẩm</summary>
    [HttpGet]
    public IActionResult GetProductRating(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return Json(new { avgRating = 0.0, count = 0 });

        var reviews = _context.Reviews
            .Where(r => r.ProductId == productId)
            .ToList();

        if (!reviews.Any())
            return Json(new { avgRating = 0.0, count = 0 });

        double avg = Math.Round(reviews.Average(r => r.Rating), 1);
        int count  = reviews.Count;

        return Json(new { avgRating = avg, count });
    }
}

// DTO để parse JSON giỏ hàng từ client
public class CartItemDto
{
    public string Id        { get; set; } = string.Empty;
    public string Name      { get; set; } = string.Empty;
    public decimal Price    { get; set; }
    public int Quantity     { get; set; } = 1;
    public string? Image    { get; set; }
}

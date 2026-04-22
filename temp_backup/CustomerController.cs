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
    private readonly PizzaDeli.Services.UserService _userService;
    private readonly PizzaDeli.Services.OrderService _orderService;
    private readonly PizzaDeli.Services.VoucherService _voucherService;
    private readonly PizzaDeli.Services.ReviewService _reviewService;
    private readonly PizzaDeli.Services.ProductService _productService;

    public CustomerController(
        PizzaDeli.Services.UserService userService,
        PizzaDeli.Services.OrderService orderService,
        PizzaDeli.Services.VoucherService voucherService,
        PizzaDeli.Services.ReviewService reviewService,
        PizzaDeli.Services.ProductService productService)
    {
        _userService = userService;
        _orderService = orderService;
        _voucherService = voucherService;
        _reviewService = reviewService;
        _productService = productService;
    }

    private IActionResult? Guard() => RequireLogin();

    // ---- Thông tin cá nhân ----
    public async Task<IActionResult> Profile()
    {
        var g = Guard(); if (g != null) return g;
        var user = await _userService.GetByIdAsync(CurrentUserId!);
        if (user == null) return NotFound();
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string fullName, string phone, DateTime? dateOfBirth, string gender, string address)
    {
        var g = Guard(); if (g != null) return g;
        
        bool success = await _userService.UpdateProfileAsync(CurrentUserId!, fullName, phone, dateOfBirth, gender, address);
        if (!success) return NotFound();

        // Cập nhật lại session name
        HttpContext.Session.SetString("USER_NAME", fullName);

        TempData["Success"] = "Cập nhật thông tin thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(string newEmail, string verificationCode)
    {
        var g = Guard(); if (g != null) return g;
        
        var sessionCode = HttpContext.Session.GetString("VERIFICATION_CODE");
        var targetEmail = HttpContext.Session.GetString("TARGET_EMAIL");

        if (string.IsNullOrEmpty(sessionCode) || verificationCode != sessionCode || newEmail != targetEmail)
        {
            TempData["Error"] = "Mã xác nhận không hợp lệ hoặc không khớp Email!";
            return RedirectToAction("Profile");
        }

        bool success = await _userService.ChangeEmailAsync(CurrentUserId!, newEmail);
        if (!success) return NotFound();

        // Cập nhật session
        HttpContext.Session.SetString("USER_EMAIL", newEmail);
        TempData["Success"] = "Đổi email thành công!";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public async Task<IActionResult> RequestVerificationCode(string newEmail)
    {
        var g = Guard(); if (g != null) return Json(new { success = false, message = "Chưa đăng nhập!" });
        
        if (string.IsNullOrWhiteSpace(newEmail))
            return Json(new { success = false, message = "Email không hợp lệ!" });

        var user = await _userService.GetByIdAsync(CurrentUserId!);
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

    [HttpGet]
    public IActionResult ChangePassword()
    {
        var g = Guard(); if (g != null) return g;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPasswordOtp(string currentPassword)
    {
        var g = Guard(); if (g != null) return g;
        
        var user = await _userService.GetByIdAsync(CurrentUserId!);
        if (user == null) return Json(new { success = false, message = "Người dùng không tồn tại!" });

        if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
        {
            return Json(new { success = false, message = "Mật khẩu hiện tại không đúng!" });
        }

        // Tạo mã ngẫu nhiên 6 số
        Random rnd = new Random();
        string code = rnd.Next(100000, 999999).ToString();

        // Lưu session OTP đổi mật khẩu
        HttpContext.Session.SetString("PWD_OTP", code);
        HttpContext.Session.SetString("PWD_OTP_VALIDATED", "false");

        try 
        {
            var client = new System.Net.Mail.SmtpClient("smtp.gmail.com", 587)
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential("phamanhkhoa56789@gmail.com", "ejff yemk uhwd wdtv")
            };

            var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress("pizzadelidemo1@gmail.com", "PizzaDeli Security"),
                Subject = "PizzaDeli - Xác nhận đổi Mật khẩu",
                Body = $"<h2>Yêu cầu thay đổi mật khẩu tài khoản {user.FullName}</h2><hr/>" +
                       $"<p>Chào bạn,</p>" +
                       $"<p>Đây là mã xác nhận (OTP) 6 chữ số để tiếp tục đổi mật khẩu của bạn:</p>" +
                       $"<h1 style='color: #16a34a; letter-spacing: 0.2em;'>{code}</h1>" +
                       $"<p>Vui lòng tuyệt đối không tiết lộ mã này cho bất kỳ ai.</p>",
                IsBodyHtml = true,
            };
            mailMessage.To.Add(user.Email);
            client.Send(mailMessage);

            return Json(new { success = true, message = "Mã xác nhận đã được gửi về email của bạn!" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Gặp lỗi hệ thống gửi mail: {ex.Message}" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyPasswordOtp(string code)
    {
        var g = Guard(); if (g != null) return g;

        var savedOtp = HttpContext.Session.GetString("PWD_OTP");
        if (string.IsNullOrEmpty(savedOtp) || savedOtp != code)
        {
            return Json(new { success = false, message = "Mã xác nhận không hợp lệ hoặc đã hết hạn!" });
        }

        // Đánh dấu là đã xác minh đúng
        HttpContext.Session.SetString("PWD_OTP_VALIDATED", "true");
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string newPassword)
    {
        var g = Guard(); if (g != null) return g;

        // Kiểm tra xem đã qua vòng gác OTP chưa
        var isValidated = HttpContext.Session.GetString("PWD_OTP_VALIDATED");
        if (isValidated != "true")
        {
            TempData["Error"] = "Bạn chưa hoàn tất xác minh mã OTP!";
            return RedirectToAction("ChangePassword");
        }

        // Cập nhật mật khẩu 
        bool success = await _userService.ChangePasswordForceAsync(CurrentUserId!, newPassword);
        if (!success) return NotFound();

        // Xóa dấu vết session
        HttpContext.Session.Remove("PWD_OTP");
        HttpContext.Session.Remove("PWD_OTP_VALIDATED");

        TempData["Success"] = "Đổi mật khẩu thành công!";
        return RedirectToAction("Profile");
    }

    // ---- Giỏ hàng (lưu trên localStorage ở client) ----
    public IActionResult Cart()                 { var g = Guard(); if (g != null) return g; return View(); }

    // ---- Đặt hàng ----
    public async Task<IActionResult> Checkout()
    {
        var g = Guard(); if (g != null) return g;
        ViewBag.ActiveVouchers = await _voucherService.GetValidVouchersAsync();

        // Truyền thông vị profile để tự động điền form giao hàng
        var user = await _userService.GetByIdAsync(CurrentUserId!);
        if (user != null)
        {
            ViewBag.UserFullName = user.FullName ?? "";
            ViewBag.UserPhone    = user.Phone    ?? "";
            ViewBag.UserAddress  = user.Address  ?? "";
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(string paymentMethod, string? voucherCode, string? cartJson, string? shippingAddress)
    {
        if (!IsLoggedIn)
            return Json(new { success = false, message = "Vui lòng đăng nhập để đặt hàng." });

        if (string.IsNullOrWhiteSpace(cartJson))
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });

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
            return Json(new { success = false, message = "Giỏ hàng trống, không thể đặt hàng." });

        var order = await _orderService.PlaceOrderAsync(CurrentUserId!, paymentMethod, voucherCode, items, shippingAddress);

        return Json(new { success = true, orderId = order.Id.Substring(0, 8).ToUpper() });
    }

    [HttpGet]
    public async Task<IActionResult> GetLatestOrder()
    {
        if (!IsLoggedIn) return Json(new { success = false, message = "Vui lòng đăng nhập" });

        var latestOrder = await _orderService.GetLatestOrderAsync(CurrentUserId!);

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
    public async Task<IActionResult> Orders()
    {
        var g = Guard(); if (g != null) return g;
        var orders = await _orderService.GetByUserAsync(CurrentUserId!);
        return View(orders);
    }
    public IActionResult OrderDetail(string id) { var g = Guard(); if (g != null) return g; ViewBag.Id = id; return View(); }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(string id)
    {
        var g = Guard(); if (g != null) return g;
        var result = await _orderService.CancelOrderAsync(id, CurrentUserId!);
        if (!result)
        {
            TempData["Error"] = "Không thể hủy đơn hàng này.";
            return RedirectToAction("Orders");
        }
        TempData["Success"] = "Đã hủy đơn hàng thành công.";
        return RedirectToAction("Orders");
    }

    // ---- Voucher ----
    public IActionResult Vouchers()             { var g = Guard(); if (g != null) return g; return View(); }

    [HttpPost]
    public async Task<IActionResult> ApplyVoucher(string code, string currentSubtotal)
    {
        if (!IsLoggedIn)
            return Json(new { success = false, message = "Vui lòng đăng nhập." });

        if (string.IsNullOrWhiteSpace(code))
            return Json(new { success = false, message = "Vui lòng nhập mã." });

        if (!decimal.TryParse(
                currentSubtotal?.Replace(",", "").Replace(" ", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal subtotal))
            subtotal = 0;

        var voucher = await _voucherService.ValidateAsync(code, subtotal);
        if (voucher == null)
            return Json(new { success = false, message = "Mã giảm giá không tồn tại, đã hết hạn, hoặc chưa đủ điều kiện." });

        decimal discountAmount = 0;
        if (voucher.DiscountAmount.HasValue && voucher.DiscountAmount > 0)
            discountAmount = voucher.DiscountAmount.Value;
        else if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent > 0)
            discountAmount = subtotal * (voucher.DiscountPercent.Value / 100m);

        return Json(new { success = true, discount = discountAmount, message = "Áp dụng mã giảm giá thành công!" });
    }

    public class ReviewDto
    {
        public string ProductId { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    [HttpPost]
    public async Task<IActionResult> AddComment(string productId, string content, int rating)
    {
        var g = Guard(); if (g != null) return g;
        
        var realId = productId.Contains("-") ? productId.Split('-')[0] : productId;
        await _reviewService.AddAsync(CurrentUserId!, realId, content ?? "", rating);

        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> SubmitMultipleReviews([FromBody] List<ReviewDto> reviews)
    {
        var g = Guard(); if (g != null) return g;

        if (reviews == null || !reviews.Any()) 
            return Json(new { success = false });

        var reviewTuples = reviews.Select(r => (r.ProductId, r.Rating, r.Content));
        await _reviewService.AddMultipleAsync(CurrentUserId!, reviewTuples);

        return Json(new { success = true, message = "Cảm ơn bạn đã đánh giá!" });
    }
    
    [HttpGet]
    public async Task<IActionResult> ProductReviews()
    {
        var g = Guard(); if (g != null) return g;
        var orders = await _orderService.GetByUserAsync(CurrentUserId!);
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> ReviewDetail(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();

        var reviews = await _reviewService.GetByProductAsync(id);

        ViewBag.Product = product;
        return View(reviews);
    }

    /// <summary>API: Lấy điểm đánh giá trung bình + số lượng đánh giá của sản phẩm</summary>
    [HttpGet]
    public async Task<IActionResult> GetProductRating(string productId)
    {
        if (string.IsNullOrWhiteSpace(productId))
            return Json(new { avgRating = 0.0, count = 0 });

        var reviews = await _reviewService.GetByProductAsync(productId);

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

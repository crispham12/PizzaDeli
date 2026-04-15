using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;

namespace PizzaDeli.Controllers
{
    [ApiController]
    [Route("api/sepay")]
    public class SepayController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public SepayController(ApplicationDbContext db)
        {
            _db = db;
        }

        // DTO chuẩn SePay webhook
        public class SepayWebhookData
        {
            public string? gateway            { get; set; }
            public string? transactionDate    { get; set; }
            public string? accountNumber      { get; set; }
            public string? subAccount         { get; set; }
            public decimal? transferAmount    { get; set; }
            public decimal? amountIn          { get; set; }
            public decimal? amountOut         { get; set; }
            public decimal? accumulated       { get; set; }
            public string? code               { get; set; }
            public string? transactionContent { get; set; }
            public string? referenceNumber    { get; set; }
            public string? body               { get; set; }
            // Alias hỗ trợ cả định dạng tùy chỉnh
            public string? content            { get; set; }
            public decimal? amount            { get; set; }
        }

        // ========== WEBHOOK TỪ SEPAY ==========
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveWebhook([FromBody] SepayWebhookData data)
        {
            try
            {
                // ① Guard: kiểm tra null
                if (data == null)
                    return Ok(new { success = false, message = "No data" });

                // Lấy nội dung & số tiền (hỗ trợ cả hai định dạng SePay)
                string txContent = !string.IsNullOrEmpty(data.content)
                                    ? data.content : (data.transactionContent ?? "");

                // Lấy số tiền — ưu tiên: amount → transferAmount → amountIn
                decimal received = data.amount.GetValueOrDefault() > 0         ? data.amount.GetValueOrDefault()
                                 : data.transferAmount.GetValueOrDefault() > 0  ? data.transferAmount.GetValueOrDefault()
                                 : data.amountIn.GetValueOrDefault();

                Console.WriteLine($"[SePay Webhook] Nội dung: {txContent} | Số tiền: {received}");

                if (string.IsNullOrEmpty(txContent))
                    return Ok(new { success = false, message = "Không có nội dung giao dịch" });

                // ② Dùng Regex để parse an toàn: khớp "DH" + chuỗi chữ-số
                var upperContent = txContent.ToUpper();
                var match = Regex.Match(upperContent, @"DH([A-Z0-9]+)");

                if (!match.Success)
                    return Ok(new { success = false, message = "Không tìm thấy mã DH trong nội dung" });

                var orderShortId = match.Groups[1].Value;
                Console.WriteLine($"[SePay Webhook] Tìm đơn hàng với shortId: {orderShortId}");

                // Order.Id là Guid("N") → 32 ký tự hex, 8 ký tự đầu viết hoa là shortId
                var order = await _db.Orders.FirstOrDefaultAsync(o =>
                    o.Id.ToUpper().StartsWith(orderShortId));

                if (order == null)
                {
                    Console.WriteLine("[SePay Webhook] Không tìm thấy đơn hàng.");
                    return Ok(new { success = false, message = "Không tìm thấy đơn hàng" });
                }

                // Bảo mật: kiểm tra số tiền (±1000đ bù phí quy đổi nhỏ)
                if (received < order.FinalAmount - 1000)
                {
                    Console.WriteLine($"[SePay Webhook] Số tiền không khớp: nhận {received} < cần {order.FinalAmount}");
                    return Ok(new { success = false, message = "Số tiền không khớp" });
                }

                // Chỉ cập nhật nếu đang ở trạng thái chờ
                if (order.Status == "Pending" || order.Status == "Chờ xử lý")
                {
                    order.Status = "Confirmed";
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[SePay Webhook] ✅ Xác nhận thanh toán đơn: {order.Id}");
                }

                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SePay Webhook] Lỗi: {ex.Message}");
                // Trả về Ok để SePay không retry liên tục
                return Ok(new { success = false, message = ex.Message });
            }
        }

        // ========== FRONTEND POLLING: Kiểm tra trạng thái đơn ==========
        [HttpGet("order-status/{shortId}")]
        public async Task<IActionResult> CheckOrderStatus(string shortId)
        {
            if (string.IsNullOrWhiteSpace(shortId))
                return BadRequest("Thiếu mã đơn hàng");

            var upperShortId = shortId.ToUpper();
            var order = await _db.Orders.FirstOrDefaultAsync(o =>
                o.Id.ToUpper().StartsWith(upperShortId));

            if (order == null)
                return Ok(new { status = "not_found", isPaid = false });

            bool isPaid = order.Status == "Confirmed"
                       || order.Status == "Đã xác nhận"
                       || order.Status == "Shipping"
                       || order.Status == "Đang giao hàng"
                       || order.Status == "Completed"
                       || order.Status == "Đã hoàn thành";

            return Ok(new
            {
                status  = order.Status,
                isPaid  = isPaid,
                orderId = order.Id
            });
        }
    }
}

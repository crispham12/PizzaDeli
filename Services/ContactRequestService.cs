using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý yêu cầu hỗ trợ khách hàng từ form Liên hệ</summary>
public class ContactRequestService
{
    private readonly ApplicationDbContext _db;
    public ContactRequestService(ApplicationDbContext db) => _db = db;

    // ---- Khách hàng gửi yêu cầu ----
    public async Task<ContactRequest> AddAsync(string fullName, string? orderCode, string issueType, string message, string? phone, string? address, string? userId)
    {
        var request = new ContactRequest
        {
            FullName  = fullName,
            OrderCode = orderCode,
            IssueType = issueType,
            Message   = message,
            Phone     = phone,
            Address   = address,
            Status    = "Waiting",
            CreatedAt = DateTime.Now,
            UserId    = userId
        };
        _db.ContactRequests.Add(request);
        await _db.SaveChangesAsync();
        return request;
    }

    // ---- Staff: Lấy tất cả yêu cầu (mới nhất trước) ----
    public async Task<List<ContactRequest>> GetAllAsync()
        => await _db.ContactRequests
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

    // ---- Lấy yêu cầu theo khách hàng (cho trang Hồi đáp) ----
    public async Task<List<ContactRequest>> GetByUserAsync(string userId)
        => await _db.ContactRequests
                    .Where(r => r.UserId == userId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

    // ---- Staff: Cập nhật trạng thái ticket ----
    public async Task<bool> UpdateStatusAsync(int id, string status)
    {
        var request = await _db.ContactRequests.FindAsync(id);
        if (request == null) return false;
        request.Status = status;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- CHAT SYSTEM ----
    public async Task<List<ContactMessage>> GetChatMessagesAsync(int ticketId)
    {
        return await _db.ContactMessages
                        .Where(m => m.ContactRequestId == ticketId)
                        .OrderBy(m => m.CreatedAt)
                        .ToListAsync();
    }

    public async Task<ContactMessage?> SendChatMessageAsync(int ticketId, string sender, string content)
    {
        var ticket = await _db.ContactRequests.FindAsync(ticketId);
        if (ticket == null) return null;

        var message = new ContactMessage
        {
            ContactRequestId = ticketId,
            Sender = sender,
            Content = content,
            CreatedAt = DateTime.Now
        };
        _db.ContactMessages.Add(message);
        
        // Tự động chuyển trạng thái nếu Staff vừa chát
        if (sender == "Staff" && ticket.Status == "Waiting")
        {
            ticket.Status = "Processing"; 
        }

        await _db.SaveChangesAsync();
        return message;
    }
}

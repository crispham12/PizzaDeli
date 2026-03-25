using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;
using BCrypt.Net;

namespace PizzaDeli.Services;

public class AuthService
{
    private readonly ApplicationDbContext _db;

    public AuthService(ApplicationDbContext db) 
    {
        _db = db;
    }

    // ---- Login ----
    public async Task<(bool ok, string? token, UserInfo? user, string? error)>
        LoginAsync(string email, string password)
    {
        var emailLower = email.Trim().ToLower();
        var user = await _db.Users
            .FirstOrDefaultAsync(x => x.Email.ToLower() == emailLower);
        
        if (user == null || !user.IsActive)
            return (false, null, null, "Không tìm thấy tài khoản email này hoặc tài khoản đã bị khóa.");

        bool isValid = false;
        try
        {
            isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Nếu password trong DB đang ở dạng plain-text (tạo tay), so sánh trực tiếp
            if (user.PasswordHash == password)
            {
                isValid = true;
                // Auto-heal: Cập nhật lại mật khẩu thành mã băm BCrypt chuẩn
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _db.SaveChangesAsync();
            }
        }

        if (!isValid)
            return (false, null, null, "Sai mật khẩu.");

        // Tạo token giả (do chưa dùng JWT chính thức)
        var fakeToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user.Id}:{DateTime.Now.Ticks}"));

        var userInfo = new UserInfo
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            Phone = user.Phone,
            Avatar = user.Avatar
        };

        return (true, fakeToken, userInfo, null);
    }

    // ---- Register ----
    public async Task<(bool ok, string? error)> RegisterAsync(RegisterRequest req)
    {
        var emailLower = req.Email.Trim().ToLower();
        var exists = await _db.Users.AnyAsync(x => x.Email.ToLower() == emailLower);
        
        if (exists)
            return (false, "Email này đã được sử dụng.");

        var newUser = new User
        {
            Id = Guid.NewGuid().ToString("N"),
            FullName = req.FullName,
            Email = req.Email,
            Phone = req.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = UserRole.Customer, // Mặc định là Customer
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        
        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        return (true, null);
    }
}

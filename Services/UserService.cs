using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Tài khoản người dùng - Admin quản lý, Customer xem thông tin cá nhân</summary>
public class UserService
{
    private readonly ApplicationDbContext _db;
    public UserService(ApplicationDbContext db) => _db = db;

    // ---- Lấy tất cả tài khoản (Admin) ----
    public async Task<List<User>> GetAllAsync()
        => await _db.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();

    // ---- Lấy danh sách theo Role ----
    public async Task<List<User>> GetByRoleAsync(UserRole role)
        => await _db.Users.Where(u => u.Role == role).ToListAsync();

    // ---- Lấy chi tiết 1 tài khoản ----
    public async Task<User?> GetByIdAsync(string id)
        => await _db.Users.FirstOrDefaultAsync(u => u.Id == id);

    // ---- Tìm theo Email (dùng cho đăng nhập) ----
    public async Task<User?> GetByEmailAsync(string email)
        => await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    // ---- Tìm theo Email (chính xác hơn, cho quên mật khẩu) ----
    public async Task<User?> GetUserByEmailExactAsync(string email)
    {
        var emailLower = email.Trim().ToLower();
        return await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
    }

    // ---- Đặt lại mật khẩu (quên mật khẩu) ----
    public async Task<bool> ResetPasswordByEmailAsync(string email, string newPassword)
    {
        var emailLower = email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
        if (user == null) return false;
        
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Customer: Cập nhật thông tin cá nhân ----
    public async Task<bool> UpdateProfileAsync(string userId, string fullName, string? phone, DateTime? dateOfBirth, string? gender, string? address)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.FullName    = fullName;
        user.Phone       = phone;
        user.DateOfBirth = dateOfBirth;
        user.Gender      = gender;
        if (!string.IsNullOrEmpty(address)) {
            user.Address = address;
        }
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Customer: Đổi Email ----
    public async Task<bool> ChangeEmailAsync(string userId, string newEmail)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.Email = newEmail;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Customer: Đổi mật khẩu (biết mật khẩu cũ) ----
    public async Task<bool> ChangePasswordAsync(string userId, string oldHash, string newHash)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null || user.PasswordHash != oldHash) return false;
        user.PasswordHash = newHash;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Customer: Đổi mật khẩu (ép buộc) ----
    public async Task<bool> ChangePasswordForceAsync(string userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Admin: Khóa / Mở tài khoản ----
    public async Task<bool> SetActiveAsync(string userId, bool isActive)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        user.IsActive = isActive;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Admin: Xóa tài khoản ----
    public async Task<bool> DeleteAsync(string userId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return false;
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Admin: Tạo tài khoản Nhân viên ----
    public async Task<User> CreateStaffAsync(string fullName, string email, string passwordHash, string? phone)
    {
        var user = new User
        {
            Id           = Guid.NewGuid().ToString("N"),
            FullName     = fullName,
            Email        = email,
            PasswordHash = passwordHash,
            Phone        = phone,
            Role         = UserRole.Staff,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }
}

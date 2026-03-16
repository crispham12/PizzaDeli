using Microsoft.EntityFrameworkCore;
using PizzaDeli.Data;
using PizzaDeli.Models;

namespace PizzaDeli.Services;

/// <summary>Quản lý Bình luận / Đánh giá - Customer bình luận, Staff ẩn vi phạm</summary>
public class ReviewService
{
    private readonly ApplicationDbContext _db;
    public ReviewService(ApplicationDbContext db) => _db = db;

    // ---- Lấy bình luận theo sản phẩm ----
    public async Task<List<Review>> GetByProductAsync(string productId)
        => await _db.Reviews
                    .Include(r => r.User)
                    .Where(r => r.ProductId == productId && !r.IsHidden)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

    // ---- Lấy tất cả bình luận (Staff/Admin quản lý) ----
    public async Task<List<Review>> GetAllAsync()
        => await _db.Reviews
                    .Include(r => r.User)
                    .Include(r => r.Product)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

    // ---- Customer: Thêm bình luận ----
    public async Task<Review> AddAsync(string userId, string productId, string comment, int rating)
    {
        var review = new Review
        {
            UserId    = userId,
            ProductId = productId,
            Comment   = comment,
            Rating    = Math.Clamp(rating, 1, 5),
            IsHidden  = false,
            CreatedAt = DateTime.Now
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();
        return review;
    }

    // ---- Staff/Admin: Ẩn bình luận vi phạm ----
    public async Task<bool> HideAsync(int reviewId)
    {
        var review = await _db.Reviews.FindAsync(reviewId);
        if (review == null) return false;
        review.IsHidden = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Staff/Admin: Xóa bình luận ----
    public async Task<bool> DeleteAsync(int reviewId)
    {
        var review = await _db.Reviews.FindAsync(reviewId);
        if (review == null) return false;
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Tính điểm đánh giá trung bình ----
    public async Task<double> GetAverageRatingAsync(string productId)
    {
        var ratings = await _db.Reviews
                               .Where(r => r.ProductId == productId && !r.IsHidden)
                               .Select(r => r.Rating)
                               .ToListAsync();
        return ratings.Any() ? ratings.Average() : 0;
    }
}

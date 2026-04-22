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
    /// <summary>
    /// Logic: Lấy danh sách bình luận của 1 sản phẩm
    /// Cách hoạt động: Lấy các bình luận không bị ẩn (IsHidden = false), kèm theo thông tin User để hiển thị tên và avatar.
    /// </summary>
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

    public async Task<(List<Review> Reviews, int Total, double AverageRating, int PendingReplies)> GetStaffReviewsAsync(int ratingFilter)
    {
        var query = _db.Reviews.Include(r => r.User).Include(r => r.Product).AsQueryable();

        var total = await query.CountAsync();
        var average = total > 0 ? await query.AverageAsync(r => r.Rating) : 0.0;
        var pending = await query.CountAsync(r => r.Rating <= 3 && string.IsNullOrEmpty(r.AdminReply));
        
        if (ratingFilter > 0)
        {
            if (ratingFilter == 2) query = query.Where(r => r.Rating <= 2);
            else query = query.Where(r => r.Rating == ratingFilter);
        }
        
        var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return (reviews, total, average, pending);
    }

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

    /// <summary>
    /// Logic: Lưu nhiều bình luận cùng lúc (Đánh giá sau khi mua hàng)
    /// Cách hoạt động: Lặp qua mảng đánh giá được gửi từ Form để Add vào DbContext, sau đó SaveChanges 1 lần để tối ưu hiệu suất.
    /// </summary>
    public async Task<bool> AddMultipleAsync(string userId, IEnumerable<(string ProductId, int Rating, string Content)> reviews)
    {
        if (reviews == null || !reviews.Any()) return false;

        foreach (var r in reviews)
        {
            var realId = r.ProductId.Contains("-") ? r.ProductId.Split('-')[0] : r.ProductId;
            if (r.Rating < 1 || r.Rating > 5) continue;

            var review = new Review
            {
                UserId = userId,
                ProductId = realId,
                Rating = r.Rating,
                Comment = r.Content ?? "",
                CreatedAt = DateTime.Now,
                IsHidden = false
            };
            _db.Reviews.Add(review);
        }

        await _db.SaveChangesAsync();
        return true;
    }

    // ---- Staff/Admin: Toggle Ẩn/Hiện bình luận ----
    public async Task<bool> ToggleVisibilityAsync(int reviewId)
    {
        var review = await _db.Reviews.FindAsync(reviewId);
        if (review == null) return false;
        review.IsHidden = !review.IsHidden;
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

    // ---- Staff/Admin: Trả lời bình luận ----
    public async Task<bool> ReplyAsync(int reviewId, string reply)
    {
        var review = await _db.Reviews.FindAsync(reviewId);
        if (review == null) return false;
        review.AdminReply = reply;
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

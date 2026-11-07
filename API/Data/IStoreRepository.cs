using DGustAR.Api.Models;

namespace DGustAR.Api.Data;

public interface IStoreRepository
{
    RestaurantInfo GetRestaurantInfo();
    Task<RestaurantInfo> UpdateGoogleMapsSettingsAsync(GoogleMapsSettingsDto dto);
    Task<(int totalClicks, GoogleReviewClickLog entry)> RecordGoogleReviewClickAsync(GoogleReviewClickDto dto);
    List<GoogleReviewClickLog> GetGoogleReviewClicksLog();

    List<string> GetTables();
    Task<List<string>> UpdateTablesAsync(List<string> tables);

    Task<object> GetMenuAsync();
    List<Category> GetCategories();
    Task<Category> CreateCategoryAsync(Category category);
    Task<Category?> DeleteCategoryAsync(string id);

    List<Product> GetAllProductsAdmin();
    Product? GetProductById(string id);
    Task<Product> AddProductAsync(Product product);
    Task<Product?> UpdateProductAsync(string id, Product updated);
    Task<Product?> DeleteProductAsync(string id);
    Task<Product?> ToggleProductAvailabilityAsync(string id);
    Task<string> UploadAssetAsync(AssetUploadDto asset);

    List<Order> GetAllOrders(string? estado = null, string? mesa = null);
    Order? GetOrderById(string id);
    Order? GetOrderByToken(string token);
    Task<Order> CreateOrderAsync(Order order);
    Task<Order?> UpdateOrderStatusAsync(string orderId, string newStatus);
    Task<Order?> MarkOrderClosedAsync(string orderId);

    Task<(Payment payment, Order order)> RegisterPaymentAsync(RegisterPaymentDto dto);

    List<Review> GetReviews();
    Task<Review> CreateReviewAsync(Review review);

    Task<CorteCaja> CreateCorteAsync(CreateCorteDto dto);
    List<CorteCaja> GetCortes();

    Task<object> GetStatsAsync(string period);
    Task<object> GetSalesReportAsync(string period);

    (bool valid, string role, string token) VerifyPin(string pin);
    (bool valid, string role) VerifyToken(string token);
    Task<string> UpdateAdminPinAsync(string currentPin, string newPin);
    Task<bool> UpdateStaffPinAsync(string adminPin, string newStaffPin);
}

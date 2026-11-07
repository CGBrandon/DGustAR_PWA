using DGustAR.Api.Data;
using DGustAR.Api.Models;
using DGustAR.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGustAR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IStoreRepository _db;
    private readonly AppWebSocketManager _wsManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IStoreRepository db, AppWebSocketManager wsManager, ILogger<AdminController> logger)
    {
        _db = db;
        _wsManager = wsManager;
        _logger = logger;
    }

    private (bool authorized, string role) CheckAuth(string? token, bool requireAdmin = false)
    {
        var auth = _db.VerifyToken(token ?? "");
        if (!auth.valid) return (false, string.Empty);
        if (requireAdmin && auth.role != "ADMIN") return (false, auth.role);
        return (true, auth.role);
    }

    // POST /api/admin/login
    [HttpPost("login")]
    public IActionResult Login([FromBody] AdminLoginDto body)
    {
        if (string.IsNullOrWhiteSpace(body?.Pin))
        {
            return BadRequest(new { error = "Debe ingresar el PIN de acceso." });
        }

        var auth = _db.VerifyPin(body.Pin);
        if (!auth.valid)
        {
            return StatusCode(401, new { error = "PIN incorrecto. Intente nuevamente." });
        }

        return Ok(new
        {
            success = true,
            token = auth.token,
            role = auth.role,
            restaurant = _db.GetRestaurantInfo(),
            message = auth.role == "ADMIN"
                ? "Acceso Gerencial Autorizado (Control Total)."
                : "Acceso de Personal Autorizado (Cocina KDS y Meseros)."
        });
    }

    // PUT /api/admin/security/pin
    [HttpPut("security/pin")]
    public async Task<IActionResult> UpdateAdminPin([FromBody] UpdatePinDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        try
        {
            var newToken = await _db.UpdateAdminPinAsync(body.CurrentPin, body.NewPin);
            await _wsManager.BroadcastToKitchenAsync("ADMIN_PIN_CHANGED", new { timestamp = DateTime.UtcNow.ToString("o") });

            return Ok(new
            {
                success = true,
                message = "PIN de Gerencia actualizado con éxito.",
                token = newToken
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT /api/admin/security/staff-pin
    [HttpPut("security/staff-pin")]
    public async Task<IActionResult> UpdateStaffPin([FromBody] UpdateStaffPinDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        try
        {
            await _db.UpdateStaffPinAsync(body.AdminPin, body.NewStaffPin);
            await _wsManager.BroadcastToKitchenAsync("STAFF_PIN_CHANGED", new { timestamp = DateTime.UtcNow.ToString("o") });

            return Ok(new
            {
                success = true,
                message = "PIN de Personal actualizado con éxito."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // PUT /api/admin/restaurant/google-maps
    [HttpPut("restaurant/google-maps")]
    public async Task<IActionResult> UpdateGoogleMaps([FromBody] GoogleMapsSettingsDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var updated = await _db.UpdateGoogleMapsSettingsAsync(body);
        await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { timestamp = DateTime.UtcNow.ToString("o") });
        await _wsManager.BroadcastToKitchenAsync("RESTAURANT_GOOGLE_MAPS_UPDATED", updated);

        return Ok(new
        {
            success = true,
            message = "Configuración de Google Maps guardada correctamente.",
            restaurant = updated
        });
    }

    // GET /api/admin/orders
    [HttpGet("orders")]
    public IActionResult GetOrders([FromQuery] string? estado, [FromQuery] string? mesa, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: false);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Ingrese el PIN de acceso." });

        var orders = _db.GetAllOrders(estado, mesa);
        return Ok(new
        {
            total = orders.Count,
            orders
        });
    }

    // PATCH /api/admin/orders/{id}/status
    [HttpPatch("orders/{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: false);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Ingrese el PIN de acceso." });

        if (string.IsNullOrWhiteSpace(body?.Estado))
        {
            return BadRequest(new { error = "Debe especificar el nuevo estado." });
        }

        Order? updated;
        try
        {
            updated = await _db.UpdateOrderStatusAsync(id, body.Estado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        if (updated == null)
        {
            return NotFound(new { error = "Orden no encontrada." });
        }

        await _wsManager.BroadcastToKitchenAsync("ORDER_STATUS_CHANGED", updated);
        await _wsManager.BroadcastToOrderAsync(updated.OrderToken, "ORDER_STATUS_CHANGED", updated);

        return Ok(new
        {
            success = true,
            message = $"Estado de la orden actualizado a {body.Estado}",
            order = updated
        });
    }

    // GET /api/admin/categories
    [HttpGet("categories")]
    public IActionResult GetCategories([FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        return Ok(_db.GetCategories());
    }

    // POST /api/admin/categories
    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory([FromBody] Category category, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var created = await _db.CreateCategoryAsync(category);
        await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "category-create", category = created });

        return StatusCode(201, new { success = true, category = created });
    }

    // DELETE /api/admin/categories/{id}
    [HttpDelete("categories/{id}")]
    public async Task<IActionResult> DeleteCategory(string id, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var deleted = await _db.DeleteCategoryAsync(id);
        if (deleted == null)
        {
            return NotFound(new { error = "Categoría no encontrada." });
        }

        await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "category-delete", id });

        return Ok(new
        {
            success = true,
            message = $"Categoría \"{deleted.Nombre}\" eliminada exitosamente.",
            category = deleted
        });
    }

    // GET /api/admin/products
    [HttpGet("products")]
    public IActionResult GetProducts([FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        return Ok(_db.GetAllProductsAdmin());
    }

    // POST /api/admin/products
    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct([FromBody] Product product, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        if (string.IsNullOrWhiteSpace(product.Nombre) || string.IsNullOrWhiteSpace(product.CategoriaId))
        {
            return BadRequest(new { error = "Nombre y categoría son obligatorios." });
        }

        try
        {
            var created = await _db.AddProductAsync(product);
            await _wsManager.BroadcastToKitchenAsync("MENU_UPDATED", new { action = "create", product = created });
            await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "create", product = created });

            return StatusCode(201, new
            {
                success = true,
                message = $"Platillo \"{created.Nombre}\" creado exitosamente.",
                product = created
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando platillo: {Msg}", ex.Message);
            return StatusCode(500, new { error = $"Error al registrar el platillo: {ex.Message}" });
        }
    }

    // PUT /api/admin/products/{id}
    [HttpPut("products/{id}")]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] Product product, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        try
        {
            var updated = await _db.UpdateProductAsync(id, product);
            if (updated == null)
            {
                return NotFound(new { error = "Producto no encontrado." });
            }

            await _wsManager.BroadcastToKitchenAsync("MENU_UPDATED", new { action = "update", product = updated });
            await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "update", product = updated });

            return Ok(new
            {
                success = true,
                message = $"Platillo \"{updated.Nombre}\" actualizado exitosamente.",
                product = updated
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando platillo: {Msg}", ex.Message);
            return StatusCode(500, new { error = $"Error al actualizar el platillo: {ex.Message}" });
        }
    }

    // DELETE /api/admin/products/{id}
    [HttpDelete("products/{id}")]
    public async Task<IActionResult> DeleteProduct(string id, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        try
        {
            var deleted = await _db.DeleteProductAsync(id);
            if (deleted == null)
            {
                return NotFound(new { error = "Producto no encontrado." });
            }

            await _wsManager.BroadcastToKitchenAsync("MENU_UPDATED", new { action = "delete", id });
            await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "delete", id });

            return Ok(new
            {
                success = true,
                message = "Platillo eliminado del menú exitosamente."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando platillo: {Msg}", ex.Message);
            return StatusCode(500, new { error = $"Error al eliminar el platillo: {ex.Message}" });
        }
    }

    // PATCH /api/admin/products/{id}/toggle
    [HttpPatch("products/{id}/toggle")]
    public async Task<IActionResult> ToggleProductAvailability(string id, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: false);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Ingrese el PIN de acceso." });

        var updated = await _db.ToggleProductAvailabilityAsync(id);
        if (updated == null)
        {
            return NotFound(new { error = "Producto no encontrado." });
        }

        await _wsManager.BroadcastToKitchenAsync("PRODUCT_AVAILABILITY_CHANGED", updated);
        await _wsManager.BroadcastToRoomAsync("public:catalog", "PRODUCT_AVAILABILITY_CHANGED", updated);
        await _wsManager.BroadcastToRoomAsync("public:catalog", "MENU_UPDATED", new { action = "toggle", product = updated });

        return Ok(new
        {
            success = true,
            message = $"Disponibilidad de \"{updated.Nombre}\" actualizada a {(updated.Disponible ? "Disponible" : "Agotado")}",
            product = updated
        });
    }

    // POST /api/admin/assets
    [HttpPost("assets")]
    public async Task<IActionResult> UploadAsset([FromBody] AssetUploadDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        if (string.IsNullOrWhiteSpace(body?.FileName) || string.IsNullOrWhiteSpace(body?.Base64))
        {
            return BadRequest(new { error = "Archivo incompleto." });
        }

        var url = await _db.UploadAssetAsync(body);
        return StatusCode(201, new { success = true, url });
    }

    // POST /api/admin/payments
    [HttpPost("payments")]
    public async Task<IActionResult> RegisterPayment([FromBody] RegisterPaymentDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: false);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Ingrese el PIN de acceso." });

        var method = (body?.Method ?? "").ToUpperInvariant();
        if (method != "EFECTIVO" && method != "TERMINAL_EXTERNA")
        {
            return BadRequest(new { error = "La PWA solo registra efectivo o pagos realizados en la terminal física del restaurante." });
        }

        try
        {
            var (payment, order) = await _db.RegisterPaymentAsync(body!);
            var result = new { payment, order };

            await _wsManager.BroadcastToKitchenAsync("PAYMENT_REGISTERED", result);
            await _wsManager.BroadcastToOrderAsync(order.OrderToken, "PAYMENT_REGISTERED", order);

            return StatusCode(201, new { success = true, payment, order });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // POST /api/admin/payments/collect-all-pending
    [HttpPost("payments/collect-all-pending")]
    public async Task<IActionResult> CollectAllPending([FromBody] CollectAllPendingDto? body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: false);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Ingrese el PIN de acceso." });

        var method = body?.Method ?? "EFECTIVO";
        var receivedBy = body?.ReceivedBy ?? "Caja";

        var orders = _db.GetAllOrders().Where(o => o.Estado == "ENTREGADO" && o.EstadoPago != "PAGADO").ToList();
        var results = new List<object>();

        foreach (var ord in orders)
        {
            var amount = ord.SaldoPendiente > 0 ? ord.SaldoPendiente : ord.Total;
            if (amount > 0)
            {
                var (payment, updatedOrder) = await _db.RegisterPaymentAsync(new RegisterPaymentDto
                {
                    OrderId = ord.Id,
                    Amount = amount,
                    Method = method,
                    ReceivedBy = receivedBy
                });

                var paymentResult = new { payment, order = updatedOrder };
                results.Add(paymentResult);

                await _wsManager.BroadcastToKitchenAsync("PAYMENT_REGISTERED", paymentResult);
                await _wsManager.BroadcastToOrderAsync(ord.OrderToken, "PAYMENT_REGISTERED", updatedOrder);
            }
        }

        return Ok(new { success = true, count = results.Count, payments = results });
    }

    // GET /api/admin/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats([FromQuery] string? period, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var stats = await _db.GetStatsAsync(period ?? "today");
        return Ok(stats);
    }

    // GET /api/admin/corte-caja/report
    [HttpGet("corte-caja/report")]
    public async Task<IActionResult> GetCorteReport([FromQuery] string? period, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var report = await _db.GetSalesReportAsync(period ?? "shift");
        return Ok(report);
    }

    // POST /api/admin/corte-caja
    [HttpPost("corte-caja")]
    public async Task<IActionResult> CreateCorte([FromBody] CreateCorteDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        var corte = await _db.CreateCorteAsync(body ?? new());
        return StatusCode(201, new
        {
            success = true,
            message = "Corte de caja registrado exitosamente.",
            corte
        });
    }

    // GET /api/admin/corte-caja/history
    [HttpGet("corte-caja/history")]
    public IActionResult GetCorteHistory([FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        return Ok(new { cortes = _db.GetCortes() });
    }

    // PUT /api/admin/tables
    [HttpPut("tables")]
    public async Task<IActionResult> UpdateTables([FromBody] UpdateTablesDto body, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = CheckAuth(token, requireAdmin: true);
        if (!auth.authorized) return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });

        if (body?.Mesas == null || body.Mesas.Count == 0)
        {
            return BadRequest(new { error = "Debe enviar un arreglo con la lista de mesas." });
        }

        try
        {
            var updated = await _db.UpdateTablesAsync(body.Mesas);
            await _wsManager.BroadcastAllAsync(new
            {
                type = "TABLES_UPDATED",
                mesas = updated
            });

            return Ok(new { ok = true, mesas = updated });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

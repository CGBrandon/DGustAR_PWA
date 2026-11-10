using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DGustAR.Api.Models;
using Microsoft.Data.SqlClient;

namespace DGustAR.Api.Data;

public class StoreRepository : IStoreRepository
{
    private readonly string _dbFilePath;
    private readonly string? _connectionString;
    private readonly string _webRootPath;
    private readonly ILogger<StoreRepository> _logger;
    private readonly object _lock = new();

    // true cuando SQL Server está disponible y con datos
    private bool _useSqlServer = false;
    private readonly Task _initSqlTask;

    private RestaurantInfo _restaurant = new();
    private List<string> _tables = new();
    private List<Category> _categories = new();
    private List<Product> _products = new();
    private List<Order> _orders = new();
    private List<Payment> _payments = new();
    private List<Review> _reviews = new();
    private List<CorteCaja> _cortes = new();
    private List<GoogleReviewClickLog> _googleReviewClicksLog = new();
    private string _adminPin = "1234";
    private string _staffPin = "1111";
    private string _adminToken = "bistro-admin-authorized";
    private string _staffToken = "bistro-staff-authorized";

    public StoreRepository(IConfiguration configuration, IWebHostEnvironment env, ILogger<StoreRepository> logger)
    {
        _logger = logger;
        _connectionString = configuration.GetConnectionString("DefaultConnection");
        _webRootPath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _adminPin = configuration["RestaurantSettings:DefaultAdminPin"]?.Trim() is { Length: >= 4 } configuredAdminPin
            ? configuredAdminPin
            : _adminPin;
        _staffPin = configuration["RestaurantSettings:DefaultStaffPin"]?.Trim() is { Length: >= 4 } configuredStaffPin
            ? configuredStaffPin
            : _staffPin;

        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _dbFilePath = Path.Combine(dataDir, "db.json");

        LoadData();
        // Verificar SQL Server; almacena la tarea para sincronización
        _initSqlTask = TryInitSqlServerAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SQL SERVER: inicialización y helpers
    // ─────────────────────────────────────────────────────────────────────────

    private async Task TryInitSqlServerAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString)) return;

        try
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = new SqlCommand(@"
                SELECT COUNT(*)
                FROM sys.tables
                WHERE name IN (N'Restaurantes', N'Categorias', N'Platillos', N'PlatilloExtras')", conn);
            var count = (int)(await cmd.ExecuteScalarAsync() ?? 0);

            if (count != 4)
            {
                _useSqlServer = false;
                _logger.LogWarning(
                    "[SQL Server] La BD no contiene el esquema compatible del repositorio. Usando db.json como fuente de datos.");
                return;
            }

            _useSqlServer = true;
            _logger.LogInformation(
                "[SQL Server] Modo SQL activo — {Count} platillos en la BD DGustAR_DB.", count);
        }
        catch (Exception ex)
        {
            _useSqlServer = false;
            _logger.LogWarning(
                "[SQL Server] No disponible ({Message}). Usando db.json como fuente de datos.",
                ex.Message);
        }
    }

    private bool SqlServerAvailable => _useSqlServer && !string.IsNullOrWhiteSpace(_connectionString);

    private async Task EnsureSqlServerCheckedAsync()
    {
        try
        {
            if (_initSqlTask != null && !_initSqlTask.IsCompleted)
            {
                await _initSqlTask;
            }
        }
        catch { }
    }

    private void EnsureSqlServerCheckedSync()
    {
        try
        {
            if (_initSqlTask != null && !_initSqlTask.IsCompleted)
            {
                _initSqlTask.GetAwaiter().GetResult();
            }
        }
        catch { }
    }

    /// <summary>Abre una conexión SQL y la devuelve ya abierta de forma asíncrona.</summary>
    private async Task<SqlConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Abre una conexión SQL y la devuelve ya abierta de forma síncrona.</summary>
    private SqlConnection OpenConnection()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static string? NormalizeModelUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var trimmed = url.Trim();
        if (trimmed.Contains("cdn.dgustar.com/models/"))
        {
            var fileName = Path.GetFileName(trimmed);
            return $"/models/{fileName}";
        }
        return trimmed;
    }

    private static string NormalizeEmoji(string? emoji, string? slug, string? id)
    {
        if (!string.IsNullOrWhiteSpace(emoji) && !emoji.Contains('?')) return emoji.Trim();
        var key = $"{slug} {id}".ToLowerInvariant();
        if (key.Contains("burger")) return "🍔";
        if (key.Contains("pizza")) return "🍕";
        if (key.Contains("snack") || key.Contains("papa")) return "🍟";
        if (key.Contains("postre") || key.Contains("cake")) return "🍰";
        if (key.Contains("bebida") || key.Contains("drink") || key.Contains("cocktail")) return "🥤";
        if (key.Contains("cafe") || key.Contains("coffee")) return "☕";
        return "🍽️";
    }

    private static Product ReadProductFromReader(SqlDataReader reader)
    {
        var alergenosRaw = reader.IsDBNull(10) ? "" : reader.GetString(10);
        var alergenos = alergenosRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => a.Length > 0)
            .ToList();

        var ingredientesRaw = (reader.IsDBNull(11) ? "" : reader.GetString(11)).Trim();
        List<string> ingredientes;
        if (ingredientesRaw.StartsWith("["))
        {
            try
            {
                ingredientes = JsonSerializer.Deserialize<List<string>>(ingredientesRaw) ?? new();
            }
            catch
            {
                ingredientes = ingredientesRaw
                    .Trim('[', ']')
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i.Trim('"', ' '))
                    .Where(i => i.Length > 0)
                    .ToList();
            }
        }
        else
        {
            ingredientes = ingredientesRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(i => i.Trim())
                .Where(i => i.Length > 0)
                .ToList();
        }

        return new Product
        {
            Id                = reader.GetString(0),
            CategoriaId       = reader.GetString(1),
            Nombre            = reader.GetString(2),
            Descripcion       = reader.IsDBNull(3) ? "" : reader.GetString(3),
            PrecioBase        = reader.GetDecimal(4),
            Imagen            = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Modelo3d          = NormalizeModelUrl(reader.IsDBNull(6) ? null : reader.GetString(6)),
            TiempoPreparacion = reader.GetInt32(7),
            Disponible        = reader.GetBoolean(8),
            Destacado         = reader.GetBoolean(9),
            Alergenos         = alergenos,
            IngredientesBase  = ingredientes,
            ExtrasDisponibles = new List<ProductExtra>()
        };
    }

    private void LoadData()
    {
        lock (_lock)
        {
            if (File.Exists(_dbFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_dbFilePath, Encoding.UTF8);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var jsonOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                    };

                    if (root.TryGetProperty("restaurant", out var restProp))
                    {
                        _restaurant = JsonSerializer.Deserialize<RestaurantInfo>(restProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("tables", out var tablesProp))
                    {
                        _tables = JsonSerializer.Deserialize<List<string>>(tablesProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("categories", out var catsProp))
                    {
                        _categories = JsonSerializer.Deserialize<List<Category>>(catsProp.GetRawText(), jsonOptions) ?? new();
                        foreach (var c in _categories)
                        {
                            c.Emoji = NormalizeEmoji(c.Emoji, c.Slug, c.Id);
                        }
                    }
                    if (root.TryGetProperty("products", out var prodsProp))
                    {
                        _products = JsonSerializer.Deserialize<List<Product>>(prodsProp.GetRawText(), jsonOptions) ?? new();
                        foreach (var p in _products)
                        {
                            p.Modelo3d = NormalizeModelUrl(p.Modelo3d);
                        }
                    }
                    if (root.TryGetProperty("orders", out var ordsProp))
                    {
                        _orders = JsonSerializer.Deserialize<List<Order>>(ordsProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("payments", out var payProp))
                    {
                        _payments = JsonSerializer.Deserialize<List<Payment>>(payProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("reviews", out var revProp))
                    {
                        _reviews = JsonSerializer.Deserialize<List<Review>>(revProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("cortes", out var cortProp))
                    {
                        _cortes = JsonSerializer.Deserialize<List<CorteCaja>>(cortProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("google_review_clicks_log", out var glogProp))
                    {
                        _googleReviewClicksLog = JsonSerializer.Deserialize<List<GoogleReviewClickLog>>(glogProp.GetRawText(), jsonOptions) ?? new();
                    }
                    if (root.TryGetProperty("admin_pin", out var adminPinProp) && adminPinProp.ValueKind == JsonValueKind.String)
                    {
                        _adminPin = adminPinProp.GetString() ?? _adminPin;
                    }
                    if (root.TryGetProperty("staff_pin", out var staffPinProp) && staffPinProp.ValueKind == JsonValueKind.String)
                    {
                        _staffPin = staffPinProp.GetString() ?? _staffPin;
                    }
                    if (root.TryGetProperty("admin_token", out var adminTokenProp) && adminTokenProp.ValueKind == JsonValueKind.String)
                    {
                        _adminToken = adminTokenProp.GetString() ?? _adminToken;
                    }
                    if (root.TryGetProperty("staff_token", out var staffTokenProp) && staffTokenProp.ValueKind == JsonValueKind.String)
                    {
                        _staffToken = staffTokenProp.GetString() ?? _staffToken;
                    }

                    _logger.LogInformation("[DB] Datos cargados correctamente: {CountProds} productos, {CountOrders} órdenes.", _products.Count, _orders.Count);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[DB] Error leyendo archivo de datos. Inicializando valores por defecto.");
                }
            }

            SeedDefaults();
            SaveData();
        }
    }

    private void SeedDefaults()
    {
        _restaurant = new RestaurantInfo();
        _tables = new List<string>
        {
            "Mesa 01", "Mesa 02", "Mesa 03", "Mesa 04",
            "Mesa 05", "Mesa 06", "Mesa 07", "Mesa 08",
            "Mesa 09", "Mesa 10", "Mesa 11", "Mesa 12",
            "Para Llevar"
        };

        _categories = new List<Category>
        {
            new() { Id = "cat-burgers", Nombre = "Hamburguesas Gourmet", Slug = "hamburguesas", Icono = "lunch_dining", Emoji = "🍔", Orden = 1, Activo = true },
            new() { Id = "cat-pizzas", Nombre = "Pizzas Artesanales", Slug = "pizzas", Icono = "local_pizza", Emoji = "🍕", Orden = 2, Activo = true },
            new() { Id = "cat-snacks", Nombre = "Snacks & Papas", Slug = "snacks", Icono = "fastfood", Emoji = "🍟", Orden = 3, Activo = true },
            new() { Id = "cat-postres", Nombre = "Postres de Autor", Slug = "postres", Icono = "cake", Emoji = "🍰", Orden = 4, Activo = true },
            new() { Id = "cat-bebidas", Nombre = "Bebidas & Mocktails", Slug = "bebidas", Icono = "local_bar", Emoji = "🥤", Orden = 5, Activo = true },
            new() { Id = "cat-cafe", Nombre = "Café de Especialidad", Slug = "cafe", Icono = "coffee", Emoji = "☕", Orden = 6, Activo = true }
        };

        _products = new List<Product>
        {
            new()
            {
                Id = "prod-burger-bbq",
                CategoriaId = "cat-burgers",
                Nombre = "Hamburguesa BBQ Trufa",
                Descripcion = "Carne Angus certificada de 180g, queso cheddar fundido, cebolla caramelizada al vino tinto y salsa especial de trufa sobre pan brioche artesanal.",
                PrecioBase = 149.00m,
                Imagen = "https://images.unsplash.com/photo-1568901346375-23c9450c58cd?auto=format&fit=crop&w=800&q=80",
                Modelo3d = "/models/burger.glb",
                Disponible = true,
                Destacado = true,
                TiempoPreparacion = 15,
                Alergenos = new() { "Gluten", "Lácteos" },
                IngredientesBase = new() { "Pan brioche artesanal", "Carne Angus 180g", "Queso cheddar fundido", "Cebolla caramelizada al vino tinto", "Salsa de trufa negra" },
                ExtrasDisponibles = new()
                {
                    new() { Id = "ext-tocino", Nombre = "Tocino Ahumado Crujiente", Precio = 25.00m },
                    new() { Id = "ext-doble-queso", Nombre = "Doble Queso Cheddar", Precio = 20.00m },
                    new() { Id = "ext-carne-extra", Nombre = "Carne Angus Extra (180g)", Precio = 55.00m }
                }
            },
            new()
            {
                Id = "prod-pizza-pepperoni",
                CategoriaId = "cat-pizzas",
                Nombre = "Pizza Pepperoni Suprema",
                Descripcion = "Masa madre fermentada 48 horas, salsa de tomate San Marzano, mozzarella fior di latte y pepperoni artesanal.",
                PrecioBase = 189.00m,
                Imagen = "https://images.unsplash.com/photo-1628840042765-356cda07504e?auto=format&fit=crop&w=800&q=80",
                Modelo3d = "/models/pizza.glb",
                Disponible = true,
                Destacado = true,
                TiempoPreparacion = 18,
                Alergenos = new() { "Gluten", "Lácteos" },
                IngredientesBase = new() { "Masa madre 48h", "Tomates San Marzano", "Queso mozzarella", "Pepperoni artesanal" },
                ExtrasDisponibles = new()
                {
                    new() { Id = "ext-extra-queso", Nombre = "Extra Queso Mozzarella", Precio = 30.00m },
                    new() { Id = "ext-orilla-queso", Nombre = "Orilla Rellena de Queso", Precio = 35.00m }
                }
            },
            new()
            {
                Id = "prod-papas-trufa",
                CategoriaId = "cat-snacks",
                Nombre = "Papas Rústicas Trufa & Parmesano",
                Descripcion = "Papas cortadas con piel, fritas a doble cocción, aceite de trufa blanca y queso parmesano Reggiano.",
                PrecioBase = 89.00m,
                Imagen = "https://images.unsplash.com/photo-1576107232684-1279f3908594?auto=format&fit=crop&w=800&q=80",
                Modelo3d = "/models/burger.glb",
                Disponible = true,
                Destacado = false,
                TiempoPreparacion = 10,
                Alergenos = new() { "Lácteos" },
                IngredientesBase = new() { "Papas rústicas", "Aceite de trufa", "Queso parmesano" },
                ExtrasDisponibles = new()
                {
                    new() { Id = "ext-dip-ajo", Nombre = "Dip de Ajo Negro y Romero", Precio = 18.00m }
                }
            }
        };
    }

    private void SaveData()
    {
        lock (_lock)
        {
            try
            {
                var payload = new
                {
                    restaurant = _restaurant,
                    tables = _tables,
                    categories = _categories,
                    products = _products,
                    orders = _orders,
                    payments = _payments,
                    reviews = _reviews,
                    cortes = _cortes,
                    google_review_clicks_log = _googleReviewClicksLog,
                    admin_pin = _adminPin,
                    staff_pin = _staffPin,
                    admin_token = _adminToken,
                    staff_token = _staffToken
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_dbFilePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DB] Error guardando datos en disco: {Path}", _dbFilePath);
            }
        }
    }

    public RestaurantInfo GetRestaurantInfo()
    {
        lock (_lock)
        {
            return _restaurant;
        }
    }

    public Task<RestaurantInfo> UpdateGoogleMapsSettingsAsync(GoogleMapsSettingsDto dto)
    {
        lock (_lock)
        {
            if (dto.GoogleMapsUrl != null) _restaurant.GoogleMapsUrl = dto.GoogleMapsUrl.Trim();
            if (dto.GoogleMapsReviewsCount.HasValue) _restaurant.GoogleMapsReviewsCount = dto.GoogleMapsReviewsCount.Value;
            if (dto.GoogleMapsRating.HasValue) _restaurant.GoogleMapsRating = dto.GoogleMapsRating.Value;

            // Si llega un Place ID, úsalo para auto-generar la URL de reseña
            // (a menos que el admin haya escrito una URL personalizada)
            if (dto.GooglePlaceId != null)
            {
                var placeId = dto.GooglePlaceId.Trim();
                _restaurant.GooglePlaceId = placeId;

                // Si la URL de reseña que manda el admin está vacía o genérica,
                // la construimos automáticamente desde el Place ID para que funcione
                // la app nativa de Maps sin pedir login
                bool reviewUrlIsGeneric = string.IsNullOrWhiteSpace(dto.GoogleMapsReviewUrl)
                    || dto.GoogleMapsReviewUrl.Contains("ChIJ_CONFIGURA_EN_ADMIN")
                    || dto.GoogleMapsReviewUrl.Contains("ChIJN1t_tDeuEmsRUsoyG83frY4");

                if (!string.IsNullOrEmpty(placeId) && reviewUrlIsGeneric)
                {
                    _restaurant.GoogleMapsReviewUrl =
                        $"https://search.google.com/local/writereview?placeid={placeId}";
                }
                else if (dto.GoogleMapsReviewUrl != null)
                {
                    _restaurant.GoogleMapsReviewUrl = dto.GoogleMapsReviewUrl.Trim();
                }

                // Si la URL del perfil tampoco está configurada, la generamos desde el Place ID
                bool profileUrlIsGeneric = string.IsNullOrWhiteSpace(dto.GoogleMapsUrl)
                    || dto.GoogleMapsUrl == "https://maps.google.com"
                    || dto.GoogleMapsUrl.Contains("CONFIGURA_EN_ADMIN");

                if (!string.IsNullOrEmpty(placeId) && profileUrlIsGeneric)
                {
                    _restaurant.GoogleMapsUrl =
                        $"https://www.google.com/maps/place/?q=place_id:{placeId}";
                }
                else if (dto.GoogleMapsUrl != null)
                {
                    _restaurant.GoogleMapsUrl = dto.GoogleMapsUrl.Trim();
                }
            }
            else
            {
                // No llegó Place ID: guardar tal como vino
                if (dto.GoogleMapsUrl != null) _restaurant.GoogleMapsUrl = dto.GoogleMapsUrl.Trim();
                if (dto.GoogleMapsReviewUrl != null) _restaurant.GoogleMapsReviewUrl = dto.GoogleMapsReviewUrl.Trim();
            }

            SaveData();
            return Task.FromResult(_restaurant);
        }
    }

    public Task<(int totalClicks, GoogleReviewClickLog entry)> RecordGoogleReviewClickAsync(GoogleReviewClickDto dto)
    {
        lock (_lock)
        {
            _restaurant.GoogleReviewsClicks++;
            var entry = new GoogleReviewClickLog
            {
                Id = $"gclick-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Mesa = dto.Mesa ?? "Desconocida",
                Comensal = dto.Comensal ?? "Comensal",
                RatingIntent = dto.RatingIntent ?? 5,
                OrderToken = dto.OrderToken ?? string.Empty
            };
            _googleReviewClicksLog.Insert(0, entry);
            if (_googleReviewClicksLog.Count > 100)
            {
                _googleReviewClicksLog = _googleReviewClicksLog.Take(100).ToList();
            }
            SaveData();
            return Task.FromResult((_restaurant.GoogleReviewsClicks, entry));
        }
    }

    public List<GoogleReviewClickLog> GetGoogleReviewClicksLog()
    {
        lock (_lock)
        {
            return _googleReviewClicksLog.ToList();
        }
    }

    public List<string> GetTables()
    {
        lock (_lock)
        {
            return _tables.ToList();
        }
    }

    public Task<List<string>> UpdateTablesAsync(List<string> tables)
    {
        if (tables == null || tables.Count == 0)
        {
            throw new ArgumentException("Debe proporcionar al menos una mesa válida.");
        }
        lock (_lock)
        {
            _tables = tables.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList();
            SaveData();
            return Task.FromResult(_tables.ToList());
        }
    }

    public async Task<object> GetMenuAsync()
    {
        await EnsureSqlServerCheckedAsync();

        // ── Modo SQL Server ───────────────────────────────────────────────────
        if (SqlServerAvailable)
        {
            try
            {
                return await GetMenuFromSqlAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[SQL Server] Error leyendo menú de SQL Server. Cayendo a db.json.");
            }
        }

        // ── Fallback: db.json ─────────────────────────────────────────────────
        lock (_lock)
        {
            var activeCategories = _categories.Where(c => c.Activo).OrderBy(c => c.Orden).ToList();
            var availableProducts = _products.Where(p => p.Disponible).ToList();

            return new
            {
                restaurant = _restaurant,
                categories = activeCategories,
                products = availableProducts,
                tables = _tables,
                restaurante = _restaurant,
                categorias = activeCategories,
                productos = availableProducts,
                mesas = _tables
            };
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura del menú directamente desde SQL Server (esquema real DGustAR_DB)
    // Tablas: Restaurantes, Categorias, Platillos, PlatilloExtras (PascalCase)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<object> GetMenuFromSqlAsync()
    {
        using var conn = await OpenConnectionAsync();

        // 1. Información del restaurante (tabla: Restaurantes)
        var restaurant = new RestaurantInfo();
        using (var cmd = new SqlCommand(
            @"SELECT TOP 1
                Nombre, Moneda, SimboloMoneda,
                ISNULL(Telefono,''), ISNULL(Direccion,''), Activo,
                ISNULL(GoogleMapsUrl,''), ISNULL(GoogleMapsReviewUrl,''),
                GoogleMapsRating, GoogleMapsReviewsCount,
                ISNULL(GooglePlaceId,''), GoogleReviewsClicks
              FROM dbo.Restaurantes", conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                restaurant.Nombre               = reader.GetString(0);
                restaurant.Moneda               = reader.GetString(1);
                restaurant.SimboloMoneda        = reader.GetString(2);
                restaurant.Telefono             = reader.GetString(3);
                restaurant.Direccion            = reader.GetString(4);
                restaurant.Activo               = reader.GetBoolean(5);
                restaurant.GoogleMapsUrl        = reader.GetString(6);
                restaurant.GoogleMapsReviewUrl  = reader.GetString(7);
                restaurant.GoogleMapsRating     = (double)reader.GetDecimal(8);
                restaurant.GoogleMapsReviewsCount = reader.GetInt32(9);
                restaurant.GooglePlaceId        = reader.GetString(10);
                restaurant.GoogleReviewsClicks  = reader.GetInt32(11);
            }
        }

        // 2. Mesas activas (la BD usa SesionesMesa para sesiones, no mesas fijas;
        //    usamos el listado guardado en db.json como fuente de mesas)
        List<string> tables;
        lock (_lock) { tables = _tables.ToList(); }

        // 3. Categorías activas
        var categories = new List<Category>();
        using (var cmd = new SqlCommand(
            @"SELECT Id, Nombre, Slug, Icono, Emoji, Orden, Activo
              FROM dbo.Categorias
              WHERE Activo = 1
              ORDER BY Orden", conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    Id     = reader.GetString(0),
                    Nombre = reader.GetString(1),
                    Slug   = reader.GetString(2),
                    Icono  = reader.GetString(3),
                    Emoji  = NormalizeEmoji(reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(2), reader.GetString(0)),
                    Orden  = reader.GetInt32(5),
                    Activo = reader.GetBoolean(6)
                });
            }
        }

        // 4. Productos disponibles con sus extras
        var products = new List<Product>();
        using (var cmd = new SqlCommand(
            @"SELECT
                Id,
                CategoriaId,
                Nombre,
                ISNULL(Descripcion,''),
                PrecioBase,
                ISNULL(ImagenUrl,''),
                Modelo3dUrl,
                TiempoPreparacionMin,
                Disponible,
                Destacado,
                ISNULL(Alergenos,''),
                ISNULL(IngredientesBaseJson,'')
              FROM dbo.Platillos
              WHERE Disponible = 1
              ORDER BY Nombre", conn))
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                products.Add(ReadProductFromReader(reader));
            }
        }

        // 5. Extras de todos los productos disponibles
        if (products.Count > 0)
        {
            var ids = string.Join(",",
                products.Select(p => $"'{p.Id.Replace("'", "''")}'"));

            using var cmd = new SqlCommand(
                $@"SELECT Id, PlatilloId, Nombre, PrecioExtra
                   FROM dbo.PlatilloExtras
                   WHERE PlatilloId IN ({ids})
                     AND Activo = 1", conn);
            using var reader = await cmd.ExecuteReaderAsync();

            var productMap = products.ToDictionary(p => p.Id);
            while (await reader.ReadAsync())
            {
                var idPlatillo = reader.GetString(1);
                if (productMap.TryGetValue(idPlatillo, out var prod))
                {
                    prod.ExtrasDisponibles.Add(new ProductExtra
                    {
                        Id     = reader.GetString(0),
                        Nombre = reader.GetString(2),
                        Precio = reader.GetDecimal(3)
                    });
                }
            }
        }

        _logger.LogInformation(
            "[SQL Server] Menú cargado: {Cat} categorías, {Prod} productos, {Mesas} mesas.",
            categories.Count, products.Count, tables.Count);

        return new
        {
            restaurant,
            tables,
            categories,
            products,
            // Dual keys para compatibilidad total con todas las versiones del frontend:
            restaurante = restaurant,
            mesas = tables,
            categorias = categories,
            productos = products
        };
    }

    public List<Category> GetCategories()
    {
        EnsureSqlServerCheckedSync();

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = OpenConnection();
                using var cmd = new SqlCommand(
                    "SELECT Id, Nombre, Slug, Icono, Emoji, Orden, Activo FROM dbo.Categorias WHERE Activo = 1 ORDER BY Orden",
                    conn);
                using var reader = cmd.ExecuteReader();
                var list = new List<Category>();
                while (reader.Read())
                {
                    list.Add(new Category
                    {
                        Id = reader.GetString(0),
                        Nombre = reader.GetString(1),
                        Slug = reader.GetString(2),
                        Icono = reader.GetString(3),
                        Emoji = NormalizeEmoji(reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(2), reader.GetString(0)),
                        Orden = reader.GetInt32(5),
                        Activo = reader.GetBoolean(6)
                    });
                }
                if (list.Count > 0) return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al obtener categorías");
            }
        }

        lock (_lock)
        {
            return _categories.OrderBy(c => c.Orden).ToList();
        }
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        await EnsureSqlServerCheckedAsync();

        if (string.IsNullOrWhiteSpace(category.Id))
        {
            category.Id = $"cat-{Guid.NewGuid():N}"[..12];
        }
        if (string.IsNullOrWhiteSpace(category.Slug))
        {
            category.Slug = category.Nombre.ToLowerInvariant().Replace(" ", "-");
        }
        category.Activo = true;

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmdCount = new SqlCommand("SELECT COUNT(*) FROM dbo.Categorias", conn);
                var count = (int)(await cmdCount.ExecuteScalarAsync() ?? 0);
                category.Orden = count + 1;

                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Categorias (Id, RestauranteId, Nombre, Slug, Icono, Emoji, Orden, Activo, CreatedAt, UpdatedAt)
                    VALUES (@Id, 'default', @Nombre, @Slug, @Icono, @Emoji, @Orden, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())", conn);
                cmd.Parameters.AddWithValue("@Id", category.Id);
                cmd.Parameters.AddWithValue("@Nombre", category.Nombre);
                cmd.Parameters.AddWithValue("@Slug", category.Slug);
                cmd.Parameters.AddWithValue("@Icono", string.IsNullOrWhiteSpace(category.Icono) ? "restaurant" : category.Icono);
                var finalEmoji = NormalizeEmoji(category.Emoji, category.Slug, category.Id);
                category.Emoji = finalEmoji;
                var pEmoji = cmd.Parameters.Add("@Emoji", System.Data.SqlDbType.NVarChar, 50);
                pEmoji.Value = finalEmoji;
                cmd.Parameters.AddWithValue("@Orden", category.Orden);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al crear categoría en BD");
                throw;
            }
        }

        lock (_lock)
        {
            if (!_categories.Any(c => c.Id == category.Id))
            {
                _categories.Add(category);
                SaveData();
            }
        }

        return category;
    }

    public async Task<Category?> DeleteCategoryAsync(string id)
    {
        await EnsureSqlServerCheckedAsync();
        Category? deleted = null;

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmd = new SqlCommand("DELETE FROM dbo.Categorias WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al eliminar categoría en BD");
            }
        }

        lock (_lock)
        {
            deleted = _categories.FirstOrDefault(c => c.Id == id);
            if (deleted != null)
            {
                _categories.Remove(deleted);
                SaveData();
            }
        }

        return deleted;
    }

    public List<Product> GetAllProductsAdmin()
    {
        EnsureSqlServerCheckedSync();

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = OpenConnection();
                var products = new List<Product>();
                using (var cmd = new SqlCommand(@"
                    SELECT
                        Id, CategoriaId, Nombre, ISNULL(Descripcion,''), PrecioBase,
                        ISNULL(ImagenUrl,''), Modelo3dUrl, TiempoPreparacionMin,
                        Disponible, Destacado, ISNULL(Alergenos,''), ISNULL(IngredientesBaseJson,'')
                    FROM dbo.Platillos
                    ORDER BY Nombre", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        products.Add(ReadProductFromReader(reader));
                    }
                }

                if (products.Count > 0)
                {
                    var ids = string.Join(",", products.Select(p => $"'{p.Id.Replace("'", "''")}'"));
                    using var cmdExtras = new SqlCommand($@"
                        SELECT Id, PlatilloId, Nombre, PrecioExtra
                        FROM dbo.PlatilloExtras
                        WHERE PlatilloId IN ({ids})", conn);
                    using var readerExtras = cmdExtras.ExecuteReader();
                    var map = products.ToDictionary(p => p.Id);
                    while (readerExtras.Read())
                    {
                        var platilloId = readerExtras.GetString(1);
                        if (map.TryGetValue(platilloId, out var prod))
                        {
                            prod.ExtrasDisponibles.Add(new ProductExtra
                            {
                                Id = readerExtras.GetString(0),
                                Nombre = readerExtras.GetString(2),
                                Precio = readerExtras.GetDecimal(3)
                            });
                        }
                    }
                }

                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al consultar platillos de administración");
            }
        }

        lock (_lock)
        {
            return _products.ToList();
        }
    }

    public Product? GetProductById(string id)
    {
        EnsureSqlServerCheckedSync();

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = OpenConnection();
                Product? prod = null;
                using (var cmd = new SqlCommand(@"
                    SELECT TOP 1
                        Id, CategoriaId, Nombre, ISNULL(Descripcion,''), PrecioBase,
                        ISNULL(ImagenUrl,''), Modelo3dUrl, TiempoPreparacionMin,
                        Disponible, Destacado, ISNULL(Alergenos,''), ISNULL(IngredientesBaseJson,'')
                    FROM dbo.Platillos
                    WHERE Id = @Id", conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        prod = ReadProductFromReader(reader);
                    }
                }

                if (prod != null)
                {
                    using var cmdExtras = new SqlCommand(@"
                        SELECT Id, PlatilloId, Nombre, PrecioExtra
                        FROM dbo.PlatilloExtras
                        WHERE PlatilloId = @Id", conn);
                    cmdExtras.Parameters.AddWithValue("@Id", id);
                    using var readerExtras = cmdExtras.ExecuteReader();
                    while (readerExtras.Read())
                    {
                        prod.ExtrasDisponibles.Add(new ProductExtra
                        {
                            Id = readerExtras.GetString(0),
                            Nombre = readerExtras.GetString(2),
                            Precio = readerExtras.GetDecimal(3)
                        });
                    }
                    return prod;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error consultando platillo {Id}", id);
            }
        }

        lock (_lock)
        {
            return _products.FirstOrDefault(p => p.Id == id);
        }
    }

    public async Task<Product> AddProductAsync(Product product)
    {
        await EnsureSqlServerCheckedAsync();

        product.Modelo3d = NormalizeModelUrl(product.Modelo3d);

        if (string.IsNullOrWhiteSpace(product.Id))
        {
            product.Id = $"prod-{Guid.NewGuid():N}"[..18];
        }

        if (string.IsNullOrWhiteSpace(product.CategoriaId))
        {
            product.CategoriaId = "cat-burgers";
        }

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Platillos (
                        Id, RestauranteId, CategoriaId, Nombre, Descripcion,
                        PrecioBase, ImagenUrl, Modelo3dUrl, Disponible, Destacado,
                        TiempoPreparacionMin, EscalaRealMetros, Alergenos, IngredientesBaseJson,
                        CreatedAt, UpdatedAt
                    ) VALUES (
                        @Id, 'default', @CategoriaId, @Nombre, @Descripcion,
                        @PrecioBase, @ImagenUrl, @Modelo3dUrl, @Disponible, @Destacado,
                        @TiempoPreparacionMin, 0.20, @Alergenos, @IngredientesBaseJson,
                        SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
                    )", conn);

                cmd.Parameters.AddWithValue("@Id", product.Id);
                cmd.Parameters.AddWithValue("@CategoriaId", product.CategoriaId);
                cmd.Parameters.AddWithValue("@Nombre", product.Nombre);
                cmd.Parameters.AddWithValue("@Descripcion", (object?)product.Descripcion ?? "");
                cmd.Parameters.AddWithValue("@PrecioBase", product.PrecioBase);
                cmd.Parameters.AddWithValue("@ImagenUrl", string.IsNullOrWhiteSpace(product.Imagen) ? "https://images.unsplash.com/photo-1546069901-ba9599a7e63c?auto=format&fit=crop&w=800&q=80" : product.Imagen);
                cmd.Parameters.AddWithValue("@Modelo3dUrl", string.IsNullOrWhiteSpace(product.Modelo3d) ? DBNull.Value : (object)product.Modelo3d);
                cmd.Parameters.AddWithValue("@Disponible", product.Disponible);
                cmd.Parameters.AddWithValue("@Destacado", product.Destacado);
                cmd.Parameters.AddWithValue("@TiempoPreparacionMin", product.TiempoPreparacion > 0 ? product.TiempoPreparacion : 15);

                var alergenosStr = product.Alergenos != null && product.Alergenos.Count > 0
                    ? string.Join(", ", product.Alergenos)
                    : (object)DBNull.Value;
                cmd.Parameters.AddWithValue("@Alergenos", alergenosStr);

                var ingredientesJson = product.IngredientesBase != null && product.IngredientesBase.Count > 0
                    ? JsonSerializer.Serialize(product.IngredientesBase)
                    : (object)DBNull.Value;
                cmd.Parameters.AddWithValue("@IngredientesBaseJson", ingredientesJson);

                await cmd.ExecuteNonQueryAsync();

                // Extras si vienen especificados
                if (product.ExtrasDisponibles != null && product.ExtrasDisponibles.Count > 0)
                {
                    foreach (var extra in product.ExtrasDisponibles)
                    {
                        var extraId = string.IsNullOrWhiteSpace(extra.Id) ? $"ext-{Guid.NewGuid():N}"[..12] : extra.Id;
                        using var cmdExtra = new SqlCommand(@"
                            INSERT INTO dbo.PlatilloExtras (Id, PlatilloId, Nombre, PrecioExtra, Activo)
                            VALUES (@Id, @PlatilloId, @Nombre, @PrecioExtra, 1)", conn);
                        cmdExtra.Parameters.AddWithValue("@Id", extraId);
                        cmdExtra.Parameters.AddWithValue("@PlatilloId", product.Id);
                        cmdExtra.Parameters.AddWithValue("@Nombre", extra.Nombre);
                        cmdExtra.Parameters.AddWithValue("@PrecioExtra", extra.Precio);
                        await cmdExtra.ExecuteNonQueryAsync();
                    }
                }

                _logger.LogInformation("[SQL Server] Platillo registrado exitosamente: {Id} - {Nombre}", product.Id, product.Nombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al insertar platillo en BD: {Msg}", ex.Message);
                throw;
            }
        }

        lock (_lock)
        {
            var idx = _products.FindIndex(p => p.Id == product.Id);
            if (idx >= 0) _products[idx] = product;
            else _products.Add(product);
            SaveData();
        }

        return product;
    }

    public async Task<Product?> UpdateProductAsync(string id, Product updated)
    {
        await EnsureSqlServerCheckedAsync();

        // Obtener el producto existente para completar cualquier campo no enviado
        var current = GetProductById(id);
        if (current == null)
        {
            lock (_lock)
            {
                current = _products.FirstOrDefault(p => p.Id == id);
            }
        }

        if (current != null)
        {
            if (string.IsNullOrWhiteSpace(updated.CategoriaId)) updated.CategoriaId = current.CategoriaId;
            if (string.IsNullOrWhiteSpace(updated.Nombre)) updated.Nombre = current.Nombre;
            if (updated.Descripcion == null) updated.Descripcion = current.Descripcion;
            if (updated.PrecioBase <= 0) updated.PrecioBase = current.PrecioBase;
            if (string.IsNullOrWhiteSpace(updated.Imagen)) updated.Imagen = current.Imagen;
            if (string.IsNullOrWhiteSpace(updated.Modelo3d)) updated.Modelo3d = current.Modelo3d;
            if (updated.TiempoPreparacion <= 0) updated.TiempoPreparacion = current.TiempoPreparacion;
            if (updated.Alergenos == null || updated.Alergenos.Count == 0) updated.Alergenos = current.Alergenos;
            if (updated.IngredientesBase == null || updated.IngredientesBase.Count == 0) updated.IngredientesBase = current.IngredientesBase;
            if (updated.ExtrasDisponibles == null || updated.ExtrasDisponibles.Count == 0) updated.ExtrasDisponibles = current.ExtrasDisponibles;
        }

        updated.Modelo3d = NormalizeModelUrl(updated.Modelo3d);

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.Platillos
                    SET CategoriaId = @CategoriaId,
                        Nombre = @Nombre,
                        Descripcion = @Descripcion,
                        PrecioBase = @PrecioBase,
                        ImagenUrl = @ImagenUrl,
                        Modelo3dUrl = @Modelo3dUrl,
                        Disponible = @Disponible,
                        Destacado = @Destacado,
                        TiempoPreparacionMin = @TiempoPreparacionMin,
                        Alergenos = @Alergenos,
                        IngredientesBaseJson = @IngredientesBaseJson,
                        UpdatedAt = SYSDATETIMEOFFSET()
                    WHERE Id = @Id", conn);

                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@CategoriaId", updated.CategoriaId);
                cmd.Parameters.AddWithValue("@Nombre", updated.Nombre);
                cmd.Parameters.AddWithValue("@Descripcion", (object?)updated.Descripcion ?? "");
                cmd.Parameters.AddWithValue("@PrecioBase", updated.PrecioBase);
                cmd.Parameters.AddWithValue("@ImagenUrl", string.IsNullOrWhiteSpace(updated.Imagen) ? "" : updated.Imagen);
                cmd.Parameters.AddWithValue("@Modelo3dUrl", string.IsNullOrWhiteSpace(updated.Modelo3d) ? DBNull.Value : (object)updated.Modelo3d);
                cmd.Parameters.AddWithValue("@Disponible", updated.Disponible);
                cmd.Parameters.AddWithValue("@Destacado", updated.Destacado);
                cmd.Parameters.AddWithValue("@TiempoPreparacionMin", updated.TiempoPreparacion > 0 ? updated.TiempoPreparacion : 15);

                var alergenosStr = updated.Alergenos != null && updated.Alergenos.Count > 0
                    ? string.Join(", ", updated.Alergenos)
                    : (object)DBNull.Value;
                cmd.Parameters.AddWithValue("@Alergenos", alergenosStr);

                var ingredientesJson = updated.IngredientesBase != null && updated.IngredientesBase.Count > 0
                    ? JsonSerializer.Serialize(updated.IngredientesBase)
                    : (object)DBNull.Value;
                cmd.Parameters.AddWithValue("@IngredientesBaseJson", ingredientesJson);

                var rows = await cmd.ExecuteNonQueryAsync();
                if (rows > 0)
                {
                    _logger.LogInformation("[SQL Server] Platillo {Id} actualizado con éxito", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al actualizar platillo {Id}", id);
                throw;
            }
        }

        lock (_lock)
        {
            var existing = _products.FirstOrDefault(p => p.Id == id);
            if (existing != null)
            {
                existing.Nombre = updated.Nombre;
                existing.Descripcion = updated.Descripcion;
                existing.PrecioBase = updated.PrecioBase;
                existing.CategoriaId = updated.CategoriaId;
                existing.Imagen = updated.Imagen;
                existing.Modelo3d = updated.Modelo3d;
                existing.Disponible = updated.Disponible;
                existing.Destacado = updated.Destacado;
                existing.TiempoPreparacion = updated.TiempoPreparacion;
                existing.Alergenos = updated.Alergenos ?? new();
                existing.IngredientesBase = updated.IngredientesBase ?? new();
                existing.ExtrasDisponibles = updated.ExtrasDisponibles ?? new();
                SaveData();
                return existing;
            }
        }

        return updated;
    }

    public async Task<Product?> UpdateProductPartialAsync(string id, JsonElement body)
    {
        await EnsureSqlServerCheckedAsync();

        var current = GetProductById(id);
        if (current == null)
        {
            lock (_lock)
            {
                current = _products.FirstOrDefault(p => p.Id == id);
            }
        }
        if (current == null) return null;

        var updated = new Product
        {
            Id = current.Id,
            CategoriaId = current.CategoriaId,
            Nombre = current.Nombre,
            Descripcion = current.Descripcion,
            PrecioBase = current.PrecioBase,
            Imagen = current.Imagen,
            Modelo3d = current.Modelo3d,
            TiempoPreparacion = current.TiempoPreparacion,
            Disponible = current.Disponible,
            Destacado = current.Destacado,
            Alergenos = new List<string>(current.Alergenos ?? new()),
            IngredientesBase = new List<string>(current.IngredientesBase ?? new()),
            ExtrasDisponibles = current.ExtrasDisponibles ?? new()
        };

        if (body.TryGetProperty("destacado", out var pDest)) updated.Destacado = pDest.GetBoolean();
        if (body.TryGetProperty("disponible", out var pDisp)) updated.Disponible = pDisp.GetBoolean();
        if (body.TryGetProperty("nombre", out var pNom) && !string.IsNullOrWhiteSpace(pNom.GetString())) updated.Nombre = pNom.GetString()!.Trim();
        if (body.TryGetProperty("categoria_id", out var pCat) && !string.IsNullOrWhiteSpace(pCat.GetString())) updated.CategoriaId = pCat.GetString()!.Trim();
        if (body.TryGetProperty("precio_base", out var pPre))
        {
            if (pPre.ValueKind == JsonValueKind.Number) updated.PrecioBase = pPre.GetDecimal();
            else if (decimal.TryParse(pPre.GetString(), out var parsedPre)) updated.PrecioBase = parsedPre;
        }
        if (body.TryGetProperty("descripcion", out var pDesc)) updated.Descripcion = pDesc.GetString() ?? "";
        if (body.TryGetProperty("imagen", out var pImg)) updated.Imagen = pImg.GetString() ?? "";
        if (body.TryGetProperty("modelo_3d", out var pMod)) updated.Modelo3d = NormalizeModelUrl(pMod.GetString());
        if (body.TryGetProperty("tiempo_preparacion_min", out var pT) || body.TryGetProperty("tiempo_preparacion", out pT))
        {
            if (pT.ValueKind == JsonValueKind.Number) updated.TiempoPreparacion = pT.GetInt32();
            else if (int.TryParse(pT.GetString(), out var parsedT)) updated.TiempoPreparacion = parsedT;
        }
        if (body.TryGetProperty("alergenos", out var pAle) && pAle.ValueKind == JsonValueKind.Array)
        {
            updated.Alergenos = JsonSerializer.Deserialize<List<string>>(pAle.GetRawText()) ?? new();
        }
        if (body.TryGetProperty("ingredientes_base", out var pIng) && pIng.ValueKind == JsonValueKind.Array)
        {
            updated.IngredientesBase = JsonSerializer.Deserialize<List<string>>(pIng.GetRawText()) ?? new();
        }

        return await UpdateProductAsync(id, updated);
    }

    public async Task<Product?> DeleteProductAsync(string id)
    {
        await EnsureSqlServerCheckedAsync();
        Product? deleted = null;

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmdExtras = new SqlCommand("DELETE FROM dbo.PlatilloExtras WHERE PlatilloId = @Id", conn);
                cmdExtras.Parameters.AddWithValue("@Id", id);
                await cmdExtras.ExecuteNonQueryAsync();

                using var cmd = new SqlCommand("DELETE FROM dbo.Platillos WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                await cmd.ExecuteNonQueryAsync();
                _logger.LogInformation("[SQL Server] Platillo {Id} eliminado de la base de datos", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al eliminar platillo {Id}", id);
                throw;
            }
        }

        lock (_lock)
        {
            deleted = _products.FirstOrDefault(x => x.Id == id);
            if (deleted != null)
            {
                _products.Remove(deleted);
                SaveData();
            }
        }

        return deleted;
    }

    public async Task<Product?> ToggleProductAvailabilityAsync(string id)
    {
        await EnsureSqlServerCheckedAsync();

        if (SqlServerAvailable)
        {
            try
            {
                using var conn = await OpenConnectionAsync();
                using var cmd = new SqlCommand(@"
                    UPDATE dbo.Platillos
                    SET Disponible = CASE WHEN Disponible = 1 THEN 0 ELSE 1 END,
                        UpdatedAt = SYSDATETIMEOFFSET()
                    WHERE Id = @Id;
                    SELECT Disponible FROM dbo.Platillos WHERE Id = @Id;", conn);
                cmd.Parameters.AddWithValue("@Id", id);
                var newDisp = await cmd.ExecuteScalarAsync();
                if (newDisp != null)
                {
                    var isDisp = (bool)newDisp;
                    lock (_lock)
                    {
                        var prod = _products.FirstOrDefault(p => p.Id == id);
                        if (prod != null)
                        {
                            prod.Disponible = isDisp;
                            SaveData();
                            return prod;
                        }
                    }
                    var pSql = GetProductById(id);
                    return pSql;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQL Server] Error al alternar disponibilidad de platillo {Id}", id);
            }
        }

        lock (_lock)
        {
            var p = _products.FirstOrDefault(x => x.Id == id);
            if (p != null)
            {
                p.Disponible = !p.Disponible;
                SaveData();
            }
            return p;
        }
    }

    public async Task<string> UploadAssetAsync(AssetUploadDto asset)
    {
        var ext = Path.GetExtension(asset.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".webp", ".gif", ".glb", ".gltf"
        };
        if (!allowedExtensions.Contains(ext))
        {
            throw new InvalidOperationException("Tipo de archivo no permitido.");
        }

        var encoded = asset.Base64.Trim();
        var comma = encoded.IndexOf(',');
        if (comma >= 0) encoded = encoded[(comma + 1)..];

        byte[] content;
        try
        {
            content = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("El contenido del archivo no es Base64 válido.");
        }

        if (content.Length == 0 || content.Length > 25 * 1024 * 1024)
        {
            throw new InvalidOperationException("El archivo debe tener contenido y no superar 25 MB.");
        }

        var safeName = $"asset-{Guid.NewGuid():N}{ext}";
        var uploadDirectory = Path.Combine(_webRootPath, "uploads");
        Directory.CreateDirectory(uploadDirectory);
        await File.WriteAllBytesAsync(Path.Combine(uploadDirectory, safeName), content);

        var relativeUrl = $"/uploads/{safeName}";
        return relativeUrl;
    }

    public List<Order> GetAllOrders(string? estado = null, string? mesa = null)
    {
        lock (_lock)
        {
            var query = _orders.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(estado))
            {
                query = query.Where(o => string.Equals(o.Estado, estado, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrWhiteSpace(mesa))
            {
                query = query.Where(o => string.Equals(o.Mesa, mesa, StringComparison.OrdinalIgnoreCase));
            }
            return query.OrderByDescending(o => o.CreadoEn).ToList();
        }
    }

    public Order? GetOrderById(string id)
    {
        lock (_lock)
        {
            return _orders.FirstOrDefault(o => o.Id == id);
        }
    }

    public Order? GetOrderByToken(string token)
    {
        lock (_lock)
        {
            return _orders.FirstOrDefault(o => string.Equals(o.OrderToken, token, StringComparison.OrdinalIgnoreCase));
        }
    }

    public Task<Order> CreateOrderAsync(Order order)
    {
        lock (_lock)
        {
            _orders.Add(order);
            SaveData();
            return Task.FromResult(order);
        }
    }

    public Task<Order?> UpdateOrderStatusAsync(string orderId, string newStatus)
    {
        lock (_lock)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return Task.FromResult<Order?>(null);

            var allowedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "NUEVO", "ACEPTADO", "EN_PREPARACION", "LISTO", "ENTREGADO", "CANCELADO"
            };
            var normalizedStatus = (newStatus ?? string.Empty).Trim().ToUpperInvariant();
            if (!allowedStatuses.Contains(normalizedStatus))
            {
                throw new InvalidOperationException("Estado de orden no válido.");
            }

            order.Estado = normalizedStatus;
            order.ActualizadoEn = DateTime.UtcNow.ToString("o");
            order.HistorialEstados.Add(new OrderStatusHistory
            {
                Estado = order.Estado,
                Timestamp = order.ActualizadoEn,
                CambiadoPor = "Personal / Cocina"
            });

            SaveData();
            return Task.FromResult<Order?>(order);
        }
    }

    public Task<Order?> MarkOrderClosedAsync(string orderId)
    {
        lock (_lock)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return Task.FromResult<Order?>(null);

            order.Cerrada = true;
            order.CerradaEn = DateTime.UtcNow.ToString("o");
            SaveData();
            return Task.FromResult<Order?>(order);
        }
    }

    public Task<(Payment payment, Order order)> RegisterPaymentAsync(RegisterPaymentDto dto)
    {
        lock (_lock)
        {
            var order = _orders.FirstOrDefault(o => o.Id == dto.OrderId);
            if (order == null)
            {
                throw new InvalidOperationException($"No se encontró la orden con id {dto.OrderId}");
            }

            if (dto.Amount <= 0)
            {
                throw new InvalidOperationException("El monto a pagar debe ser mayor a 0.");
            }

            if (order.Estado == "CANCELADO")
            {
                throw new InvalidOperationException("No se puede cobrar una orden cancelada.");
            }

            if (order.SaldoPendiente <= 0 || order.EstadoPago == "PAGADO")
            {
                throw new InvalidOperationException("La orden ya está pagada.");
            }

            if (dto.Amount > order.SaldoPendiente)
            {
                throw new InvalidOperationException("El monto no puede superar el saldo pendiente.");
            }

            var method = (dto.Method ?? string.Empty).Trim().ToUpperInvariant();
            if (method != "EFECTIVO" && method != "TERMINAL_EXTERNA")
            {
                throw new InvalidOperationException("Método de pago no válido.");
            }

            var paymentId = $"pay-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString("N")[..4]}";
            var payment = new Payment
            {
                Id = paymentId,
                OrderId = order.Id,
                OrderToken = order.OrderToken,
                Amount = dto.Amount,
                Method = method,
                ReceivedBy = dto.ReceivedBy ?? "Caja",
                Timestamp = DateTime.UtcNow.ToString("o"),
                Status = "COMPLETED"
            };

            _payments.Add(payment);

            order.MontoPagado += dto.Amount;
            order.SaldoPendiente = Math.Max(0, order.Total - order.MontoPagado);
            order.EstadoPago = order.SaldoPendiente <= 0 ? "PAGADO" : "PARCIAL";
            order.ActualizadoEn = DateTime.UtcNow.ToString("o");

            SaveData();
            return Task.FromResult((payment, order));
        }
    }

    public List<Review> GetReviews()
    {
        lock (_lock)
        {
            return _reviews.OrderByDescending(r => r.Fecha).ToList();
        }
    }

    public Task<Review> CreateReviewAsync(Review review)
    {
        lock (_lock)
        {
            _reviews.Insert(0, review);
            SaveData();
            return Task.FromResult(review);
        }
    }

    public Task<CorteCaja> CreateCorteAsync(CreateCorteDto dto)
    {
        lock (_lock)
        {
            var ahora = DateTime.UtcNow.ToString("o");
            DateTimeOffset cutoff;
            string inicio;

            if (_cortes.Count > 0 && DateTimeOffset.TryParse(_cortes.First().FinPeriodo, out var cdt))
            {
                cutoff = cdt;
                inicio = _cortes.First().FinPeriodo;
            }
            else
            {
                cutoff = DateTimeOffset.MinValue;
                inicio = DateTime.UtcNow.AddHours(-12).ToString("o");
            }

            var paymentsShift = _payments.Where(p =>
            {
                if (DateTimeOffset.TryParse(p.Timestamp, out var dtoTs)) return dtoTs >= cutoff;
                return true;
            }).ToList();

            var totalEfectivo = paymentsShift.Where(p => p.Method == "EFECTIVO").Sum(p => p.Amount);
            var totalTerminal = paymentsShift.Where(p => p.Method == "TERMINAL_EXTERNA").Sum(p => p.Amount);
            var totalVentas = paymentsShift.Sum(p => p.Amount);

            var fondo = dto.FondoInicial ?? 0;
            var contado = dto.EfectivoContado ?? 0;
            var diferencia = contado - (fondo + totalEfectivo);

            var corte = new CorteCaja
            {
                Id = $"corte-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}",
                Responsable = dto.Responsable ?? "Administrador",
                InicioPeriodo = inicio,
                FinPeriodo = ahora,
                FondoInicial = fondo,
                EfectivoContado = contado,
                TotalEfectivo = totalEfectivo,
                TotalTerminal = totalTerminal,
                TotalTransferencia = 0,
                TotalVentas = totalVentas,
                DiferenciaEfectivo = diferencia,
                CantidadPedidos = paymentsShift.Select(p => p.OrderId).Distinct().Count(),
                Notas = dto.Notas ?? string.Empty,
                Timestamp = ahora
            };

            _cortes.Insert(0, corte);
            SaveData();
            return Task.FromResult(corte);
        }
    }

    public List<CorteCaja> GetCortes()
    {
        lock (_lock)
        {
            return _cortes.ToList();
        }
    }

    public Task<object> GetStatsAsync(string period)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            DateTimeOffset cutoff = period switch
            {
                "today" => new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero),
                "week" => now.AddDays(-7),
                "month" => now.AddDays(-30),
                _ => DateTimeOffset.MinValue
            };

            var ordersInPeriod = _orders.Where(o =>
            {
                if (DateTimeOffset.TryParse(o.CreadoEn, out var dto)) return dto >= cutoff;
                return true;
            }).ToList();

            var totalVentas = ordersInPeriod.Where(o => o.Estado != "CANCELADO").Sum(o => o.Total);
            var totalPedidos = ordersInPeriod.Count;
            var pedidosEntregados = ordersInPeriod.Count(o => o.Estado == "ENTREGADO");

            var stats = new
            {
                periodo = period,
                total_ventas = totalVentas,
                total_pedidos = totalPedidos,
                pedidos_entregados = pedidosEntregados,
                ticket_promedio = totalPedidos > 0 ? Math.Round(totalVentas / totalPedidos, 2) : 0,
                reviews_promedio = _reviews.Count > 0 ? Math.Round((double)_reviews.Average(r => r.Rating), 1) : 5.0,
                total_reviews = _reviews.Count
            };

            return Task.FromResult<object>(stats);
        }
    }

    public Task<object> GetSalesReportAsync(string period)
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            var periodClean = (period ?? "shift").ToLowerInvariant().Trim();

            DateTimeOffset cutoff;
            string periodName;

            switch (periodClean)
            {
                case "all":
                case "historico":
                case "total":
                    cutoff = DateTimeOffset.MinValue;
                    periodName = "Histórico Total";
                    break;
                case "today":
                    cutoff = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
                    periodName = "Hoy";
                    break;
                case "week":
                    cutoff = now.AddDays(-7);
                    periodName = "Últimos 7 días";
                    break;
                case "month":
                    cutoff = now.AddDays(-30);
                    periodName = "Últimos 30 días";
                    break;
                case "shift":
                default:
                    if (_cortes.Count > 0 && DateTimeOffset.TryParse(_cortes.First().FinPeriodo, out var cdt))
                    {
                        cutoff = cdt;
                    }
                    else
                    {
                        cutoff = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
                        if (!_payments.Any(p => DateTimeOffset.TryParse(p.Timestamp, out var dto) && dto >= cutoff))
                        {
                            cutoff = DateTimeOffset.MinValue;
                        }
                    }
                    periodName = "Turno Actual";
                    break;
            }

            var paymentsInPeriod = _payments.Where(p =>
            {
                if (DateTimeOffset.TryParse(p.Timestamp, out var dto)) return dto >= cutoff;
                return true;
            }).ToList();

            var paymentOrderIds = new HashSet<string>(paymentsInPeriod.Select(p => p.OrderId));

            var ordersInPeriod = _orders.Where(o =>
            {
                if (paymentOrderIds.Contains(o.Id)) return true;
                if (DateTimeOffset.TryParse(o.CreadoEn, out var dto)) return dto >= cutoff;
                return false;
            }).OrderByDescending(o => o.CreadoEn).ToList();

            var totalEfectivo = paymentsInPeriod.Where(p => p.Method == "EFECTIVO").Sum(p => p.Amount);
            var totalTerminal = paymentsInPeriod.Where(p => p.Method == "TERMINAL_EXTERNA").Sum(p => p.Amount);
            var totalVentas = paymentsInPeriod.Sum(p => p.Amount);

            var pendientes = ordersInPeriod.Where(o => o.Estado != "CANCELADO" && o.EstadoPago != "PAGADO").ToList();
            var ventasPendientes = pendientes.Sum(o => o.SaldoPendiente > 0 ? o.SaldoPendiente : o.Total);
            var ordenesPendientes = pendientes.Count;

            var ordenesPagadas = ordersInPeriod.Count(o => o.EstadoPago == "PAGADO");
            var ordenesEntregadas = ordersInPeriod.Count(o => o.Estado == "ENTREGADO");

            var ticketPromedio = ordenesPagadas > 0
                ? Math.Round(totalVentas / ordenesPagadas, 2)
                : (ordersInPeriod.Count > 0 ? Math.Round(totalVentas / ordersInPeriod.Count, 2) : 0m);

            var topProductos = ordersInPeriod
                .Where(o => o.Estado != "CANCELADO")
                .SelectMany(o => o.Items)
                .GroupBy(i => i.ProductoId)
                .Select(g => new
                {
                    producto_id = g.Key,
                    nombre = g.First().Nombre,
                    cantidad = g.Sum(x => x.Cantidad),
                    subtotal = g.Sum(x => x.PrecioUnitario * x.Cantidad)
                })
                .OrderByDescending(x => x.cantidad)
                .Take(5)
                .ToList();

            var report = new
            {
                ventas_totales = totalVentas,
                ventas_efectivo = totalEfectivo,
                ventas_tarjeta = totalTerminal,
                total_ordenes = ordersInPeriod.Count,
                ordenes_entregadas = ordenesEntregadas,
                ordenes_pagadas = ordenesPagadas,
                ticket_promedio = ticketPromedio,
                ventas_pendientes_cobro = ventasPendientes,
                ordenes_pendientes_cobro = ordenesPendientes,
                periodName = periodName,
                periodo = periodClean,
                inicio_periodo = cutoff == DateTimeOffset.MinValue
                    ? (_orders.LastOrDefault()?.CreadoEn ?? _payments.LastOrDefault()?.Timestamp ?? now.AddMonths(-1).ToString("o"))
                    : cutoff.ToString("o"),
                fin_periodo = now.ToString("o"),
                ultimo_corte_timestamp = _cortes.FirstOrDefault()?.Timestamp,
                top_productos = topProductos,
                orders = ordersInPeriod,

                total_ventas = totalVentas,
                total_efectivo = totalEfectivo,
                total_terminal = totalTerminal,
                total_transferencia = 0m,
                cantidad_pagos = paymentsInPeriod.Count,
                cantidad_ordenes = ordersInPeriod.Count,
                pagos = paymentsInPeriod.Take(50).ToList()
            };

            return Task.FromResult<object>(report);
        }
    }

    public (bool valid, string role, string token) VerifyPin(string pin)
    {
        var cleanPin = (pin ?? "").Trim();
        if (cleanPin == _adminPin)
        {
            return (true, "ADMIN", _adminToken);
        }
        if (cleanPin == _staffPin)
        {
            return (true, "STAFF", _staffToken);
        }
        return (false, string.Empty, string.Empty);
    }

    public (bool valid, string role) VerifyToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return (false, string.Empty);
        var t = token.Trim();
        if (t == _adminToken)
        {
            return (true, "ADMIN");
        }
        if (t == _staffToken)
        {
            return (true, "STAFF");
        }
        return (false, string.Empty);
    }

    public Task<string> UpdateAdminPinAsync(string currentPin, string newPin)
    {
        if (currentPin != _adminPin)
        {
            throw new InvalidOperationException("El PIN actual de Gerencia es incorrecto.");
        }
        if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4)
        {
            throw new InvalidOperationException("El nuevo PIN debe tener al menos 4 dígitos.");
        }
        _adminPin = newPin.Trim();
        _adminToken = $"bistro-admin-{Guid.NewGuid():N}";
        SaveData();
        return Task.FromResult(_adminToken);
    }

    public Task<bool> UpdateStaffPinAsync(string adminPin, string newStaffPin)
    {
        if (adminPin != _adminPin)
        {
            throw new InvalidOperationException("El PIN de Gerencia es incorrecto para autorizar esta operación.");
        }
        if (string.IsNullOrWhiteSpace(newStaffPin) || newStaffPin.Length < 4)
        {
            throw new InvalidOperationException("El nuevo PIN de Personal debe tener al menos 4 dígitos.");
        }
        _staffPin = newStaffPin.Trim();
        _staffToken = $"bistro-staff-{Guid.NewGuid():N}";
        SaveData();
        return Task.FromResult(true);
    }
}

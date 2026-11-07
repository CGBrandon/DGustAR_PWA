using DGustAR.Api.Data;
using DGustAR.Api.Models;

namespace DGustAR.Api.Services;

public class OrderService
{
    private readonly IStoreRepository _db;
    private readonly AppWebSocketManager _wsManager;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IStoreRepository db, AppWebSocketManager wsManager, ILogger<OrderService> logger)
    {
        _db = db;
        _wsManager = wsManager;
        _logger = logger;
    }

    public async Task<Order> CreateOrderAsync(CreateOrderDto payload)
    {
        if (payload == null)
        {
            throw new ArgumentException("El cuerpo de la comanda es inválido.");
        }

        // 1. Validar Mesa
        var mesa = (payload.Mesa ?? "").Trim();
        if (string.IsNullOrWhiteSpace(mesa))
        {
            throw new ArgumentException("Debe especificar una mesa válida o indicar 'Para Llevar'");
        }

        var validTables = _db.GetTables();
        if (!validTables.Contains(mesa, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"La mesa '{mesa}' no se encuentra registrada en el restaurante.");
        }

        // 2. Validar Items
        if (payload.Items == null || payload.Items.Count == 0)
        {
            throw new ArgumentException("La comanda debe contener al menos un producto.");
        }

        if (payload.Items.Count > 50)
        {
            throw new ArgumentException("No se pueden ordenar más de 50 platillos por pedido.");
        }

        // 3. Recálculo forzoso de precios por ítem desde el servidor
        var itemsCalculados = new List<OrderItem>();
        decimal totalAcumulado = 0;

        foreach (var rawItem in payload.Items)
        {
            if (string.IsNullOrWhiteSpace(rawItem.ProductoId))
            {
                throw new ArgumentException("Cada ítem debe contener un 'producto_id' válido.");
            }

            var product = _db.GetProductById(rawItem.ProductoId);
            if (product == null)
            {
                throw new ArgumentException($"El producto con ID '{rawItem.ProductoId}' no existe.");
            }

            if (!product.Disponible)
            {
                throw new ArgumentException($"El producto '{product.Nombre}' se encuentra temporalmente agotado.");
            }

            if (rawItem.Cantidad < 1 || rawItem.Cantidad > 20)
            {
                throw new ArgumentException($"Cantidad no válida para '{product.Nombre}'. Debe ser entre 1 y 20.");
            }

            // Validar ingredientes removidos
            var ingredientesRemovidos = (rawItem.IngredientesRemovidos ?? new())
                .Select(i => (i ?? "").Trim())
                .Where(i => product.IngredientesBase.Contains(i, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Validar extras seleccionados con precio en catálogo oficial
            var extrasResueltos = new List<ProductExtra>();
            decimal extrasPrecioTotal = 0;

            if (rawItem.ExtrasSeleccionados != null)
            {
                foreach (var extraId in rawItem.ExtrasSeleccionados)
                {
                    var extraDef = product.ExtrasDisponibles.FirstOrDefault(e => e.Id == extraId);
                    if (extraDef == null)
                    {
                        throw new ArgumentException($"El extra '{extraId}' no es válido para '{product.Nombre}'.");
                    }
                    extrasResueltos.Add(new ProductExtra
                    {
                        Id = extraDef.Id,
                        Nombre = extraDef.Nombre,
                        Precio = extraDef.Precio
                    });
                    extrasPrecioTotal += extraDef.Precio;
                }
            }

            var precioBase = product.PrecioBase;
            var precioUnitario = Math.Round(precioBase + extrasPrecioTotal, 2);
            var subtotalItem = Math.Round(precioUnitario * rawItem.Cantidad, 2);

            totalAcumulado += subtotalItem;

            itemsCalculados.Add(new OrderItem
            {
                ProductoId = product.Id,
                Nombre = product.Nombre,
                Imagen = product.Imagen ?? string.Empty,
                PrecioBase = precioBase,
                Cantidad = rawItem.Cantidad,
                IngredientesRemovidos = ingredientesRemovidos,
                Extras = extrasResueltos,
                PrecioUnitario = precioUnitario,
                Subtotal = subtotalItem
            });
        }

        var totalFinal = Math.Round(totalAcumulado, 2);
        var ahora = DateTime.UtcNow.ToString("o");
        var id = $"ord-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString("N")[..4]}";
        var orderToken = Guid.NewGuid().ToString();

        var tipoCuenta = string.Equals(payload.TipoCuenta, "SEPARADA", StringComparison.OrdinalIgnoreCase)
            ? "SEPARADA"
            : "MESA";

        var comensal = !string.IsNullOrWhiteSpace(payload.Comensal)
            ? payload.Comensal.Trim()
            : (tipoCuenta == "MESA" ? "Cuenta General de la Mesa" : "Comensal Individual");

        var nuevaOrden = new Order
        {
            Id = id,
            OrderToken = orderToken,
            Mesa = mesa,
            TipoCuenta = tipoCuenta,
            Comensal = comensal,
            TipoServicio = string.Equals(payload.TipoServicio, "Para Llevar", StringComparison.OrdinalIgnoreCase) ? "Para Llevar" : "En Mesa",
            MetodoPago = payload.MetodoPago ?? "Pago al mesero en mesa",
            Estado = "NUEVO",
            EstadoPago = "PENDIENTE",
            MontoPagado = 0,
            SaldoPendiente = totalFinal,
            Items = itemsCalculados,
            Subtotal = totalFinal,
            Total = totalFinal,
            NotasCliente = payload.NotasCliente?.Trim() ?? string.Empty,
            CreadoEn = ahora,
            ActualizadoEn = ahora,
            HistorialEstados = new List<OrderStatusHistory>
            {
                new() { Estado = "NUEVO", Timestamp = ahora, CambiadoPor = "Comensal PWA" }
            }
        };

        var created = await _db.CreateOrderAsync(nuevaOrden);

        // Notificación en tiempo real al KDS de la cocina
        await _wsManager.BroadcastToKitchenAsync("NEW_ORDER", created);

        _logger.LogInformation("[OrderService] Orden {OrderId} ({OrderToken}) creada exitosamente con total ${Total}", created.Id, created.OrderToken, created.Total);

        return created;
    }

    public Task<object?> GetTableAccountSummaryAsync(string mesa)
    {
        if (string.IsNullOrWhiteSpace(mesa)) return Task.FromResult<object?>(null);

        var cleanMesa = mesa.Trim();
        var orders = _db.GetAllOrders(mesa: cleanMesa);

        var cutoffTime = DateTimeOffset.UtcNow.AddHours(-4);
        var activeOrders = orders.Where(o =>
        {
            if (o.Estado == "CANCELADO" || o.Cerrada) return false;
            if (DateTimeOffset.TryParse(o.CreadoEn, out var dto))
            {
                return dto >= cutoffTime;
            }
            return true;
        }).ToList();

        var totalMesa = activeOrders.Sum(o => o.Total);

        var cuentasMap = new Dictionary<string, dynamic>();
        foreach (var ord in activeOrders)
        {
            var cKey = !string.IsNullOrWhiteSpace(ord.Comensal)
                ? ord.Comensal
                : (ord.TipoCuenta == "MESA" ? "Cuenta General de la Mesa" : "Comensal");

            if (!cuentasMap.TryGetValue(cKey, out var cInfo))
            {
                cInfo = new
                {
                    comensal = cKey,
                    tipo_cuenta = ord.TipoCuenta,
                    total = 0m,
                    ordenes_count = 0,
                    tokens = new List<string>()
                };
            }

            var currentTokens = (List<string>)cInfo.tokens;
            currentTokens.Add(ord.OrderToken);

            cuentasMap[cKey] = new
            {
                comensal = cKey,
                tipo_cuenta = ord.TipoCuenta,
                total = Math.Round((decimal)cInfo.total + ord.Total, 2),
                ordenes_count = (int)cInfo.ordenes_count + 1,
                tokens = currentTokens
            };
        }

        var hayCuentasSeparadas = activeOrders.Any(o => o.TipoCuenta == "SEPARADA");

        var summary = new
        {
            mesa = cleanMesa,
            total_mesa = Math.Round(totalMesa, 2),
            cantidad_ordenes = activeOrders.Count,
            hay_cuentas_separadas = hayCuentasSeparadas,
            cuentas = cuentasMap.Values.ToList(),
            ordenes = activeOrders.Select(o => new
            {
                id = o.Id,
                order_token = o.OrderToken,
                comensal = o.Comensal,
                tipo_cuenta = o.TipoCuenta,
                estado = o.Estado,
                total = o.Total,
                items_count = o.Items.Sum(it => it.Cantidad),
                creado_en = o.CreadoEn
            }).ToList()
        };

        return Task.FromResult<object?>(summary);
    }
}

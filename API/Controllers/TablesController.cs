using DGustAR.Api.Data;
using DGustAR.Api.Models;
using DGustAR.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGustAR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TablesController : ControllerBase
{
    private readonly IStoreRepository _db;
    private readonly OrderService _orderService;
    private readonly AppWebSocketManager _wsManager;

    public TablesController(IStoreRepository db, OrderService orderService, AppWebSocketManager wsManager)
    {
        _db = db;
        _orderService = orderService;
        _wsManager = wsManager;
    }

    [HttpGet]
    public IActionResult GetTables()
    {
        var mesas = _db.GetTables();
        return Ok(new { ok = true, mesas });
    }

    [HttpGet("{mesa}/account")]
    public async Task<IActionResult> GetAccountSummary(string mesa)
    {
        var summary = await _orderService.GetTableAccountSummaryAsync(mesa);
        if (summary == null)
        {
            return NotFound(new { error = "Mesa no encontrada." });
        }
        return Ok(summary);
    }

    [HttpPost("{mesa}/call-waiter")]
    public async Task<IActionResult> CallWaiter(string mesa, [FromBody] CallWaiterDto? body)
    {
        var cleanMesa = !string.IsNullOrWhiteSpace(mesa) ? mesa.Trim() : "Mesa";
        var comensal = !string.IsNullOrWhiteSpace(body?.Comensal) ? body.Comensal.Trim() : "Comensal";
        var motivo = !string.IsNullOrWhiteSpace(body?.Motivo) ? body.Motivo.Trim() : "Asistencia en mesa";

        var alertData = new
        {
            id = $"call-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            mesa = cleanMesa,
            comensal,
            motivo,
            timestamp = DateTime.UtcNow.ToString("o")
        };

        await _wsManager.BroadcastToKitchenAsync("WAITER_CALLED", alertData);

        return Ok(new
        {
            success = true,
            message = $"Mesero notificado para acudir a {cleanMesa}",
            data = alertData
        });
    }

    [HttpPost("{mesa}/request-bill")]
    public async Task<IActionResult> RequestBill(string mesa, [FromBody] RequestBillDto? body)
    {
        var cleanMesa = !string.IsNullOrWhiteSpace(mesa) ? mesa.Trim() : "Mesa";
        var billData = new
        {
            id = $"bill-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            mesa = cleanMesa,
            tipo = body?.Tipo ?? "MESA_COMPLETA",
            comensal = body?.Comensal ?? "Toda la Mesa",
            total = body?.Total ?? 0,
            metodo_pago = body?.MetodoPago ?? "Pago al mesero en mesa",
            split_count = Math.Max(1, body?.SplitCount ?? 1),
            timestamp = DateTime.UtcNow.ToString("o")
        };

        await _wsManager.BroadcastToKitchenAsync("BILL_REQUESTED", billData);

        return Ok(new
        {
            success = true,
            message = $"Cuenta solicitada para {cleanMesa}",
            data = billData
        });
    }

    [HttpPost("{mesa}/close")]
    public async Task<IActionResult> CloseTable(string mesa, [FromHeader(Name = "x-admin-token")] string? token)
    {
        var auth = _db.VerifyToken(token ?? "");
        if (!auth.valid || auth.role != "ADMIN")
        {
            return StatusCode(401, new { error = "No autorizado. Se requiere inicio de sesión de Gerencia." });
        }

        var cleanMesa = (mesa ?? "").Trim();
        var orders = _db.GetAllOrders(mesa: cleanMesa).Where(o => o.Estado != "CANCELADO").ToList();
        var unpaid = orders.Where(o => o.EstadoPago != "PAGADO").ToList();

        if (unpaid.Count > 0)
        {
            return Conflict(new
            {
                error = "No se puede cerrar la mesa: existen órdenes pendientes de pago.",
                code = "UNPAID_ORDERS",
                orders = unpaid.Select(o => new { id = o.Id, total = o.Total, saldo_pendiente = o.SaldoPendiente })
            });
        }

        foreach (var ord in orders)
        {
            await _db.MarkOrderClosedAsync(ord.Id);
            if (ord.Estado != "ENTREGADO")
            {
                var updated = await _db.UpdateOrderStatusAsync(ord.Id, "ENTREGADO");
                if (updated != null)
                {
                    await _wsManager.BroadcastToOrderAsync(ord.OrderToken, "ORDER_STATUS_CHANGED", updated);
                }
            }
        }

        await _wsManager.BroadcastToKitchenAsync("TABLE_RELEASED", new
        {
            mesa = cleanMesa,
            timestamp = DateTime.UtcNow.ToString("o")
        });

        return Ok(new
        {
            success = true,
            message = $"Mesa {cleanMesa} cerrada y liberada exitosamente."
        });
    }
}

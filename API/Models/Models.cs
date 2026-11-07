using System.Text.Json.Serialization;

namespace DGustAR.Api.Models;

public class RestaurantInfo
{
    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "DGustAR Gastronomía & AR";

    [JsonPropertyName("moneda")]
    public string Moneda { get; set; } = "MXN";

    [JsonPropertyName("simbolo_moneda")]
    public string SimboloMoneda { get; set; } = "$";

    [JsonPropertyName("telefono")]
    public string Telefono { get; set; } = "+52 55 1234 5678";

    [JsonPropertyName("direccion")]
    public string Direccion { get; set; } = "Av. Tecnológica 100, Col. Innovación Gastronómica";

    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;

    [JsonPropertyName("google_maps_url")]
    public string GoogleMapsUrl { get; set; } = "https://maps.google.com";

    [JsonPropertyName("google_maps_review_url")]
    public string GoogleMapsReviewUrl { get; set; } = "https://g.page/r/sample/review";

    [JsonPropertyName("google_maps_rating")]
    public double GoogleMapsRating { get; set; } = 4.9;

    [JsonPropertyName("google_maps_reviews_count")]
    public int GoogleMapsReviewsCount { get; set; } = 128;

    [JsonPropertyName("google_place_id")]
    public string GooglePlaceId { get; set; } = "ChIJ_sample_place_id";

    [JsonPropertyName("google_reviews_clicks")]
    public int GoogleReviewsClicks { get; set; } = 0;
}

public class Category
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("icono")]
    public string Icono { get; set; } = "restaurant";

    [JsonPropertyName("emoji")]
    public string Emoji { get; set; } = "🍽️";

    [JsonPropertyName("orden")]
    public int Orden { get; set; } = 0;

    [JsonPropertyName("activo")]
    public bool Activo { get; set; } = true;
}

public class ProductExtra
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("precio")]
    public decimal Precio { get; set; }
}

public class Product
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("categoria_id")]
    public string CategoriaId { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("precio_base")]
    public decimal PrecioBase { get; set; }

    [JsonPropertyName("imagen")]
    public string? Imagen { get; set; } = string.Empty;

    [JsonPropertyName("modelo_3d")]
    public string? Modelo3d { get; set; }

    [JsonPropertyName("tiempo_preparacion")]
    public int TiempoPreparacion { get; set; } = 15;

    [JsonPropertyName("tiempo_preparacion_min")]
    public int? TiempoPreparacionMin
    {
        get => TiempoPreparacion;
        set { if (value.HasValue) TiempoPreparacion = value.Value; }
    }

    [JsonPropertyName("disponible")]
    public bool Disponible { get; set; } = true;

    [JsonPropertyName("destacado")]
    public bool Destacado { get; set; } = false;

    [JsonPropertyName("alergenos")]
    public List<string> Alergenos { get; set; } = new();

    [JsonPropertyName("ingredientes_base")]
    public List<string> IngredientesBase { get; set; } = new();

    [JsonPropertyName("extras_disponibles")]
    public List<ProductExtra> ExtrasDisponibles { get; set; } = new();
}

public class OrderItem
{
    [JsonPropertyName("producto_id")]
    public string ProductoId { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("imagen")]
    public string Imagen { get; set; } = string.Empty;

    [JsonPropertyName("precio_base")]
    public decimal PrecioBase { get; set; }

    [JsonPropertyName("cantidad")]
    public int Cantidad { get; set; } = 1;

    [JsonPropertyName("ingredientes_removidos")]
    public List<string> IngredientesRemovidos { get; set; } = new();

    [JsonPropertyName("extras")]
    public List<ProductExtra> Extras { get; set; } = new();

    [JsonPropertyName("precio_unitario")]
    public decimal PrecioUnitario { get; set; }

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }
}

public class OrderStatusHistory
{
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "NUEVO";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("cambiado_por")]
    public string CambiadoPor { get; set; } = "Sistema";
}

public class Order
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = string.Empty;

    [JsonPropertyName("mesa")]
    public string Mesa { get; set; } = string.Empty;

    [JsonPropertyName("tipo_cuenta")]
    public string TipoCuenta { get; set; } = "MESA"; // "MESA" o "SEPARADA"

    [JsonPropertyName("comensal")]
    public string Comensal { get; set; } = "Cuenta General de la Mesa";

    [JsonPropertyName("tipo_servicio")]
    public string TipoServicio { get; set; } = "En Mesa";

    [JsonPropertyName("metodo_pago")]
    public string MetodoPago { get; set; } = "Pago al mesero en mesa";

    [JsonPropertyName("estado")]
    public string Estado { get; set; } = "NUEVO"; // NUEVO, EN_PREPARACION, LISTO, ENTREGADO, CANCELADO

    [JsonPropertyName("estado_pago")]
    public string EstadoPago { get; set; } = "PENDIENTE"; // PENDIENTE, PARCIAL, PAGADO

    [JsonPropertyName("monto_pagado")]
    public decimal MontoPagado { get; set; } = 0;

    [JsonPropertyName("saldo_pendiente")]
    public decimal SaldoPendiente { get; set; }

    [JsonPropertyName("items")]
    public List<OrderItem> Items { get; set; } = new();

    [JsonPropertyName("subtotal")]
    public decimal Subtotal { get; set; }

    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("notas_cliente")]
    public string NotasCliente { get; set; } = string.Empty;

    [JsonPropertyName("creado_en")]
    public string CreadoEn { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("actualizado_en")]
    public string ActualizadoEn { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("cerrada")]
    public bool Cerrada { get; set; } = false;

    [JsonPropertyName("cerrada_en")]
    public string? CerradaEn { get; set; }

    [JsonPropertyName("historial_estados")]
    public List<OrderStatusHistory> HistorialEstados { get; set; } = new();
}

public class Payment
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "EFECTIVO"; // EFECTIVO, TERMINAL_EXTERNA

    [JsonPropertyName("received_by")]
    public string ReceivedBy { get; set; } = "Cajero";

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("status")]
    public string Status { get; set; } = "COMPLETED";
}

public class Review
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("rating")]
    public int Rating { get; set; } = 5;

    [JsonPropertyName("comentario")]
    public string Comentario { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("mesa")]
    public string Mesa { get; set; } = "Mesa";

    [JsonPropertyName("comensal")]
    public string Comensal { get; set; } = "Comensal";

    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = string.Empty;

    [JsonPropertyName("fecha")]
    public string Fecha { get; set; } = DateTime.UtcNow.ToString("o");
}

public class CorteCaja
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("responsable")]
    public string Responsable { get; set; } = "Administrador";

    [JsonPropertyName("inicio_periodo")]
    public string InicioPeriodo { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("fin_periodo")]
    public string FinPeriodo { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("fondo_inicial")]
    public decimal? FondoInicial { get; set; }

    [JsonPropertyName("efectivo_contado")]
    public decimal? EfectivoContado { get; set; }

    [JsonPropertyName("total_efectivo")]
    public decimal? TotalEfectivo { get; set; }

    [JsonPropertyName("total_terminal")]
    public decimal? TotalTerminal { get; set; }

    [JsonPropertyName("total_transferencia")]
    public decimal? TotalTransferencia { get; set; }

    [JsonPropertyName("total_ventas")]
    public decimal? TotalVentas { get; set; }

    [JsonPropertyName("diferencia_efectivo")]
    public decimal? DiferenciaEfectivo { get; set; }

    [JsonPropertyName("cantidad_pedidos")]
    public int? CantidadPedidos { get; set; }

    [JsonPropertyName("notas")]
    public string Notas { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");
}

public class GoogleReviewClickLog
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonPropertyName("mesa")]
    public string Mesa { get; set; } = "Desconocida";

    [JsonPropertyName("comensal")]
    public string Comensal { get; set; } = "Comensal";

    [JsonPropertyName("rating_intent")]
    public int RatingIntent { get; set; } = 5;

    [JsonPropertyName("order_token")]
    public string OrderToken { get; set; } = string.Empty;
}

// --- DTOs para Request y Response ---

public class CreateOrderItemDto
{
    [JsonPropertyName("producto_id")]
    public string ProductoId { get; set; } = string.Empty;

    [JsonPropertyName("cantidad")]
    public int Cantidad { get; set; } = 1;

    [JsonPropertyName("ingredientes_removidos")]
    public List<string>? IngredientesRemovidos { get; set; }

    [JsonPropertyName("extras_seleccionados")]
    public List<string>? ExtrasSeleccionados { get; set; }
}

public class CreateOrderDto
{
    [JsonPropertyName("mesa")]
    public string Mesa { get; set; } = string.Empty;

    [JsonPropertyName("tipo_cuenta")]
    public string? TipoCuenta { get; set; } // MESA o SEPARADA

    [JsonPropertyName("comensal")]
    public string? Comensal { get; set; }

    [JsonPropertyName("tipo_servicio")]
    public string? TipoServicio { get; set; }

    [JsonPropertyName("metodo_pago")]
    public string? MetodoPago { get; set; }

    [JsonPropertyName("notas_cliente")]
    public string? NotasCliente { get; set; }

    [JsonPropertyName("items")]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CallWaiterDto
{
    [JsonPropertyName("comensal")]
    public string? Comensal { get; set; }

    [JsonPropertyName("motivo")]
    public string? Motivo { get; set; }
}

public class RequestBillDto
{
    [JsonPropertyName("tipo")]
    public string? Tipo { get; set; } // MESA_COMPLETA | INDIVIDUAL | SPLIT

    [JsonPropertyName("comensal")]
    public string? Comensal { get; set; }

    [JsonPropertyName("total")]
    public decimal? Total { get; set; }

    [JsonPropertyName("metodo_pago")]
    public string? MetodoPago { get; set; }

    [JsonPropertyName("split_count")]
    public int? SplitCount { get; set; }
}

public class CreateReviewDto
{
    [JsonPropertyName("rating")]
    public int Rating { get; set; }

    [JsonPropertyName("comentario")]
    public string? Comentario { get; set; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("mesa")]
    public string? Mesa { get; set; }

    [JsonPropertyName("comensal")]
    public string? Comensal { get; set; }

    [JsonPropertyName("order_token")]
    public string? OrderToken { get; set; }
}

public class GoogleReviewClickDto
{
    [JsonPropertyName("mesa")]
    public string? Mesa { get; set; }

    [JsonPropertyName("comensal")]
    public string? Comensal { get; set; }

    [JsonPropertyName("rating_intent")]
    public int? RatingIntent { get; set; }

    [JsonPropertyName("order_token")]
    public string? OrderToken { get; set; }
}

public class AdminLoginDto
{
    [JsonPropertyName("pin")]
    public string Pin { get; set; } = string.Empty;
}

public class UpdatePinDto
{
    [JsonPropertyName("currentPin")]
    public string CurrentPin { get; set; } = string.Empty;

    [JsonPropertyName("newPin")]
    public string NewPin { get; set; } = string.Empty;
}

public class UpdateStaffPinDto
{
    [JsonPropertyName("adminPin")]
    public string AdminPin { get; set; } = string.Empty;

    [JsonPropertyName("newStaffPin")]
    public string NewStaffPin { get; set; } = string.Empty;
}

public class GoogleMapsSettingsDto
{
    [JsonPropertyName("google_maps_url")]
    public string? GoogleMapsUrl { get; set; }

    [JsonPropertyName("google_maps_review_url")]
    public string? GoogleMapsReviewUrl { get; set; }

    [JsonPropertyName("google_maps_rating")]
    public double? GoogleMapsRating { get; set; }

    [JsonPropertyName("google_maps_reviews_count")]
    public int? GoogleMapsReviewsCount { get; set; }

    [JsonPropertyName("google_place_id")]
    public string? GooglePlaceId { get; set; }
}

public class UpdateOrderStatusDto
{
    [JsonPropertyName("estado")]
    public string Estado { get; set; } = string.Empty;
}

public class RegisterPaymentDto
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = "EFECTIVO";

    [JsonPropertyName("received_by")]
    public string? ReceivedBy { get; set; }
}

public class CollectAllPendingDto
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "EFECTIVO";

    [JsonPropertyName("received_by")]
    public string? ReceivedBy { get; set; }
}

public class AssetUploadDto
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string? ContentType { get; set; }

    [JsonPropertyName("base64")]
    public string Base64 { get; set; } = string.Empty;
}

public class CreateCorteDto
{
    [JsonPropertyName("responsable")]
    public string? Responsable { get; set; }

    [JsonPropertyName("fondo_inicial")]
    public decimal? FondoInicial { get; set; }

    [JsonPropertyName("efectivo_contado")]
    public decimal? EfectivoContado { get; set; }

    [JsonPropertyName("notas")]
    public string? Notas { get; set; }
}

public class UpdateTablesDto
{
    [JsonPropertyName("mesas")]
    public List<string> Mesas { get; set; } = new();
}

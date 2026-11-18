using DGustAR.Api.Data;
using DGustAR.Api.Models;
using DGustAR.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DGustAR.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IStoreRepository _db;
    private readonly AppWebSocketManager _wsManager;

    public ReviewsController(IStoreRepository db, AppWebSocketManager wsManager)
    {
        _db = db;
        _wsManager = wsManager;
    }

    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto body)
    {
        if (body.Rating < 1 || body.Rating > 5)
        {
            return BadRequest(new { error = "La calificación debe ser entre 1 y 5 estrellas." });
        }

        var review = new Review
        {
            Id = $"rev-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds():x}-{Guid.NewGuid().ToString("N")[..4]}",
            Rating = body.Rating,
            Comentario = body.Comentario?.Trim() ?? string.Empty,
            Tags = (body.Tags ?? new()).Select(t => (t ?? "").Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList(),
            Mesa = !string.IsNullOrWhiteSpace(body.Mesa) ? body.Mesa.Trim() : "Mesa",
            Comensal = !string.IsNullOrWhiteSpace(body.Comensal) ? body.Comensal.Trim() : "Comensal",
            OrderToken = body.OrderToken?.Trim() ?? string.Empty,
            Fecha = DateTime.UtcNow.ToString("o")
        };

        var saved = await _db.CreateReviewAsync(review);
        await _wsManager.BroadcastToKitchenAsync("NEW_REVIEW", saved);

        return StatusCode(201, new
        {
            success = true,
            message = "¡Muchas gracias por tu calificación y reseña!",
            review = saved
        });
    }

    [HttpGet]
    public IActionResult GetReviews()
    {
        var reviews = _db.GetReviews();
        var total = reviews.Count;
        var avg = total > 0 ? Math.Round((double)reviews.Average(r => r.Rating), 1) : 5.0;

        var restInfo = _db.GetRestaurantInfo();
        var clicksLog = _db.GetGoogleReviewClicksLog();

        return Ok(new
        {
            total,
            promedio = avg,
            reviews,
            google_maps = new
            {
                url            = restInfo.GoogleMapsUrl,
                review_url     = restInfo.GoogleMapsReviewUrl,
                rating         = restInfo.GoogleMapsRating,
                reviews_count  = restInfo.GoogleMapsReviewsCount,
                place_id       = restInfo.GooglePlaceId,
                clicks_count   = restInfo.GoogleReviewsClicks,
                recent_clicks  = clicksLog.Take(10).ToList()
            }
        });
    }

    [HttpPost("google-click")]
    public async Task<IActionResult> GoogleReviewClick([FromBody] GoogleReviewClickDto body)
    {
        var result = await _db.RecordGoogleReviewClickAsync(body);
        var restInfo = _db.GetRestaurantInfo();

        await _wsManager.BroadcastToKitchenAsync("NEW_GOOGLE_REVIEW_CLICK", new
        {
            mesa = result.entry.Mesa,
            comensal = result.entry.Comensal,
            rating_intent = result.entry.RatingIntent,
            timestamp = result.entry.Timestamp,
            total_clicks = result.totalClicks
        });

        return Ok(new
        {
            success = true,
            message = "Redirigiendo a Google Maps...",
            google_maps_review_url = restInfo.GoogleMapsReviewUrl,
            google_maps_url = restInfo.GoogleMapsUrl,
            total_clicks = result.totalClicks
        });
    }
}

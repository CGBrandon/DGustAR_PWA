using System.Text.Json;
using DGustAR.Api.Data;
using DGustAR.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Registro de Servicios e Inyección de Dependencias
builder.Services.AddSingleton<IStoreRepository, StoreRepository>();
builder.Services.AddSingleton<AppWebSocketManager>();
builder.Services.AddScoped<OrderService>();

// 2. Controladores con serialización JSON uniforme
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
    });

// 3. Configuración de CORS amplia para Frontend en desarrollo y producción
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
    options.AddPolicy("NuevaPolitica", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 4. Configuración de Swagger / OpenAPI para Visual Studio Community
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DGustAR - API de Gastronomía y Realidad Aumentada",
        Version = "v1",
        Description = "API REST oficial y servidor WebSocket para DGustAR PWA desarrollada en ASP.NET Core .NET 8 y Microsoft SQL Server."
    });
});

var app = builder.Build();

// 5. Canal de procesamiento HTTP (Pipeline)
// Swagger UI siempre disponible para evaluación del profesor y pruebas de desarrollo
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DGustAR API v1");
    c.RoutePrefix = "swagger";
});

// El menú solo puede abrirse desde un QR de mesa. Los accesos directos sin
// mesa se envían al mismo shell de la PWA con la bandera que abre el escáner.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    var isMenuEntry = string.Equals(path, "/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(path, "/menu", StringComparison.OrdinalIgnoreCase);
    var hasTable = !string.IsNullOrWhiteSpace(context.Request.Query["mesa"]);

    if (isMenuEntry && !hasTable && !context.Request.Query.ContainsKey("qr"))
    {
        context.Response.Redirect("/menu?qr=required");
        return;
    }

    await next();
});

// Publica el frontend creado con HTML, CSS y JavaScript desde wwwroot
var fileExtensionProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
fileExtensionProvider.Mappings[".glb"] = "model/gltf-binary";
fileExtensionProvider.Mappings[".gltf"] = "model/gltf+json";

// La entrada pública de la aplicación es la PWA.
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "app.html" }
});
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = fileExtensionProvider,
    OnPrepareResponse = context =>
    {
        if (context.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
        }
    }
});

app.UseCors("NuevaPolitica");

// 6. Middleware WebSocket nativo
var webSocketOptions = new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
};
app.UseWebSockets(webSocketOptions);

// Mapeo del endpoint WebSocket en /ws
app.Map("/ws", async (HttpContext context, AppWebSocketManager wsManager) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await wsManager.HandleWebSocketAsync(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Se requiere una conexión WebSocket.");
    }
});

app.UseAuthorization();
app.MapControllers();

// 7. Enrutamiento SPA: cualquier ruta de cliente (/menu, /admin, /carrito, etc.) sirve app.html
app.MapFallbackToFile("app.html");

Console.WriteLine("=================================================================");
Console.WriteLine("🍽️  DGustAR .NET Web API iniciada con éxito");
Console.WriteLine("🌐  Swagger UI disponible en: http://localhost:5000/swagger");
Console.WriteLine("🔌  WebSocket Server listo en: ws://localhost:5000/ws");
Console.WriteLine("=================================================================");

app.Run();

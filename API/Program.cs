using System.Text.Json;
using DGustAR.Api.Data;
using DGustAR.Api.Services;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Overrides locales con secretos (p. ej. la cadena de conexión a Azure SQL).
// appsettings.Local.json está en .gitignore: nunca se sube al repositorio.
// En Azure la cadena se define en App Service > Configuración > Cadenas de conexión
// (nombre: DefaultConnection), que tiene prioridad sobre appsettings.json.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

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

// 3. CORS explícito. La PWA se sirve desde el mismo host en producción, por lo
// que no necesita CORS; solo se habilitan orígenes externos declarados.
var allowedCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(section => section.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin!.Trim().TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("NuevaPolitica", policy =>
    {
        if (allowedCorsOrigins.Length > 0)
        {
            policy.WithOrigins(allowedCorsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Sin orígenes externos configurados, el mismo origen sigue
            // funcionando y las páginas de terceros no reciben permisos CORS.
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

// 4. Compresión de respuestas HTTP (Brotli + Gzip) para GLB, JS, CSS, JSON
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "model/gltf-binary",
        "model/gltf+json",
        "application/octet-stream",
        "application/javascript",
        "text/css",
        "application/json"
    });
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    // Fastest evita latencia extra; los archivos GLB ya son binarios bien comprimidos
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// 4b. Configuración de Swagger / OpenAPI para Visual Studio Community
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DGustAR - API de Gastronomía y Realidad Aumentada",
        Version = "v1",
        Description = "API REST oficial y servidor WebSocket para DGustAR PWA desarrollada en ASP.NET Core .NET 10 y Microsoft SQL Server."
    });
});

var app = builder.Build();

// 5. Canal de procesamiento HTTP (Pipeline)
// Swagger solo se publica fuera de Production. Así sigue disponible para
// desarrollo/staging, pero no expone la consola de la API a clientes finales.
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DGustAR API v1");
        c.RoutePrefix = "swagger";
    });
}

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
app.UseCors("NuevaPolitica");

// Compresión Brotli/Gzip — debe ir ANTES de UseStaticFiles
app.UseResponseCompression();

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
        var fileName = context.File.Name;

        if (fileName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
        {
            // HTML siempre fresco — el SW decide desde caché
            context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            context.Context.Response.Headers.Pragma = "no-cache";
        }
        else if (fileName.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)
              || fileName.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase))
        {
            // Modelos 3D: caché de 1 año — inmutables, versión controlada por nombre
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (fileName.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
              || fileName.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            // Assets con hash en el nombre (Vite): caché de 1 año
            context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
              || fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
              || fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
              || fileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            // Imágenes: caché de 7 días (pueden cambiar con uploads)
            context.Context.Response.Headers.CacheControl = "public, max-age=604800";
        }
    }
});

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
Console.WriteLine(app.Environment.IsProduction()
    ? "🌐  Swagger UI deshabilitado en Production"
    : "🌐  Swagger UI disponible en: /swagger");
Console.WriteLine("🔌  WebSocket Server listo en: ws://localhost:5000/ws");
Console.WriteLine("=================================================================");

app.Run();

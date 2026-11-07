# DGustAR

PWA gastronómica con menú digital, modelos 3D/realidad aumentada, comandas en mesa, panel administrativo y API REST con actualizaciones por WebSocket.

El repositorio actual contiene una solución ASP.NET Core .NET 8 que sirve la PWA ya compilada desde `API/wwwroot`. No incluye un proyecto fuente independiente de Vue/Vite.

## Estado actual del proyecto

- Backend: ASP.NET Core Web API sobre .NET 8.
- Frontend servido por la API: HTML, CSS y JavaScript compilados en `API/wwwroot`.
- Persistencia operativa: datos locales en `API/Data/db.json`, con arranque seguro cuando SQL Server no está disponible.
- Integración SQL Server: se activa únicamente si la instancia contiene el esquema compatible con el repositorio; si detecta otro esquema, conserva el fallback local para no ejecutar operaciones parciales.
- Tiempo real: WebSocket nativo en `/ws`.
- Documentación de API: Swagger en `/swagger`.
- Modelos 3D: archivos `.glb` en `API/wwwroot/models`.

## Estructura del repositorio

```text
DGustAR_PWA/
├── DGustAR.sln
├── API/
│   ├── Controllers/       # Endpoints REST
│   ├── Data/              # Repositorios y datos locales
│   ├── Models/            # DTOs y modelos de dominio
│   ├── Services/          # Pedidos y WebSockets
│   ├── Properties/        # Perfiles de ejecución
│   ├── wwwroot/           # PWA, panel, recursos y modelos 3D
│   ├── appsettings.json
│   └── DGustAR.Api.csproj
├── BaseDatos/
│   └── Scripts.sql        # Base de datos, tablas, procedimientos y datos iniciales
├── Postman/
│   └── Coleccion.json     # Colección para probar la API
└── README.md
```

## Tecnologías

- C# y ASP.NET Core Web API (.NET 8).
- Swagger/OpenAPI mediante Swashbuckle.
- SQL Server mediante `Microsoft.Data.SqlClient`.
- HTML, CSS y JavaScript para la PWA distribuida.
- WebSocket nativo para el tablero de cocina y el seguimiento de pedidos.
- `model-viewer` y modelos GLB para la visualización 3D/AR.

## Requisitos

- Windows 10/11.
- .NET SDK 8.0 o superior.
- SQL Server 2019, 2022 o Express (opcional si se usa el fallback local).
- SQL Server Management Studio, si se va a ejecutar el script de base de datos.
- Visual Studio 2022 o la CLI de .NET.
- Postman, opcional para importar la colección de pruebas.

## Configuración de la base de datos

1. Abre `BaseDatos/Scripts.sql` en SQL Server Management Studio.
2. Conéctate a la instancia de SQL Server que utilizarás.
3. Ejecuta el script completo.

El script crea la base de datos `DGustAR_DB`, tablas, restricciones, índices, datos iniciales y procedimientos almacenados para catálogo, pedidos, pagos y cierre de mesa. La API valida el esquema antes de activar SQL Server; si no coincide con el esquema que espera su repositorio actual, continúa con `API/Data/db.json` y lo deja registrado en el log.

La aplicación toma la cadena de conexión de `API/appsettings.json`. Ajusta `ConnectionStrings` a tu instancia local antes de ejecutar la API. Ejemplo:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=DGustAR_DB;Trusted_Connection=True;TrustServerCertificate=True;",
    "CadenaSQL": "Server=localhost;Database=DGustAR_DB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

Para SQL Server Express, sustituye el servidor por `.`\\`SQLEXPRESS` según corresponda a tu instalación.

> No subas contraseñas, tokens ni cadenas de conexión con credenciales reales al repositorio. Para cada equipo, configura los valores locales en `appsettings.Development.json` o mediante variables de entorno.

## Ejecutar la aplicación

Desde Visual Studio:

1. Abre `DGustAR.sln`.
2. Selecciona `DGustAR.Api` como proyecto de inicio.
3. Ejecuta con `F5`.

Desde la terminal:

```powershell
cd API
dotnet run --urls "http://localhost:5000"
```

Accesos oficiales de la aplicación:

| Recurso | URL |
| --- | --- |
| Menú digital | `http://localhost:5000/menu` |
| Panel administrativo y KDS | `http://localhost:5000/admin` |
| Tarjetas QR del panel | `http://localhost:5000/admin/tarjetas-qr` |
| Swagger (aparte) | `http://localhost:5000/swagger` |

La raíz `http://localhost:5000/` abre el escáner QR. El menú solo se habilita después de validar una mesa; los QR generados por el panel abren `/menu?mesa=...`. La configuración de desarrollo de `API/Properties/launchSettings.json` usa `http://localhost:5000` y `https://localhost:5001`.

Las rutas `/platillo`, `/ar`, `/carrito` y `/pedido` son pantallas internas del flujo del menú, no accesos independientes. Las rutas `/api/*` y `/ws` son servicios técnicos consumidos por la aplicación.

## Acceso por PIN

El panel administrativo utiliza un único formulario de acceso por PIN. Las credenciales predeterminadas se definen en `API/appsettings.json`:

| PIN | Rol | Uso |
| --- | --- | --- |
| `1234` | ADMIN | Administración, caja, menú y reportes |
| `1111` | STAFF | Cocina KDS y operación |

El panel administrativo utiliza los tokens generados por el endpoint `POST /api/admin/login`. Para pruebas directas autorizadas, la colección de Postman incluye las variables necesarias.

## API y WebSocket

Los controladores disponibles incluyen:

- `Admin`: acceso por PIN para ADMIN y STAFF, operación de pedidos, caja y configuración.
- `Health`: comprobación del estado de la aplicación.
- `Menu`: menú y productos públicos.
- `Orders`: creación y consulta de pedidos.
- `Tables`: mesas, llamadas al mesero, cuentas y cierre.
- `Reviews`: reseñas y enlaces de Google Maps.
- `Admin`: menú, pedidos, pagos, caja, mesas y configuración.
Swagger ofrece el detalle de rutas, parámetros y modelos cuando la aplicación está ejecutándose.

## Pruebas con Postman

1. Inicia la API en `http://localhost:5000`.
2. Importa `Postman/Coleccion.json` en Postman.
3. Verifica que `base_url` apunte a `http://localhost:5000`.
4. Ejecuta primero los endpoints de salud/autenticación y después las operaciones protegidas.

## Notas importantes

- La PWA que se ejecuta en producción está dentro de `API/wwwroot`; por eso no debe buscarse una carpeta `Frontend/` en este repositorio.
- Los archivos `API/bin` y `API/obj` son artefactos generados por .NET y no forman parte del código fuente.
- Los modelos 3D y los manifiestos PWA están en `API/wwwroot/models` y `API/wwwroot/manifest*.json`.
- Swagger está habilitado para facilitar el desarrollo y la evaluación local; en producción conviene restringirlo.

# DGustAR

PWA gastronómica con menú digital, modelos 3D/realidad aumentada, comandas en mesa, panel administrativo y API REST con actualizaciones por WebSocket.

El repositorio actual contiene una solución ASP.NET Core .NET 10 que sirve la PWA ya compilada desde `API/wwwroot`. No incluye un proyecto fuente independiente de Vue/Vite.

## Estado actual del proyecto

- Backend: ASP.NET Core Web API sobre .NET 10.
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

- C# y ASP.NET Core Web API (.NET 10).
- Swagger/OpenAPI mediante Swashbuckle.
- SQL Server mediante `Microsoft.Data.SqlClient`.
- HTML, CSS y JavaScript para la PWA distribuida.
- WebSocket nativo para el tablero de cocina y el seguimiento de pedidos.
- `model-viewer` y modelos GLB para la visualización 3D/AR.

## Requisitos

- Windows 10/11.
- .NET SDK 10.0 o superior.
- SQL Server 2019, 2022 o Express (opcional si se usa el fallback local).
- SQL Server Management Studio, si se va a ejecutar el script de base de datos.
- Visual Studio 2022 o la CLI de .NET.
- Postman, opcional para importar la colección de pruebas.

## Configuración de la base de datos

1. Abre `BaseDatos/Scripts.sql` en SQL Server Management Studio.
2. Conéctate a la instancia de SQL Server que utilizarás.
3. Ejecuta el script completo.

El script crea la base de datos `DGustAR_DB`, tablas, restricciones, índices, datos iniciales y procedimientos almacenados para catálogo, pedidos, pagos y cierre de mesa. La API valida el esquema antes de activar SQL Server; en Development puede continuar con `API/Data/db.json` para facilitar pruebas locales, pero en Production se detiene si Azure SQL no está disponible o no coincide con el esquema esperado.

La aplicación toma la cadena de conexión desde `ConnectionStrings:DefaultConnection`. En producción configúrala mediante Azure App Settings o variables de entorno (`ConnectionStrings__DefaultConnection`); no la guardes en el repositorio. Para desarrollo local puedes usar User Secrets, una variable de entorno o el fallback local `API/Data/db.json`. Ejemplo:

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

### Configuración requerida en Azure App Service

En `Configuration > Application settings`, agrega estos valores como secretos de la aplicación. No los guardes en `appsettings.json` ni en el repositorio:

| Nombre | Valor |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | Cadena de conexión real de Azure SQL |
| `RestaurantSettings__DefaultAdminPin` | PIN de gerencia nuevo, no `1234` |
| `RestaurantSettings__DefaultStaffPin` | PIN de personal nuevo, no `1111` |
| `RestaurantSettings__AdminToken` | Token aleatorio largo |
| `RestaurantSettings__StaffToken` | Token aleatorio largo |

Para Azure SQL con autenticación SQL, la cadena debe tener un formato equivalente a este, reemplazando los marcadores por valores reales:

```text
Server=tcp:<servidor>.database.windows.net,1433;Initial Catalog=<base_de_datos>;Persist Security Info=False;User ID=<usuario>;Password=<contraseña>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

Si la PWA y la API se sirven desde dominios distintos, configura también `Cors__AllowedOrigins__0` con el origen exacto de la PWA, por ejemplo `https://menu.ejemplo.com`. Si ambas viven en el mismo dominio de Azure, no es necesario agregarlo.

Después de guardar App Settings, reinicia el App Service y verifica `/api/health`. La aplicación debe iniciar en `Production` y conectarse a Azure SQL; si la conexión o el esquema fallan, debe detenerse en vez de operar sobre `db.json`.

Accesos oficiales de la aplicación:

| Recurso | URL |
| --- | --- |
| Menú digital | `http://localhost:5000/menu` |
| Panel administrativo y KDS | `http://localhost:5000/admin` |
| Tarjetas QR del panel | `http://localhost:5000/admin/tarjetas-qr` |
| Swagger (solo Development/Staging) | `http://localhost:5000/swagger` |

La raíz `http://localhost:5000/` abre el escáner QR. El menú solo se habilita después de validar una mesa; los QR generados por el panel abren `/menu?mesa=...`. La configuración de desarrollo de `API/Properties/launchSettings.json` usa `http://localhost:5000` y `https://localhost:5001`.

Las rutas `/platillo`, `/ar`, `/carrito` y `/pedido` son pantallas internas del flujo del menú, no accesos independientes. Las rutas `/api/*` y `/ws` son servicios técnicos consumidos por la aplicación.

## Acceso por PIN

El panel administrativo utiliza un único formulario de acceso por PIN. En desarrollo, los valores de prueba viven en `API/appsettings.Development.json`. En producción deben configurarse como Azure App Settings o variables de entorno (`RestaurantSettings__DefaultAdminPin`, `RestaurantSettings__DefaultStaffPin`, `RestaurantSettings__AdminToken` y `RestaurantSettings__StaffToken`):

| PIN | Rol | Uso |
| --- | --- | --- |
| `1234` | ADMIN | Administración, caja, menú y reportes |
| `1111` | STAFF | Cocina KDS y operación |

El panel administrativo utiliza los tokens generados por el endpoint `POST /api/admin/login`. Para pruebas directas autorizadas, la colección de Postman incluye las variables necesarias.

## API y WebSocket

En producción, CORS solo se habilita para orígenes externos declarados mediante
`Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, etc. Si la PWA y la API
usan el mismo dominio, no es necesario configurar orígenes externos.

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

El repositorio incluye la colección oficial lista para importar en `Postman/Coleccion.json`. Cubre tanto los endpoints públicos como las operaciones protegidas con token de administración y cocina.

### Métodos de importación en Postman

Hay dos alternativas para cargar las pruebas en Postman:

#### Opción A: Importar el archivo JSON local (Recomendado)
1. Abre Postman y presiona el botón **Import** (esquina superior izquierda).
2. Selecciona o arrastra el archivo [`Postman/Coleccion.json`](file:///d:/Aplicaciones%20Web%20Progresivas/material/CRUDV2/DGustAR_PWA/Postman/Coleccion.json).
3. La colección **"DGustAR - API REST & PWA Suite"** aparecerá organizada en carpetas con ejemplos de peticiones y cuerpos listos para enviar.

#### Opción B: Importar desde la URL de Swagger
1. Inicia la API localmente (`dotnet run` en el puerto 5000).
2. En Postman, ve a **Import** y pega la URL de OpenAPI:
   ```text
   http://localhost:5000/swagger/v1/swagger.json
   ```
3. Postman generará automáticamente una colección con todos los esquemas y tipos de datos declarados en los controladores.

### Variables preconfiguradas de la colección

La colección ya incluye las siguientes variables de entorno para que no tengas que escribir URLs ni tokens manualmente:

| Variable | Valor por defecto | Descripción |
| --- | --- | --- |
| `base_url` | `http://localhost:5000` | Dirección base del servidor de desarrollo local |
| `admin_token` | `bistro-admin-authorized` | Token predeterminado para rol ADMIN (Gerencia) |
| `staff_token` | `bistro-staff-authorized` | Token predeterminado para rol STAFF (Cocina KDS) |
| `mesa` | `Mesa 01` | Mesa predeterminada para pruebas de salón y pedidos |

### Carpetas y endpoints incluidos en la colección

1. **1. Salud & Diagnóstico:**
   - `GET /api/health`: Comprobación del estado del servicio y si está operando sobre Azure SQL o el fallback local `db.json`.
   - `GET /swagger/v1/swagger.json`: Especificación OpenAPI v3.
2. **2. Menú Digital (Público):**
   - `GET /api/menu`: Catálogo completo con categorías, platillos y enlaces a modelos 3D (`.glb`).
   - `GET /api/menu/products/{id}`: Ficha detallada de un platillo.
3. **3. Mesas & Salón:**
   - `GET /api/tables`: Listado de mesas físicas configuradas.
   - `GET /api/tables/{mesa}/account`: Resumen de consumo y cuenta activa de una mesa.
   - `POST /api/tables/{mesa}/call-waiter`: Alerta de llamado al mesero enviada a cocina vía WebSockets.
   - `POST /api/tables/{mesa}/request-bill`: Solicitud de ticket o cuenta de la mesa.
   - `POST /api/tables/{mesa}/close`: Cierre y liberación de mesa (protegido con `x-admin-token`).
4. **4. Pedidos & Comandas:**
   - `POST /api/orders`: Registro de comanda en mesa con notificación instantánea a cocina.
   - `GET /api/orders/{token}`: Consulta de estatus de preparación por token de comensal.
5. **5. Administración & KDS (Protegido con `x-admin-token`):**
   - `POST /api/admin/login`: Validación de PIN (`1234` para Gerencia, `1111` para Personal).
   - `GET /api/admin/orders`: Listado de comandas activas con filtros por estado y mesa.
   - `PATCH /api/admin/orders/{id}/status`: Avance de estados en cocina (`NUEVO` ➔ `EN_PREPARACION` ➔ `LISTO` ➔ `ENTREGADO`).
   - `GET /api/admin/products`: Consulta de platillos con filtros (nombre, categoría, disponibilidad, precios).
   - `GET /api/admin/products/{id}`: Consulta directa de platillo por ID.
   - `POST /api/admin/products`: Creación de nuevos platillos.
   - `PATCH /api/admin/products/{id}/toggle`: Alternar disponibilidad (agotado/disponible).
6. **6. Pagos & Caja:**
   - `POST /api/admin/payments`: Registro de cobro en efectivo con validación de monto recibido y cálculo automático de cambio (`received_amount`, `change`), con liberación opcional de mesa.
   - `POST /api/admin/payments/collect-all-pending`: Liquidación de todas las órdenes entregadas pendientes.
   - `GET /api/admin/corte-caja/report`: Arqueo y cálculo de ventas en efectivo, tarjeta y esperado en caja.
   - `POST /api/admin/corte-caja`: Ejecución y guardado de corte de caja en el historial.
   - `GET /api/admin/corte-caja/history`: Historial de cortes previos.
7. **7. Reseñas & Calificaciones:**
   - `POST /api/reviews`: Publicación de reseñas y estrellas de clientes.
   - `GET /api/reviews`: Estadísticas agregadas, porcentaje de satisfacción y listado de opiniones.

### Flujo recomendado para realizar pruebas completas

1. **Paso 1:** Ejecutar `GET /api/health` para comprobar que la API esté respondiendo en `http://localhost:5000`.
2. **Paso 2:** Probar `POST /api/admin/login` con PIN `1234` para verificar la autenticación administrativa.
3. **Paso 3:** Consultar el catálogo con `GET /api/menu` o buscar platillos con `GET /api/admin/products?nombre=pizza`.
4. **Paso 4:** Simular un pedido de mesa enviando `POST /api/orders` y avanzar su estado con `PATCH /api/admin/orders/{id}/status`.
5. **Paso 5:** Simular el cobro con `POST /api/admin/payments` enviando `amount`, `received_amount` y `change` para verificar el registro de cambio en efectivo.
6. **Paso 6:** Consultar el corte con `GET /api/admin/corte-caja/report` y cerrarlo con `POST /api/admin/corte-caja`.

## Notas importantes

- La PWA que se ejecuta en producción está dentro de `API/wwwroot`; por eso no debe buscarse una carpeta `Frontend/` en este repositorio.
- Los archivos `API/bin` y `API/obj` son artefactos generados por .NET y no forman parte del código fuente.
- Los modelos 3D y los manifiestos PWA están en `API/wwwroot/models` y `API/wwwroot/manifest*.json`.
- Swagger está habilitado para facilitar el desarrollo y la evaluación local; se deshabilita automáticamente cuando `ASPNETCORE_ENVIRONMENT=Production`.

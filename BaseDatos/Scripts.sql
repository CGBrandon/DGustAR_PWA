-- ==============================================================================
-- PROYECTO: DGustAR - Gastronomía & Realidad Aumentada (PWA)
-- SCRIPT: Creación de Base de Datos y Esquema Relacional Oficial para SQL Server
-- OBJETIVO: Sincronización exacta con el modelo relacional del backend en .NET 8
--           y generación limpia del Database Diagram en SSMS.
-- MOTOR: Microsoft SQL Server 2016 / 2019 / 2022 / Azure SQL & SSMS
-- TABLAS DEL SISTEMA (11 tablas exactas en SSMS):
--   1. dbo.Restaurantes          7. dbo.PedidoItems
--   2. dbo.Categorias            8. dbo.HistorialEstadoPedido
--   3. dbo.Platillos             9. dbo.LlamadosMesero
--   4. dbo.PlatilloExtras       10. dbo.CortesCaja
--   5. dbo.SesionesMesa         11. dbo.empleados
--   6. dbo.Pedidos
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. CREACIÓN O CONEXIÓN A LA BASE DE DATOS
-- ------------------------------------------------------------------------------
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'DGustAR_DB')
BEGIN
    CREATE DATABASE DGustAR_DB;
END
GO

USE DGustAR_DB;
GO

-- Habilitar permisos de diagramación para el usuario actual / sa
-- (Evita el error en SSMS: "This database does not have one or more support objects required...")
BEGIN TRY
    EXEC sp_changedbowner 'sa';
END TRY
BEGIN CATCH
    -- Si no existe el usuario 'sa' o es Azure SQL, continúa de forma segura
END CATCH
GO

-- ------------------------------------------------------------------------------
-- 2. ELIMINACIÓN DE TABLAS PREVIAS (ORDEN INVERSO DE DEPENDENCIAS)
-- ------------------------------------------------------------------------------
IF OBJECT_ID('dbo.HistorialEstadoPedido', 'U') IS NOT NULL DROP TABLE dbo.HistorialEstadoPedido;
IF OBJECT_ID('dbo.historial_estados_pedido', 'U') IS NOT NULL DROP TABLE dbo.historial_estados_pedido;
IF OBJECT_ID('dbo.extras_detalle_pedido', 'U') IS NOT NULL DROP TABLE dbo.extras_detalle_pedido;
IF OBJECT_ID('dbo.detalles_pedido', 'U') IS NOT NULL DROP TABLE dbo.detalles_pedido;
IF OBJECT_ID('dbo.PedidoItems', 'U') IS NOT NULL DROP TABLE dbo.PedidoItems;
IF OBJECT_ID('dbo.pagos', 'U') IS NOT NULL DROP TABLE dbo.pagos;
IF OBJECT_ID('dbo.resenas', 'U') IS NOT NULL DROP TABLE dbo.resenas;
IF OBJECT_ID('dbo.Pedidos', 'U') IS NOT NULL DROP TABLE dbo.Pedidos;
IF OBJECT_ID('dbo.pedidos', 'U') IS NOT NULL DROP TABLE dbo.pedidos;
IF OBJECT_ID('dbo.PlatilloExtras', 'U') IS NOT NULL DROP TABLE dbo.PlatilloExtras;
IF OBJECT_ID('dbo.extras_producto', 'U') IS NOT NULL DROP TABLE dbo.extras_producto;
IF OBJECT_ID('dbo.Platillos', 'U') IS NOT NULL DROP TABLE dbo.Platillos;
IF OBJECT_ID('dbo.productos', 'U') IS NOT NULL DROP TABLE dbo.productos;
IF OBJECT_ID('dbo.Categorias', 'U') IS NOT NULL DROP TABLE dbo.Categorias;
IF OBJECT_ID('dbo.categorias', 'U') IS NOT NULL DROP TABLE dbo.categorias;
IF OBJECT_ID('dbo.LlamadosMesero', 'U') IS NOT NULL DROP TABLE dbo.LlamadosMesero;
IF OBJECT_ID('dbo.SesionesMesa', 'U') IS NOT NULL DROP TABLE dbo.SesionesMesa;
IF OBJECT_ID('dbo.mesas', 'U') IS NOT NULL DROP TABLE dbo.mesas;
IF OBJECT_ID('dbo.CortesCaja', 'U') IS NOT NULL DROP TABLE dbo.CortesCaja;
IF OBJECT_ID('dbo.cortes_caja', 'U') IS NOT NULL DROP TABLE dbo.cortes_caja;
IF OBJECT_ID('dbo.empleados', 'U') IS NOT NULL DROP TABLE dbo.empleados;
IF OBJECT_ID('dbo.Restaurantes', 'U') IS NOT NULL DROP TABLE dbo.Restaurantes;
IF OBJECT_ID('dbo.restaurantes', 'U') IS NOT NULL DROP TABLE dbo.restaurantes;
GO

-- ------------------------------------------------------------------------------
-- 3. TABLA: dbo.Restaurantes
--    Parámetros de negocio, Google Maps, Ratings y Llaves de Acceso
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.Restaurantes (
    Id NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Moneda NVARCHAR(10) NOT NULL CONSTRAINT DF_Restaurantes_Moneda DEFAULT N'MXN',
    SimboloMoneda NVARCHAR(5) NOT NULL CONSTRAINT DF_Restaurantes_Simbolo DEFAULT N'$',
    Telefono NVARCHAR(30) NULL,
    Direccion NVARCHAR(255) NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Restaurantes_Activo DEFAULT 1,
    GoogleMapsUrl NVARCHAR(500) NULL,
    GoogleMapsReviewUrl NVARCHAR(500) NULL,
    GoogleMapsRating DECIMAL(3,1) NOT NULL CONSTRAINT DF_Restaurantes_Rating DEFAULT 4.9,
    GoogleMapsReviewsCount INT NOT NULL CONSTRAINT DF_Restaurantes_RevCount DEFAULT 128,
    GooglePlaceId NVARCHAR(100) NULL,
    GoogleReviewsClicks INT NOT NULL CONSTRAINT DF_Restaurantes_Clicks DEFAULT 0,
    PinAdmin NVARCHAR(100) NOT NULL CONSTRAINT DF_Restaurantes_PinAdmin DEFAULT N'1234',
    PinStaff NVARCHAR(100) NOT NULL CONSTRAINT DF_Restaurantes_PinStaff DEFAULT N'1111',
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Restaurantes_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Restaurantes_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Restaurantes PRIMARY KEY CLUSTERED (Id)
);
GO

-- ------------------------------------------------------------------------------
-- 4. TABLA: dbo.Categorias
--    Clasificación del Menú con Emojis Unicode e Íconos Material Symbols
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.Categorias (
    Id NVARCHAR(50) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Categorias_Restaurante DEFAULT N'default',
    Nombre NVARCHAR(100) NOT NULL,
    Slug NVARCHAR(100) NOT NULL,
    Icono NVARCHAR(50) NOT NULL CONSTRAINT DF_Categorias_Icono DEFAULT N'restaurant',
    Emoji NVARCHAR(50) NOT NULL CONSTRAINT DF_Categorias_Emoji DEFAULT N'🍽️',
    Orden INT NOT NULL CONSTRAINT DF_Categorias_Orden DEFAULT 0,
    Activo BIT NOT NULL CONSTRAINT DF_Categorias_Activo DEFAULT 1,
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Categorias_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Categorias_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Categorias PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Categorias_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 5. TABLA: dbo.Platillos
--    Catálogo con Modelos 3D (.glb), Alérgenos y Tiempos de Cocina KDS
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.Platillos (
    Id NVARCHAR(50) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Platillos_Restaurante DEFAULT N'default',
    CategoriaId NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Descripcion NVARCHAR(MAX) NULL,
    PrecioBase DECIMAL(12,2) NOT NULL CONSTRAINT DF_Platillos_PrecioBase DEFAULT 0.00,
    ImagenUrl NVARCHAR(500) NULL,
    Modelo3dUrl NVARCHAR(500) NULL,
    TiempoPreparacionMin INT NOT NULL CONSTRAINT DF_Platillos_Tiempo DEFAULT 15,
    EscalaRealMetros DECIMAL(6,2) NOT NULL CONSTRAINT DF_Platillos_Escala DEFAULT 0.20,
    Disponible BIT NOT NULL CONSTRAINT DF_Platillos_Disponible DEFAULT 1,
    Destacado BIT NOT NULL CONSTRAINT DF_Platillos_Destacado DEFAULT 0,
    Alergenos NVARCHAR(255) NULL,
    IngredientesBaseJson NVARCHAR(MAX) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Platillos_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Platillos_UpdatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Platillos PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Platillos_Categoria FOREIGN KEY (CategoriaId)
        REFERENCES dbo.Categorias (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Platillos_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Platillos_PrecioBase CHECK (PrecioBase >= 0)
);
GO

-- ------------------------------------------------------------------------------
-- 6. TABLA: dbo.PlatilloExtras
--    Extras e Ingredientes Personalizables por Platillo
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.PlatilloExtras (
    Id NVARCHAR(50) NOT NULL,
    PlatilloId NVARCHAR(50) NOT NULL,
    Nombre NVARCHAR(100) NOT NULL,
    PrecioExtra DECIMAL(12,2) NOT NULL CONSTRAINT DF_PlatilloExtras_Precio DEFAULT 0.00,
    Activo BIT NOT NULL CONSTRAINT DF_PlatilloExtras_Activo DEFAULT 1,
    CONSTRAINT PK_PlatilloExtras PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PlatilloExtras_Platillos FOREIGN KEY (PlatilloId)
        REFERENCES dbo.Platillos (Id) ON DELETE CASCADE,
    CONSTRAINT CK_PlatilloExtras_Precio CHECK (PrecioExtra >= 0)
);
GO

-- ------------------------------------------------------------------------------
-- 7. TABLA: dbo.SesionesMesa
--    Mesas Físicas, Códigos QR y Control de Sesión de Comensales
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.SesionesMesa (
    Id INT IDENTITY(1,1) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_SesionesMesa_Restaurante DEFAULT N'default',
    NombreMesa NVARCHAR(50) NOT NULL,
    CodigoQR NVARCHAR(255) NULL,
    Estado NVARCHAR(30) NOT NULL CONSTRAINT DF_SesionesMesa_Estado DEFAULT N'DISPONIBLE',
    Comensales INT NOT NULL CONSTRAINT DF_SesionesMesa_Comensales DEFAULT 1,
    Activa BIT NOT NULL CONSTRAINT DF_SesionesMesa_Activa DEFAULT 1,
    FechaInicio DATETIMEOFFSET NULL,
    UltimaActividad DATETIMEOFFSET NOT NULL CONSTRAINT DF_SesionesMesa_UltimaAct DEFAULT SYSDATETIMEOFFSET(),
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SesionesMesa_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_SesionesMesa PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_SesionesMesa_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE CASCADE,
    CONSTRAINT UQ_SesionesMesa_Nombre UNIQUE (RestauranteId, NombreMesa),
    CONSTRAINT CK_SesionesMesa_Estado CHECK (Estado IN (N'DISPONIBLE', N'OCUPADA', N'EN_ATENCION', N'RESERVADA'))
);
GO

-- ------------------------------------------------------------------------------
-- 8. TABLA: dbo.Pedidos
--    Comandas creadas desde la PWA móvil con sincronización WebSocket KDS
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.Pedidos (
    Id NVARCHAR(50) NOT NULL,
    OrderToken NVARCHAR(100) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Pedidos_Restaurante DEFAULT N'default',
    Mesa NVARCHAR(50) NOT NULL,
    TipoCuenta NVARCHAR(20) NOT NULL CONSTRAINT DF_Pedidos_TipoCuenta DEFAULT N'MESA',
    Comensal NVARCHAR(100) NOT NULL CONSTRAINT DF_Pedidos_Comensal DEFAULT N'Cuenta General de la Mesa',
    TipoServicio NVARCHAR(30) NOT NULL CONSTRAINT DF_Pedidos_TipoServicio DEFAULT N'En Mesa',
    MetodoPago NVARCHAR(50) NULL CONSTRAINT DF_Pedidos_MetodoPago DEFAULT N'Pago al mesero en mesa',
    Estado NVARCHAR(30) NOT NULL CONSTRAINT DF_Pedidos_Estado DEFAULT N'NUEVO',
    EstadoPago NVARCHAR(20) NOT NULL CONSTRAINT DF_Pedidos_EstadoPago DEFAULT N'PENDIENTE',
    MontoPagado DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pedidos_MontoPagado DEFAULT 0.00,
    SaldoPendiente DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pedidos_SaldoPendiente DEFAULT 0.00,
    Subtotal DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pedidos_Subtotal DEFAULT 0.00,
    Total DECIMAL(12,2) NOT NULL CONSTRAINT DF_Pedidos_Total DEFAULT 0.00,
    NotasCliente NVARCHAR(500) NULL,
    Cerrada BIT NOT NULL CONSTRAINT DF_Pedidos_Cerrada DEFAULT 0,
    CerradaEn DATETIMEOFFSET NULL,
    CreadoEn DATETIMEOFFSET NOT NULL CONSTRAINT DF_Pedidos_CreadoEn DEFAULT SYSDATETIMEOFFSET(),
    ActualizadoEn DATETIMEOFFSET NOT NULL CONSTRAINT DF_Pedidos_ActualizadoEn DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_Pedidos PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Pedidos_OrderToken UNIQUE (OrderToken),
    CONSTRAINT FK_Pedidos_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE NO ACTION,
    CONSTRAINT CK_Pedidos_Estado CHECK (Estado IN (N'NUEVO', N'ACEPTADO', N'EN_PREPARACION', N'LISTO', N'ENTREGADO', N'CANCELADO')),
    CONSTRAINT CK_Pedidos_EstadoPago CHECK (EstadoPago IN (N'PENDIENTE', N'PARCIAL', N'PAGADO'))
);
GO

-- ------------------------------------------------------------------------------
-- 9. TABLA: dbo.PedidoItems
--    Platillos individuales ordenados con personalizaciones e ingredientes
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.PedidoItems (
    Id INT IDENTITY(1,1) NOT NULL,
    PedidoId NVARCHAR(50) NOT NULL,
    ProductoId NVARCHAR(50) NULL,
    Nombre NVARCHAR(150) NOT NULL,
    Imagen NVARCHAR(500) NULL,
    Cantidad INT NOT NULL CONSTRAINT DF_PedidoItems_Cantidad DEFAULT 1,
    PrecioBase DECIMAL(12,2) NOT NULL CONSTRAINT DF_PedidoItems_PrecioBase DEFAULT 0.00,
    PrecioUnitario DECIMAL(12,2) NOT NULL CONSTRAINT DF_PedidoItems_PrecioUnit DEFAULT 0.00,
    Subtotal DECIMAL(12,2) NOT NULL CONSTRAINT DF_PedidoItems_Subtotal DEFAULT 0.00,
    Notas NVARCHAR(500) NULL,
    IngredientesRemovidosJson NVARCHAR(MAX) NULL,
    ExtrasJson NVARCHAR(MAX) NULL,
    CONSTRAINT PK_PedidoItems PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_PedidoItems_Pedidos FOREIGN KEY (PedidoId)
        REFERENCES dbo.Pedidos (Id) ON DELETE CASCADE,
    CONSTRAINT FK_PedidoItems_Platillos FOREIGN KEY (ProductoId)
        REFERENCES dbo.Platillos (Id) ON DELETE SET NULL,
    CONSTRAINT CK_PedidoItems_Cantidad CHECK (Cantidad > 0)
);
GO

-- ------------------------------------------------------------------------------
-- 10. TABLA: dbo.HistorialEstadoPedido
--     Trazabilidad y Auditoría de Cocina KDS por Comanda
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.HistorialEstadoPedido (
    Id INT IDENTITY(1,1) NOT NULL,
    PedidoId NVARCHAR(50) NOT NULL,
    Estado NVARCHAR(30) NOT NULL,
    Timestamp DATETIMEOFFSET NOT NULL CONSTRAINT DF_HistorialEstado_Timestamp DEFAULT SYSDATETIMEOFFSET(),
    CambiadoPor NVARCHAR(100) NOT NULL CONSTRAINT DF_HistorialEstado_CambiadoPor DEFAULT N'Sistema',
    CONSTRAINT PK_HistorialEstadoPedido PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_HistorialEstadoPedido_Pedidos FOREIGN KEY (PedidoId)
        REFERENCES dbo.Pedidos (Id) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 11. TABLA: dbo.LlamadosMesero
--     Alertas en tiempo real desde la mesa al personal de servicio
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.LlamadosMesero (
    Id INT IDENTITY(1,1) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Llamados_Restaurante DEFAULT N'default',
    Mesa NVARCHAR(50) NOT NULL,
    TipoLlamado NVARCHAR(50) NOT NULL CONSTRAINT DF_Llamados_Tipo DEFAULT N'ASISTENCIA',
    Estado NVARCHAR(30) NOT NULL CONSTRAINT DF_Llamados_Estado DEFAULT N'PENDIENTE',
    AtendidoPor NVARCHAR(100) NULL,
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Llamados_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    AtendidoEn DATETIMEOFFSET NULL,
    CONSTRAINT PK_LlamadosMesero PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_LlamadosMesero_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE CASCADE,
    CONSTRAINT CK_Llamados_Estado CHECK (Estado IN (N'PENDIENTE', N'ATENDIDO', N'CANCELADO'))
);
GO

-- ------------------------------------------------------------------------------
-- 12. TABLA: dbo.CortesCaja
--     Arqueos de Turno, Cuadres de Caja y Reportes Financieros
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.CortesCaja (
    Id NVARCHAR(50) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Cortes_Restaurante DEFAULT N'default',
    Responsable NVARCHAR(100) NOT NULL CONSTRAINT DF_Cortes_Responsable DEFAULT N'Administrador',
    InicioPeriodo DATETIMEOFFSET NOT NULL,
    FinPeriodo DATETIMEOFFSET NOT NULL,
    FondoInicial DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_Fondo DEFAULT 0.00,
    EfectivoContado DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_Contado DEFAULT 0.00,
    TotalEfectivo DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_TotalEfectivo DEFAULT 0.00,
    TotalTerminal DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_TotalTerminal DEFAULT 0.00,
    TotalTransferencia DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_TotalTransfer DEFAULT 0.00,
    TotalVentas DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_TotalVentas DEFAULT 0.00,
    DiferenciaEfectivo DECIMAL(12,2) NOT NULL CONSTRAINT DF_Cortes_Diferencia DEFAULT 0.00,
    CantidadPedidos INT NOT NULL CONSTRAINT DF_Cortes_CantPedidos DEFAULT 0,
    Notas NVARCHAR(MAX) NULL,
    Timestamp DATETIMEOFFSET NOT NULL CONSTRAINT DF_Cortes_Timestamp DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_CortesCaja PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_CortesCaja_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 13. TABLA: dbo.empleados
--     Equipo Operativo, Puestos y Credenciales PIN de Seguridad
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.empleados (
    Id INT IDENTITY(1,1) NOT NULL,
    RestauranteId NVARCHAR(50) NOT NULL CONSTRAINT DF_Empleados_Restaurante DEFAULT N'default',
    Nombre NVARCHAR(100) NOT NULL,
    Rol NVARCHAR(50) NOT NULL CONSTRAINT DF_Empleados_Rol DEFAULT N'Mesero',
    PinHash NVARCHAR(255) NOT NULL,
    Activo BIT NOT NULL CONSTRAINT DF_Empleados_Activo DEFAULT 1,
    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Empleados_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT PK_empleados PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_empleados_Restaurantes FOREIGN KEY (RestauranteId)
        REFERENCES dbo.Restaurantes (Id) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 14. ÍNDICES DE RENDIMIENTO RELACIONAL PARA CONSULTAS RÁPIDAS
-- ------------------------------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_Categorias_Restaurante ON dbo.Categorias (RestauranteId, Orden ASC);
CREATE NONCLUSTERED INDEX IX_Platillos_Categoria ON dbo.Platillos (CategoriaId, Disponible);
CREATE NONCLUSTERED INDEX IX_Platillos_Destacado ON dbo.Platillos (Destacado, Disponible);
CREATE NONCLUSTERED INDEX IX_PlatilloExtras_PlatilloId ON dbo.PlatilloExtras (PlatilloId, Activo);
CREATE NONCLUSTERED INDEX IX_SesionesMesa_Rest_Mesa ON dbo.SesionesMesa (RestauranteId, NombreMesa);
CREATE NONCLUSTERED INDEX IX_Pedidos_Rest_Estado ON dbo.Pedidos (RestauranteId, Estado);
CREATE NONCLUSTERED INDEX IX_Pedidos_Mesa ON dbo.Pedidos (Mesa, Cerrada);
CREATE NONCLUSTERED INDEX IX_Pedidos_CreadoEn ON dbo.Pedidos (CreadoEn DESC);
CREATE NONCLUSTERED INDEX IX_PedidoItems_PedidoId ON dbo.PedidoItems (PedidoId);
CREATE NONCLUSTERED INDEX IX_HistorialEstado_PedidoId ON dbo.HistorialEstadoPedido (PedidoId, Timestamp ASC);
CREATE NONCLUSTERED INDEX IX_Llamados_Rest_Estado ON dbo.LlamadosMesero (RestauranteId, Estado);
CREATE NONCLUSTERED INDEX IX_Cortes_Rest_Fecha ON dbo.CortesCaja (RestauranteId, Timestamp DESC);
GO

-- ------------------------------------------------------------------------------
-- 15. CARGA DE DATOS INICIALES (SEED DATA DEL SISTEMA DGUSTAR)
-- ------------------------------------------------------------------------------

-- 1. Restaurante Principal y Parámetros
INSERT INTO dbo.Restaurantes (
    Id, Nombre, Moneda, SimboloMoneda, Telefono, Direccion, Activo,
    GoogleMapsUrl, GoogleMapsReviewUrl, GoogleMapsRating, GoogleMapsReviewsCount,
    GooglePlaceId, GoogleReviewsClicks, PinAdmin, PinStaff
)
VALUES (
    N'default',
    N'DGustAR Gastronomía & AR',
    N'MXN',
    N'$',
    N'+52 55 1234 5678',
    N'Av. Tecnológica 100, Col. Innovación Gastronómica',
    1,
    N'https://www.google.com/maps/place/?q=place_id:ChIJ4dnUvJC9YoYR34PKM6Icy4s',
    N'https://search.google.com/local/writereview?placeid=ChIJ4dnUvJC9YoYR34PKM6Icy4s',
    4.1,
    843,
    N'ChIJ4dnUvJC9YoYR34PKM6Icy4s',
    1,
    N'1234',
    N'1111'
);

-- 2. Categorías del Menú (Con Emojis Unicode Compatibles)
INSERT INTO dbo.Categorias (Id, RestauranteId, Nombre, Slug, Icono, Emoji, Orden, Activo)
VALUES
    (N'cat-burgers',   N'default', N'Hamburguesas Gourmet', N'hamburguesas', N'lunch_dining', N'🍔', 1, 1),
    (N'cat-pizzas',    N'default', N'Pizzas Artesanales',   N'pizzas',       N'local_pizza',  N'🍕', 2, 1),
    (N'cat-snacks',    N'default', N'Snacks & Papas',       N'snacks',       N'fastfood',     N'🍟', 3, 1),
    (N'cat-postres',   N'default', N'Postres de Autor',     N'postres',      N'cake',         N'🍰', 4, 1),
    (N'cat-bebidas',   N'default', N'Bebidas & Mocktails',  N'bebidas',      N'local_bar',    N'🥤', 5, 1),
    (N'cat-cafe',      N'default', N'Café de Especialidad', N'cafe',         N'coffee',       N'☕', 6, 1),
    (N'cat-221f74f9',  N'default', N'especialistas',        N'especialistas',N'category',     N'🍽️', 7, 1);

-- 3. Platillos y Experiencias 3D AR
INSERT INTO dbo.Platillos (
    Id, RestauranteId, CategoriaId, Nombre, Descripcion, PrecioBase, ImagenUrl, Modelo3dUrl,
    TiempoPreparacionMin, EscalaRealMetros, Disponible, Destacado, Alergenos, IngredientesBaseJson
)
VALUES
    (
        N'prod-burger-clasica',
        N'default',
        N'cat-burgers',
        N'Smash Burger Doble carne',
        N'Doble medallón de 100g smash caramelizado, queso americano fundido, tiras de tocino crocante, pepinillos encurtidos y aderezo especial de la casa en pan brioche tostado con mantequilla.',
        139.00,
        N'https://images.unsplash.com/photo-1568901346375-23c9450c58cd?auto=format&fit=crop&w=800&q=80',
        N'/models/burger.glb',
        12,
        0.20,
        1,
        1,
        N'Gluten, Lácteos',
        N'["Doble medallón res 100g","Queso americano","Tocino crocante","Pepinillos","Pan brioche"]'
    ),
    (
        N'prod-pizza-pepperoni',
        N'default',
        N'cat-pizzas',
        N'Pizza Pepperoni Suprema',
        N'Masa madre fermentada 48 horas, salsa de tomate San Marzano, mozzarella fior di latte y pepperoni artesanal.',
        189.00,
        N'https://images.unsplash.com/photo-1628840042765-356cda07504e?auto=format&fit=crop&w=800&q=80',
        N'/models/pizza.glb',
        18,
        0.30,
        1,
        1,
        N'Gluten, Lácteos',
        N'["Masa madre 48h","Tomates San Marzano","Queso mozzarella","Pepperoni artesanal"]'
    ),
    (
        N'prod-papas-trufa',
        N'default',
        N'cat-pizzas',
        N'Papas Rústicas',
        N'Corte grueso con piel, doble cocción crujiente, aceite de trufa blanca, parmesano y perejil fresco.',
        100.00,
        N'/uploads/asset-633213d609054d849af3b7fc091e6758.png',
        N'/models/fries.glb',
        10,
        0.18,
        1,
        1,
        N'Lácteos',
        N'["Papas rústicas","Aceite de trufa blanca","Parmesano Reggiano 24 meses","Perejil picado","Sal de mar"]'
    ),
    (
        N'prod-volcan-chocolate',
        N'default',
        N'cat-postres',
        N'Volcán de Chocolate',
        N'Bizcocho tibio de chocolate amargo 70% con corazón líquido fundente, acompañado de helado artesanal de vainilla Bourbon de Madagascar.',
        89.00,
        N'https://images.unsplash.com/photo-1606313564200-e75d5e30476c?auto=format&fit=crop&w=800&q=80',
        N'/models/dessert.glb',
        12,
        0.16,
        1,
        1,
        N'Gluten, Lácteos, Huevo',
        N'["Chocolate amargo 70%","Helado de vainilla Bourbon","Frutos rojos frescos"]'
    ),
    (
        N'prod-mocktail-berries',
        N'default',
        N'cat-bebidas',
        N'Mocktail Frutos Rojos',
        N'Infusión fresca de frutos del bosque macerados con limón eureka, jarabe artesanal de albahaca morada, soda artesanal y hielo cristalino.',
        65.00,
        N'https://images.unsplash.com/photo-1551024709-8f23befc6f87?auto=format&fit=crop&w=800&q=80',
        N'/models/drink.glb',
        5,
        0.14,
        1,
        0,
        N'',
        N'["Frambuesas","Moras azules","Albahaca morada","Limón eureka","Soda artesanal"]'
    ),
    (
        N'prod-32f11ac0',
        N'default',
        N'cat-burgers',
        N'Tacos al Pastor',
        N'Deliciosos tacos de cerdo marinado al pastor con piña asada, cilantro y cebolla sobre tortillas de maíz recién hechas.',
        99.00,
        N'https://images.unsplash.com/photo-1551504734-5ee1c4a1479b?auto=format&fit=crop&w=800&q=80',
        NULL,
        10,
        0.20,
        1,
        0,
        N'',
        N'["Carne de cerdo al pastor","Tortillas de maíz","Cebolla","Cilantro","Piña asada"]'
    );

-- 4. Extras Configurables
INSERT INTO dbo.PlatilloExtras (Id, PlatilloId, Nombre, PrecioExtra, Activo)
VALUES
    (N'ext-extra-queso',   N'prod-pizza-pepperoni', N'Extra Queso Mozzarella', 30.00, 1),
    (N'ext-orilla-queso',  N'prod-pizza-pepperoni', N'Orilla Rellena de Queso', 35.00, 1),
    (N'ext-tocino-burger', N'prod-burger-clasica',  N'Tocino Crujiente Extra',  25.00, 1),
    (N'ext-doble-carne',   N'prod-burger-clasica',  N'Medallón de Res Extra',   45.00, 1);

-- 5. Mesas Físicas y Códigos QR
INSERT INTO dbo.SesionesMesa (RestauranteId, NombreMesa, CodigoQR, Estado, Comensales, Activa)
VALUES
    (N'default', N'Mesa 01', N'/menu?mesa=Mesa+01', N'DISPONIBLE', 4, 1),
    (N'default', N'Mesa 02', N'/menu?mesa=Mesa+02', N'DISPONIBLE', 4, 1),
    (N'default', N'baño',    N'/menu?mesa=ba%C3%B1o',  N'DISPONIBLE', 2, 1);

-- 6. Personal y Empleados
INSERT INTO dbo.empleados (RestauranteId, Nombre, Rol, PinHash, Activo)
VALUES
    (N'default', N'Gerencia General', N'Administrador', N'1234', 1),
    (N'default', N'Servicio y Meseros', N'Mesero',      N'1111', 1),
    (N'default', N'Jefe de Cocina KDS', N'Cocina',      N'2222', 1);

-- 7. Comanda de Demostración
INSERT INTO dbo.Pedidos (
    Id, OrderToken, RestauranteId, Mesa, TipoCuenta, Comensal, TipoServicio,
    MetodoPago, Estado, EstadoPago, MontoPagado, SaldoPendiente, Subtotal, Total,
    NotasCliente, Cerrada, CreadoEn, ActualizadoEn
)
VALUES (
    N'ord-demo-001',
    N'token-demo-12345',
    N'default',
    N'Mesa 01',
    N'MESA',
    N'Cuenta General de la Mesa',
    N'En Mesa',
    N'Pago al mesero en mesa',
    N'ENTREGADO',
    N'PAGADO',
    239.00,
    0.00,
    239.00,
    239.00,
    N'Hamburguesa sin pepinillos por favor',
    1,
    SYSDATETIMEOFFSET(),
    SYSDATETIMEOFFSET()
);

-- 8. Items de la Comanda
INSERT INTO dbo.PedidoItems (
    PedidoId, ProductoId, Nombre, Imagen, Cantidad, PrecioBase, PrecioUnitario, Subtotal, Notas
)
VALUES
    (N'ord-demo-001', N'prod-burger-clasica', N'Smash Burger Doble carne', N'https://images.unsplash.com/photo-1568901346375-23c9450c58cd', 1, 139.00, 139.00, 139.00, N'Sin pepinillos'),
    (N'ord-demo-001', N'prod-papas-trufa',     N'Papas Rústicas',          N'/uploads/asset-633213d609054d849af3b7fc091e6758.png', 1, 100.00, 100.00, 100.00, NULL);

-- 9. Historial de Estados de Cocina KDS
INSERT INTO dbo.HistorialEstadoPedido (PedidoId, Estado, CambiadoPor)
VALUES
    (N'ord-demo-001', N'NUEVO',          N'Cliente desde PWA'),
    (N'ord-demo-001', N'ACEPTADO',        N'Personal de Barra'),
    (N'ord-demo-001', N'EN_PREPARACION',  N'Cocina KDS'),
    (N'ord-demo-001', N'LISTO',           N'Cocina KDS'),
    (N'ord-demo-001', N'ENTREGADO',       N'Mesero');

-- 10. Corte de Caja de Ejemplo
INSERT INTO dbo.CortesCaja (
    Id, RestauranteId, Responsable, InicioPeriodo, FinPeriodo,
    FondoInicial, EfectivoContado, TotalEfectivo, TotalTerminal, TotalTransferencia,
    TotalVentas, DiferenciaEfectivo, CantidadPedidos, Notas
)
VALUES (
    N'corte-demo-001',
    N'default',
    N'Administrador',
    DATEADD(HOUR, -8, SYSDATETIMEOFFSET()),
    SYSDATETIMEOFFSET(),
    1000.00,
    1239.00,
    239.00,
    0.00,
    0.00,
    239.00,
    0.00,
    1,
    N'Corte de turno inicial con comanda de demostración DGustAR.'
);
GO

-- ------------------------------------------------------------------------------
-- 16. PROCEDIMIENTOS ALMACENADOS OFICIALES
-- ------------------------------------------------------------------------------

-- 16.1 Consulta del Menú Completo para la PWA
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarMenuCompleto
AS
BEGIN
    SET NOCOUNT ON;

    -- Información del Restaurante
    SELECT TOP 1
        Nombre, Moneda, SimboloMoneda,
        ISNULL(Telefono,'') AS Telefono,
        ISNULL(Direccion,'') AS Direccion,
        Activo,
        ISNULL(GoogleMapsUrl,'') AS GoogleMapsUrl,
        ISNULL(GoogleMapsReviewUrl,'') AS GoogleMapsReviewUrl,
        GoogleMapsRating, GoogleMapsReviewsCount,
        ISNULL(GooglePlaceId,'') AS GooglePlaceId,
        GoogleReviewsClicks
    FROM dbo.Restaurantes;

    -- Categorías Activas ordenadas
    SELECT Id, Nombre, Slug, Icono, Emoji, Orden, Activo
    FROM dbo.Categorias
    WHERE Activo = 1
    ORDER BY Orden ASC;

    -- Platillos Disponibles
    SELECT Id, CategoriaId, Nombre, ISNULL(Descripcion,'') AS Descripcion,
           PrecioBase, ISNULL(ImagenUrl,'') AS ImagenUrl, Modelo3dUrl,
           TiempoPreparacionMin, Disponible, Destacado,
           ISNULL(Alergenos,'') AS Alergenos,
           ISNULL(IngredientesBaseJson,'') AS IngredientesBaseJson
    FROM dbo.Platillos
    WHERE Disponible = 1
    ORDER BY Destacado DESC, Nombre ASC;

    -- Extras de Platillos Activos
    SELECT Id, PlatilloId, Nombre, PrecioExtra
    FROM dbo.PlatilloExtras
    WHERE Activo = 1;
END;
GO

-- 16.2 Consulta de Platillo por ID
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarPlatilloPorId
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        Id, CategoriaId, Nombre, ISNULL(Descripcion,'') AS Descripcion,
        PrecioBase, ISNULL(ImagenUrl,'') AS ImagenUrl, Modelo3dUrl,
        TiempoPreparacionMin, Disponible, Destacado,
        ISNULL(Alergenos,'') AS Alergenos,
        ISNULL(IngredientesBaseJson,'') AS IngredientesBaseJson
    FROM dbo.Platillos
    WHERE Id = @Id;

    SELECT Id, PlatilloId, Nombre, PrecioExtra
    FROM dbo.PlatilloExtras
    WHERE PlatilloId = @Id AND Activo = 1;
END;
GO

-- 16.3 Alternar Disponibilidad de Platillo
CREATE OR ALTER PROCEDURE dbo.sp_AlternarDisponibilidadPlatillo
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Platillos
    SET Disponible = CASE WHEN Disponible = 1 THEN 0 ELSE 1 END,
        UpdatedAt = SYSDATETIMEOFFSET()
    WHERE Id = @Id;

    SELECT Disponible FROM dbo.Platillos WHERE Id = @Id;
END;
GO

-- ------------------------------------------------------------------------------
-- 17. VERIFICACIÓN FINAL: CONTEO DE REGISTROS POR TABLA
-- ------------------------------------------------------------------------------
SELECT 
    t.name AS [Tabla en SSMS],
    SUM(p.rows) AS [Registros Cargados]
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY t.name ASC;
GO

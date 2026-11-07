-- ==============================================================================
-- PROYECTO: DGustAR - Gastronomía & Realidad Aumentada (PWA)
-- SCRIPT: Creación de Base de Datos y Esquema Relacional en Español para SQL Server
-- OBJETIVO: Generar el Diagrama de Base de Datos (Database Diagram) en SSMS
-- IDIOMA: Español (Nombres de tablas, columnas, restricciones y registros)
-- COMPATIBILIDAD: Microsoft SQL Server 2016, 2019, 2022, Azure SQL & SSMS
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. CREACIÓN O REINICIO DE LA BASE DE DATOS
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
    -- Si no existe el usuario 'sa' o es Azure SQL, continúa de manera segura
END CATCH
GO

-- ------------------------------------------------------------------------------
-- 2. ELIMINACIÓN DE TABLAS PREVIAS (ORDEN INVERSO DE DEPENDENCIAS)
-- ------------------------------------------------------------------------------
IF OBJECT_ID('dbo.extras_detalle_pedido', 'U') IS NOT NULL DROP TABLE dbo.extras_detalle_pedido;
IF OBJECT_ID('dbo.historial_estados_pedido', 'U') IS NOT NULL DROP TABLE dbo.historial_estados_pedido;
IF OBJECT_ID('dbo.resenas', 'U') IS NOT NULL DROP TABLE dbo.resenas;
IF OBJECT_ID('dbo.pagos', 'U') IS NOT NULL DROP TABLE dbo.pagos;
IF OBJECT_ID('dbo.detalles_pedido', 'U') IS NOT NULL DROP TABLE dbo.detalles_pedido;
IF OBJECT_ID('dbo.pedidos', 'U') IS NOT NULL DROP TABLE dbo.pedidos;
IF OBJECT_ID('dbo.extras_producto', 'U') IS NOT NULL DROP TABLE dbo.extras_producto;
IF OBJECT_ID('dbo.productos', 'U') IS NOT NULL DROP TABLE dbo.productos;
IF OBJECT_ID('dbo.categorias', 'U') IS NOT NULL DROP TABLE dbo.categorias;
IF OBJECT_ID('dbo.mesas', 'U') IS NOT NULL DROP TABLE dbo.mesas;
IF OBJECT_ID('dbo.cortes_caja', 'U') IS NOT NULL DROP TABLE dbo.cortes_caja;
IF OBJECT_ID('dbo.restaurantes', 'U') IS NOT NULL DROP TABLE dbo.restaurantes;
GO

-- ------------------------------------------------------------------------------
-- 3. TABLA: restaurantes (Configuración General y Parámetros del Establecimiento)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.restaurantes (
    id_restaurante NVARCHAR(50) NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    moneda NVARCHAR(10) NOT NULL CONSTRAINT DF_restaurantes_moneda DEFAULT N'MXN',
    simbolo_moneda NVARCHAR(5) NOT NULL CONSTRAINT DF_restaurantes_simbolo DEFAULT N'$',
    telefono NVARCHAR(30) NULL,
    direccion NVARCHAR(255) NULL,
    activo BIT NOT NULL CONSTRAINT DF_restaurantes_activo DEFAULT 1,
    pin_admin_hash NVARCHAR(255) NOT NULL,
    pin_staff_hash NVARCHAR(255) NOT NULL,
    token_sesion_admin NVARCHAR(255) NULL,
    token_sesion_staff NVARCHAR(255) NULL,
    fecha_ultimo_corte DATETIME2 NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_restaurantes_creado DEFAULT SYSDATETIME(),
    actualizado_en DATETIME2 NOT NULL CONSTRAINT DF_restaurantes_actualizado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_restaurantes PRIMARY KEY CLUSTERED (id_restaurante)
);
GO

-- ------------------------------------------------------------------------------
-- 4. TABLA: mesas (Mesas Físicas y Puntos de Servicio del Restaurante)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.mesas (
    id_mesa INT IDENTITY(1,1) NOT NULL,
    id_restaurante NVARCHAR(50) NOT NULL,
    nombre_mesa NVARCHAR(50) NOT NULL,
    capacidad INT NOT NULL CONSTRAINT DF_mesas_capacidad DEFAULT 4,
    activa BIT NOT NULL CONSTRAINT DF_mesas_activa DEFAULT 1,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_mesas_creado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_mesas PRIMARY KEY CLUSTERED (id_mesa),
    CONSTRAINT FK_mesas_restaurantes FOREIGN KEY (id_restaurante)
        REFERENCES dbo.restaurantes (id_restaurante) ON DELETE CASCADE,
    CONSTRAINT UQ_mesas_nombre_rest UNIQUE (id_restaurante, nombre_mesa)
);
GO

-- ------------------------------------------------------------------------------
-- 5. TABLA: categorias (Categorías del Menú de Comidas y Bebidas)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.categorias (
    id_categoria NVARCHAR(50) NOT NULL,
    nombre NVARCHAR(100) NOT NULL,
    slug NVARCHAR(100) NOT NULL,
    icono NVARCHAR(50) NOT NULL CONSTRAINT DF_categorias_icono DEFAULT N'restaurant',
    emoji NVARCHAR(10) NOT NULL CONSTRAINT DF_categorias_emoji DEFAULT N'🍽️',
    orden_visualizacion INT NOT NULL CONSTRAINT DF_categorias_orden DEFAULT 0,
    activa BIT NOT NULL CONSTRAINT DF_categorias_activa DEFAULT 1,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_categorias_creado DEFAULT SYSDATETIME(),
    actualizado_en DATETIME2 NOT NULL CONSTRAINT DF_categorias_actualizado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_categorias PRIMARY KEY CLUSTERED (id_categoria)
);
GO

-- ------------------------------------------------------------------------------
-- 6. TABLA: productos (Catálogo de Platillos, Bebidas y Modelos 3D en AR)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.productos (
    id_producto NVARCHAR(50) NOT NULL,
    id_categoria NVARCHAR(50) NOT NULL,
    nombre NVARCHAR(150) NOT NULL,
    descripcion NVARCHAR(MAX) NULL,
    precio_base DECIMAL(12,2) NOT NULL,
    imagen_url NVARCHAR(500) NULL,
    modelo_3d_url NVARCHAR(500) NULL,
    tiempo_preparacion_min INT NOT NULL CONSTRAINT DF_productos_tiempo DEFAULT 15,
    disponible BIT NOT NULL CONSTRAINT DF_productos_disponible DEFAULT 1,
    destacado BIT NOT NULL CONSTRAINT DF_productos_destacado DEFAULT 0,
    alergenos NVARCHAR(255) NULL,
    ingredientes_base NVARCHAR(MAX) NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_productos_creado DEFAULT SYSDATETIME(),
    actualizado_en DATETIME2 NOT NULL CONSTRAINT DF_productos_actualizado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_productos PRIMARY KEY CLUSTERED (id_producto),
    CONSTRAINT FK_productos_categorias FOREIGN KEY (id_categoria)
        REFERENCES dbo.categorias (id_categoria) ON DELETE CASCADE,
    CONSTRAINT CK_productos_precio CHECK (precio_base >= 0)
);
GO

-- ------------------------------------------------------------------------------
-- 7. TABLA: extras_producto (Ingredientes Adicionales Configurables por Platillo)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.extras_producto (
    id_extra NVARCHAR(50) NOT NULL,
    id_producto NVARCHAR(50) NOT NULL,
    nombre NVARCHAR(100) NOT NULL,
    precio DECIMAL(12,2) NOT NULL CONSTRAINT DF_extras_precio DEFAULT 0.00,
    disponible BIT NOT NULL CONSTRAINT DF_extras_disponible DEFAULT 1,
    CONSTRAINT PK_extras_producto PRIMARY KEY CLUSTERED (id_extra),
    CONSTRAINT FK_extras_productos FOREIGN KEY (id_producto)
        REFERENCES dbo.productos (id_producto) ON DELETE CASCADE,
    CONSTRAINT CK_extras_precio CHECK (precio >= 0)
);
GO

-- ------------------------------------------------------------------------------
-- 8. TABLA: pedidos (Comandas Generadas por los Comensales desde la PWA)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.pedidos (
    id_pedido NVARCHAR(50) NOT NULL,
    token_pedido UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_pedidos_token DEFAULT NEWID(),
    id_restaurante NVARCHAR(50) NOT NULL,
    id_mesa INT NULL,
    nombre_mesa NVARCHAR(50) NOT NULL,
    tipo_cuenta NVARCHAR(20) NOT NULL CONSTRAINT DF_pedidos_cuenta DEFAULT N'MESA',
    nombre_cliente NVARCHAR(100) NOT NULL CONSTRAINT DF_pedidos_cliente DEFAULT N'Cuenta General de la Mesa',
    tipo_servicio NVARCHAR(30) NOT NULL CONSTRAINT DF_pedidos_servicio DEFAULT N'En Mesa',
    preferencia_pago NVARCHAR(50) NULL,
    estado NVARCHAR(30) NOT NULL CONSTRAINT DF_pedidos_estado DEFAULT N'NUEVO',
    estado_pago NVARCHAR(20) NOT NULL CONSTRAINT DF_pedidos_estado_pago DEFAULT N'PENDIENTE',
    subtotal DECIMAL(12,2) NOT NULL CONSTRAINT DF_pedidos_subtotal DEFAULT 0.00,
    total DECIMAL(12,2) NOT NULL CONSTRAINT DF_pedidos_total DEFAULT 0.00,
    total_pagado DECIMAL(12,2) NOT NULL CONSTRAINT DF_pedidos_pagado DEFAULT 0.00,
    saldo_pendiente DECIMAL(12,2) NOT NULL CONSTRAINT DF_pedidos_saldo DEFAULT 0.00,
    notas_cliente NVARCHAR(500) NULL,
    cerrado BIT NOT NULL CONSTRAINT DF_pedidos_cerrado DEFAULT 0,
    cerrado_en DATETIME2 NULL,
    creado_en DATETIME2 NOT NULL CONSTRAINT DF_pedidos_creado DEFAULT SYSDATETIME(),
    actualizado_en DATETIME2 NOT NULL CONSTRAINT DF_pedidos_actualizado DEFAULT SYSDATETIME(),
    CONSTRAINT PK_pedidos PRIMARY KEY CLUSTERED (id_pedido),
    CONSTRAINT UQ_pedidos_token UNIQUE (token_pedido),
    CONSTRAINT FK_pedidos_restaurantes FOREIGN KEY (id_restaurante)
        REFERENCES dbo.restaurantes (id_restaurante) ON DELETE NO ACTION,
    CONSTRAINT FK_pedidos_mesas FOREIGN KEY (id_mesa)
        REFERENCES dbo.mesas (id_mesa) ON DELETE SET NULL,
    CONSTRAINT CK_pedidos_estado CHECK (estado IN (N'NUEVO', N'ACEPTADO', N'EN_PREPARACION', N'LISTO', N'ENTREGADO', N'CANCELADO')),
    CONSTRAINT CK_pedidos_estado_pago CHECK (estado_pago IN (N'PENDIENTE', N'PARCIAL', N'PAGADO'))
);
GO

-- ------------------------------------------------------------------------------
-- 9. TABLA: detalles_pedido (Platillos y Bebidas que Integran Cada Comanda)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.detalles_pedido (
    id_detalle INT IDENTITY(1,1) NOT NULL,
    id_pedido NVARCHAR(50) NOT NULL,
    id_producto NVARCHAR(50) NULL,
    nombre_producto NVARCHAR(150) NOT NULL,
    cantidad INT NOT NULL CONSTRAINT DF_detalles_cantidad DEFAULT 1,
    precio_base DECIMAL(12,2) NOT NULL,
    precio_unitario DECIMAL(12,2) NOT NULL,
    subtotal DECIMAL(12,2) NOT NULL,
    ingredientes_removidos NVARCHAR(MAX) NULL,
    CONSTRAINT PK_detalles_pedido PRIMARY KEY CLUSTERED (id_detalle),
    CONSTRAINT FK_detalles_pedidos FOREIGN KEY (id_pedido)
        REFERENCES dbo.pedidos (id_pedido) ON DELETE CASCADE,
    CONSTRAINT FK_detalles_productos FOREIGN KEY (id_producto)
        REFERENCES dbo.productos (id_producto) ON DELETE SET NULL,
    CONSTRAINT CK_detalles_cantidad CHECK (cantidad > 0)
);
GO

-- ------------------------------------------------------------------------------
-- 10. TABLA: extras_detalle_pedido (Extras Agregados a Cada Platillo de la Orden)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.extras_detalle_pedido (
    id_detalle_extra INT IDENTITY(1,1) NOT NULL,
    id_detalle INT NOT NULL,
    id_extra NVARCHAR(50) NULL,
    nombre_extra NVARCHAR(100) NOT NULL,
    precio DECIMAL(12,2) NOT NULL,
    CONSTRAINT PK_extras_detalle_pedido PRIMARY KEY CLUSTERED (id_detalle_extra),
    CONSTRAINT FK_extras_detalle_detalles FOREIGN KEY (id_detalle)
        REFERENCES dbo.detalles_pedido (id_detalle) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 11. TABLA: historial_estados_pedido (Auditoría KDS y Trazabilidad de Cocina)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.historial_estados_pedido (
    id_historial INT IDENTITY(1,1) NOT NULL,
    id_pedido NVARCHAR(50) NOT NULL,
    estado NVARCHAR(30) NOT NULL,
    cambiado_en DATETIME2 NOT NULL CONSTRAINT DF_historial_cambiado DEFAULT SYSDATETIME(),
    cambiado_por NVARCHAR(50) NULL CONSTRAINT DF_historial_por DEFAULT N'Sistema',
    CONSTRAINT PK_historial_estados_pedido PRIMARY KEY CLUSTERED (id_historial),
    CONSTRAINT FK_historial_pedidos FOREIGN KEY (id_pedido)
        REFERENCES dbo.pedidos (id_pedido) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 12. TABLA: pagos (Cobros, Transacciones y Liquidaciones de Comandas)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.pagos (
    id_pago NVARCHAR(50) NOT NULL,
    id_pedido NVARCHAR(50) NOT NULL,
    token_pedido UNIQUEIDENTIFIER NOT NULL,
    monto DECIMAL(12,2) NOT NULL,
    metodo_pago NVARCHAR(30) NOT NULL,
    recibido_por NVARCHAR(100) NOT NULL CONSTRAINT DF_pagos_por DEFAULT N'Administrador',
    codigo_referencia NVARCHAR(100) NULL,
    notas NVARCHAR(255) NULL,
    fecha_pago DATETIME2 NOT NULL CONSTRAINT DF_pagos_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT PK_pagos PRIMARY KEY CLUSTERED (id_pago),
    CONSTRAINT FK_pagos_pedidos FOREIGN KEY (id_pedido)
        REFERENCES dbo.pedidos (id_pedido) ON DELETE CASCADE,
    CONSTRAINT CK_pagos_monto CHECK (monto > 0),
    CONSTRAINT CK_pagos_metodo CHECK (metodo_pago IN (N'EFECTIVO', N'TERMINAL_EXTERNA', N'TARJETA', N'TRANSFERENCIA', N'MIXTO'))
);
GO

-- ------------------------------------------------------------------------------
-- 13. TABLA: resenas (Calificaciones y Comentarios de Clientes)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.resenas (
    id_resena NVARCHAR(50) NOT NULL,
    id_pedido NVARCHAR(50) NULL,
    nombre_mesa NVARCHAR(50) NULL,
    calificacion INT NOT NULL,
    comentario NVARCHAR(MAX) NULL,
    personal_atendio NVARCHAR(100) NULL,
    etiquetas NVARCHAR(255) NULL,
    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_resenas_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT PK_resenas PRIMARY KEY CLUSTERED (id_resena),
    CONSTRAINT FK_resenas_pedidos FOREIGN KEY (id_pedido)
        REFERENCES dbo.pedidos (id_pedido) ON DELETE SET NULL,
    CONSTRAINT CK_resenas_calificacion CHECK (calificacion BETWEEN 1 AND 5)
);
GO

-- ------------------------------------------------------------------------------
-- 14. TABLA: cortes_caja (Arqueos Financieros, Turnos y Cierres de Caja)
-- ------------------------------------------------------------------------------
CREATE TABLE dbo.cortes_caja (
    id_corte NVARCHAR(50) NOT NULL,
    id_restaurante NVARCHAR(50) NOT NULL,
    inicio_periodo DATETIME2 NOT NULL,
    fin_periodo DATETIME2 NOT NULL,
    fondo_inicial DECIMAL(12,2) NOT NULL CONSTRAINT DF_cortes_fondo DEFAULT 0.00,
    total_efectivo DECIMAL(12,2) NOT NULL CONSTRAINT DF_cortes_efectivo DEFAULT 0.00,
    total_terminal DECIMAL(12,2) NOT NULL CONSTRAINT DF_cortes_terminal DEFAULT 0.00,
    total_transferencia DECIMAL(12,2) NOT NULL CONSTRAINT DF_cortes_transfer DEFAULT 0.00,
    total_ventas DECIMAL(12,2) NOT NULL CONSTRAINT DF_cortes_ventas DEFAULT 0.00,
    cantidad_pedidos INT NOT NULL CONSTRAINT DF_cortes_pedidos DEFAULT 0,
    usuario_cajero NVARCHAR(100) NOT NULL CONSTRAINT DF_cortes_cajero DEFAULT N'Administrador',
    notas NVARCHAR(MAX) NULL,
    fecha_creacion DATETIME2 NOT NULL CONSTRAINT DF_cortes_fecha DEFAULT SYSDATETIME(),
    CONSTRAINT PK_cortes_caja PRIMARY KEY CLUSTERED (id_corte),
    CONSTRAINT FK_cortes_restaurantes FOREIGN KEY (id_restaurante)
        REFERENCES dbo.restaurantes (id_restaurante) ON DELETE CASCADE
);
GO

-- ------------------------------------------------------------------------------
-- 15. ÍNDICES DE RENDIMIENTO PARA CONSULTAS Y RELACIONES
-- ------------------------------------------------------------------------------
CREATE NONCLUSTERED INDEX IX_mesas_restaurante ON dbo.mesas (id_restaurante);
CREATE NONCLUSTERED INDEX IX_productos_categoria ON dbo.productos (id_categoria);
CREATE NONCLUSTERED INDEX IX_extras_producto ON dbo.extras_producto (id_producto);
CREATE NONCLUSTERED INDEX IX_pedidos_rest_estado ON dbo.pedidos (id_restaurante, estado);
CREATE NONCLUSTERED INDEX IX_pedidos_mesa ON dbo.pedidos (nombre_mesa);
CREATE NONCLUSTERED INDEX IX_pedidos_creado ON dbo.pedidos (creado_en DESC);
CREATE NONCLUSTERED INDEX IX_detalles_pedido ON dbo.detalles_pedido (id_pedido);
CREATE NONCLUSTERED INDEX IX_detalles_producto ON dbo.detalles_pedido (id_producto);
CREATE NONCLUSTERED INDEX IX_extras_detalle ON dbo.extras_detalle_pedido (id_detalle);
CREATE NONCLUSTERED INDEX IX_historial_pedido ON dbo.historial_estados_pedido (id_pedido);
CREATE NONCLUSTERED INDEX IX_pagos_pedido ON dbo.pagos (id_pedido);
CREATE NONCLUSTERED INDEX IX_pagos_fecha ON dbo.pagos (fecha_pago DESC);
CREATE NONCLUSTERED INDEX IX_resenas_pedido ON dbo.resenas (id_pedido);
CREATE NONCLUSTERED INDEX IX_cortes_rest_fecha ON dbo.cortes_caja (id_restaurante, fecha_creacion DESC);
GO

-- ------------------------------------------------------------------------------
-- 16. INSERCIÓN DE DATOS DE EJEMPLO EN ESPAÑOL (SEED DATA DEL SISTEMA)
-- ------------------------------------------------------------------------------

-- 1. Restaurante Principal
INSERT INTO dbo.restaurantes (
    id_restaurante, nombre, moneda, simbolo_moneda, telefono, direccion, activo, pin_admin_hash, pin_staff_hash
)
VALUES (
    N'rest-principal',
    N'DGustAR Gastronomía & AR',
    N'MXN',
    N'$',
    N'+52 55 1234 5678',
    N'Av. Tecnológica 100, Col. Innovación Gastronómica',
    1,
    N'1234',
    N'1111'
);

-- 2. Mesas Físicas del Restaurante
INSERT INTO dbo.mesas (id_restaurante, nombre_mesa, capacidad, activa)
VALUES 
    (N'rest-principal', N'Mesa 01', 4, 1),
    (N'rest-principal', N'Mesa 02', 4, 1),
    (N'rest-principal', N'Mesa 03', 2, 1),
    (N'rest-principal', N'Mesa 04', 6, 1),
    (N'rest-principal', N'Mesa 05', 4, 1),
    (N'rest-principal', N'Mesa 06', 4, 1),
    (N'rest-principal', N'Mesa 07', 8, 1),
    (N'rest-principal', N'Mesa 08', 2, 1),
    (N'rest-principal', N'Mesa 09', 4, 1),
    (N'rest-principal', N'Mesa 10', 4, 1),
    (N'rest-principal', N'Mesa 11', 2, 1),
    (N'rest-principal', N'Mesa 12', 4, 1),
    (N'rest-principal', N'Para Llevar', 10, 1);

-- 3. Categorías del Menú
INSERT INTO dbo.categorias (id_categoria, nombre, slug, icono, emoji, orden_visualizacion, activa)
VALUES
    (N'cat-hamburguesas', N'Hamburguesas Gourmet', N'hamburguesas', N'lunch_dining', N'🍔', 1, 1),
    (N'cat-pizzas', N'Pizzas Artesanales', N'pizzas', N'local_pizza', N'🍕', 2, 1),
    (N'cat-snacks', N'Snacks & Papas', N'snacks', N'fastfood', N'🍟', 3, 1),
    (N'cat-postres', N'Postres de Autor', N'postres', N'cake', N'🍰', 4, 1),
    (N'cat-bebidas', N'Bebidas & Mocktails', N'bebidas', N'local_bar', N'🥤', 5, 1),
    (N'cat-cafe', N'Café de Especialidad', N'cafe', N'coffee', N'☕', 6, 1);

-- 4. Productos del Menú con Experiencia AR
INSERT INTO dbo.productos (
    id_producto, id_categoria, nombre, descripcion, precio_base, imagen_url, modelo_3d_url, tiempo_preparacion_min, disponible, destacado, alergenos, ingredientes_base
)
VALUES
    (
        N'prod-burger-bbq',
        N'cat-hamburguesas',
        N'Hamburguesa BBQ Trufa',
        N'Carne Angus certificada de 180g, queso cheddar fundido, cebolla caramelizada al vino tinto y salsa especial de trufa sobre pan brioche artesanal.',
        149.00,
        N'https://images.unsplash.com/photo-1568901346375-23c9450c58cd?auto=format&fit=crop&w=800&q=80',
        N'/models/burger.glb',
        15,
        1,
        1,
        N'Gluten, Lácteos',
        N'Pan brioche artesanal, Carne Angus 180g, Queso cheddar, Cebolla caramelizada, Salsa de trufa'
    ),
    (
        N'prod-pizza-pepperoni',
        N'cat-pizzas',
        N'Pizza Pepperoni Suprema',
        N'Masa madre fermentada 48 horas, salsa de tomate San Marzano, abundante mozzarella fior di latte y pepperoni artesanal ligeramente ahumado con miel spicy.',
        189.00,
        N'https://images.unsplash.com/photo-1628840042765-356cda07504e?auto=format&fit=crop&w=800&q=80',
        N'/models/pizza.glb',
        18,
        1,
        1,
        N'Gluten, Lácteos',
        N'Masa madre 48h, Tomates San Marzano, Queso mozzarella, Pepperoni artesanal'
    ),
    (
        N'prod-papas-trufa',
        N'cat-snacks',
        N'Papas Rústicas Trufa & Parmesano',
        N'Papas cortadas a mano con piel, fritas a doble cocción, bañadas en aceite de trufa blanca de alba, queso parmesano Reggiano rallado y perejil fresco.',
        89.00,
        N'https://images.unsplash.com/photo-1576107232684-1279f3908594?auto=format&fit=crop&w=800&q=80',
        N'/models/fries.glb',
        10,
        1,
        1,
        N'Lácteos',
        N'Papas rústicas con piel, Aceite de trufa blanca, Queso parmesano, Perejil fresco'
    );

-- 5. Extras Personalizables
INSERT INTO dbo.extras_producto (id_extra, id_producto, nombre, precio, disponible)
VALUES
    (N'ext-tocino', N'prod-burger-bbq', N'Tocino Ahumado Crujiente', 25.00, 1),
    (N'ext-doble-queso', N'prod-burger-bbq', N'Doble Queso Cheddar Fundido', 20.00, 1),
    (N'ext-queso-extra', N'prod-pizza-pepperoni', N'Queso Mozzarella Extra', 30.00, 1),
    (N'ext-dip-trufa', N'prod-papas-trufa', N'Aderezo Especial de Trufa', 18.00, 1);

-- 6. Comanda / Pedido de Ejemplo
DECLARE @TokenPedidoDemo UNIQUEIDENTIFIER = NEWID();
INSERT INTO dbo.pedidos (
    id_pedido, token_pedido, id_restaurante, id_mesa, nombre_mesa, tipo_cuenta, nombre_cliente, tipo_servicio, status, estado_pago, subtotal, total, total_pagado, saldo_pendiente, notas_cliente
)
VALUES (
    N'ped-20261001-001',
    @TokenPedidoDemo,
    N'rest-principal',
    1,
    N'Mesa 01',
    N'MESA',
    N'Carlos Ramírez',
    N'En Mesa',
    N'ENTREGADO',
    N'PAGADO',
    238.00,
    238.00,
    238.00,
    0.00,
    N'Por favor, la hamburguesa sin cebolla caramelizada'
);

-- 7. Detalles del Pedido
INSERT INTO dbo.detalles_pedido (
    id_pedido, id_producto, nombre_producto, cantidad, precio_base, precio_unitario, subtotal, ingredientes_removidos
)
VALUES 
    (N'ped-20261001-001', N'prod-burger-bbq', N'Hamburguesa BBQ Trufa', 1, 149.00, 174.00, 174.00, N'Cebolla caramelizada'),
    (N'ped-20261001-001', N'prod-papas-trufa', N'Papas Rústicas Trufa & Parmesano', 1, 89.00, 89.00, 89.00, NULL);

-- 8. Extras Seleccionados en el Detalle
INSERT INTO dbo.extras_detalle_pedido (id_detalle, id_extra, nombre_extra, precio)
VALUES 
    (1, N'ext-tocino', N'Tocino Ahumado Crujiente', 25.00);

-- 9. Historial de Estados de la Comanda
INSERT INTO dbo.historial_estados_pedido (id_pedido, estado, cambiado_por)
VALUES 
    (N'ped-20261001-001', N'NUEVO', N'Cliente desde PWA'),
    (N'ped-20261001-001', N'ACEPTADO', N'Mesero Carlos'),
    (N'ped-20261001-001', N'EN_PREPARACION', N'Cocinero Jefe KDS'),
    (N'ped-20261001-001', N'LISTO', N'Cocinero Jefe KDS'),
    (N'ped-20261001-001', N'ENTREGADO', N'Mesero Carlos');

-- 10. Pago Registrado
INSERT INTO dbo.pagos (
    id_pago, id_pedido, token_pedido, monto, metodo_pago, recibido_por, codigo_referencia, notas
)
VALUES (
    N'pago-20261001-001',
    N'ped-20261001-001',
    @TokenPedidoDemo,
    238.00,
    N'TERMINAL_EXTERNA',
    N'Mesero Carlos',
    N'AUTH-TERMINAL-987412',
    N'Cobro realizado con tarjeta de débito en datáfono Clip'
);

-- 11. Reseña del Cliente
INSERT INTO dbo.resenas (
    id_resena, id_pedido, nombre_mesa, calificacion, comentario, personal_atendio, etiquetas
)
VALUES (
    N'res-20261001-001',
    N'ped-20261001-001',
    N'Mesa 01',
    5,
    N'Excelente sabor y fue fascinante proyectar la hamburguesa en Realidad Aumentada en la mesa antes de ordenar.',
    N'Mesero Carlos',
    N'rapido, delicioso, realidad_aumentada'
);

-- 12. Corte de Caja del Turno
INSERT INTO dbo.cortes_caja (
    id_corte, id_restaurante, inicio_periodo, fin_periodo, fondo_inicial, total_efectivo, total_terminal, total_transferencia, total_ventas, cantidad_pedidos, usuario_cajero, notas
)
VALUES (
    N'corte-20261001-turno1',
    N'rest-principal',
    DATEADD(HOUR, -8, SYSDATETIME()),
    SYSDATETIME(),
    1000.00,
    1500.00,
    2450.00,
    300.00,
    4250.00,
    18,
    N'Gerente Turno Matutino',
    N'Cierre de turno matutino cuadrado al 100% sin faltantes ni sobrantes.'
);
GO

-- ------------------------------------------------------------------------------
-- 17. CONSULTA DE VERIFICACIÓN DE LAS TABLAS CREADAS EN ESPAÑOL
-- ------------------------------------------------------------------------------
SELECT 
    t.name AS [Nombre de la Tabla],
    SUM(p.rows) AS [Cantidad de Registros]
FROM sys.tables t
INNER JOIN sys.partitions p ON t.object_id = p.object_id
WHERE p.index_id IN (0, 1)
GROUP BY t.name
ORDER BY [Nombre de la Tabla] ASC;
GO

-- ------------------------------------------------------------------------------
-- 18. PROCEDIMIENTOS ALMACENADOS (STORED PROCEDURES)
-- ------------------------------------------------------------------------------

-- 18.1 Consulta General: sp_ConsultarMenuCompleto
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarMenuCompleto
AS
BEGIN
    SET NOCOUNT ON;
    -- Categorías activas
    SELECT id_categoria, nombre, slug, icono, emoji, orden_visualizacion, activa
    FROM dbo.categorias
    WHERE activa = 1
    ORDER BY orden_visualizacion ASC;

    -- Productos disponibles
    SELECT p.id_producto, p.id_categoria, p.nombre, p.descripcion, p.precio_base,
           p.imagen_url, p.modelo_3d_url, p.tiempo_preparacion_min, p.disponible,
           p.destacado, p.alergenos, p.ingredientes_base
    FROM dbo.productos p
    WHERE p.disponible = 1
    ORDER BY p.nombre ASC;
END;
GO

-- 18.2 Consulta por ID: sp_ConsultarProductoPorId
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarProductoPorId
    @id_producto NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id_producto, id_categoria, nombre, descripcion, precio_base,
           imagen_url, modelo_3d_url, tiempo_preparacion_min, disponible,
           destacado, alergenos, ingredientes_base
    FROM dbo.productos
    WHERE id_producto = @id_producto;

    SELECT id_extra, id_producto, nombre, precio, disponible
    FROM dbo.extras_producto
    WHERE id_producto = @id_producto AND disponible = 1;
END;
GO

-- 18.3 Consulta con Filtro: sp_ConsultarProductosPorCategoria
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarProductosPorCategoria
    @id_categoria NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT id_producto, id_categoria, nombre, descripcion, precio_base,
           imagen_url, modelo_3d_url, tiempo_preparacion_min, disponible, destacado
    FROM dbo.productos
    WHERE id_categoria = @id_categoria AND disponible = 1
    ORDER BY nombre ASC;
END;
GO

-- 18.4 Inserción: sp_CrearProducto
CREATE OR ALTER PROCEDURE dbo.sp_CrearProducto
    @id_producto NVARCHAR(50),
    @id_categoria NVARCHAR(50),
    @nombre NVARCHAR(150),
    @descripcion NVARCHAR(MAX),
    @precio_base DECIMAL(12,2),
    @imagen_url NVARCHAR(500),
    @modelo_3d_url NVARCHAR(500),
    @tiempo_preparacion_min INT = 15,
    @alergenos NVARCHAR(255) = NULL,
    @ingredientes_base NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.productos (
        id_producto, id_categoria, nombre, descripcion, precio_base,
        imagen_url, modelo_3d_url, tiempo_preparacion_min, disponible, destacado,
        alergenos, ingredientes_base
    )
    VALUES (
        @id_producto, @id_categoria, @nombre, @descripcion, @precio_base,
        @imagen_url, @modelo_3d_url, @tiempo_preparacion_min, 1, 0,
        @alergenos, @ingredientes_base
    );
END;
GO

-- 18.5 Actualización: sp_ActualizarProducto
CREATE OR ALTER PROCEDURE dbo.sp_ActualizarProducto
    @id_producto NVARCHAR(50),
    @id_categoria NVARCHAR(50),
    @nombre NVARCHAR(150),
    @descripcion NVARCHAR(MAX),
    @precio_base DECIMAL(12,2),
    @imagen_url NVARCHAR(500),
    @modelo_3d_url NVARCHAR(500),
    @disponible BIT,
    @destacado BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.productos
    SET id_categoria = @id_categoria,
        nombre = @nombre,
        descripcion = @descripcion,
        precio_base = @precio_base,
        imagen_url = @imagen_url,
        modelo_3d_url = @modelo_3d_url,
        disponible = @disponible,
        destacado = @destacado,
        actualizado_en = SYSDATETIME()
    WHERE id_producto = @id_producto;
END;
GO

-- 18.6 Eliminación Física: sp_EliminarProductoFisico
CREATE OR ALTER PROCEDURE dbo.sp_EliminarProductoFisico
    @id_producto NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.productos
    WHERE id_producto = @id_producto;
END;
GO

-- 18.7 Transacción Atómica: sp_RegistrarPagoAtomico
CREATE OR ALTER PROCEDURE dbo.sp_RegistrarPagoAtomico
    @id_pago NVARCHAR(50),
    @id_pedido NVARCHAR(50),
    @monto DECIMAL(12,2),
    @metodo_pago NVARCHAR(30),
    @recibido_por NVARCHAR(100) = N'Cajero',
    @codigo_referencia NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        DECLARE @token_pedido UNIQUEIDENTIFIER;
        DECLARE @total DECIMAL(12,2);
        DECLARE @total_pagado DECIMAL(12,2);
        DECLARE @nuevo_pagado DECIMAL(12,2);
        DECLARE @nuevo_saldo DECIMAL(12,2);
        DECLARE @nuevo_estado_pago NVARCHAR(20);

        SELECT @token_pedido = token_pedido, @total = total, @total_pagado = total_pagado
        FROM dbo.pedidos WITH (UPDLOCK, ROWLOCK)
        WHERE id_pedido = @id_pedido;

        IF @token_pedido IS NULL
        BEGIN
            RAISERROR(N'Pedido no encontrado', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        INSERT INTO dbo.pagos (id_pago, id_pedido, token_pedido, monto, metodo_pago, recibido_por, codigo_referencia, fecha_pago)
        VALUES (@id_pago, @id_pedido, @token_pedido, @monto, @metodo_pago, @recibido_por, @codigo_referencia, SYSDATETIME());

        SET @nuevo_pagado = @total_pagado + @monto;
        SET @nuevo_saldo = CASE WHEN (@total - @nuevo_pagado) < 0 THEN 0.00 ELSE (@total - @nuevo_pagado) END;
        SET @nuevo_estado_pago = CASE WHEN @nuevo_saldo <= 0 THEN N'PAGADO' ELSE N'PARCIAL' END;

        UPDATE dbo.pedidos
        SET total_pagado = @nuevo_pagado,
            saldo_pendiente = @nuevo_saldo,
            estado_pago = @nuevo_estado_pago,
            actualizado_en = SYSDATETIME()
        WHERE id_pedido = @id_pedido;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- 18.8 Cierre de Mesa con Validación: sp_CerrarMesa
CREATE OR ALTER PROCEDURE dbo.sp_CerrarMesa
    @nombre_mesa NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @pendientes INT;
    SELECT @pendientes = COUNT(*)
    FROM dbo.pedidos
    WHERE nombre_mesa = @nombre_mesa AND estado <> N'CANCELADO' AND estado_pago <> N'PAGADO';

    IF @pendientes > 0
    BEGIN
        RAISERROR(N'No se puede cerrar la mesa: existen pedidos con saldo pendiente de pago.', 16, 1);
        RETURN;
    END

    UPDATE dbo.pedidos
    SET cerrado = 1,
        cerrado_en = SYSDATETIME(),
        estado = N'ENTREGADO',
        actualizado_en = SYSDATETIME()
    WHERE nombre_mesa = @nombre_mesa AND estado <> N'CANCELADO';
END;
GO



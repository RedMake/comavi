CREATE DATABASE COMAVI
GO

USE COMAVI
GO

CREATE TABLE Usuario (
    id_usuario INT PRIMARY KEY IDENTITY,
    nombre_usuario VARCHAR(50) NOT NULL,
    correo_electronico VARCHAR(100) NOT NULL,
    contrasena VARCHAR(255) NOT NULL,
    rol VARCHAR(20) CHECK (rol IN ('admin', 'user')) NOT NULL,
    ultimo_ingreso DATETIME
)
GO

CREATE TABLE IntentosLogin (
    id_intento INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    fecha_hora DATETIME NOT NULL,
    exitoso BIT NOT NULL,
    direccion_ip VARCHAR(45) NOT NULL
)
GO

CREATE TABLE SesionesActivas (
    id_sesion INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    dispositivo VARCHAR(100) NOT NULL,
    ubicacion VARCHAR(100),
    fecha_inicio DATETIME NOT NULL,
    fecha_ultima_actividad DATETIME NOT NULL
)
GO

CREATE TABLE Notificaciones_Usuario (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    tipo_notificacion VARCHAR(50) CHECK (tipo_notificacion IN ('intentos_fallidos', 'bloqueo')) NOT NULL,
    fecha_hora DATETIME NOT NULL,
    mensaje TEXT NOT NULL
)
GO

CREATE TABLE RestablecimientoContrasena (
    id_reset INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    token VARCHAR(255) NOT NULL,
    fecha_solicitud DATETIME NOT NULL,
    fecha_expiracion DATETIME NOT NULL
)
GO

CREATE TABLE MFA (
    id_mfa INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    codigo VARCHAR(10) NOT NULL,
    fecha_generacion DATETIME NOT NULL,
    usado BIT NOT NULL
)
GO

CREATE TABLE Choferes (
    id_chofer INT PRIMARY KEY IDENTITY,
    nombreCompleto VARCHAR(100) NOT NULL,
    edad INT NOT NULL,
    numero_cedula VARCHAR(20) NOT NULL,
    licencia VARCHAR(50) NOT NULL,
    fecha_venc_licencia DATE NOT NULL,
    estado VARCHAR(10) CHECK (estado IN ('activo', 'inactivo')) NOT NULL,
    genero varchar(10) CHECK (genero IN ('masculino', 'femenino')) NOT NULL
)
GO

CREATE TABLE Documentos (
    id_documento INT PRIMARY KEY IDENTITY,
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    tipo_documento VARCHAR(50) NOT NULL,
    fecha_emision DATE NOT NULL,
    fecha_vencimiento DATE NOT NULL
)
GO

CREATE TABLE Notificaciones_Documento (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_documento INT FOREIGN KEY REFERENCES Documentos(id_documento),
    mensaje TEXT NOT NULL,
    fecha_envio DATETIME NOT NULL,
    estado VARCHAR(10) CHECK (estado IN ('pendiente', 'enviado')) NOT NULL
)
GO

CREATE TABLE Acciones_Chofer (
    id_accion INT PRIMARY KEY IDENTITY,
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    tipo_accion VARCHAR(20) CHECK (tipo_accion IN ('editar', 'eliminar', 'desactivar', 'reactivar')) NOT NULL,
    fecha_accion DATETIME NOT NULL,
    usuario_responsable VARCHAR(50) NOT NULL
)
GO

CREATE TABLE Camiones (
    id_camion INT PRIMARY KEY IDENTITY,
    marca VARCHAR(50) NOT NULL,
    modelo VARCHAR(50) NOT NULL,
    anio INT NOT NULL,
    numero_placa VARCHAR(20) UNIQUE NOT NULL,
    estado VARCHAR(10) CHECK (estado IN ('activo', 'inactivo')) NOT NULL,
    chofer_asignado INT FOREIGN KEY REFERENCES Choferes(id_chofer)
)
GO

CREATE TABLE Mantenimiento_Camiones (
    id_mantenimiento INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    descripcion VARCHAR(MAX) NOT NULL,
    fecha_mantenimiento DATE NOT NULL,
    costo DECIMAL(10, 2) NOT NULL
)
GO

CREATE TABLE Documentos_Camiones (
    id_documento INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    tipo_documento VARCHAR(50) NOT NULL,
    fecha_emision DATE NOT NULL,
    fecha_vencimiento DATE NOT NULL
)
GO

CREATE TABLE Notificaciones_Camiones (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_documento INT FOREIGN KEY REFERENCES Documentos_Camiones(id_documento),
    tipo_notificacion VARCHAR(20) CHECK (tipo_notificacion IN ('vencimiento', 'actualizacion', 'eliminacion')) NOT NULL,
    fecha_envio DATETIME NOT NULL,
    mensaje TEXT NOT NULL
)
GO

CREATE TABLE Asignacion_Choferes_Camiones (
    id_asignacion INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    fecha_asignacion DATETIME NOT NULL,
    comentarios TEXT
)
GO

CREATE OR ALTER PROCEDURE sp_ActualizarDocumento
    @id_documento INT = NULL,
    @id_chofer INT,
    @tipo_documento NVARCHAR(50),
    @fecha_emision DATE,
    @fecha_vencimiento DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @id_documento IS NULL OR @id_documento = 0
    BEGIN
        INSERT INTO Documentos (id_chofer, tipo_documento, fecha_emision, fecha_vencimiento)
        VALUES (@id_chofer, @tipo_documento, @fecha_emision, @fecha_vencimiento);
    END
    ELSE
    BEGIN
        UPDATE Documentos
        SET tipo_documento = @tipo_documento,
            fecha_emision = @fecha_emision,
            fecha_vencimiento = @fecha_vencimiento
        WHERE id_documento = @id_documento AND id_chofer = @id_chofer;
    END
END;
GO

CREATE OR ALTER PROCEDURE sp_MonitorearVencimientos
    @dias_previos INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @fecha_limite DATE = DATEADD(DAY, @dias_previos, GETDATE());
    
    SELECT 
        d.id_documento,
        d.tipo_documento,
        d.fecha_emision,
        d.fecha_vencimiento,
        ch.id_chofer,
        ch.nombreCompleto AS nombre_chofer,
        DATEDIFF(DAY, GETDATE(), d.fecha_vencimiento) AS dias_para_vencimiento,
        'Documento' AS tipo_vencimiento
    FROM 
        Documentos d
    INNER JOIN 
        Choferes ch ON d.id_chofer = ch.id_chofer
    WHERE 
        d.fecha_vencimiento BETWEEN GETDATE() AND @fecha_limite
        AND ch.estado = 'activo'
    
    UNION
    
    SELECT 
        NULL AS id_documento,
        'Licencia de conducir' AS tipo_documento,
        NULL AS fecha_emision,
        ch.fecha_venc_licencia AS fecha_vencimiento,
        ch.id_chofer,
        ch.nombreCompleto AS nombre_chofer,
        DATEDIFF(DAY, GETDATE(), ch.fecha_venc_licencia) AS dias_para_vencimiento,
        'Licencia' AS tipo_vencimiento
    FROM 
        Choferes ch
    WHERE 
        ch.fecha_venc_licencia BETWEEN GETDATE() AND @fecha_limite
        AND ch.estado = 'activo'
    
    ORDER BY 
        dias_para_vencimiento;
END;
GO

CREATE OR ALTER PROCEDURE sp_RegistrarDocumento
    @id_chofer INT,
    @tipo_documento NVARCHAR(50),
    @fecha_emision DATE,
    @fecha_vencimiento DATE
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO Documentos (id_chofer, tipo_documento, fecha_emision, fecha_vencimiento)
    VALUES (@id_chofer, @tipo_documento, @fecha_emision, @fecha_vencimiento);
    
    DECLARE @mensaje NVARCHAR(MAX);
    DECLARE @usuario_admin INT;
    
    SELECT TOP 1 @usuario_admin = id_usuario FROM Usuario WHERE rol = 'admin';
    
    IF @usuario_admin IS NOT NULL
    BEGIN
        SELECT @mensaje = 'Documento ' + @tipo_documento + ' registrado para chofer ' +
                         (SELECT nombreCompleto FROM Choferes WHERE id_chofer = @id_chofer);
        
        INSERT INTO Notificaciones_Usuario (id_usuario, tipo_notificacion, fecha_hora, mensaje)
        VALUES (@usuario_admin, 'documento_registrado', GETDATE(), @mensaje);
    END
END;
GO

CREATE OR ALTER PROCEDURE sp_EliminarDocumento
    @id_documento INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DELETE FROM Documentos
    WHERE id_documento = @id_documento;
END;
GO

CREATE PROCEDURE sp_RegistrarCamion
    @marca VARCHAR(50),
    @modelo VARCHAR(50),
    @anio INT,
    @numero_placa VARCHAR(20),
    @estado VARCHAR(10),
    @chofer_asignado INT = NULL
AS
BEGIN
    INSERT INTO Camiones (marca, modelo, anio, numero_placa, estado, chofer_asignado)
    VALUES (@marca, @modelo, @anio, @numero_placa, @estado, @chofer_asignado);
END
GO

CREATE PROCEDURE sp_ActualizarCamion
    @id_camion INT,
    @marca VARCHAR(50),
    @modelo VARCHAR(50),
    @anio INT,
    @estado VARCHAR(10)
AS
BEGIN
    UPDATE Camiones 
    SET marca = @marca, modelo = @modelo, anio = @anio, estado = @estado
    WHERE id_camion = @id_camion;
END
GO

CREATE OR ALTER PROCEDURE sp_RegistrarMantenimiento
    @id_camion INT,
    @descripcion NVARCHAR(MAX),
    @fecha_mantenimiento DATE,
    @costo DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    
    INSERT INTO Mantenimiento_Camiones (id_camion, descripcion, fecha_mantenimiento, costo)
    VALUES (@id_camion, @descripcion, @fecha_mantenimiento, @costo);
    
    IF @fecha_mantenimiento = CAST(GETDATE() AS DATE)
    BEGIN
        UPDATE Camiones
        SET estado = 'mantenimiento'
        WHERE id_camion = @id_camion;
    END
    
    DECLARE @mensaje NVARCHAR(MAX);
    DECLARE @usuario_admin INT;
    DECLARE @info_camion NVARCHAR(100);
    
    SELECT @info_camion = marca + ' ' + modelo + ' (' + numero_placa + ')'
    FROM Camiones
    WHERE id_camion = @id_camion;
    
    SELECT TOP 1 @usuario_admin = id_usuario FROM Usuario WHERE rol = 'admin';
    
    IF @usuario_admin IS NOT NULL
    BEGIN
        SET @mensaje = 'Mantenimiento registrado para ' + @info_camion + 
                      ': ' + @descripcion + '. Costo: $' + CAST(@costo AS NVARCHAR(20));
        
        INSERT INTO Notificaciones_Usuario (id_usuario, tipo_notificacion, fecha_hora, mensaje)
        VALUES (@usuario_admin, 'mantenimiento_registrado', GETDATE(), @mensaje);
    END
END;
GO

CREATE OR ALTER PROCEDURE sp_ObtenerHistorialMantenimiento
    @id_camion INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        m.id_mantenimiento,
        m.id_camion,
        m.descripcion,
        m.fecha_mantenimiento,
        m.costo,
        c.marca,
        c.modelo,
        c.numero_placa,
        ch.nombreCompleto AS nombre_chofer
    FROM 
        Mantenimiento_Camiones m
    INNER JOIN 
        Camiones c ON m.id_camion = c.id_camion
    LEFT JOIN 
        Choferes ch ON c.chofer_asignado = ch.id_chofer
    WHERE 
        m.id_camion = @id_camion
    ORDER BY 
        m.fecha_mantenimiento DESC;
END;
GO



CREATE PROCEDURE sp_DesactivarCamion
    @id_camion INT
AS
BEGIN
    UPDATE Camiones 
    SET estado = 'inactivo' 
    WHERE id_camion = @id_camion;
END
GO

CREATE PROCEDURE sp_AsignarChofer
    @id_camion INT,
    @id_chofer INT
AS
BEGIN
    UPDATE Camiones 
    SET chofer_asignado = @id_chofer 
    WHERE id_camion = @id_camion;
END
GO

CREATE PROCEDURE sp_EliminarCamion
    @id_camion INT
AS
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Mantenimiento_Camiones WHERE id_camion = @id_camion)
        AND NOT EXISTS (SELECT 1 FROM Documentos_Camiones WHERE id_camion = @id_camion)
    BEGIN
        DELETE FROM Camiones WHERE id_camion = @id_camion;
        RETURN 1; 
    END
    RETURN 0; 
END
GO

CREATE PROCEDURE sp_RegistrarChofer
    @nombreCompleto VARCHAR(100),
    @edad INT,
    @numero_cedula VARCHAR(20),
    @licencia VARCHAR(50),
    @fecha_venc_licencia DATE,
    @estado VARCHAR(10),
    @genero VARCHAR(10)
AS
BEGIN
    INSERT INTO Choferes (nombreCompleto, edad, numero_cedula, licencia, fecha_venc_licencia, estado, genero)
    VALUES (@nombreCompleto, @edad, @numero_cedula, @licencia, @fecha_venc_licencia, @estado, @genero);
END
GO



CREATE PROCEDURE sp_ActualizarDatosChofer
    @id_chofer INT,
    @nombreCompleto VARCHAR(100),
    @edad INT,
    @numero_cedula VARCHAR(20),
    @licencia VARCHAR(50),
    @fecha_venc_licencia DATE,
    @genero VARCHAR(10)
AS
BEGIN
    UPDATE Choferes 
    SET 
        nombreCompleto = @nombreCompleto,
        edad = @edad,
        numero_cedula = @numero_cedula,
        licencia = @licencia,
        fecha_venc_licencia = @fecha_venc_licencia,
        genero = @genero
    WHERE id_chofer = @id_chofer;
END
GO

CREATE PROCEDURE sp_DesactivarChofer
    @id_chofer INT
AS
BEGIN
    UPDATE Choferes 
    SET estado = 'inactivo' 
    WHERE id_chofer = @id_chofer;
END
GO

CREATE PROCEDURE sp_EliminarChofer
    @id_chofer INT
AS
BEGIN
    BEGIN TRANSACTION;
    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM Camiones WHERE chofer_asignado = @id_chofer)
            AND NOT EXISTS (SELECT 1 FROM Documentos WHERE id_chofer = @id_chofer)
            AND NOT EXISTS (SELECT 1 FROM Asignacion_Choferes_Camiones WHERE id_chofer = @id_chofer)
        BEGIN
            DELETE FROM Choferes WHERE id_chofer = @id_chofer;
            COMMIT TRANSACTION;
            RETURN 1; 
        END
        ELSE
        BEGIN
            ROLLBACK TRANSACTION;
            RETURN 0; 
        END
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE sp_ObtenerDocumentosChofer
    @id_chofer INT
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        d.id_documento,
        d.id_chofer,
        d.tipo_documento,
        d.fecha_emision,
        d.fecha_vencimiento,
        ch.nombreCompleto AS nombre_chofer,
        CASE 
            WHEN d.fecha_vencimiento < GETDATE() THEN 'Vencido'
            WHEN d.fecha_vencimiento < DATEADD(DAY, 30, GETDATE()) THEN 'Por vencer'
            ELSE 'Vigente'
        END AS estado
    FROM 
        Documentos d
    INNER JOIN 
        Choferes ch ON d.id_chofer = ch.id_chofer
    WHERE 
        d.id_chofer = @id_chofer
    ORDER BY 
        d.fecha_vencimiento;
END;
GO

CREATE OR ALTER PROCEDURE sp_ObtenerNotificacionesMantenimiento
    @dias_antelacion INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @fecha_limite DATE = DATEADD(DAY, @dias_antelacion, GETDATE());
    
    SELECT 
        c.id_camion,
        c.marca,
        c.modelo,
        c.numero_placa,
        m.id_mantenimiento,
        m.descripcion,
        m.fecha_mantenimiento,
        ch.nombreCompleto AS nombre_chofer,
        DATEDIFF(DAY, GETDATE(), m.fecha_mantenimiento) AS dias_para_mantenimiento,
        'Programado' AS tipo_notificacion
    FROM 
        Camiones c
    INNER JOIN 
        Mantenimiento_Camiones m ON c.id_camion = m.id_camion
    LEFT JOIN 
        Choferes ch ON c.chofer_asignado = ch.id_chofer
    WHERE 
        c.estado = 'activo'
        AND m.fecha_mantenimiento BETWEEN GETDATE() AND @fecha_limite
    
    UNION
    
    SELECT 
        c.id_camion,
        c.marca,
        c.modelo,
        c.numero_placa,
        NULL AS id_mantenimiento,
        'Mantenimiento preventivo requerido' AS descripcion,
        GETDATE() AS fecha_mantenimiento,
        ch.nombreCompleto AS nombre_chofer,
        0 AS dias_para_mantenimiento,
        'Requerido' AS tipo_notificacion
    FROM 
        Camiones c
    LEFT JOIN 
        Choferes ch ON c.chofer_asignado = ch.id_chofer
    LEFT JOIN 
        (SELECT id_camion, MAX(fecha_mantenimiento) AS ultima_fecha
         FROM Mantenimiento_Camiones
         GROUP BY id_camion) ultimo ON c.id_camion = ultimo.id_camion
    WHERE 
        c.estado = 'activo'
        AND (ultimo.ultima_fecha IS NULL 
             OR DATEDIFF(MONTH, ultimo.ultima_fecha, GETDATE()) >= 6)
    
    ORDER BY 
        dias_para_mantenimiento;
END;
GO

CREATE OR ALTER PROCEDURE sp_ObtenerChoferesRango
    @inicio INT = 1,
    @cantidad INT = 10,
    @total INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @inicio < 1 SET @inicio = 1;
    IF @cantidad < 1 SET @cantidad = 10;
    
    SELECT @total = COUNT(*) FROM Choferes;
    
    IF @inicio > @total
    BEGIN
        SET @inicio = 1;
    END
    
    DECLARE @offset INT = @inicio - 1;
    
    SELECT 
        c.id_chofer,
        c.nombreCompleto,
        c.edad,
        c.numero_cedula,
        c.licencia,
        c.fecha_venc_licencia,
        c.estado,
        c.genero,
        CASE WHEN c.fecha_venc_licencia < GETDATE() THEN 'Vencida' 
             WHEN c.fecha_venc_licencia < DATEADD(MONTH, 1, GETDATE()) THEN 'Por vencer'
             ELSE 'Vigente' END AS estado_licencia,
        cam.id_camion,
        CONCAT(cam.marca, ' ', cam.modelo, ' (', cam.numero_placa, ')') AS camion_asignado,
        (SELECT COUNT(*) FROM Documentos WHERE id_chofer = c.id_chofer) AS total_documentos,
        ROW_NUMBER() OVER (ORDER BY c.nombreCompleto) AS numero_registro
    FROM 
        Choferes c
    LEFT JOIN 
        Camiones cam ON c.id_chofer = cam.chofer_asignado
    ORDER BY 
        c.nombreCompleto
    OFFSET @offset ROWS 
    FETCH NEXT @cantidad ROWS ONLY;
    
    SELECT 
        @total AS total_registros,
        @inicio AS registro_inicio,
        CASE WHEN @inicio + @cantidad - 1 > @total 
             THEN @total 
             ELSE @inicio + @cantidad - 1 
        END AS registro_fin,
        CEILING(CAST(@total AS FLOAT) / @cantidad) AS total_paginas,
        CEILING(CAST(@inicio AS FLOAT) / @cantidad) AS pagina_actual;
END;
GO


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
    descripcion TEXT NOT NULL,
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

CREATE PROCEDURE sp_ObtenerHistorialMantenimiento
    @id_camion INT
AS
BEGIN
    SELECT * FROM Mantenimiento_Camiones 
    WHERE id_camion = @id_camion 
    ORDER BY fecha_mantenimiento DESC;
END
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

CREATE PROCEDURE sp_MonitorearVencimientos
    @dias_previos INT = 30
AS
BEGIN
    SELECT * FROM Documentos 
    WHERE DATEDIFF(DAY, GETDATE(), fecha_vencimiento) <= @dias_previos;
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
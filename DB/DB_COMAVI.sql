CREATE DATABASE COMAVI
GO

USE COMAVI
GO

-- Tabla Usuario
CREATE TABLE Usuario (
    id_usuario INT PRIMARY KEY IDENTITY,
    nombre_usuario VARCHAR(50) NOT NULL,
    correo_electronico VARCHAR(100) NOT NULL,
    contrasena VARCHAR(255) NOT NULL,
    rol VARCHAR(20) CHECK (rol IN ('admin', 'user')) NOT NULL,
    ultimo_ingreso DATETIME
)
GO

-- Tabla IntentosLogin
CREATE TABLE IntentosLogin (
    id_intento INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    fecha_hora DATETIME NOT NULL,
    exitoso BIT NOT NULL,
    direccion_ip VARCHAR(45) NOT NULL
)
GO

-- Tabla SesionesActivas
CREATE TABLE SesionesActivas (
    id_sesion INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    dispositivo VARCHAR(100) NOT NULL,
    ubicacion VARCHAR(100),
    fecha_inicio DATETIME NOT NULL,
    fecha_ultima_actividad DATETIME NOT NULL
)
GO

-- Tabla Notificaciones
CREATE TABLE Notificaciones_Usuario (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    tipo_notificacion VARCHAR(50) CHECK (tipo_notificacion IN ('intentos_fallidos', 'bloqueo')) NOT NULL,
    fecha_hora DATETIME NOT NULL,
    mensaje TEXT NOT NULL
)
GO

-- Tabla RestablecimientoContrasena
CREATE TABLE RestablecimientoContrasena (
    id_reset INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    token VARCHAR(255) NOT NULL,
    fecha_solicitud DATETIME NOT NULL,
    fecha_expiracion DATETIME NOT NULL
)
GO

-- Tabla MFA
CREATE TABLE MFA (
    id_mfa INT PRIMARY KEY IDENTITY,
    id_usuario INT FOREIGN KEY REFERENCES Usuario(id_usuario),
    codigo VARCHAR(10) NOT NULL,
    fecha_generacion DATETIME NOT NULL,
    usado BIT NOT NULL
)
GO

-- Tabla Choferes
CREATE TABLE Choferes (
    id_chofer INT PRIMARY KEY IDENTITY,
    nombre VARCHAR(50) NOT NULL,
    apellido VARCHAR(50) NOT NULL,
    edad INT NOT NULL,
    numero_cedula VARCHAR(20) NOT NULL,
    licencia VARCHAR(50) NOT NULL,
    fecha_venc_licencia DATE NOT NULL,
    estado VARCHAR(10) CHECK (estado IN ('activo', 'inactivo')) NOT NULL
)
GO

-- Tabla Documentos
CREATE TABLE Documentos (
    id_documento INT PRIMARY KEY IDENTITY,
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    tipo_documento VARCHAR(50) NOT NULL,
    fecha_emision DATE NOT NULL,
    fecha_vencimiento DATE NOT NULL
)
GO

-- Tabla Notificaciones
CREATE TABLE Notificaciones_Documento (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_documento INT FOREIGN KEY REFERENCES Documentos(id_documento),
    mensaje TEXT NOT NULL,
    fecha_envio DATETIME NOT NULL,
    estado VARCHAR(10) CHECK (estado IN ('pendiente', 'enviado')) NOT NULL
)
GO

-- Tabla Acciones_Chofer
CREATE TABLE Acciones_Chofer (
    id_accion INT PRIMARY KEY IDENTITY,
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    tipo_accion VARCHAR(20) CHECK (tipo_accion IN ('editar', 'eliminar', 'desactivar', 'reactivar')) NOT NULL,
    fecha_accion DATETIME NOT NULL,
    usuario_responsable VARCHAR(50) NOT NULL
)
GO

-- Tabla Camiones
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

-- Tabla Mantenimiento_Camiones
CREATE TABLE Mantenimiento_Camiones (
    id_mantenimiento INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    descripcion TEXT NOT NULL,
    fecha_mantenimiento DATE NOT NULL,
    costo DECIMAL(10, 2) NOT NULL
)
GO

-- Tabla Documentos_Camiones
CREATE TABLE Documentos_Camiones (
    id_documento INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    tipo_documento VARCHAR(50) NOT NULL,
    fecha_emision DATE NOT NULL,
    fecha_vencimiento DATE NOT NULL
)
GO

-- Tabla Notificaciones_Camiones
CREATE TABLE Notificaciones_Camiones (
    id_notificacion INT PRIMARY KEY IDENTITY,
    id_documento INT FOREIGN KEY REFERENCES Documentos_Camiones(id_documento),
    tipo_notificacion VARCHAR(20) CHECK (tipo_notificacion IN ('vencimiento', 'actualizacion', 'eliminacion')) NOT NULL,
    fecha_envio DATETIME NOT NULL,
    mensaje TEXT NOT NULL
)
GO

-- Tabla Asignacion_Choferes_Camiones
CREATE TABLE Asignacion_Choferes_Camiones (
    id_asignacion INT PRIMARY KEY IDENTITY,
    id_camion INT FOREIGN KEY REFERENCES Camiones(id_camion),
    id_chofer INT FOREIGN KEY REFERENCES Choferes(id_chofer),
    fecha_asignacion DATETIME NOT NULL,
    comentarios TEXT
)
GO

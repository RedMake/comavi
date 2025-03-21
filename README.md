# COMAVI S.A. - Sistema de Gestión de Transportes

![Logo COMAVI S.A.](https://github.com/RedMake/comavi/blob/development/wwwroot/img/Comavi_SA_Logo.png)

## Descripción

Este proyecto es un sistema de gestión integral diseñado específicamente para COMAVI S.A., una empresa de transporte que necesita administrar su flota de camiones, conductores, documentación y mantenimientos. El sistema proporciona una plataforma centralizada para el seguimiento de vencimientos de documentos críticos, gestión de mantenimientos de vehículos, y administración de personal.

## Características principales

### Gestión de usuarios con autenticación de dos factores (2FA)
- Registro y login seguro
- Verificación por email
- Autenticación de dos factores con códigos de respaldo
- Gestión de contraseñas y recuperación segura

### Gestión de conductores
- Perfiles completos de los conductores
- Seguimiento de licencias y documentación
- Alertas de vencimiento de documentos personales
- Asignación de vehículos

### Administración de flota
- Registro y seguimiento de vehículos
- Historial de mantenimientos
- Alertas de mantenimientos próximos
- Reportes de estado

### Sistema de documentación digital
- Almacenamiento de documentos en formato PDF
- Proceso de validación por administradores
- Alertas automáticas de vencimientos
- Histórico de documentación

### Calendario y agenda
- Programación de eventos y mantenimientos
- Vista de calendario integrada
- Recordatorios automáticos
- Seguimiento de actividades

### Dashboard administrativo
- Estadísticas en tiempo real
- Indicadores clave de rendimiento
- Gráficos y visualizaciones de datos
- Monitoreo de vencimientos críticos

### Sistema de notificaciones
- Alertas por correo electrónico
- Notificaciones en la plataforma
- Configuración de preferencias personalizadas

## Arquitectura técnica

El sistema está desarrollado utilizando:

- **Frontend**: HTML, CSS, JavaScript, Bootstrap, jQuery
- **Backend**: ASP.NET MVC Core
- **Base de datos**: SQL Server
- **Autenticación**: Identity Framework con 2FA personalizado
- **Reportes**: Generación de PDF mediante bibliotecas integradas
- **Calendario**: FullCalendar.js

## Capturas de pantalla

*(Pendiente de agregar capturas de pantalla de las principales funcionalidades)*

## Instalación y configuración

### Requisitos previos

- Visual Studio 2022 o superior
- .NET Core 6.0 o superior
- SQL Server 2019 o superior
- Node.js y npm (para algunas dependencias del frontend)

### Pasos para la instalación

1. Clonar el repositorio
https://github.com/RedMake/comavi.git

2. Restaurar los paquetes NuGet

dotnet restore

4. Configurar la cadena de conexión a la base de datos en `appsettings.json` y definir los diferentes secretos el JWT Token (generarlo), Email y su configuración DKIM.

5. Aplicar las migraciones para crear la base de datos

dotnet ef database update

## Estructura del proyecto

COMAVI_SA/
└── Comavi/
    ├── .config/
    │   └── dotnet-tools.json
    ├── .github/
    │   └── workflows/
    │       └── ... Workflow
    ├── Controllers/
    │   └── ... Controllers
    ├── Data/
    │   └── ... Database Context
    ├── Middleware/
    │   └── ... Middleware
    ├── Models/
    │   └── ... Models
    ├── Properties/
    │   └── launchSettings.json
    ├── Repository/
    │   └── ... Repositories
    ├── Services/
    │   └── ... Services
    ├── Tests/
    │   └── ... Tests
    ├── Utils/
    │   └── ... Utils
    ├── Views/
    │   ├── _ViewImports.cshtml
    │   ├── _ViewStart.cshtml
    │   ├── Admin/
    │   │   └── ... Pages 
    │   ├── Agenda/
    │   │   └── ... Pages 
    │   ├── Calendar/
    │   │   └── ... Pages 
    │   ├── Camion/
    │   │   └── ... Pages 
    │   ├── Documentos/
    │   │   └── ... Pages 
    │   ├── Home/
    │   │   └── ... Pages 
    │   ├── Login/
    │   │   └── ... Pages 
    │   ├── Notifications/
    │   │   └── ... Pages 
    │   └── Shared/
    │       └── ... Pages
    ├── wwwroot/
    │   ├── css/
    │   ├── js/
    │   ├── img/
    │   ├── lib/
    │   ├── plantillas/
    │   ├── vendor/
    │   ├── favicon.ico
    │   └── robots.txt
    ├── .gitattributes
    ├── .gitignore
    ├── appsettings.Development.json
    ├── appsettings.json
    ├── COMAVI_SA.csproj
    ├── COMAVI_SA.sln
    ├── LICENSE.txt
    ├── NOTICE.txt
    ├── Program.cs
    ├── README.md
    └── web.config
    
## Flujo de trabajo de desarrollo

1. **Autenticación y perfil**: Los usuarios se registran, verifican su cuenta por email y pueden configurar la autenticación de dos factores
2. **Gestión de conductores**: Administradores registran/editan conductores y sus documentos
3. **Gestión de vehículos**: Registro de camiones, asignación a conductores y seguimiento de mantenimientos
4. **Documentación**: Carga, verificación y seguimiento de documentos críticos
5. **Monitorización**: Dashboard y reportes sobre el estado de vehículos, conductores y documentación

## Seguridad

El sistema implementa múltiples capas de seguridad:

- Autenticación de dos factores
- Códigos de respaldo para acceso de emergencia
- Verificación por correo electrónico
- Control de sesiones activas
- Políticas de contraseñas robustas
- Recuperación segura de contraseñas
- Validación de formularios client-side y server-side

## Mantenimiento y soporte

Para reportar problemas o solicitar nuevas características, por favor abra un issue en el repositorio de GitHub o contacte al equipo de soporte a través de:

- Email: info@comavicr.com
- Teléfono: +506 2551-1117

## Licencia

Este software es propiedad de COMAVI S.A. y está protegido por las leyes de propiedad intelectual. Su uso no autorizado está estrictamente prohibido.

## Equipo de desarrollo

- Gabriel Amador Artavia, Reynaldo Solano Vega - Desarrollo completo del proyecto

© 2025 COMAVI S.A. Todos los derechos reservados.

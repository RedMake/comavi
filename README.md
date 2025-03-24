OMAVI S.A. - Transport Management System
========================================

![Logo COMAVI S.A.](https://github.com/RedMake/comavi/blob/development/wwwroot/img/Comavi_SA_Logo.png)

Description
-----------

This project is a comprehensive management system specifically designed for COMAVI S.A., a transport company that needs to manage its fleet of trucks, drivers, documentation, and maintenance. The system provides a centralized platform for tracking critical document expirations, vehicle maintenance management, and personnel administration.

Main Features
-------------

### User Management with Two-Factor Authentication (2FA)

-   Secure registration and login
-   Email verification
-   Two-factor authentication with backup codes
-   Password management and secure recovery

### Driver Management

-   Comprehensive driver profiles
-   License and documentation tracking
-   Personal document expiration alerts
-   Vehicle assignment

### Fleet Administration

-   Vehicle registration and tracking
-   Maintenance history
-   Upcoming maintenance alerts
-   Status reports

### Digital Documentation System

-   PDF document storage
-   Administrator validation process
-   Automatic expiration alerts
-   Documentation history

### Calendar and Scheduling

-   Event and maintenance scheduling
-   Integrated calendar view
-   Automatic reminders
-   Activity tracking

### Administrative Dashboard

-   Real-time statistics
-   Key performance indicators
-   Data graphs and visualizations
-   Critical expiration monitoring

### Notification System

-   Email alerts
-   Platform notifications
-   Custom preference configuration

Technical Architecture
----------------------

The system is developed using:

-   **Frontend**: HTML, CSS, JavaScript, Bootstrap, jQuery
-   **Backend**: ASP.NET MVC Core
-   **Database**: SQL Server
-   **Authentication**: Identity Framework with custom 2FA
-   **Reports**: PDF generation through integrated libraries
-   **Calendar**: FullCalendar.js

Screenshots
-----------

*(Screenshots of main functionalities pending)*

Installation and Configuration
------------------------------

### Prerequisites

-   Visual Studio 2022 or higher
-   .NET Core 6.0 or higher
-   SQL Server 2019 or higher
-   Node.js and npm (for some frontend dependencies)

### Installation Steps

1.  Clone the repository


`https://github.com/RedMake/comavi.git`

1.  Restore NuGet packages


`dotnet restore`

1.  Configure the database connection string in `appsettings.json` and define the different secrets for the JWT Token (generate it), Email, and its DKIM configuration.
2.  Apply migrations to create the database


`dotnet ef database update`

Project Structure
-----------------

<pre>
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
</pre>

Development Workflow
--------------------

1.  **Authentication and Profile**: Users register, verify their account by email, and can configure two-factor authentication
2.  **Driver Management**: Administrators register/edit drivers and their documents
3.  **Vehicle Management**: Truck registration, driver assignment, and maintenance tracking
4.  **Documentation**: Upload, verification, and tracking of critical documents
5.  **Monitoring**: Dashboard and reports on the status of vehicles, drivers, and documentation

Security
--------

The system implements multiple security layers:

-   Two-factor authentication
-   Backup codes for emergency access
-   Email verification
-   Active session control
-   Strong password policies
-   Secure password recovery
-   Client-side and server-side form validation

Maintenance and Support
-----------------------

To report issues or request new features, please open an issue on the GitHub repository or contact the support team through:

-   Email: <info@comavicr.com>
-   Phone: +506 2551-1117

License
-------

This software is the property of COMAVI S.A. and is protected by intellectual property laws. Unauthorized use is strictly prohibited.

Development Team
----------------

-   Gabriel Amador Artavia, Reynaldo Solano Vega - Complete project development

© 2025 COMAVI S.A. All rights reserved.

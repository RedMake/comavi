using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace COMAVI_SA.Migrations
{
    /// <inheritdoc />
    public partial class MigracionInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Usuario",
                columns: table => new
                {
                    id_usuario = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    nombre_usuario = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    correo_electronico = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    contrasena = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ultimo_ingreso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    estado_verificacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "pendiente"),
                    fecha_verificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    token_verificacion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    fecha_expiracion_token = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fecha_registro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    mfa_habilitado = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    fecha_actualizacion_password = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuario", x => x.id_usuario);
                });

            migrationBuilder.CreateTable(
                name: "Choferes",
                columns: table => new
                {
                    id_chofer = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: true),
                    nombreCompleto = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    edad = table.Column<int>(type: "int", nullable: false),
                    numero_cedula = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    licencia = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha_venc_licencia = table.Column<DateTime>(type: "datetime2", nullable: false),
                    estado = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    genero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Choferes", x => x.id_chofer);
                    table.ForeignKey(
                        name: "FK_Choferes_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CodigosRespaldoMFA",
                columns: table => new
                {
                    id_codigo_respaldo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    fecha_generacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    usado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosRespaldoMFA", x => x.id_codigo_respaldo);
                    table.ForeignKey(
                        name: "FK_CodigosRespaldoMFA_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IntentosLogin",
                columns: table => new
                {
                    id_intento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: true),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    exitoso = table.Column<bool>(type: "bit", nullable: false),
                    direccion_ip = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosLogin", x => x.id_intento);
                    table.ForeignKey(
                        name: "FK_IntentosLogin_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario");
                });

            migrationBuilder.CreateTable(
                name: "MFA",
                columns: table => new
                {
                    id_mfa = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    codigo = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    fecha_generacion = table.Column<DateTime>(type: "datetime2", nullable: false),
                    usado = table.Column<bool>(type: "bit", nullable: false),
                    esta_activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MFA", x => x.id_mfa);
                    table.ForeignKey(
                        name: "FK_MFA_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notificaciones_Usuario",
                columns: table => new
                {
                    id_notificacion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    tipo_notificacion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha_hora = table.Column<DateTime>(type: "datetime2", nullable: false),
                    mensaje = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    leida = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificaciones_Usuario", x => x.id_notificacion);
                    table.ForeignKey(
                        name: "FK_Notificaciones_Usuario_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PreferenciasNotificacion",
                columns: table => new
                {
                    id_preferencia = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    notificar_por_correo = table.Column<bool>(type: "bit", nullable: false),
                    dias_anticipacion = table.Column<int>(type: "int", nullable: false),
                    notificar_vencimiento_licencia = table.Column<bool>(type: "bit", nullable: false),
                    notificar_vencimiento_documentos = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreferenciasNotificacion", x => x.id_preferencia);
                    table.ForeignKey(
                        name: "FK_PreferenciasNotificacion_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RestablecimientoContrasena",
                columns: table => new
                {
                    id_reset = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    token = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    fecha_solicitud = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_expiracion = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestablecimientoContrasena", x => x.id_reset);
                    table.ForeignKey(
                        name: "FK_RestablecimientoContrasena_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SesionesActivas",
                columns: table => new
                {
                    id_sesion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    dispositivo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ubicacion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    fecha_inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_ultima_actividad = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesActivas", x => x.id_sesion);
                    table.ForeignKey(
                        name: "FK_SesionesActivas_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Camiones",
                columns: table => new
                {
                    id_camion = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    marca = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    modelo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    anio = table.Column<int>(type: "int", nullable: false),
                    numero_placa = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: false),
                    chofer_asignado = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Camiones", x => x.id_camion);
                    table.ForeignKey(
                        name: "FK_Camiones_Choferes_chofer_asignado",
                        column: x => x.chofer_asignado,
                        principalTable: "Choferes",
                        principalColumn: "id_chofer",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Documentos",
                columns: table => new
                {
                    id_documento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_chofer = table.Column<int>(type: "int", nullable: false),
                    tipo_documento = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    fecha_emision = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_vencimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ruta_archivo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    contenido_archivo = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    tipo_mime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true, defaultValue: "application/pdf"),
                    tamano_archivo = table.Column<int>(type: "int", nullable: true),
                    hash_documento = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    estado_validacion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "pendiente")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentos", x => x.id_documento);
                    table.ForeignKey(
                        name: "FK_Documentos_Choferes_id_chofer",
                        column: x => x.id_chofer,
                        principalTable: "Choferes",
                        principalColumn: "id_chofer",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventosAgenda",
                columns: table => new
                {
                    id_evento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_usuario = table.Column<int>(type: "int", nullable: false),
                    id_chofer = table.Column<int>(type: "int", nullable: true),
                    titulo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fecha_inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    fecha_fin = table.Column<DateTime>(type: "datetime2", nullable: true),
                    tipo_evento = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    requiere_notificacion = table.Column<bool>(type: "bit", nullable: false),
                    dias_anticipacion_notificacion = table.Column<int>(type: "int", nullable: true),
                    notificacion_enviada = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosAgenda", x => x.id_evento);
                    table.ForeignKey(
                        name: "FK_EventosAgenda_Choferes_id_chofer",
                        column: x => x.id_chofer,
                        principalTable: "Choferes",
                        principalColumn: "id_chofer");
                    table.ForeignKey(
                        name: "FK_EventosAgenda_Usuario_id_usuario",
                        column: x => x.id_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mantenimiento_Camiones",
                columns: table => new
                {
                    id_mantenimiento = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_camion = table.Column<int>(type: "int", nullable: false),
                    descripcion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    fecha_mantenimiento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    costo = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    moneda = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    detalles_costo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mantenimiento_Camiones", x => x.id_mantenimiento);
                    table.ForeignKey(
                        name: "FK_Mantenimiento_Camiones_Camiones_id_camion",
                        column: x => x.id_camion,
                        principalTable: "Camiones",
                        principalColumn: "id_camion",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Solicitudes_Mantenimiento",
                columns: table => new
                {
                    id_solicitud = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    id_camion = table.Column<int>(type: "int", nullable: false),
                    id_chofer = table.Column<int>(type: "int", nullable: false),
                    fecha_solicitud = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    observaciones = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    estado = table.Column<string>(type: "nvarchar(450)", nullable: true, defaultValue: "pendiente"),
                    comentario_admin = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    fecha_programada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fecha_completado = table.Column<DateTime>(type: "datetime2", nullable: true),
                    fecha_revision = table.Column<DateTime>(type: "datetime2", nullable: true),
                    id_admin_revisor = table.Column<int>(type: "int", nullable: true),
                    AdminRevisorid_usuario = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Solicitudes_Mantenimiento", x => x.id_solicitud);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Mantenimiento_Camiones_id_camion",
                        column: x => x.id_camion,
                        principalTable: "Camiones",
                        principalColumn: "id_camion",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Mantenimiento_Choferes_id_chofer",
                        column: x => x.id_chofer,
                        principalTable: "Choferes",
                        principalColumn: "id_chofer",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Solicitudes_Mantenimiento_Usuario_AdminRevisorid_usuario",
                        column: x => x.AdminRevisorid_usuario,
                        principalTable: "Usuario",
                        principalColumn: "id_usuario");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Camiones_chofer_asignado",
                table: "Camiones",
                column: "chofer_asignado");

            migrationBuilder.CreateIndex(
                name: "IX_Camiones_numero_placa",
                table: "Camiones",
                column: "numero_placa",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Choferes_id_usuario",
                table: "Choferes",
                column: "id_usuario",
                unique: true,
                filter: "[id_usuario] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Choferes_numero_cedula",
                table: "Choferes",
                column: "numero_cedula",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodigosRespaldoMFA_id_usuario_codigo",
                table: "CodigosRespaldoMFA",
                columns: new[] { "id_usuario", "codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_estado_validacion",
                table: "Documentos",
                column: "estado_validacion");

            migrationBuilder.CreateIndex(
                name: "IX_Documentos_id_chofer",
                table: "Documentos",
                column: "id_chofer");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAgenda_fecha_inicio",
                table: "EventosAgenda",
                column: "fecha_inicio");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAgenda_id_chofer",
                table: "EventosAgenda",
                column: "id_chofer");

            migrationBuilder.CreateIndex(
                name: "IX_EventosAgenda_id_usuario",
                table: "EventosAgenda",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosLogin_id_usuario_fecha_hora",
                table: "IntentosLogin",
                columns: new[] { "id_usuario", "fecha_hora" });

            migrationBuilder.CreateIndex(
                name: "IX_Mantenimiento_Camiones_id_camion",
                table: "Mantenimiento_Camiones",
                column: "id_camion");

            migrationBuilder.CreateIndex(
                name: "IX_MFA_id_usuario_esta_activo",
                table: "MFA",
                columns: new[] { "id_usuario", "esta_activo" });

            migrationBuilder.CreateIndex(
                name: "IX_Notificaciones_Usuario_id_usuario",
                table: "Notificaciones_Usuario",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_PreferenciasNotificacion_id_usuario",
                table: "PreferenciasNotificacion",
                column: "id_usuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestablecimientoContrasena_id_usuario",
                table: "RestablecimientoContrasena",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesActivas_id_usuario",
                table: "SesionesActivas",
                column: "id_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_Mantenimiento_AdminRevisorid_usuario",
                table: "Solicitudes_Mantenimiento",
                column: "AdminRevisorid_usuario");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_Mantenimiento_estado",
                table: "Solicitudes_Mantenimiento",
                column: "estado");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_Mantenimiento_fecha_solicitud",
                table: "Solicitudes_Mantenimiento",
                column: "fecha_solicitud");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_Mantenimiento_id_camion",
                table: "Solicitudes_Mantenimiento",
                column: "id_camion");

            migrationBuilder.CreateIndex(
                name: "IX_Solicitudes_Mantenimiento_id_chofer",
                table: "Solicitudes_Mantenimiento",
                column: "id_chofer");

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_correo_electronico",
                table: "Usuario",
                column: "correo_electronico",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuario_token_verificacion",
                table: "Usuario",
                column: "token_verificacion");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosRespaldoMFA");

            migrationBuilder.DropTable(
                name: "Documentos");

            migrationBuilder.DropTable(
                name: "EventosAgenda");

            migrationBuilder.DropTable(
                name: "IntentosLogin");

            migrationBuilder.DropTable(
                name: "Mantenimiento_Camiones");

            migrationBuilder.DropTable(
                name: "MFA");

            migrationBuilder.DropTable(
                name: "Notificaciones_Usuario");

            migrationBuilder.DropTable(
                name: "PreferenciasNotificacion");

            migrationBuilder.DropTable(
                name: "RestablecimientoContrasena");

            migrationBuilder.DropTable(
                name: "SesionesActivas");

            migrationBuilder.DropTable(
                name: "Solicitudes_Mantenimiento");

            migrationBuilder.DropTable(
                name: "Camiones");

            migrationBuilder.DropTable(
                name: "Choferes");

            migrationBuilder.DropTable(
                name: "Usuario");
        }
    }
}

using COMAVI_SA.Utils;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace COMAVI_SA.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task EnviarCorreoAsync(string to, string subject, string body) => SendEmailAsync(to, subject, body);

    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<EmailService> logger)
        {
            _configuration = NotNull.Check(configuration);
            _environment = NotNull.Check(environment);
            _logger = NotNull.Check(logger);
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            // Validar que ningún parámetro sea nulo
            to = NotNull.CheckNotNullOrEmpty(to, message: "La dirección de correo destino no puede ser nula o vacía");
            subject = NotNull.CheckNotNullOrEmpty(subject, message: "El asunto del correo no puede ser nulo o vacío");
            body = NotNull.CheckNotNullOrEmpty(body, message: "El cuerpo del correo no puede ser nulo o vacío");

            try
            {
                // Configuración de correo de Namecheap
                var email = new MimeMessage();
                email.From.Add(MailboxAddress.Parse("no-reply-support@docktrack.lat"));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new TextPart(MimeKit.Text.TextFormat.Html) { Text = body };

                // En producción, usar las credenciales de KeyVault
                string password = _environment.IsDevelopment()
                    ? "_YcNJTF(H!v-3yy" // Solo para desarrollo
                    : NotNull.CheckNotNullOrEmpty(_configuration["EmailPassword"],
                        message: "La contraseña de email no está configurada en KeyVault");

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync("mail.privateemail.com", 587, SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync("no-reply-support@docktrack.lat", password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _logger.LogInformation($"Correo enviado exitosamente a: {to}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al enviar correo a {to}");
                throw;
            }
        }
    }
}
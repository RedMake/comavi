using MailKit.Net.Smtp;
using MimeKit;

public class EmailService 
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task EnviarCorreoAsync(string destinatario, string asunto, string mensaje)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_config["SmtpSettings:FromName"], _config["SmtpSettings:FromAddress"]));
        email.To.Add(new MailboxAddress(destinatario, destinatario));
        email.Subject = asunto;

        var bodyBuilder = new BodyBuilder { HtmlBody = mensaje };
        email.Body = bodyBuilder.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]), false);
        await smtp.AuthenticateAsync(_config["SmtpSettings:Username"], _config["SmtpSettings:Password"]);
        await smtp.SendAsync(email);
        await smtp.DisconnectAsync(true);
    }
}

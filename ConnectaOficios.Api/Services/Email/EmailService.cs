using Resend;

namespace ConnectaOficios.Api.Services.Email;

public class EmailService : IEmailService
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;

    public EmailService(
        IResend resend,
        IConfiguration configuration)
    {
        _resend = resend;
        _configuration = configuration;
    }

    public async Task SendPasswordResetEmailAsync(
        string email,
        string nombre,
        string resetToken)
    {
        var fromEmail =
            _configuration["Resend:FromEmail"]
            ?? "onboarding@resend.dev";

        var fromName =
            _configuration["Resend:FromName"]
            ?? "ConnectaOficios";

        var message = new EmailMessage
        {
            From = $"{fromName} <{fromEmail}>",
            Subject = "Recuperación de contraseña - ConnectaOficios",
            HtmlBody = $"""
                <!DOCTYPE html>
                <html>
                <body style="font-family: Arial, sans-serif;">
                    <h2>Recuperación de contraseña</h2>

                    <p>Hola {nombre},</p>

                    <p>
                        Recibimos una solicitud para restablecer
                        la contraseña de tu cuenta en ConnectaOficios.
                    </p>

                    <p>
                        Tu código temporal de recuperación es:
                    </p>

                    <div style="
                        padding: 15px;
                        background: #f2f2f2;
                        font-size: 20px;
                        font-weight: bold;
                        word-break: break-all;
                    ">
                        {resetToken}
                    </div>

                    <p>
                        Este código expirará en 15 minutos.
                    </p>

                    <p>
                        Si no solicitaste este cambio,
                        puedes ignorar este correo.
                    </p>

                    <p>
                        ConnectaOficios
                    </p>
                </body>
                </html>
                """,
        };

        message.To.Add(email);

        await _resend.EmailSendAsync(message);
    }
}
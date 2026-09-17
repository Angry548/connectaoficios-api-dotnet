namespace ConnectaOficios.Api.Services.Email;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(
        string email,
        string nombre,
        string resetToken
    );
}
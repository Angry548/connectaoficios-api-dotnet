namespace ConnectaOficios.Api.Services.Users;

public class PasswordResetRequestResult
{
    public bool Accepted { get; set; }

    public string? ResetToken { get; set; }
}
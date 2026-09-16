namespace ConnectaOficios.Api.DTOs.Users;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    public UserResponse Usuario { get; set; } = null!;
}
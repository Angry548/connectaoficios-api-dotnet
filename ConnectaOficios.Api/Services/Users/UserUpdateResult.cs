using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Users;

public class UserUpdateResult
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public UserResponse? User { get; set; }
}
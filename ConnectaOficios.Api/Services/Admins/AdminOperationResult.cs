using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Admins;

public class AdminOperationResult
{
    public bool Success { get; set; }

    public string? Error { get; set; }

    public UserResponse? Admin { get; set; }
}
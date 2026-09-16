using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Users;

public interface IUserServices
{
    Task<UserResponse?> Register(UserRequest user);

    Task<LoginResponse?> Login(LoginRequest login);
}
using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Users;

public interface IUserServices
{
    Task<UserResponse?> Register(UserRequest user);

    Task<LoginResponse?> Login(LoginRequest login);

    Task<IEnumerable<UserResponse>> GetAll(
        string? nombre = null,
        string? correo = null,
        int? rolId = null,
        int? estado = null
    );

    Task<UserResponse?> GetById(int id);

    Task<UserResponse?> ChangeStatus(
        int id,
        int estado
    );

    Task<PasswordChangeResult> ChangePassword(
    int userId,
    ChangePasswordRequest request
        
    );

    Task<PasswordResetRequestResult> RequestPasswordReset(
        ForgotPasswordRequest request
    );
}
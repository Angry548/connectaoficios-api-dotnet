using ConnectaOficios.Api.DTOs.Common;
using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Users;

public interface IUserServices
{
    Task<UserResponse?> Register(UserRequest user);

    Task<LoginResponse?> Login(LoginRequest login);

    Task<PagedResult<UserResponse>> GetAll(
        string? nombre = null,
        string? correo = null,
        int? rolId = null,
        int? estado = null,
        int page = 1,
        int pageSize = 20
    );

    Task<IEnumerable<UserSearchResponse>> Search(
        string texto,
        int? rolId = null,
        int limit = 10
    );

    Task<UserResponse?> GetById(int id);

    Task<UserResponse?> ChangeStatus(
        int id,
        int estado
    );

    Task<UserUpdateResult> UpdateCurrentUser(
        int userId,
        UserUpdateRequest request
    );

    Task<PasswordChangeResult> ChangePassword(
        int userId,
        ChangePasswordRequest request
    );

    Task<PasswordResetRequestResult> RequestPasswordReset(
        ForgotPasswordRequest request
    );

    Task<PasswordResetResult> ResetPassword(
        ResetPasswordRequest request
    );
}
using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.DTOs.Common;
using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Admins;

public interface IAdminServices
{
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

    Task<UserResponse?> Create(
        AdminAccountRequest admin
    );

    Task<AdminOperationResult> Update(
        int id,
        AdminAccountUpdateRequest admin
    );

    Task<AdminOperationResult> ChangeStatus(
        int id,
        int estado,
        int currentUserId
    );
}
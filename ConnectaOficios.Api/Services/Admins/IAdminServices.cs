using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.DTOs.Users;

namespace ConnectaOficios.Api.Services.Admins;

public interface IAdminServices
{
    Task<IEnumerable<UserResponse>> GetAll();

    Task<UserResponse?> GetById(int id);

    Task<UserResponse?> Create(AdminAccountRequest admin);

    Task<AdminOperationResult> Update(
        int id,
        AdminAccountUpdateRequest admin);

    Task<AdminOperationResult> ChangeStatus(
        int id,
        int estado,
        int currentUserId);
}
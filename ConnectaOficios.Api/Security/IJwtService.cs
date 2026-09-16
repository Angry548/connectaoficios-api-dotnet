using ConnectaOficios.Domain.Entities;

namespace ConnectaOficios.Api.Security;

public interface IJwtService
{
    string GenerateToken(Usuario usuario);
}
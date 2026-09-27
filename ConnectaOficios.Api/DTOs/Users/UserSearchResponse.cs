namespace ConnectaOficios.Api.DTOs.Users;

public class UserSearchResponse
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public int RolId { get; set; }

    public string Rol { get; set; } = string.Empty;
}
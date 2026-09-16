using ConnectaOficios.Domain.Enums;

namespace ConnectaOficios.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public int RolId { get; set; }

    public Rol Rol { get; set; } = null!;

    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Activo;

    public DateTime FechaCreacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public DateTime? UltimoAcceso { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Admins;

public class AdminAccountRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(150)]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contraseña debe contener al menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(25)]
    public string? Telefono { get; set; }

    [Required(ErrorMessage = "El rol es obligatorio.")]
    public int RolId { get; set; }
}
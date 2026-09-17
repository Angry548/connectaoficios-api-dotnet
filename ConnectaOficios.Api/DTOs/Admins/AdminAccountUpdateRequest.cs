using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Admins;

public class AdminAccountUpdateRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(150)]
    public string Correo { get; set; } = string.Empty;

    [MaxLength(25)]
    public string? Telefono { get; set; }
}
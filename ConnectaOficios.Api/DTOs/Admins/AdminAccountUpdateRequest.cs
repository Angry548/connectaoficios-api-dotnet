using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Admins;

public class AdminAccountUpdateRequest
{
    [MaxLength(100)]
    public string? Nombre { get; set; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(150)]
    public string? Correo { get; set; }

    [MaxLength(25)]
    public string? Telefono { get; set; }
}
using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Users;

public class UserUpdateRequest
{
    [MaxLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres.")]
    public string? Nombre { get; set; }

    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    public string? Correo { get; set; }

    [MaxLength(25, ErrorMessage = "El teléfono no puede superar los 25 caracteres.")]
    public string? Telefono { get; set; }
}
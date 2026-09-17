using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Users;

public class ForgotPasswordRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(150)]
    public string Correo { get; set; } = string.Empty;
}
using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Users;

public class ResetPasswordRequest
{
    [Required(ErrorMessage = "El token es obligatorio.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(
        6,
        ErrorMessage = "La nueva contraseña debe contener al menos 6 caracteres."
    )]
    public string NewPassword { get; set; } = string.Empty;
}
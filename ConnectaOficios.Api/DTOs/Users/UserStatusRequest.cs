using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Users;

public class UserStatusRequest
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public int Estado { get; set; }
}
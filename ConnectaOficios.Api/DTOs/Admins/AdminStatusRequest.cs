using System.ComponentModel.DataAnnotations;

namespace ConnectaOficios.Api.DTOs.Admins;

public class AdminStatusRequest
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public int Estado { get; set; }
}
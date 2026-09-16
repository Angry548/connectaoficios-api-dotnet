using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Api.Services.Users;

namespace ConnectaOficios.Api.Endpoints;

public static class UserEndpoints
{
    public static void AddUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/users")
            .WithTags("Users");

        group.MapPost("/register", async (
    UserRequest user,
    IUserServices userServices) =>
        {
            if (user == null)
            {
                return Results.BadRequest(new
                {
                    message = "Los datos del usuario son obligatorios."
                });
            }

            // Validar rol permitido para el registro público
            if (user.RolId != 1 && user.RolId != 2)
            {
                return Results.BadRequest(new
                {
                    message = "Debe seleccionar el rol Cliente o Trabajador."
                });
            }

            var result = await userServices.Register(user);

            if (result == null)
            {
                return Results.Conflict(new
                {
                    message = "El correo electrónico ya está registrado."
                });
            }

            return Results.Created(
                $"/api/users/{result.Id}",
                result
            );
        })
.WithName("RegisterUser");
    }
}
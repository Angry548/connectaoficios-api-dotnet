using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Api.Services.Users;
using System.Security.Claims;

namespace ConnectaOficios.Api.Endpoints;

public static class UserEndpoints
{
    public static void AddUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/users")
            .WithTags("Users");

        // POST: /api/users/register
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

        // POST: /api/users/login
        group.MapPost("/login", async (
            LoginRequest login,
            IUserServices userServices) =>
        {
            var result = await userServices.Login(login);

            if (result == null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(result);
        })
        .WithName("LoginUser");

        group.MapPut("/me/password", async (
    ChangePasswordRequest request,
    ClaimsPrincipal currentUser,
    IUserServices userServices) =>
        {
            var userIdValue = currentUser.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(userIdValue, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await userServices.ChangePassword(
                userId,
                request
            );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "USER_NOT_FOUND" => Results.NotFound(new
                    {
                        message = "Usuario no encontrado."
                    }),

                    "USER_INACTIVE" => Results.Forbid(),

                    "INVALID_CURRENT_PASSWORD" => Results.BadRequest(new
                    {
                        message = "La contraseña actual es incorrecta."
                    }),

                    "SAME_PASSWORD" => Results.BadRequest(new
                    {
                        message =
                            "La nueva contraseña debe ser diferente de la contraseña actual."
                    }),

                    _ => Results.BadRequest(new
                    {
                        message = "No fue posible cambiar la contraseña."
                    })
                };
            }

            return Results.Ok(new
            {
                message = "Contraseña actualizada correctamente."
            });
        })
.RequireAuthorization()
.WithName("ChangePassword");
    }
}
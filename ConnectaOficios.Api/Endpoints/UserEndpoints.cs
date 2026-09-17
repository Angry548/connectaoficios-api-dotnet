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

        group.MapPost("/password/forgot", async (
    ForgotPasswordRequest request,
    IUserServices userServices) =>
        {
            await userServices.RequestPasswordReset(request);

            return Results.Ok(new
            {
                message =
                    "Si existe una cuenta asociada al correo indicado, se enviarán instrucciones para restablecer la contraseña."
            });
        })
.WithName("ForgotPassword");

        group.MapPost("/password/reset", async (
    ResetPasswordRequest request,
    IUserServices userServices) =>
        {
            var result = await userServices.ResetPassword(
                request
            );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "TOKEN_INVALID" => Results.BadRequest(new
                    {
                        message =
                            "El token de recuperación no es válido."
                    }),

                    "TOKEN_EXPIRED" => Results.BadRequest(new
                    {
                        message =
                            "El token de recuperación ha expirado. Solicite uno nuevo."
                    }),

                    "USER_INACTIVE" => Results.BadRequest(new
                    {
                        message =
                            "No fue posible restablecer la contraseña."
                    }),

                    "SAME_PASSWORD" => Results.BadRequest(new
                    {
                        message =
                            "La nueva contraseña debe ser diferente de la contraseña anterior."
                    }),

                    _ => Results.BadRequest(new
                    {
                        message =
                            "No fue posible restablecer la contraseña."
                    })
                };
            }

            return Results.Ok(new
            {
                message =
                    "Contraseña restablecida correctamente."
            });
        })
.WithName("ResetPassword");

        group.MapPut("/me", async (
    UserUpdateRequest request,
    IUserServices userServices,
    ClaimsPrincipal user) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var result = await userServices.UpdateCurrentUser(
                userId,
                request
            );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "USER_NOT_FOUND" =>
                        Results.NotFound(new
                        {
                            message = "Usuario no encontrado."
                        }),

                    "USER_INACTIVE" =>
                        Results.BadRequest(new
                        {
                            message = "La cuenta del usuario se encuentra inactiva."
                        }),

                    "NO_FIELDS" =>
                        Results.BadRequest(new
                        {
                            message = "Debe proporcionar al menos un campo para actualizar."
                        }),

                    "INVALID_NAME" =>
                        Results.BadRequest(new
                        {
                            message = "El nombre no puede estar vacío."
                        }),

                    "INVALID_EMAIL" =>
                        Results.BadRequest(new
                        {
                            message = "El correo no puede estar vacío."
                        }),

                    "EMAIL_EXISTS" =>
                        Results.Conflict(new
                        {
                            message = "El correo indicado ya está registrado."
                        }),

                    _ =>
                        Results.BadRequest(new
                        {
                            message = "No fue posible actualizar los datos del usuario."
                        })
                };
            }

            return Results.Ok(new
            {
                message = "Datos personales actualizados correctamente.",
                user = result.User
            });
        })
.RequireAuthorization(policy =>
    policy.RequireRole("Cliente", "Trabajador"));
    }

}
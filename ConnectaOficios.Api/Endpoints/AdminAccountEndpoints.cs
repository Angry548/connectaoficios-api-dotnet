using ConnectaOficios.Api.DTOs.Admins;
using ConnectaOficios.Api.Services.Admins;
using System.Security.Claims;

namespace ConnectaOficios.Api.Endpoints;

public static class AdminAccountEndpoints
{
    public static void AddAdminAccountEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/admin/accounts")
            .WithTags("Admin Accounts")
            .RequireAuthorization("AdministradorPrincipal");

        // GET: /api/admin/accounts
        group.MapGet("/", async (
            IAdminServices adminServices) =>
        {
            var administradores = await adminServices.GetAll();

            return Results.Ok(administradores);
        })
        .WithName("GetAdminAccounts");

        // GET: /api/admin/accounts/{id}
        group.MapGet("/{id:int}", async (
            int id,
            IAdminServices adminServices) =>
        {
            var administrador = await adminServices.GetById(id);

            if (administrador == null)
            {
                return Results.NotFound(new
                {
                    message = "Cuenta administrativa no encontrada."
                });
            }

            return Results.Ok(administrador);
        })
        .WithName("GetAdminAccountById");

        // POST: /api/admin/accounts
        group.MapPost("/", async (
            AdminAccountRequest request,
            IAdminServices adminServices) =>
        {
            if (request.RolId != 3 &&
                request.RolId != 4)
            {
                return Results.BadRequest(new
                {
                    message =
                        "Solo se pueden crear cuentas con rol Administrador o AdministradorPrincipal."
                });
            }

            var administrador = await adminServices.Create(request);

            if (administrador == null)
            {
                return Results.Conflict(new
                {
                    message =
                        "No fue posible crear la cuenta administrativa. Verifique que el correo no esté registrado."
                });
            }

            return Results.Created(
                $"/api/admin/accounts/{administrador.Id}",
                administrador
            );
        })
        .WithName("CreateAdminAccount");

        // PUT: /api/admin/accounts/{id}
        group.MapPut("/{id:int}", async (
            int id,
            AdminAccountUpdateRequest request,
            IAdminServices adminServices) =>
        {
            var result = await adminServices.Update(
                id,
                request
            );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "NOT_FOUND" => Results.NotFound(new
                    {
                        message = "Cuenta administrativa no encontrada."
                    }),

                    "EMAIL_EXISTS" => Results.Conflict(new
                    {
                        message = "El correo electrónico ya está registrado."
                    }),

                    "INVALID_NAME" => Results.BadRequest(new
                    {
                        message = "El nombre no puede estar vacío."
                    }),

                    "INVALID_EMAIL" => Results.BadRequest(new
                    {
                        message = "El correo no puede estar vacío."
                    }),

                    "NO_FIELDS" => Results.BadRequest(new
                    {
                        message = "Debe proporcionar al menos un campo para actualizar."
                    }),

                    _ => Results.BadRequest(new
                    {
                        message = "No fue posible actualizar la cuenta administrativa."
                    })
                };
            }

            return Results.Ok(result.Admin);
        })
        .WithName("UpdateAdminAccount");

        // PATCH: /api/admin/accounts/{id}/status
        group.MapPatch("/{id:int}/status", async (
            int id,
            AdminStatusRequest request,
            ClaimsPrincipal currentUser,
            IAdminServices adminServices) =>
        {
            if (request.Estado != 1 &&
                request.Estado != 2)
            {
                return Results.BadRequest(new
                {
                    message =
                        "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                });
            }

            var userIdValue = currentUser.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            if (!int.TryParse(
                userIdValue,
                out var currentUserId))
            {
                return Results.Unauthorized();
            }

            var result = await adminServices.ChangeStatus(
                id,
                request.Estado,
                currentUserId
            );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "NOT_FOUND" => Results.NotFound(new
                    {
                        message =
                            "Cuenta administrativa no encontrada."
                    }),

                    "CANNOT_DISABLE_SELF" => Results.Conflict(new
                    {
                        message =
                            "No puede desactivar su propia cuenta administrativa."
                    }),

                    "LAST_ACTIVE_PRINCIPAL" => Results.Conflict(new
                    {
                        message =
                            "No se puede desactivar el último Administrador Principal activo."
                    }),

                    "INVALID_STATUS" => Results.BadRequest(new
                    {
                        message =
                            "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                    }),

                    _ => Results.BadRequest(new
                    {
                        message =
                            "No fue posible cambiar el estado de la cuenta administrativa."
                    })
                };
            }

            return Results.Ok(result.Admin);
        })
        .WithName("ChangeAdminAccountStatus");
    }
}
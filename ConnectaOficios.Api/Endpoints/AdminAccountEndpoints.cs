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
            .MapGroup(
                "/api/admin/accounts"
            )
            .WithTags(
                "Admin Accounts"
            )
            .RequireAuthorization(
                "AdministradorPrincipal"
            );

        group.MapGet("/", async (
            IAdminServices adminServices,
            string? nombre,
            string? correo,
            int? rolId,
            int? estado,
            int page = 1,
            int pageSize = 20) =>
        {
            if (
                rolId.HasValue &&
                rolId.Value != 3 &&
                rolId.Value != 4
            )
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "El rol debe corresponder a Administrador o Administrador Principal."
                    }
                );
            }

            if (
                estado.HasValue &&
                estado.Value != 1 &&
                estado.Value != 2
            )
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                    }
                );
            }

            var administradores =
                await adminServices.GetAll(
                    nombre,
                    correo,
                    rolId,
                    estado,
                    page,
                    pageSize
                );

            return Results.Ok(
                administradores
            );
        })
        .WithName(
            "GetAdminAccounts"
        );

        group.MapGet("/search", async (
            IAdminServices adminServices,
            string? texto,
            int? rolId,
            int limit = 10) =>
        {
            if (
                string.IsNullOrWhiteSpace(
                    texto
                ) ||
                texto.Trim().Length < 2
            )
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "Debe proporcionar al menos 2 caracteres para realizar la búsqueda."
                    }
                );
            }

            if (
                rolId.HasValue &&
                rolId.Value != 3 &&
                rolId.Value != 4
            )
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "El rol debe corresponder a Administrador o Administrador Principal."
                    }
                );
            }

            var administradores =
                await adminServices.Search(
                    texto,
                    rolId,
                    limit
                );

            return Results.Ok(
                administradores
            );
        })
        .WithName(
            "SearchAdminAccounts"
        );

        group.MapGet("/{id:int}", async (
            int id,
            IAdminServices adminServices) =>
        {
            if (id <= 0)
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "El identificador de la cuenta administrativa no es válido."
                    }
                );
            }

            var administrador =
                await adminServices
                    .GetById(id);

            if (administrador == null)
            {
                return Results.NotFound(
                    new
                    {
                        message =
                            "Cuenta administrativa no encontrada."
                    }
                );
            }

            return Results.Ok(
                administrador
            );
        })
        .WithName(
            "GetAdminAccountById"
        );

        group.MapPost("/", async (
            AdminAccountRequest request,
            IAdminServices adminServices) =>
        {
            if (
                request.RolId != 3 &&
                request.RolId != 4
            )
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "Solo se pueden crear cuentas con rol Administrador o AdministradorPrincipal."
                    }
                );
            }

            var administrador =
                await adminServices.Create(
                    request
                );

            if (administrador == null)
            {
                return Results.Conflict(
                    new
                    {
                        message =
                            "No fue posible crear la cuenta administrativa. Verifique los datos y que el correo no esté registrado."
                    }
                );
            }

            return Results.Created(
                $"/api/admin/accounts/{administrador.Id}",
                administrador
            );
        })
        .WithName(
            "CreateAdminAccount"
        );

        group.MapPut("/{id:int}", async (
            int id,
            AdminAccountUpdateRequest request,
            IAdminServices adminServices) =>
        {
            if (id <= 0)
            {
                return Results.BadRequest(
                    new
                    {
                        message =
                            "El identificador de la cuenta administrativa no es válido."
                    }
                );
            }

            var result =
                await adminServices.Update(
                    id,
                    request
                );

            if (!result.Success)
            {
                return result.Error switch
                {
                    "NOT_FOUND" =>
                        Results.NotFound(
                            new
                            {
                                message =
                                    "Cuenta administrativa no encontrada."
                            }
                        ),

                    "EMAIL_EXISTS" =>
                        Results.Conflict(
                            new
                            {
                                message =
                                    "El correo electrónico ya está registrado."
                            }
                        ),

                    "INVALID_NAME" =>
                        Results.BadRequest(
                            new
                            {
                                message =
                                    "El nombre no puede estar vacío."
                            }
                        ),

                    "INVALID_EMAIL" =>
                        Results.BadRequest(
                            new
                            {
                                message =
                                    "El correo no puede estar vacío."
                            }
                        ),

                    "NO_FIELDS" =>
                        Results.BadRequest(
                            new
                            {
                                message =
                                    "Debe proporcionar al menos un campo para actualizar."
                            }
                        ),

                    _ =>
                        Results.BadRequest(
                            new
                            {
                                message =
                                    "No fue posible actualizar la cuenta administrativa."
                            }
                        )
                };
            }

            return Results.Ok(
                result.Admin
            );
        })
        .WithName(
            "UpdateAdminAccount"
        );

        group.MapPatch(
            "/{id:int}/status",
            async (
                int id,
                AdminStatusRequest request,
                ClaimsPrincipal currentUser,
                IAdminServices adminServices) =>
            {
                if (id <= 0)
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "El identificador de la cuenta administrativa no es válido."
                        }
                    );
                }

                if (
                    request.Estado != 1 &&
                    request.Estado != 2
                )
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                        }
                    );
                }

                var userIdValue =
                    currentUser
                        .FindFirstValue(
                            ClaimTypes
                                .NameIdentifier
                        );

                if (
                    !int.TryParse(
                        userIdValue,
                        out var currentUserId
                    )
                )
                {
                    return Results
                        .Unauthorized();
                }

                var result =
                    await adminServices
                        .ChangeStatus(
                            id,
                            request.Estado,
                            currentUserId
                        );

                if (!result.Success)
                {
                    return result.Error switch
                    {
                        "NOT_FOUND" =>
                            Results.NotFound(
                                new
                                {
                                    message =
                                        "Cuenta administrativa no encontrada."
                                }
                            ),

                        "CANNOT_DISABLE_SELF" =>
                            Results.Conflict(
                                new
                                {
                                    message =
                                        "No puede desactivar su propia cuenta administrativa."
                                }
                            ),

                        "LAST_ACTIVE_PRINCIPAL" =>
                            Results.Conflict(
                                new
                                {
                                    message =
                                        "Debe existir al menos un Administrador Principal activo."
                                }
                            ),

                        "INVALID_STATUS" =>
                            Results.BadRequest(
                                new
                                {
                                    message =
                                        "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                                }
                            ),

                        _ =>
                            Results.BadRequest(
                                new
                                {
                                    message =
                                        "No fue posible cambiar el estado de la cuenta administrativa."
                                }
                            )
                    };
                }

                return Results.Ok(
                    result.Admin
                );
            }
        )
        .WithName(
            "ChangeAdminAccountStatus"
        );

        group.MapDelete(
            "/{id:int}",
            async (
                int id,
                ClaimsPrincipal currentUser,
                IAdminServices adminServices) =>
            {
                if (id <= 0)
                {
                    return Results.BadRequest(
                        new
                        {
                            message =
                                "El identificador de la cuenta administrativa no es válido."
                        }
                    );
                }

                var userIdValue =
                    currentUser
                        .FindFirstValue(
                            ClaimTypes
                                .NameIdentifier
                        );

                if (
                    !int.TryParse(
                        userIdValue,
                        out var currentUserId
                    )
                )
                {
                    return Results
                        .Unauthorized();
                }

                var result =
                    await adminServices.Delete(
                        id,
                        currentUserId
                    );

                if (!result.Success)
                {
                    return result.Error switch
                    {
                        "NOT_FOUND" =>
                            Results.NotFound(
                                new
                                {
                                    message =
                                        "Cuenta administrativa no encontrada."
                                }
                            ),

                        "CANNOT_DELETE_SELF" =>
                            Results.Conflict(
                                new
                                {
                                    message =
                                        "No puede eliminar su propia cuenta administrativa."
                                }
                            ),

                        "LAST_ACTIVE_PRINCIPAL" =>
                            Results.Conflict(
                                new
                                {
                                    message =
                                        "No se puede eliminar el último Administrador Principal activo. Debe existir al menos uno."
                                }
                            ),

                        _ =>
                            Results.BadRequest(
                                new
                                {
                                    message =
                                        "No fue posible eliminar la cuenta administrativa."
                                }
                            )
                    };
                }

                return Results.Ok(
                    new
                    {
                        message =
                            "Cuenta administrativa eliminada correctamente."
                    }
                );
            }
        )
        .WithName(
            "DeleteAdminAccount"
        );
    }
}
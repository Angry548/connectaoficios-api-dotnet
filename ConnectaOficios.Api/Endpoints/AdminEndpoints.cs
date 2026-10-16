using ConnectaOficios.Api.DTOs.Users;
using ConnectaOficios.Api.Services.Users;

namespace ConnectaOficios.Api.Endpoints;

public static class AdminEndpoints
{
    public static void AddAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/admin")
            .WithTags("Administration")
            .RequireAuthorization("Administrador");

        group.MapGet("/users", async (
            IUserServices userServices,
            string? nombre,
            string? correo,
            int? rolId,
            int? estado,
            int page = 1,
            int pageSize = 20) =>
        {
            if (rolId.HasValue &&
                rolId.Value != 1 &&
                rolId.Value != 2)
            {
                return Results.BadRequest(new
                {
                    message =
                        "El rol debe corresponder a Cliente o Trabajador."
                });
            }

            if (estado.HasValue &&
                estado.Value != 1 &&
                estado.Value != 2)
            {
                return Results.BadRequest(new
                {
                    message =
                        "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                });
            }

            var usuarios =
                await userServices.GetAll(
                    nombre,
                    correo,
                    rolId,
                    estado,
                    page,
                    pageSize
                );

            return Results.Ok(usuarios);
        })
        .WithName("GetAllUsers");

        group.MapGet("/users/search", async (
            IUserServices userServices,
            string? texto,
            int? rolId,
            int limit = 10) =>
        {
            if (string.IsNullOrWhiteSpace(texto) ||
                texto.Trim().Length < 2)
            {
                return Results.BadRequest(new
                {
                    message =
                        "Debe proporcionar al menos 2 caracteres para realizar la búsqueda."
                });
            }

            if (rolId.HasValue &&
                rolId.Value != 1 &&
                rolId.Value != 2)
            {
                return Results.BadRequest(new
                {
                    message =
                        "El rol debe corresponder a Cliente o Trabajador."
                });
            }

            var usuarios =
                await userServices.Search(
                    texto,
                    rolId,
                    limit
                );

            return Results.Ok(usuarios);
        })
        .WithName("SearchUsers");

        group.MapGet("/users/{id:int}", async (
            int id,
            IUserServices userServices) =>
        {
            if (id <= 0)
            {
                return Results.BadRequest(new
                {
                    message =
                        "El identificador del usuario no es válido."
                });
            }

            var usuario =
                await userServices.GetById(id);

            if (usuario == null ||
                (usuario.RolId != 1 &&
                 usuario.RolId != 2))
            {
                return Results.NotFound(new
                {
                    message =
                        "Usuario no encontrado."
                });
            }

            return Results.Ok(usuario);
        })
        .WithName("GetUserById");

        group.MapPatch(
            "/users/{id:int}/status",
            async (
                int id,
                UserStatusRequest request,
                IUserServices userServices) =>
            {
                if (id <= 0)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "El identificador del usuario no es válido."
                    });
                }

                if (request.Estado != 1 &&
                    request.Estado != 2)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                    });
                }

                var usuarioActual =
                    await userServices.GetById(id);

                if (usuarioActual == null ||
                    (usuarioActual.RolId != 1 &&
                     usuarioActual.RolId != 2))
                {
                    return Results.NotFound(new
                    {
                        message =
                            "Usuario no encontrado."
                    });
                }

                var usuario =
                    await userServices.ChangeStatus(
                        id,
                        request.Estado
                    );

                if (usuario == null)
                {
                    return Results.BadRequest(new
                    {
                        message =
                            "No fue posible cambiar el estado del usuario."
                    });
                }

                return Results.Ok(usuario);
            }
        )
        .WithName("ChangeUserStatus");
    }
}
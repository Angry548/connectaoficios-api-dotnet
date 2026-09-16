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

        // GET: /api/admin/users
        group.MapGet("/users", async (
            IUserServices userServices,
            string? nombre,
            string? correo,
            int? rolId,
            int? estado) =>
        {
            var usuarios = await userServices.GetAll(
                nombre,
                correo,
                rolId,
                estado
            );

            return Results.Ok(usuarios);
        })
        .WithName("GetAllUsers");

        // GET: /api/admin/users/{id}
        group.MapGet("/users/{id:int}", async (
            int id,
            IUserServices userServices) =>
        {
            var usuario = await userServices.GetById(id);

            if (usuario == null)
            {
                return Results.NotFound(new
                {
                    message = "Usuario no encontrado."
                });
            }

            return Results.Ok(usuario);
        })
        .WithName("GetUserById");

        // PATCH: /api/admin/users/{id}/status
        group.MapPatch("/users/{id:int}/status", async (
            int id,
            UserStatusRequest request,
            IUserServices userServices) =>
        {
            if (request.Estado != 1 && request.Estado != 2)
            {
                return Results.BadRequest(new
                {
                    message = "El estado debe ser 1 (Activo) o 2 (Inactivo)."
                });
            }

            var usuario = await userServices.ChangeStatus(
                id,
                request.Estado
            );

            if (usuario == null)
            {
                return Results.NotFound(new
                {
                    message = "Usuario no encontrado."
                });
            }

            return Results.Ok(usuario);
        })
        .WithName("ChangeUserStatus");
    }
}
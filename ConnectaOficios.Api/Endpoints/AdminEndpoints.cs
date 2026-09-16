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
    }
}
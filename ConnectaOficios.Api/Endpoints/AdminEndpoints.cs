namespace ConnectaOficios.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminGroup(
        this IEndpointRouteBuilder routes)
    {
        return routes
            .MapGroup("/api/admin")
            .WithTags("Administration")
            .RequireAuthorization("Administrador");
    }
}
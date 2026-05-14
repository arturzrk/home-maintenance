using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.Properties.Commands;
using HomeMaintenance.Application.Properties.Dto;
using HomeMaintenance.Application.Properties.Queries;
using MiniValidation;

namespace HomeMaintenance.API.Endpoints;

public static class PropertyEndpoints
{
    public static IEndpointRouteBuilder MapPropertyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/properties")
            .WithTags("Properties")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreatePropertyRequest body,
            CreatePropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(new CreatePropertyCommand(body.Name), ct);
            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/properties/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("CreateProperty");

        group.MapGet("/", async (
            ListPropertiesHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new ListPropertiesQuery(), ct);
            return result.ToHttp(ctx);
        })
        .WithName("ListProperties");

        group.MapGet("/{id}", async (
            string id,
            GetPropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetPropertyQuery(id), ct);
            return result.ToHttp(ctx);
        })
        .WithName("GetProperty");

        group.MapPatch("/{id}", async (
            string id,
            RenamePropertyRequest body,
            RenamePropertyHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(new RenamePropertyCommand(id, body.Name), ct);
            return result.ToHttp(ctx);
        })
        .WithName("RenameProperty");

        return app;
    }
}

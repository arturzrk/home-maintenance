using System.ComponentModel.DataAnnotations;
using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.Assets.Commands;
using HomeMaintenance.Application.Assets.Queries;
using MiniValidation;

namespace HomeMaintenance.API.Endpoints;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/assets")
            .WithTags("Assets")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateAssetApiRequest body,
            CreateAssetHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new CreateAssetCommand(body.PropertyId, body.Name, body.Category, body.Notes),
                ct);

            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/assets/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("CreateAsset");

        group.MapGet("/", async (
            string propertyId,
            ListAssetsHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new ListAssetsQuery(propertyId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("ListAssets");

        group.MapGet("/{id}", async (
            string id,
            GetAssetHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetAssetQuery(id), ct);
            return result.ToHttp(ctx);
        })
        .WithName("GetAsset");

        group.MapPatch("/{id}", async (
            string id,
            UpdateAssetApiRequest body,
            UpdateAssetHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new UpdateAssetCommand(id, body.Name, body.Category, body.Notes, body.IsObsolete),
                ct);

            return result.ToHttp(ctx);
        })
        .WithName("UpdateAsset");

        return app;
    }
}

// ---- API request DTOs (validation annotations live here, not on the shared Application DTOs) ----

public sealed record CreateAssetApiRequest(
    [property: Required]
    string PropertyId,
    [property: Required]
    [property: StringLength(200, MinimumLength = 1)]
    string Name,
    [property: StringLength(100)]
    string? Category,
    [property: StringLength(2000)]
    string? Notes);

public sealed record UpdateAssetApiRequest(
    [property: StringLength(200, MinimumLength = 1)]
    string? Name,
    [property: StringLength(100)]
    string? Category,
    [property: StringLength(2000)]
    string? Notes,
    bool? IsObsolete);

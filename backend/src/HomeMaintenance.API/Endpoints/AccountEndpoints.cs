using System.ComponentModel.DataAnnotations;
using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.Account.Commands;
using HomeMaintenance.Application.Account.Queries;
using MiniValidation;

namespace HomeMaintenance.API.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/notification-preferences", async (
            GetNotificationPreferencesHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetNotificationPreferencesQuery(), ct);
            return result.ToHttp(ctx);
        })
        .WithName("GetNotificationPreferences");

        group.MapPatch("/notification-preferences", async (
            UpdateNotificationPreferencesApiRequest body,
            UpdateNotificationPreferencesHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new UpdateNotificationPreferencesCommand(body.RemindersEnabled!.Value),
                ct);
            return result.ToHttp(ctx);
        })
        .WithName("UpdateNotificationPreferences");

        return app;
    }
}

public sealed record UpdateNotificationPreferencesApiRequest(
    [property: Required]
    bool? RemindersEnabled);

using System.ComponentModel.DataAnnotations;
using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.JobDefinitions.Commands;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.JobDefinitions.Queries;
using MiniValidation;

namespace HomeMaintenance.API.Endpoints;

public static class JobDefinitionEndpoints
{
    public static IEndpointRouteBuilder MapJobDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/job-definitions")
            .WithTags("JobDefinitions")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateJobDefinitionApiRequest body,
            CreateJobDefinitionHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var stepDescriptions = (body.StepTemplates ?? Array.Empty<StepTemplateCreateRequest>())
                .Select(s => s.Description)
                .ToList();

            var result = await handler.Handle(
                new CreateJobDefinitionCommand(body.PropertyId, body.Name, body.Schedule, stepDescriptions, body.AssetId),
                ct);

            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/job-definitions/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("CreateJobDefinition");

        group.MapGet("/", async (
            string? propertyId,
            string? assetId,
            ListJobDefinitionsHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new ListJobDefinitionsQuery(propertyId, assetId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("ListJobDefinitions");

        group.MapGet("/{id}", async (
            string id,
            GetJobDefinitionHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetJobDefinitionQuery(id), ct);
            return result.ToHttp(ctx);
        })
        .WithName("GetJobDefinition");

        group.MapPatch("/{id}", async (
            string id,
            UpdateJobDefinitionApiRequest body,
            UpdateJobDefinitionHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var addStepDescriptions = body.AddStepTemplates?
                .Select(s => s.Description)
                .ToList();

            var result = await handler.Handle(
                new UpdateJobDefinitionCommand(
                    id,
                    body.Name,
                    body.Schedule,
                    addStepDescriptions,
                    body.RemoveStepTemplateIds,
                    body.ReorderStepTemplateIds,
                    body.EditStepTemplates),
                ct);

            return result.ToHttp(ctx);
        })
        .WithName("UpdateJobDefinition");

        group.MapPost("/{id}/generate-next", async (
            string id,
            GenerateNextOccurrenceHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GenerateNextOccurrenceCommand(id), ct);

            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/jobs/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("GenerateNextOccurrence");

        return app;
    }
}

// ---- API request DTOs (validation annotations live here, not on the shared Application DTOs) ----

public sealed record StepTemplateCreateRequest(
    [property: Required]
    [property: StringLength(500, MinimumLength = 1)]
    string Description);

public sealed record CreateJobDefinitionApiRequest(
    [property: Required]
    string PropertyId,
    [property: Required]
    [property: StringLength(200, MinimumLength = 1)]
    string Name,
    [property: Required]
    ScheduleDefinitionDto Schedule,
    IReadOnlyList<StepTemplateCreateRequest>? StepTemplates,
    string? AssetId = null);

public sealed record UpdateJobDefinitionApiRequest(
    [property: StringLength(200, MinimumLength = 1)]
    string? Name,
    ScheduleDefinitionDto? Schedule,
    [property: MinLength(1)]
    IReadOnlyList<StepTemplateCreateRequest>? AddStepTemplates,
    [property: MinLength(1)]
    IReadOnlyList<string>? RemoveStepTemplateIds,
    [property: MinLength(1)]
    IReadOnlyList<StepTemplateEdit>? EditStepTemplates,
    [property: MinLength(1)]
    IReadOnlyList<string>? ReorderStepTemplateIds);

using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.Jobs.Commands;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Jobs.Queries;
using HomeMaintenance.Domain.Jobs;
using MiniValidation;

namespace HomeMaintenance.API.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app
            .MapGroup("/api/jobs")
            .WithTags("Jobs")
            .RequireAuthorization();

        group.MapPost("/", async (
            CreateJobRequest body,
            CreateJobHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var stepDescriptions = body.Steps
                .Select(s => s.Description)
                .ToList();

            var result = await handler.Handle(
                new CreateJobCommand(body.PropertyId, body.Name, body.DueDate, stepDescriptions, body.AssetId),
                ct);

            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/jobs/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("CreateJob");

        group.MapGet("/", async (
            string? propertyId,
            string? status,
            string? assetId,
            ListJobsHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            JobStatus? parsedStatus = null;
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<JobStatus>(status, ignoreCase: true, out var s))
                    return Results.BadRequest(new
                    {
                        code = "validation",
                        detail = $"Unknown status '{status}'. Use Active or Completed.",
                    });
                parsedStatus = s;
            }

            var result = await handler.Handle(new ListJobsQuery(propertyId, parsedStatus, assetId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("ListJobs");

        group.MapGet("/{id}", async (
            string id,
            GetJobHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new GetJobQuery(id), ct);
            return result.ToHttp(ctx);
        })
        .WithName("GetJob");

        group.MapPost("/{id}/complete", async (
            string id,
            CompleteJobHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new CompleteJobCommand(id), ct);
            return result.ToHttp(ctx);
        })
        .WithName("CompleteJob");

        group.MapPost("/{jobId}/steps/{stepId}/tick", async (
            string jobId,
            string stepId,
            TickStepHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new TickStepCommand(jobId, stepId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("TickStep");

        group.MapPost("/{jobId}/steps/{stepId}/untick", async (
            string jobId,
            string stepId,
            UntickStepHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new UntickStepCommand(jobId, stepId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("UntickStep");

        // ---- Step mutation (WP07) ----

        group.MapPost("/{jobId}/steps", async (
            string jobId,
            AddStepRequest body,
            AddStepHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(new AddStepCommand(jobId, body.Description), ct);
            return result.ToHttp(ctx);
        })
        .WithName("AddStep");

        group.MapDelete("/{jobId}/steps/{stepId}", async (
            string jobId,
            string stepId,
            RemoveStepHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            var result = await handler.Handle(new RemoveStepCommand(jobId, stepId), ct);
            return result.ToHttp(ctx);
        })
        .WithName("RemoveStep");

        group.MapPatch("/{jobId}/steps/{stepId}", async (
            string jobId,
            string stepId,
            EditStepDescriptionRequest body,
            EditStepDescriptionHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new EditStepDescriptionCommand(jobId, stepId, body.Description), ct);
            return result.ToHttp(ctx);
        })
        .WithName("EditStepDescription");

        group.MapPut("/{jobId}/steps/order", async (
            string jobId,
            ReorderStepsRequest body,
            ReorderStepsHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new ReorderStepsCommand(jobId, body.OrderedStepIds), ct);
            return result.ToHttp(ctx);
        })
        .WithName("ReorderSteps");

        // ---- Job-level rename + due date (WP07) ----

        group.MapPatch("/{id}", async (
            string id,
            HttpRequest request,
            UpdateJobHandler handler,
            HttpContext ctx,
            CancellationToken ct) =>
        {
            // Read raw JSON so we can distinguish "dueDate" omitted from
            // "dueDate": null (clear). System.Text.Json deserialization to
            // UpdateJobRequest would collapse both to null.
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
            var root = doc.RootElement;

            string? name = null;
            if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                name = nameEl.GetString();

            bool dueDateProvided = root.TryGetProperty("dueDate", out var dueEl);
            DateOnly? dueDate = null;
            if (dueDateProvided && dueEl.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                var s = dueEl.GetString();
                if (!string.IsNullOrEmpty(s))
                {
                    if (!DateOnly.TryParse(s, out var parsed))
                        return Results.ValidationProblem(new Dictionary<string, string[]>
                        {
                            ["dueDate"] = new[] { "Must be ISO-8601 date (YYYY-MM-DD)." },
                        });
                    dueDate = parsed;
                }
            }

            var body = new UpdateJobRequest(name, dueDate);
            if (!MiniValidator.TryValidate(body, out var errors))
                return Results.ValidationProblem(errors);

            var result = await handler.Handle(
                new UpdateJobCommand(id, name, dueDateProvided, dueDate), ct);
            return result.ToHttp(ctx);
        })
        .WithName("UpdateJob");

        return app;
    }
}

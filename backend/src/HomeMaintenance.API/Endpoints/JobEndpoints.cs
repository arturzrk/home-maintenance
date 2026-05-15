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
                new CreateJobCommand(body.PropertyId, body.Name, body.DueDate, stepDescriptions),
                ct);

            return result.IsSuccess
                ? result.ToHttpCreated(ctx, $"/api/jobs/{result.Value!.Id}")
                : result.ToHttp(ctx);
        })
        .WithName("CreateJob");

        group.MapGet("/", async (
            string? propertyId,
            string? status,
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

            var result = await handler.Handle(new ListJobsQuery(propertyId, parsedStatus), ct);
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

        return app;
    }
}

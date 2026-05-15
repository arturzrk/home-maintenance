using HomeMaintenance.Application.Common;

namespace HomeMaintenance.API.Middleware;

/// <summary>
/// Maps <see cref="Result{T}"/> values returned by handlers to
/// HTTP responses. Success cases use the conventional 200/201/204
/// surface; failures are rendered as RFC 7807 problem-details with
/// the error's <c>Code</c> and the request's correlation id baked
/// into the body (contracts/README.md).
/// </summary>
public static class ResultToHttpExtensions
{
    public static IResult ToHttp<T>(this Result<T> result, HttpContext ctx)
        => result.IsSuccess
            ? Results.Ok(result.Value)
            : result.Error!.ToProblem(ctx);

    public static IResult ToHttpCreated<T>(this Result<T> result, HttpContext ctx, string location)
        => result.IsSuccess
            ? Results.Created(location, result.Value)
            : result.Error!.ToProblem(ctx);

    public static IResult ToHttpNoContent<T>(this Result<T> result, HttpContext ctx)
        => result.IsSuccess
            ? Results.NoContent()
            : result.Error!.ToProblem(ctx);

    internal static IResult ToProblem(this Error error, HttpContext ctx)
    {
        var (status, title) = error switch
        {
            NotFoundError => (StatusCodes.Status404NotFound, "Not Found"),
            ValidationError => (StatusCodes.Status400BadRequest, "Invalid Request"),
            BusinessRuleError => (StatusCodes.Status400BadRequest, "Business Rule Violation"),
            UnauthorizedError => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenError => (StatusCodes.Status404NotFound, "Not Found"), // research.md R5: no leak
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error"),
        };

        // For BusinessRuleError we surface the specific Rule as the body's
        // `code` (matches contracts/jobs.md and contracts/steps.md, where
        // codes like "steps_incomplete" or "job_already_completed" are
        // documented). All other Error variants use their generic Code.
        var bodyCode = error is BusinessRuleError br ? br.Rule : error.Code;

        return Results.Problem(
            type: $"https://home-maintenance/errors/{bodyCode}",
            title: title,
            statusCode: status,
            detail: error.Message,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = bodyCode,
                ["correlationId"] = ctx.GetCorrelationId(),
            });
    }
}

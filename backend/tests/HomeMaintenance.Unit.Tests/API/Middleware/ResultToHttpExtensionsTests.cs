using HomeMaintenance.API.Middleware;
using HomeMaintenance.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.API.Middleware;

public sealed class ResultToHttpExtensionsTests
{
    [Fact]
    public void ToHttp_Success_ReturnsOk()
    {
        var ctx = ContextWithCorrelation("c1");
        var result = Result<int>.Success(42);

        var http = result.ToHttp(ctx);

        http.ShouldBeOfType<Ok<int>>();
    }

    [Theory]
    [InlineData(typeof(NotFoundError), 404, "not_found")]
    [InlineData(typeof(ValidationError), 400, "validation")]
    [InlineData(typeof(BusinessRuleError), 400, "rule_x")] // surfaces Rule, not generic Code
    [InlineData(typeof(UnauthorizedError), 401, "unauthorized")]
    [InlineData(typeof(ForbiddenError), 404, "forbidden")] // R5: no leak
    public void ToHttp_Failure_MapsErrorVariantToHttpStatus(
        Type errorType, int expectedStatus, string expectedCode)
    {
        var ctx = ContextWithCorrelation("c2");
        var error = MakeError(errorType);
        var result = Result<int>.Failure(error);

        var http = result.ToHttp(ctx);

        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(expectedStatus);
        problem.ProblemDetails.Extensions["code"].ShouldBe(expectedCode);
        problem.ProblemDetails.Extensions["correlationId"].ShouldBe("c2");
    }

    [Fact]
    public void ToHttpCreated_Success_ReturnsCreated_WithLocation()
    {
        var ctx = ContextWithCorrelation("c3");
        var result = Result<string>.Success("payload");

        var http = result.ToHttpCreated(ctx, "/api/things/1");

        var created = http.ShouldBeOfType<Created<string>>();
        created.Location.ShouldBe("/api/things/1");
        created.Value.ShouldBe("payload");
    }

    [Fact]
    public void ToHttpNoContent_Success_ReturnsNoContent()
    {
        var ctx = ContextWithCorrelation("c4");
        var result = Result<None>.Success(None.Value);

        var http = result.ToHttpNoContent(ctx);

        http.ShouldBeOfType<NoContent>();
    }

    [Fact]
    public void ToHttpNoContent_Failure_RendersProblem()
    {
        var ctx = ContextWithCorrelation("c5");
        var result = Result<None>.Failure(new NotFoundError("Job", "j1"));

        var http = result.ToHttpNoContent(ctx);

        var problem = http.ShouldBeOfType<ProblemHttpResult>();
        problem.ProblemDetails.Status.ShouldBe(404);
        problem.ProblemDetails.Extensions["code"].ShouldBe("not_found");
    }

    private static HttpContext ContextWithCorrelation(string id)
    {
        var ctx = new DefaultHttpContext();
        ctx.Items["CorrelationId"] = id;
        return ctx;
    }

    private static Error MakeError(Type errorType) => errorType.Name switch
    {
        nameof(NotFoundError) => new NotFoundError("Property", "p1"),
        nameof(ValidationError) => new ValidationError("name", "required"),
        nameof(BusinessRuleError) => new BusinessRuleError("rule_x", "violated"),
        nameof(UnauthorizedError) => new UnauthorizedError(),
        nameof(ForbiddenError) => new ForbiddenError(),
        _ => throw new ArgumentException($"Unknown error type {errorType.Name}"),
    };
}

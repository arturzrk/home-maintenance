using HomeMaintenance.Application.Common;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Common;

public sealed class ResultTests
{
    [Fact]
    public void Result_Success_HoldsValue()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
        result.Value.ShouldBe(42);
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Result_Failure_HoldsError_NotValue()
    {
        var error = new NotFoundError("Property", "abc");
        var result = Result<int>.Failure(error);

        result.IsSuccess.ShouldBeFalse();
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
        result.Value.ShouldBe(0); // default for int
    }

    [Fact]
    public void Result_IsSuccess_TrueOnlyWhenErrorNull()
    {
        var success = Result<string>.Success("ok");
        var failure = Result<string>.Failure(new UnauthorizedError());

        success.IsSuccess.ShouldBeTrue();
        failure.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void Result_None_ConvenientForVoidReturns()
    {
        var result = Result<None>.Success(None.Value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(None.Value);
    }
}

public sealed class ErrorTests
{
    [Fact]
    public void NotFoundError_HasNotFoundCode_AndFormattedMessage()
    {
        var error = new NotFoundError("Property", "abc-123");

        error.Code.ShouldBe("not_found");
        error.Message.ShouldBe("Property abc-123 not found");
    }

    [Fact]
    public void ValidationError_HasValidationCode_AndFieldReasonMessage()
    {
        var error = new ValidationError("name", "must be 1..100 chars");

        error.Code.ShouldBe("validation");
        error.Message.ShouldBe("name: must be 1..100 chars");
    }

    [Fact]
    public void BusinessRuleError_HasBusinessRuleCode_AndCustomMessage()
    {
        var error = new BusinessRuleError("steps_incomplete", "Not all steps are completed");

        error.Code.ShouldBe("business_rule");
        error.Rule.ShouldBe("steps_incomplete");
        error.Message.ShouldBe("Not all steps are completed");
    }

    [Fact]
    public void UnauthorizedError_HasUnauthorizedCode()
    {
        var error = new UnauthorizedError();

        error.Code.ShouldBe("unauthorized");
        error.Message.ShouldBe("Authentication required");
    }

    [Fact]
    public void ForbiddenError_HasForbiddenCode()
    {
        var error = new ForbiddenError();

        error.Code.ShouldBe("forbidden");
        error.Message.ShouldBe("Caller does not own this resource");
    }
}

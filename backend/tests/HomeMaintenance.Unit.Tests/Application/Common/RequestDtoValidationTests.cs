using System.ComponentModel.DataAnnotations;
using HomeMaintenance.Application.JobDefinitions.Dto;
using HomeMaintenance.Application.Jobs.Dto;
using HomeMaintenance.Application.Properties.Dto;
using Shouldly;

namespace HomeMaintenance.Unit.Tests.Application.Common;

/// <summary>
/// Request DTOs carry [Required]/[StringLength]/[MinLength] attributes
/// that MiniValidation enforces at the API layer. These tests exercise
/// that same validation (via the BCL's own Validator, no extra
/// dependency needed) directly against the records, independent of the
/// API/HTTP pipeline.
/// </summary>
public sealed class RequestDtoValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void CreateJobRequest_ValidFields_PassesValidation()
    {
        var request = new CreateJobRequest("prop-1", "Service boiler", null, [new CreateJobStepRequest("Shut off gas")]);

        Validate(request).ShouldBeEmpty();
        request.PropertyId.ShouldBe("prop-1");
        request.Steps.Count.ShouldBe(1);
    }

    [Fact]
    public void CreateJobRequest_EmptyName_FailsValidation()
    {
        var request = new CreateJobRequest("prop-1", "", null, []);

        Validate(request).ShouldNotBeEmpty();
    }

    [Fact]
    public void CreateJobStepRequest_EmptyDescription_FailsValidation()
    {
        var request = new CreateJobStepRequest("");

        Validate(request).ShouldNotBeEmpty();
    }

    [Fact]
    public void AddStepRequest_ValidDescription_PassesValidation()
    {
        var request = new AddStepRequest("New step");

        Validate(request).ShouldBeEmpty();
        request.Description.ShouldBe("New step");
    }

    [Fact]
    public void AddStepRequest_EmptyDescription_FailsValidation()
    {
        Validate(new AddStepRequest("")).ShouldNotBeEmpty();
    }

    [Fact]
    public void EditStepDescriptionRequest_EmptyDescription_FailsValidation()
    {
        Validate(new EditStepDescriptionRequest("")).ShouldNotBeEmpty();
    }

    [Fact]
    public void EditStepDescriptionRequest_ValidDescription_PassesValidation()
    {
        var request = new EditStepDescriptionRequest("Updated");

        Validate(request).ShouldBeEmpty();
        request.Description.ShouldBe("Updated");
    }

    [Fact]
    public void ReorderStepsRequest_EmptyList_FailsValidation()
    {
        Validate(new ReorderStepsRequest([])).ShouldNotBeEmpty();
    }

    [Fact]
    public void ReorderStepsRequest_NonEmptyList_PassesValidation()
    {
        var request = new ReorderStepsRequest(["step-1", "step-2"]);

        Validate(request).ShouldBeEmpty();
        request.OrderedStepIds.Count.ShouldBe(2);
    }

    [Fact]
    public void UpdateJobRequest_NullName_PassesValidation_BecauseNameIsOptional()
    {
        var request = new UpdateJobRequest(null, null);

        Validate(request).ShouldBeEmpty();
        request.DueDate.ShouldBeNull();
    }

    [Fact]
    public void UpdateJobRequest_TooLongName_FailsValidation()
    {
        Validate(new UpdateJobRequest(new string('x', 201), null)).ShouldNotBeEmpty();
    }

    [Fact]
    public void CreatePropertyRequest_EmptyName_FailsValidation()
    {
        Validate(new CreatePropertyRequest("")).ShouldNotBeEmpty();
    }

    [Fact]
    public void CreatePropertyRequest_ValidName_PassesValidation()
    {
        var request = new CreatePropertyRequest("Main House");

        Validate(request).ShouldBeEmpty();
        request.Name.ShouldBe("Main House");
    }

    [Fact]
    public void RenamePropertyRequest_EmptyName_FailsValidation()
    {
        Validate(new RenamePropertyRequest("")).ShouldNotBeEmpty();
    }

    [Fact]
    public void RenamePropertyRequest_ValidName_PassesValidation()
    {
        var request = new RenamePropertyRequest("New Name");

        Validate(request).ShouldBeEmpty();
        request.Name.ShouldBe("New Name");
    }

    [Fact]
    public void StepTemplateEdit_ConstructsWithIdAndDescription()
    {
        var edit = new StepTemplateEdit("step-1", "Check filter");

        edit.Id.ShouldBe("step-1");
        edit.Description.ShouldBe("Check filter");
    }
}

using UCoverCraft.Core.Models;

namespace UCoverCraft.Tests.Models;

public class InstructorTests
{
    [Fact]
    public void Validate_ReturnsNoErrors_WhenNameIsProvided()
    {
        var instructor = new Instructor { Name = "Dr. Rahman", Department = "CSE" };

        Assert.Empty(instructor.Validate());
        Assert.True(instructor.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenNameIsMissing(string? name)
    {
        var instructor = new Instructor { Name = name! };

        var errors = instructor.Validate();

        Assert.Single(errors);
        Assert.StartsWith(nameof(Instructor.Name), errors[0]);
    }

    [Fact]
    public void Validate_IgnoresMissingDepartment()
    {
        var instructor = new Instructor { Name = "Dr. Rahman" };

        Assert.Empty(instructor.Validate());
    }
}

using UCoverCraft.Core.Models;

namespace UCoverCraft.Tests.Models;

public class StudentTests
{
    [Fact]
    public void Validate_ReturnsNoErrors_WhenNameIsProvided()
    {
        var student = new Student { Name = "Jane Doe", StudentId = "S-1001" };

        Assert.Empty(student.Validate());
        Assert.True(student.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_ReturnsError_WhenNameIsMissing(string? name)
    {
        var student = new Student { Name = name! };

        var errors = student.Validate();

        Assert.Single(errors);
        Assert.StartsWith(nameof(Student.Name), errors[0]);
    }

    [Fact]
    public void Validate_IgnoresMissingStudentId()
    {
        var student = new Student { Name = "Jane Doe" };

        Assert.Empty(student.Validate());
    }

    [Fact]
    public void Students_AreIndependent()
    {
        var first = new Student { Name = "Jane Doe" };
        var second = new Student { Name = "John Smith" };

        Assert.Equal("Jane Doe", first.Name);
        Assert.Equal("John Smith", second.Name);
        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
    }
}

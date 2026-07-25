using FluentAssertions;
using FoodLoop.Application.Common.Models;
using Xunit;

namespace FoodLoop.Application.Tests.Common.Models;

public class ResultTests
{
    [Fact]
    public void Ok_should_produce_a_successful_result_with_no_errors()
    {
        var result = Result.Ok();

        result.Success.Should().BeTrue();
        result.Message.Should().BeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_should_produce_an_unsuccessful_result_with_the_given_message()
    {
        var result = Result.Fail("Something went wrong.");

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Something went wrong.");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Fail_should_capture_the_supplied_error_details()
    {
        var errors = new[] { "Email is required.", "Password is too short." };

        var result = Result.Fail("Validation failed.", errors);

        result.Success.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Generic_Ok_should_carry_the_payload()
    {
        var result = Result<string>.Ok("payload");

        result.Success.Should().BeTrue();
        result.Data.Should().Be("payload");
    }

    [Fact]
    public void Generic_Fail_should_have_no_payload()
    {
        var result = Result<string>.Fail("nope");

        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Be("nope");
    }
}

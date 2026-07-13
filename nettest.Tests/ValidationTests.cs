using System.ComponentModel.DataAnnotations;
using nettest.Dtos;

namespace nettest.Tests;

public class ValidationTests
{
    [Fact]
    public void CreateUserDto_requires_valid_email_password_and_role()
    {
        var dto = new CreateUserDto
        {
            Email = "not-an-email",
            Password = "short",
            Role = "Owner"
        };

        var errors = Validate(dto);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateUserDto.Email)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateUserDto.Password)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateUserDto.Role)));
    }

    [Fact]
    public void CreatePropertyDto_requires_name_and_address()
    {
        var errors = Validate(new CreatePropertyDto());

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreatePropertyDto.Name)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreatePropertyDto.Address)));
    }

    [Fact]
    public void CreateUnitDto_requires_positive_unit_number()
    {
        var errors = Validate(new CreateUnitDto { UnitNumber = 0 });

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(CreateUnitDto.UnitNumber)));
    }

    [Fact]
    public void LoginDto_requires_email_and_password()
    {
        var errors = Validate(new LoginDto());

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LoginDto.Email)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(LoginDto.Password)));
    }

    [Fact]
    public void RegisterDto_requires_email_and_password()
    {
        var errors = Validate(new RegisterDto { Email = "not-an-email", Password = "short" });

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterDto.Email)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void VerifyEmailDto_requires_six_digit_code()
    {
        var errors = Validate(new VerifyEmailDto { Email = "not-an-email", Code = "123" });

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(VerifyEmailDto.Email)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(VerifyEmailDto.Code)));
    }

    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            dto,
            new ValidationContext(dto),
            results,
            validateAllProperties: true);

        return results;
    }
}

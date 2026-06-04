using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class CreateUserDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = "";

    [Required]
    [RegularExpression("^(Admin|Landlord|Tenant)$", ErrorMessage = "Role must be Admin, Landlord, or Tenant.")]
    public string Role { get; set; } = "Tenant";
}
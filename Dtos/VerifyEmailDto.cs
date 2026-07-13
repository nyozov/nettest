using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class VerifyEmailDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = "";
}

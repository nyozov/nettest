using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class ResendConfirmationDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
}

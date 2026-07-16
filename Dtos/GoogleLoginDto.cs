using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class GoogleLoginDto
{
    [Required]
    public string IdToken { get; set; } = "";
}

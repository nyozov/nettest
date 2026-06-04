using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class CreatePropertyDto
{
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string Address { get; set; } = "";

}

using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class CreateMaintenanceRequestDto
{
    [Required]
    public string Title { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

}

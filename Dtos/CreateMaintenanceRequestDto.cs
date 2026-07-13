using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace nettest.Dtos;

public class CreateMaintenanceRequestDto
{
    [Required]
    public string Title { get; set; } = "";

    [Required]
    public string Description { get; set; } = "";

}

public class CreateMaintenanceRequestWithImagesDto : CreateMaintenanceRequestDto
{
    public List<IFormFile> Images { get; set; } = [];
}

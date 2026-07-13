using System.ComponentModel.DataAnnotations;

namespace nettest.Dtos;

public class UpdateUnitDto
{
    [Range(1, int.MaxValue)]
    public int UnitNumber { get; set; }
}

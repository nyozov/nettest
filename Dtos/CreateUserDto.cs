namespace nettest.Dtos;

public class CreateUserDto
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "Tenant";
}
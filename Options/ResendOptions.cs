namespace nettest.Options;

public class ResendOptions
{
    public string ApiKey { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "NestOps";
    public string InviteSubject { get; set; } = "Your invite code";
    public string? InviteUrl { get; set; }
}

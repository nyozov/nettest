namespace nettest.Options;

public class ResendOptions
{
    public string ApiKey { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public string FromName { get; set; } = "NestOps";
    public string InviteSubject { get; set; } = "Your invite code";
    public string ConfirmationSubject { get; set; } = "Confirm your NestOps account";
    public string PasswordResetSubject { get; set; } = "Reset your NestOps password";
    public string? InviteUrl { get; set; }
}

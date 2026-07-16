namespace nettest.Services;

public record GoogleTokenUser(string Email, bool EmailVerified);

public interface IGoogleTokenVerifier
{
    Task<GoogleTokenUser?> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default);
}

using Google.Apis.Auth;

namespace nettest.Services;

public class GoogleTokenVerifier(IConfiguration config) : IGoogleTokenVerifier
{
    private readonly IConfiguration _config = config;

    public async Task<GoogleTokenUser?> VerifyAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var clientId = _config["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InvalidOperationException(
                "Authentication:Google:ClientId must be configured for Google login.");
        }

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = [clientId]
                });

            return new GoogleTokenUser(payload.Email, payload.EmailVerified);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
    }
}

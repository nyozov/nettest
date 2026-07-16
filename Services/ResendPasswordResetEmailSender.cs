namespace nettest.Services;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using nettest.Models;
using nettest.Options;

public class ResendPasswordResetEmailSender(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendPasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    private readonly ResendOptions options = options.Value;

    public async Task SendPasswordResetCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Resend:ApiKey is required to send password reset emails.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new InvalidOperationException("Resend:FromEmail is required to send password reset emails.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendEmailRequest(
                From: BuildFromAddress(),
                To: [user.Email],
                Subject: options.PasswordResetSubject,
                Html: BuildHtmlContent(code),
                Text: BuildPlainTextContent(code)))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Resend rejected password reset email for user {UserId} with status {StatusCode}: {ResponseBody}",
                user.Id,
                response.StatusCode,
                body);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(body)
                    ? "Password reset email could not be sent."
                    : $"Password reset email could not be sent: {body}");
        }
    }

    private string BuildFromAddress()
    {
        if (options.FromEmail.Contains('<'))
        {
            return options.FromEmail;
        }

        return string.IsNullOrWhiteSpace(options.FromName)
            ? options.FromEmail
            : $"{options.FromName} <{options.FromEmail}>";
    }

    private static string BuildPlainTextContent(string code)
    {
        return string.Join(
            Environment.NewLine,
            [
                "Reset your NestOps password.",
                "",
                $"Password reset code: {code}",
                "This code expires in 15 minutes.",
                "If you did not request this, you can ignore this email."
            ]);
    }

    private static string BuildHtmlContent(string code)
    {
        var encodedCode = System.Net.WebUtility.HtmlEncode(code);

        return $"""
               <p>Reset your NestOps password.</p>
               <p>Enter this code to choose a new password:</p>
               <p><strong style="font-size: 24px; letter-spacing: 4px;">{encodedCode}</strong></p>
               <p>This code expires in 15 minutes.</p>
               <p>If you did not request this, you can ignore this email.</p>
               """;
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}

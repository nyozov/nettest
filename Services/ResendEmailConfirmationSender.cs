namespace nettest.Services;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using nettest.Models;
using nettest.Options;

public class ResendEmailConfirmationSender(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendEmailConfirmationSender> logger) : IEmailConfirmationSender
{
    private readonly ResendOptions options = options.Value;

    public async Task SendConfirmationCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Resend:ApiKey is required to send confirmation emails.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new InvalidOperationException("Resend:FromEmail is required to send confirmation emails.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendEmailRequest(
                From: BuildFromAddress(),
                To: [user.Email],
                Subject: options.ConfirmationSubject,
                Html: BuildHtmlContent(code),
                Text: BuildPlainTextContent(code)))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Resend rejected confirmation email for user {UserId} with status {StatusCode}: {ResponseBody}",
                user.Id,
                response.StatusCode,
                body);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(body)
                    ? "Confirmation email could not be sent."
                    : $"Confirmation email could not be sent: {body}");
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
                "Confirm your NestOps account.",
                "",
                $"Confirmation code: {code}",
                "This code expires in 15 minutes."
            ]);
    }

    private static string BuildHtmlContent(string code)
    {
        var encodedCode = System.Net.WebUtility.HtmlEncode(code);

        return $"""
               <p>Confirm your NestOps account.</p>
               <p>Enter this code to finish creating your account:</p>
               <p><strong style="font-size: 24px; letter-spacing: 4px;">{encodedCode}</strong></p>
               <p>This code expires in 15 minutes.</p>
               """;
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}

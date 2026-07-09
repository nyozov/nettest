namespace nettest.Services;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using nettest.Models;
using nettest.Options;

public class ResendInviteEmailSender(
    HttpClient httpClient,
    IOptions<ResendOptions> options,
    ILogger<ResendInviteEmailSender> logger) : IInviteEmailSender
{
    private readonly ResendOptions options = options.Value;

    public async Task SendInviteAsync(Invite invite, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invite.SentToEmail))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Resend:ApiKey is required to send invite emails.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new InvalidOperationException("Resend:FromEmail is required to send invite emails.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
        {
            Content = JsonContent.Create(new ResendEmailRequest(
                From: BuildFromAddress(),
                To: [invite.SentToEmail],
                Subject: options.InviteSubject,
                Html: BuildHtmlContent(invite),
                Text: BuildPlainTextContent(invite)))
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "Resend rejected invite email for invite {InviteId} with status {StatusCode}: {ResponseBody}",
                invite.Id,
                response.StatusCode,
                body);

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(body)
                    ? "Invite email could not be sent."
                    : $"Invite email could not be sent: {body}");
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

    private string BuildPlainTextContent(Invite invite)
    {
        var lines = new List<string>
        {
            "You have been invited to NestOps.",
            "",
            $"Invite code: {invite.Code}",
            $"This invite expires on {invite.ExpiresAt:yyyy-MM-dd}.",
            "",
            "Create your account to view your property and unit."
        };

        if (!string.IsNullOrWhiteSpace(options.InviteUrl))
        {
            lines.Add("");
            lines.Add($"Create your account here: {BuildInviteLink(invite)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildHtmlContent(Invite invite)
    {
        var encodedCode = System.Net.WebUtility.HtmlEncode(invite.Code);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(BuildInviteLink(invite));

        var callToAction = string.IsNullOrWhiteSpace(options.InviteUrl)
            ? ""
            : $"""
               <p>
                 <a href="{encodedUrl}">Create your account</a>
               </p>
               """;

        return $"""
               <p>You have been invited to NestOps.</p>
               <p>Create your account to view your property and unit dashboard.</p>
               <p>Your invite code is:</p>
               <p><strong style="font-size: 20px; letter-spacing: 2px;">{encodedCode}</strong></p>
               <p>This invite expires on {invite.ExpiresAt:yyyy-MM-dd}.</p>
               {callToAction}
               """;
    }

    private string BuildInviteLink(Invite invite)
    {
        if (string.IsNullOrWhiteSpace(options.InviteUrl))
        {
            return "";
        }

        var separator = options.InviteUrl.Contains('?') ? "&" : "?";
        return $"{options.InviteUrl}{separator}code={Uri.EscapeDataString(invite.Code)}";
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html,
        [property: JsonPropertyName("text")] string Text);
}

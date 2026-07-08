namespace nettest.Services;

using Microsoft.Extensions.Options;
using nettest.Models;
using nettest.Options;
using SendGrid;
using SendGrid.Helpers.Mail;

public class SendGridInviteEmailSender(
    IOptions<SendGridOptions> options,
    ILogger<SendGridInviteEmailSender> logger) : IInviteEmailSender
{
    private readonly SendGridOptions options = options.Value;

    public async Task SendInviteAsync(Invite invite, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(invite.SentToEmail))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("SendGrid:ApiKey is required to send invite emails.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            throw new InvalidOperationException("SendGrid:FromEmail is required to send invite emails.");
        }

        var client = new SendGridClient(options.ApiKey);
        var from = new EmailAddress(options.FromEmail, options.FromName);
        var to = new EmailAddress(invite.SentToEmail);
        var subject = options.InviteSubject;
        var plainTextContent = BuildPlainTextContent(invite);
        var htmlContent = BuildHtmlContent(invite);
        var message = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);

        var response = await client.SendEmailAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            logger.LogWarning(
                "SendGrid rejected invite email for invite {InviteId} with status {StatusCode}: {ResponseBody}",
                invite.Id,
                response.StatusCode,
                body);

            throw new InvalidOperationException("Invite email could not be sent.");
        }
    }

    private string BuildPlainTextContent(Invite invite)
    {
        var lines = new List<string>
        {
            "You have been invited.",
            "",
            $"Invite code: {invite.Code}",
            $"This invite expires on {invite.ExpiresAt:yyyy-MM-dd}."
        };

        if (!string.IsNullOrWhiteSpace(options.InviteUrl))
        {
            lines.Add("");
            lines.Add($"Use your invite here: {options.InviteUrl}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildHtmlContent(Invite invite)
    {
        var encodedCode = System.Net.WebUtility.HtmlEncode(invite.Code);
        var encodedUrl = System.Net.WebUtility.HtmlEncode(options.InviteUrl);

        var callToAction = string.IsNullOrWhiteSpace(options.InviteUrl)
            ? ""
            : $"""
               <p>
                 <a href="{encodedUrl}">Use your invite</a>
               </p>
               """;

        return $"""
               <p>You have been invited.</p>
               <p>Your invite code is:</p>
               <p><strong style="font-size: 20px; letter-spacing: 2px;">{encodedCode}</strong></p>
               <p>This invite expires on {invite.ExpiresAt:yyyy-MM-dd}.</p>
               {callToAction}
               """;
    }
}

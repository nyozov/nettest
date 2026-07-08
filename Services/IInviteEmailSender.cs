namespace nettest.Services;

using nettest.Models;

public interface IInviteEmailSender
{
    Task SendInviteAsync(Invite invite, CancellationToken cancellationToken = default);
}

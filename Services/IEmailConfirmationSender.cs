using nettest.Models;

namespace nettest.Services;

public interface IEmailConfirmationSender
{
    Task SendConfirmationCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default);
}

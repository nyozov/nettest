using nettest.Models;

namespace nettest.Services;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetCodeAsync(
        User user,
        string code,
        CancellationToken cancellationToken = default);
}

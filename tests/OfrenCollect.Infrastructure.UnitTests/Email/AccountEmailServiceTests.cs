using NSubstitute;
using OfrenCollect.Infrastructure.Email;

namespace OfrenCollect.Infrastructure.UnitTests.Email;

public class AccountEmailServiceTests
{
    private readonly IEmailOutbox _outbox = Substitute.For<IEmailOutbox>();

    private AccountEmailService Create(bool enabled) =>
        new(_outbox, new EmailOptions
        {
            Enabled = enabled,
            ApiKey = "xkeysib-test",
            FromAddress = "no-reply@ofren.ng",
            AppBaseUrl = "https://ofren-collect.vercel.app",
        });

    [Fact]
    public async Task SendPasswordReset_WhenEnabled_EnqueuesEmail_WithRecipientAndResetLink()
    {
        await Create(enabled: true).SendPasswordResetAsync("ada@brightpath.ng", "raw-token", CancellationToken.None);

        _outbox.Received(1).Enqueue(Arg.Is<QueuedEmail>(e =>
            e != null
            && e.ToEmail == "ada@brightpath.ng"
            && e.HtmlBody.Contains("https://ofren-collect.vercel.app/reset-password?token=raw-token")));
    }

    [Fact]
    public async Task SendWelcome_WhenEnabled_EnqueuesEmail()
    {
        await Create(enabled: true).SendWelcomeAsync("ada@brightpath.ng", "BrightPath", CancellationToken.None);

        _outbox.Received(1).Enqueue(Arg.Is<QueuedEmail>(e => e != null && e.ToEmail == "ada@brightpath.ng"));
    }

    [Fact]
    public async Task SendWelcome_WhenDisabled_EnqueuesNothing()
    {
        await Create(enabled: false).SendWelcomeAsync("ada@brightpath.ng", "BrightPath", CancellationToken.None);

        _outbox.DidNotReceive().Enqueue(Arg.Any<QueuedEmail>());
    }
}

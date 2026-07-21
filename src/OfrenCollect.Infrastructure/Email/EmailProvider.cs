namespace OfrenCollect.Infrastructure.Email;

/// <summary>Which transactional email provider the dispatcher sends through.</summary>
public enum EmailProvider
{
    /// <summary>Brevo (real delivery). Requires an authenticated sender domain to reach inboxes.</summary>
    Brevo = 0,

    /// <summary>Mailtrap Sandbox — captures emails in a web inbox instead of delivering. Great for demos.</summary>
    Mailtrap
}

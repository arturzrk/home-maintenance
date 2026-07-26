using HomeMaintenance.Domain.Common;

namespace HomeMaintenance.Domain.Identity;

public sealed class OwnerProfile : Entity
{
    public OwnerId Owner { get; private set; }
    public string Email { get; private set; }
    public bool RemindersEnabled { get; private set; }

    private OwnerProfile(string id, OwnerId owner, string email, bool remindersEnabled)
        : base(id)
    {
        Owner = owner;
        Email = email;
        RemindersEnabled = remindersEnabled;
    }

    public static OwnerProfile Create(string id, OwnerId owner, string email)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ValidateEmail(email);
        return new OwnerProfile(id, owner, email.Trim(), remindersEnabled: true);
    }

    internal static OwnerProfile Hydrate(string id, OwnerId owner, string email, bool remindersEnabled)
        => new(id, owner, email, remindersEnabled);

    public void UpdateEmail(string email)
    {
        ValidateEmail(email);
        Email = email.Trim();
    }

    public void SetRemindersEnabled(bool enabled) => RemindersEnabled = enabled;

    private static void ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));
    }
}

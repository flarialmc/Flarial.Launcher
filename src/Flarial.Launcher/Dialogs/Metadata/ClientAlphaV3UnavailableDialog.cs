namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientAlphaV3UnavailableDialog : MessageDialog<ClientAlphaV3UnavailableDialog>
{
    protected override string Title => "⚠️ Client Alpha (v3) Unavailable";

    protected override string Message => @"No alpha (v3) build is available right now.

• A v3 build might not be published yet.
• Try again later or switch to another build in settings.

If you need help, join our Discord.";

    protected override string Primary => "Back";
}

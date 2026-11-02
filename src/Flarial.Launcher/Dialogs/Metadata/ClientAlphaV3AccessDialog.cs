namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientAlphaV3AccessDialog : MessageDialog<ClientAlphaV3AccessDialog>
{
    protected override string Title => "⚠️ Client Alpha (v3) Access";

    protected override string Message => @"The client's alpha (v3) requires beta access.

• Please login with an account that has beta access.
• If you just got access, try logging out & back in.

If you need help, join our Discord.";

    protected override string Primary => "Back";
}

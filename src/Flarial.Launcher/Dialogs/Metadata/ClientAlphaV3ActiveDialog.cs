namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientAlphaV3ActiveDialog : MessageDialog<ClientAlphaV3ActiveDialog>
{
    protected override string Title => "⚠️ Client Alpha (v3) Active";

    protected override string Message => @"The client's alpha (v3) has been selected.

• These alpha builds might be unstable and buggy.
• The builds are only for feedback & testing purposes only.

If you need help, join our Discord.";

    protected override string Primary => "Launch";
    protected override string Secondary => "Back";
}

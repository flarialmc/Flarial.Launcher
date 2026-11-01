namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientBetaV2ActiveDialog : MessageDialog<ClientBetaV2ActiveDialog>
{
    protected override string Title => "⚠️ Client Beta (v2) Active";

    protected override string Message => @"The client's beta (v2) has been selected.

• These beta builds might be unstable and buggy.
• The builds are only for feedback & testing purposes only.

If you need help, join our Discord.";

    protected override string Primary => "Launch";
    protected override string Secondary => "Back";
}

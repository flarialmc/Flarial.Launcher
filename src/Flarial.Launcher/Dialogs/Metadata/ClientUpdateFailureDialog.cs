namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientUpdateFailureDialog : MessageDialog<ClientUpdateFailureDialog>
{
    protected override string Title => "⚠️ Client Update Failure";

    protected override string Message => @"A client update couldn't be downloaded.

• Try closing Minecraft & click on [Launch] to update the client.
• Try rebooting your machine & see if that resolves the issue.
• If using client's beta or alpha, please verify if you have access to it.

If you need help, join our Discord.";

    protected override string Primary => "Back";
}

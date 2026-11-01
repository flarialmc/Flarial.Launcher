namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ClientUpdateFailureDialog : MessageDialog<ClientUpdateFailureDialog>
{
    protected override string Title => "⚠️ Client Update Failure";

    protected override string Message => @"A client update couldn't be downloaded.

• Try closing Minecraft & click on [Play] to update the client.
• Try rebooting your machine & see if that resolves the issue.
• If using client's beta, please verify if you have access to it.
• If using the V3 beta, a V3 build might not be published yet.

If you need help, join our Discord.";

    protected override string Primary => "Back";
}

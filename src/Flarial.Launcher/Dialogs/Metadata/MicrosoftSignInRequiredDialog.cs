using Flarial.Launcher.Types;
using ReactiveUI;

namespace Flarial.Launcher.Dialogs.Metadata;

sealed class MicrosoftSignInRequiredDialog : MessageDialog<MicrosoftSignInRequiredDialog>
{
    protected override string Title => "⚠️ Sign In Required";

    protected override string Message => @"Sign in to your Microsoft account in Settings > Accounts to download Minecraft.

• The account must own Minecraft: Bedrock Edition.
• The sign in is only used to download & license the game.

If you need help, join our Discord.";

    protected override string Primary => "Sign In";
    protected override string Secondary => "Cancel";

    protected override void OnShow(bool value)
    {
        if (!value) return;
        MessageBus.Current.SendMessage(PageTransitions.SettingsAccountsPage);
    }
}

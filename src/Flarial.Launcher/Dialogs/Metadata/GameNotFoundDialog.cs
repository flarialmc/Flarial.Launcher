using System.Threading.Tasks;
using Flarial.Launcher.Types;
using ReactiveUI;

namespace Flarial.Launcher.Dialogs.Metadata;

sealed class GameNotFoundDialog : MessageDialog<GameNotFoundDialog>
{
    protected override string Title => "⚠️ Game Not Found";

    protected override string Message => @$"The launcher cannot find an instance of Minecraft.

• Make sure there is a running instance of the game available.
• Install a version of the game via the Versions page.

If you need help, join our Discord.";

    protected override string Primary => "Install";

    protected override async void OnShow(bool value)
    {
        if (!value) return;
        MessageBus.Current.SendMessage(PageTransitions.SettingsPage);
        await Task.Delay(700);
        MessageBus.Current.SendMessage(PageTransitions.SettingsVersionsPage);
    }
}

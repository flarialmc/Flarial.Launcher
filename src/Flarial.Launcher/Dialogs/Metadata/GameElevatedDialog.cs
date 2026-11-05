namespace Flarial.Launcher.Dialogs.Metadata;

sealed class GameElevatedDialog : MessageDialog<GameElevatedDialog>
{
    protected override string Primary => "Back";
    protected override string Title => "⚠️ Game Elevated";
    protected override string Message => @"Minecraft is running as an administrator.

• This issue might occur when using a 3rd party launchers.
• Please relaunch, Minecraft without administrator permissions.

If you need help, join our Discord.";
}
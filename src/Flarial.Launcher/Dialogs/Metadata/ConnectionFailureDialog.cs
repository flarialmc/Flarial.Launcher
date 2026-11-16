namespace Flarial.Launcher.Dialogs.Metadata;

sealed class ConnectionFailureDialog : MessageDialog<ConnectionFailureDialog>
{
    protected override string Title => "⚠️ Connection Failure";
 
    protected override string Message => @"Cannot connect to Flarial's CDN.

• Try using Cloudflare WARP by downloading at 'https://1.1.1.1'.
• Try changing your DNS, disabling IPv6 or restarting your router.
• Check if 'https://cdn.flarial.xyz/202.txt' can be accessed in your browser.

If you need help, join our Discord.";

    protected override string Primary => "Exit";
}
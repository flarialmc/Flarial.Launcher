using Flarial.Runtime.Versions;

namespace Flarial.Launcher.Dialogs.Metadata;

sealed class DeleteVersionDialog(VersionItem version) : MessageDialog
{
    protected override string Title => "⚠️ Delete Version";

    protected override string Message => @$"Delete Minecraft {version}? This removes the downloaded files.

• Your worlds and settings are kept.
• You can download this version again at any time.

If you need help, join our Discord.";

    protected override string Primary => "Delete";

    protected override string Secondary => "Cancel";
}

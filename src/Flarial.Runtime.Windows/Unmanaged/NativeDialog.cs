using Windows.Win32.UI.Controls;
using static Windows.Win32.PInvoke;
using static Windows.Win32.UI.Controls.TASKDIALOG_COMMON_BUTTON_FLAGS;
using static Windows.Win32.UI.Controls.TASKDIALOG_FLAGS;

namespace Flarial.Runtime.Unmanaged;

public unsafe readonly struct NativeDialog
{
    public required nint Handle { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
    public required string? Instruction { get; init; }
    public required string? Information { get; init; }

    public void Show()
    {
        fixed (char* title = Title)
        fixed (char* content = Content)
        fixed (char* instruction = Instruction)
        fixed (char* information = Information)
        {
            TASKDIALOGCONFIG config = new()
            {
                pszContent = content,
                pszWindowTitle = title,

                pszMainInstruction = instruction,
                pszExpandedInformation = information,

                hwndParent = new(Handle),
                cbSize = (uint)sizeof(TASKDIALOGCONFIG),

                pszMainIcon = TD_ERROR_ICON,
                dwCommonButtons = TDCBF_CLOSE_BUTTON,
                dwFlags = TDF_SIZE_TO_CONTENT | TDF_ALLOW_DIALOG_CANCELLATION | TDF_POSITION_RELATIVE_TO_WINDOW
            };
            TaskDialogIndirect(&config);
        }
    }
}
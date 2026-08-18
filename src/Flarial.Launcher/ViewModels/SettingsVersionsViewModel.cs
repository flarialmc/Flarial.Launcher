using Avalonia.Collections;
using ReactiveUI.SourceGenerators;

namespace Flarial.Launcher.ViewModels;

public sealed partial class SettingsVersionsViewModel : ViewModelBase
{
    [Reactive]
    bool _isInstalling;

    [Reactive]
    AvaloniaList<VersionItemViewModel> _versions = [];
}
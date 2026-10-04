namespace Flarial.Launcher.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public SettingsConfigsViewModel SettingsConfigsViewModel { get; } = new();
    public SettingsVersionsViewModel SettingsVersionsViewModel { get; } = new();
    public SettingsGeneralViewModel SettingsGeneralViewModel { get; }
    public SettingsAccountsViewModel SettingsAccountsViewModel { get; }

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        SettingsGeneralViewModel = new(mainWindowViewModel);
        SettingsAccountsViewModel = new(mainWindowViewModel, SettingsGeneralViewModel);
    }
}

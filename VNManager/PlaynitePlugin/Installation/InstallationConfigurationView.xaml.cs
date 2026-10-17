using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace VnManager.PlaynitePlugin.Installation;

public partial class InstallationConfigurationView : UserControl, INotifyPropertyChanged
{
    private string purpose;
    private string gameDirectory;
    private string executablePath;
    private string extraFilesDirectory;
    private string validationError;

    public InstallationConfigurationView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler PropertyChanged;
    public event EventHandler BrowseGameDirectoryRequested;
    public event EventHandler BrowseExecutableRequested;
    public event EventHandler BrowseExtraFilesDirectoryRequested;
    public event EventHandler CompleteRequested;
    public event EventHandler CancelRequested;

    public string Purpose { get => purpose; set => SetField(ref purpose, value); }
    public string GameDirectory { get => gameDirectory; set => SetField(ref gameDirectory, value); }
    public string ExecutablePath { get => executablePath; set => SetField(ref executablePath, value); }
    public string ExtraFilesDirectory { get => extraFilesDirectory; set => SetField(ref extraFilesDirectory, value); }
    public string ValidationError { get => validationError; set => SetField(ref validationError, value); }

    private void BrowseGameDirectoryClick(object sender, RoutedEventArgs e) => BrowseGameDirectoryRequested?.Invoke(this, EventArgs.Empty);
    private void BrowseExecutableClick(object sender, RoutedEventArgs e) => BrowseExecutableRequested?.Invoke(this, EventArgs.Empty);
    private void BrowseExtraFilesDirectoryClick(object sender, RoutedEventArgs e) => BrowseExtraFilesDirectoryRequested?.Invoke(this, EventArgs.Empty);
    private void CompleteClick(object sender, RoutedEventArgs e) => CompleteRequested?.Invoke(this, EventArgs.Empty);
    private void CancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);

    private void SetField(ref string field, string value, [CallerMemberName] string propertyName = null)
    {
        if (string.Equals(field, value, StringComparison.Ordinal)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

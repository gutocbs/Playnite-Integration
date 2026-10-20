using System;
using System.IO;
using System.Windows;
using Playnite.SDK;
using VNInstallation.Protocol.Contracts;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class InstallationConfigurationDialog(IPlayniteAPI api)
{
    public InteractionDialogResult Show(PlayniteInteractionRequest request)
    {
        if (request.Configuration == null)
        {
            throw new ArgumentException("The installation configuration prompt is required.", nameof(request));
        }

        var form = InstallationConfigurationForm.Create(request.Configuration);
        var view = new InstallationConfigurationView
        {
            Purpose = request.Purpose,
            GameDirectory = form.GameDirectory ?? string.Empty,
            ExecutablePath = form.ExecutablePath ?? string.Empty,
            ExtraFilesDirectory = string.Empty
        };
        var owner = api.Dialogs.GetCurrentAppWindow();
        var window = api.Dialogs.CreateWindow(new WindowCreationOptions
        {
            ShowCloseButton = true,
            ShowMaximizeButton = false,
            ShowMinimizeButton = false
        });
        window.Title = request.Caption;
        window.Content = view;
        window.Owner = owner;
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        InteractionDialogResult result = null;
        view.BrowseGameDirectoryRequested += (_, _) =>
        {
            var selected = api.Dialogs.SelectFolder(view.GameDirectory);
            if (!string.IsNullOrWhiteSpace(selected)) view.GameDirectory = selected;
        };
        view.BrowseExecutableRequested += (_, _) =>
        {
            var initialDirectory = GetExecutableInitialDirectory(view.ExecutablePath, view.GameDirectory);
            var selected = api.Dialogs.SelectFile(request.FileFilter ?? InstallationDialogs.ExecutableFileFilter, initialDirectory);
            if (!string.IsNullOrWhiteSpace(selected)) view.ExecutablePath = selected;
        };
        view.BrowseExtraFilesDirectoryRequested += (_, _) =>
        {
            var selected = api.Dialogs.SelectFolder(view.ExtraFilesDirectory);
            if (!string.IsNullOrWhiteSpace(selected)) view.ExtraFilesDirectory = selected;
        };
        view.CompleteRequested += (_, _) =>
        {
            if (!form.TryConfirm(
                    view.GameDirectory,
                    view.ExecutablePath,
                    view.ExtraFilesDirectory,
                    out var selection,
                    out var validationError))
            {
                view.ValidationError = validationError ?? "Revise os caminhos selecionados.";
                return;
            }

            result = new InteractionDialogResult(InteractionOutcome.Confirmed, null, selection);
            window.DialogResult = true;
        };
        view.CancelRequested += (_, _) =>
        {
            result = Cancelled();
            window.DialogResult = false;
        };

        window.ShowDialog();
        return result ?? Cancelled();
    }

    private static string GetExecutableInitialDirectory(string executablePath, string gameDirectory)
    {
        try
        {
            return Path.GetDirectoryName(executablePath) ?? gameDirectory;
        }
        catch (Exception exception) when (exception is ArgumentException || exception is PathTooLongException)
        {
            return gameDirectory;
        }
    }

    private static InteractionDialogResult Cancelled() => new(InteractionOutcome.Cancelled, null, null);
}

using VNInstallation.Protocol.Contracts;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class InstallationConfigurationFormTests
{
    [Fact]
    public void Create_prefills_portable_destination_and_best_executable_candidate()
    {
        var form = InstallationConfigurationForm.Create(new InstallationConfigurationPrompt(
            InstallationFlowKind.Portable,
            @"D:\Jogos\星の記憶",
            @"D:\Jogos\星の記憶\星.exe",
            new[] { @"D:\Jogos\星の記憶\星.exe", @"D:\Jogos\星の記憶\configuração.exe" }));

        Assert.Equal(@"D:\Jogos\星の記憶", form.GameDirectory);
        Assert.Equal(@"D:\Jogos\星の記憶\星.exe", form.ExecutablePath);
        Assert.Null(form.ExtraFilesDirectory);
    }

    [Theory]
    [InlineData("", @"game.exe", "Selecione a pasta do jogo.")]
    [InlineData(@"D:\Jogos\Game", "", "Selecione o executável do jogo.")]
    public void TryConfirm_requires_game_directory_and_executable(string gameDirectory, string executablePath, string expectedError)
    {
        var form = InstallationConfigurationForm.Create(Prompt());

        var confirmed = form.TryConfirm(gameDirectory, executablePath, null, out var selection, out var validationError);

        Assert.False(confirmed);
        Assert.Null(selection);
        Assert.Equal(expectedError, validationError);
    }

    [Fact]
    public void TryConfirm_resolves_a_relative_executable_below_the_game_directory()
    {
        var form = InstallationConfigurationForm.Create(Prompt());

        var confirmed = form.TryConfirm(@"D:\Jogos\Game", @"bin\game.exe", null, out var selection, out var validationError);

        Assert.True(confirmed);
        Assert.Null(validationError);
        Assert.Equal(Path.GetFullPath(@"D:\Jogos\Game"), selection!.GameDirectory);
        Assert.Equal(Path.GetFullPath(@"D:\Jogos\Game\bin\game.exe"), selection.ExecutablePath);
        Assert.Null(selection.ExtraFilesDirectory);
    }

    [Fact]
    public void TryConfirm_preserves_an_absolute_unicode_executable_and_normalizes_optional_extras()
    {
        var form = InstallationConfigurationForm.Create(Prompt());

        var confirmed = form.TryConfirm(
            @"D:\Jogos\星の記憶",
            @"E:\Aplicativos\星.exe",
            @"F:\Traduções\追加ファイル",
            out var selection,
            out var validationError);

        Assert.True(confirmed);
        Assert.Null(validationError);
        Assert.Equal(Path.GetFullPath(@"D:\Jogos\星の記憶"), selection!.GameDirectory);
        Assert.Equal(Path.GetFullPath(@"E:\Aplicativos\星.exe"), selection.ExecutablePath);
        Assert.Equal(Path.GetFullPath(@"F:\Traduções\追加ファイル"), selection.ExtraFilesDirectory);
    }

    [Fact]
    public void TryConfirm_rejects_a_malformed_path_without_returning_a_partial_selection()
    {
        var form = InstallationConfigurationForm.Create(Prompt());

        var confirmed = form.TryConfirm(@"D:\Jogos\Game", "bad\0path.exe", null, out var selection, out var validationError);

        Assert.False(confirmed);
        Assert.Null(selection);
        Assert.NotNull(validationError);
    }

    private static InstallationConfigurationPrompt Prompt() => new(
        InstallationFlowKind.Portable,
        @"D:\Jogos\Game",
        @"D:\Jogos\Game\game.exe",
        new[] { @"D:\Jogos\Game\game.exe" });
}

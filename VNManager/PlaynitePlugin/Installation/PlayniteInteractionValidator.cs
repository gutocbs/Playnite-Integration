using System;
using System.IO;
using Playnite.SDK;
using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VnManager.PlayniteInstallation.Transport;
using VNInstallation.Protocol.Transport;

namespace VnManager.PlaynitePlugin.Installation;

internal sealed class PlayniteInteractionValidator(IPlayniteAPI api, AtomicTransportFileStore store) : IInteractionRequestValidator
{
    public bool CanHandle(PlayniteInteractionRequest request)
    {
        if (store.IsSuperseded(request.RequestId)) return false;

        var game = api.Database.Games.Get(request.PlayniteGameId);
        if (game == null || string.IsNullOrWhiteSpace(game.InstallDirectory)) return false;

        string currentSource;
        try { currentSource = Path.GetFullPath(game.InstallDirectory); }
        catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
        {
            return false;
        }

        if (!string.Equals(currentSource, request.SourcePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(currentSource)) return false;
        try
        {
            var info = new FileInfo(currentSource);
            var currentFingerprint = ProtocolV1.CreateSourceFingerprint(currentSource, info.Length, info.LastWriteTimeUtc);
            return string.Equals(currentFingerprint, request.SourceFingerprint, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return false;
        }
    }
}

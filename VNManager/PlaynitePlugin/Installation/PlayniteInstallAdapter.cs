using System;
using System.IO;
using System.Linq;
using Playnite.SDK.Models;
using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VnManager.PlayniteInstallation.Domain;

namespace VnManager.PlaynitePlugin.Installation;

internal static class PlayniteInstallAdapter
{
    public static GameInstallSnapshot ToSnapshot(Game game) => new(game.Id, game.Name, game.IsInstalled, game.InstallDirectory, game.Genres?.Select(genre => genre.Name).ToArray() ?? Array.Empty<string>());

    public static InstallationRequest CreateRequest(Game game)
    {
        var source = game.InstallDirectory;
        var info = new FileInfo(source);
        return new InstallationRequest(ProtocolV1.SchemaVersion, Guid.NewGuid(), game.Id, game.Name, source, ProtocolV1.CreateSourceFingerprint(Path.GetFullPath(source), info.Length, info.LastWriteTimeUtc), DateTimeOffset.UtcNow);
    }
}

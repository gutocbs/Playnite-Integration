using VNInstallation.Protocol.Contracts;
using VNInstallation.Protocol.Serialization;
using VNInstallation.Protocol.Transport;
using VnManager.PlayniteInstallation.Domain;
using Xunit;

namespace VnManager.PlayniteInstallation.Tests;

public sealed class SharedProtocolDependencyTests
{
    [Fact]
    public void Shared_transport_types_come_from_the_shared_protocol_assembly()
    {
        var sharedAssembly = typeof(ProtocolV1).Assembly;

        Assert.Same(sharedAssembly, typeof(InstallationRequest).Assembly);
        Assert.Same(sharedAssembly, typeof(AtomicTransportFileStore).Assembly);
        Assert.Same(sharedAssembly, typeof(TransportPaths).Assembly);
        Assert.Same(sharedAssembly, typeof(GameOperationLock).Assembly);
        Assert.Same(sharedAssembly, typeof(TransportEventNames).Assembly);
    }

    [Fact]
    public void Playnite_specific_policy_remains_owned_by_the_Playnite_installation_assembly()
    {
        var playniteAssembly = typeof(ResultApplicationPolicy).Assembly;

        Assert.Same(playniteAssembly, typeof(ResultApplicationPolicy).Assembly);
        Assert.Same(playniteAssembly, typeof(CurrentGameSnapshot).Assembly);
        Assert.NotSame(playniteAssembly, typeof(InstallationRequest).Assembly);
    }
}

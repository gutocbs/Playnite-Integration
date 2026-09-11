using System.Text.Json;
using Launcher.Abstractions;

namespace Launcher.Host;

public static class HostApplication
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        HostInvocation invocation;

        try
        {
            invocation = HostInvocation.Parse(arguments);
        }
        catch
        {
            return HostExitCodes.InvalidInvocation;
        }

        DispatcherRequest? request = null;

        try
        {
            var hostOptions = HostOptionsLoader.Load(Path.Combine(
                invocation.ConfigurationDirectory,
                "host.json"));
            var currentExecutionDirectory = Path.GetDirectoryName(invocation.RequestPath)
                ?? throw new InvalidDataException("Request path has no execution directory.");
            _ = TemporaryTransportCleaner.CleanupAsync(
                invocation.TransportRoot,
                currentExecutionDirectory,
                TimeSpan.FromSeconds(hostOptions.TemporaryDirectoryMaximumAgeSeconds));

            request = JsonFileTransport.ReadRequest(invocation.RequestPath);
            DispatcherRequestValidator.Validate(request);

            var registry = HostComposition.CreateRegistry(
                invocation.ConfigurationDirectory,
                invocation.LogPath);
            var result = await new Dispatcher(registry).DispatchAsync(
                request,
                (processName, _) =>
                {
                    JsonFileTransport.WriteStarted(invocation.StartedPath, request.ExecutionId, processName);
                    return Task.CompletedTask;
                });
            JsonFileTransport.WriteResult(
                invocation.ResultPath,
                HostResult.FromLaunchResult(request.ExecutionId, result));

            if (result.Success)
            {
                return HostExitCodes.Success;
            }

            return result.Error?.Code == "LAUNCH_CANCELLED"
                ? HostExitCodes.Cancelled
                : HostExitCodes.LauncherFailed;
        }
        catch (DispatcherRequestValidationException exception)
        {
            return WriteFailure(
                invocation,
                request?.ExecutionId,
                exception.Code,
                exception.Message,
                HostExitCodes.InvalidRequest);
        }
        catch (LauncherResolutionException exception)
        {
            return WriteFailure(
                invocation,
                request?.ExecutionId,
                exception.Code,
                exception.Message,
                HostExitCodes.LauncherResolutionFailed);
        }
        catch (OperationCanceledException)
        {
            return WriteFailure(
                invocation,
                request?.ExecutionId,
                "LAUNCH_CANCELLED",
                "Launcher supervision was cancelled.",
                HostExitCodes.Cancelled);
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            return WriteFailure(
                invocation,
                request?.ExecutionId,
                "INVALID_REQUEST_TRANSPORT",
                exception.Message,
                HostExitCodes.InvalidRequest);
        }
        catch (Exception exception)
        {
            return WriteFailure(
                invocation,
                request?.ExecutionId,
                "HOST_INTERNAL_ERROR",
                exception.Message,
                HostExitCodes.InternalError);
        }
    }

    private static int WriteFailure(
        HostInvocation invocation,
        string? executionId,
        string code,
        string message,
        int exitCode)
    {
        try
        {
            JsonFileTransport.WriteResult(
                invocation.ResultPath,
                HostResult.Failed(executionId, code, message));
            return exitCode;
        }
        catch
        {
            return HostExitCodes.InvalidInvocation;
        }
    }
}

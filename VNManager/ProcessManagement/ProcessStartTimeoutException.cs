namespace Launcher.ProcessManagement;

public sealed class ProcessStartTimeoutException(string message) : TimeoutException(message);

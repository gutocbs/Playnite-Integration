using System.Diagnostics;

if (args.Length < 3 || !string.Equals(args[0], "-runas", StringComparison.Ordinal))
{
    return 2;
}

var startInfo = new ProcessStartInfo
{
    FileName = args[2],
    UseShellExecute = false
};

foreach (var argument in args.Skip(3))
{
    startInfo.ArgumentList.Add(argument);
}

using var process = Process.Start(startInfo);
return process is null ? 1 : 0;

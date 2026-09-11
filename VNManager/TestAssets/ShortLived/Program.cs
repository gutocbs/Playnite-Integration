var lifetimeMilliseconds = args.Length > 0
    ? int.Parse(args[0], System.Globalization.CultureInfo.InvariantCulture)
    : 500;

await Task.Delay(lifetimeMilliseconds);

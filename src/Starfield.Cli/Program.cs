using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Starfield.Cli;
using Starfield.Cli.CommandLine;
using Starfield.Cli.Presets;
using Starfield.Core;
using Starfield.Core.Options;

var arguments = CliParser.Parse(args);

if (arguments.HelpRequested)
{
    Console.Out.WriteLine(HelpText.Usage);
    return ExitCodes.Success;
}

if (!arguments.Errors.IsEmpty)
{
    foreach (var error in arguments.Errors)
    {
        Console.Error.WriteLine(error);
    }

    return ExitCodes.UsageError;
}

var minimumLevel = arguments.Quiet
    ? LogLevel.Warning
    : arguments.Verbose ? LogLevel.Debug : LogLevel.Information;

using var loggerFactory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(minimumLevel)
    .AddSimpleConsole(console =>
    {
        console.SingleLine = true;
        console.TimestampFormat = "HH:mm:ss ";
    }));

var logger = loggerFactory.CreateLogger("starfield");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // Handling the key press ourselves turns a second Ctrl+C into the hard kill, and the first into a
    // clean stop that still closes the output file.
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    var baseline = StarfieldPresets.Default();
    if (arguments.PresetPath is not null)
    {
        baseline = PresetFile.Load(arguments.PresetPath);
        Log.PresetLoaded(logger, arguments.PresetPath);
    }

    var options = arguments.ApplyTo(baseline);

    ImmutableArray<string> problems =
    [
        .. arguments.ValidateOverrides(),
        .. options.Validate(),
    ];

    if (!problems.IsEmpty)
    {
        foreach (var problem in problems)
        {
            Log.InvalidOptions(logger, problem);
        }

        return ExitCodes.UsageError;
    }

    if (arguments.DumpPresetPath is not null)
    {
        PresetFile.Save(arguments.DumpPresetPath, options);
        Log.PresetWritten(logger, arguments.DumpPresetPath);
        return ExitCodes.Success;
    }

    var renderer = new StarfieldRenderer(options, loggerFactory);
    renderer.RenderToFile(arguments.OutputPath, cancellationToken: cancellation.Token);

    if (arguments.LayersDirectory is not null)
    {
        var stem = Path.GetFileNameWithoutExtension(arguments.OutputPath);
        var written = renderer.RenderLayerFiles(
            arguments.LayersDirectory,
            string.IsNullOrEmpty(stem) ? "layer" : stem,
            cancellation.Token);

        Log.LayersWritten(logger, written.Length, arguments.LayersDirectory);
    }

    return ExitCodes.Success;
}
catch (OperationCanceledException)
{
    Log.Cancelled(logger);
    return ExitCodes.Cancelled;
}
catch (JsonException exception)
{
    Log.Failed(logger, exception, "Reading the preset");
    return ExitCodes.UsageError;
}
catch (FileNotFoundException exception)
{
    Log.Failed(logger, exception, "Opening a file");
    return ExitCodes.UsageError;
}
catch (DirectoryNotFoundException exception)
{
    Log.Failed(logger, exception, "Opening a directory");
    return ExitCodes.UsageError;
}
catch (UnauthorizedAccessException exception)
{
    Log.Failed(logger, exception, "Writing the output");
    return ExitCodes.Failure;
}
catch (IOException exception)
{
    Log.Failed(logger, exception, "Writing the output");
    return ExitCodes.Failure;
}

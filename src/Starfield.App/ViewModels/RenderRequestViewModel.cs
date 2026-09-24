using System.Collections.Immutable;
using System.Globalization;
using Starfield.App.Mvvm;
using Starfield.Core.Options;

namespace Starfield.App.ViewModels;

/// <summary>The values a blank field falls back to, shown greyed inside it.</summary>
/// <param name="OutputPath">The default composite path.</param>
/// <param name="Width">The default width.</param>
/// <param name="Height">The default height.</param>
/// <param name="Seed">The default seed.</param>
public sealed record RequestDefaults(string OutputPath, string Width, string Height, string Seed)
{
    /// <summary>The default width: 4K UHD.</summary>
    public const int DefaultWidth = 3840;

    /// <summary>The default height: 4K UHD.</summary>
    public const int DefaultHeight = 2160;

    /// <summary>Reads the defaults: a 4K image with the built-in preset's seed.</summary>
    /// <returns>The values, written as plain digits.</returns>
    public static RequestDefaults FromBuiltIn()
    {
        var invariant = CultureInfo.InvariantCulture;
        return new RequestDefaults(
            "starfield.png",
            DefaultWidth.ToString(invariant),
            DefaultHeight.ToString(invariant),
            StarfieldPresets.Default().Seed.ToString(invariant));
    }
}

/// <summary>What the form produced: either options ready to render or the reasons it could not.</summary>
/// <param name="Options">The options, or <see langword="null"/> when there were problems.</param>
/// <param name="OutputPath">Where the image goes, trimmed.</param>
/// <param name="Problems">The problems; empty when <paramref name="Options"/> is set.</param>
public sealed record RequestBuildResult(StarfieldOptions? Options, string OutputPath, ImmutableArray<string> Problems)
{
    /// <summary>Gets whether the form can be rendered.</summary>
    public bool IsValid => Options is not null;
}

/// <summary>
/// The form: where the image goes, how big it is and which seed draws it.
/// </summary>
/// <remarks>
/// Fields are text so a half-typed value never throws. Blank means the built-in preset's value.
/// </remarks>
public sealed class RenderRequestViewModel : ObservableObject
{
    /// <summary>Creates a form filled with the defaults.</summary>
    public RenderRequestViewModel()
    {
        Defaults = RequestDefaults.FromBuiltIn();
        OutputPath = Defaults.OutputPath;
    }

    /// <summary>Gets the built-in defaults, for showing inside blank fields.</summary>
    public RequestDefaults Defaults { get; }

    /// <summary>Gets or sets the PNG to write.</summary>
    public string OutputPath { get; set => SetProperty(ref field, value); } = "";

    /// <summary>Gets or sets the width text; blank means the default.</summary>
    public string Width { get; set => SetProperty(ref field, value); } = "";

    /// <summary>Gets or sets the height text; blank means the default.</summary>
    public string Height { get; set => SetProperty(ref field, value); } = "";

    /// <summary>Gets or sets the seed text; blank means the default.</summary>
    public string Seed { get; set => SetProperty(ref field, value); } = "";

    /// <summary>Picks a fresh seed at random.</summary>
    public void RandomiseSeed() => Seed = Random.Shared.NextInt64().ToString(CultureInfo.InvariantCulture);

    /// <summary>Reads the form into options, or explains why it cannot.</summary>
    /// <returns>The result. Every problem is reported, not only the first.</returns>
    public RequestBuildResult Build()
    {
        var problems = ImmutableArray.CreateBuilder<string>();
        var preset = StarfieldPresets.Default();

        var outputPath = OutputPath.Trim();
        if (outputPath.Length == 0)
        {
            problems.Add("Save as must not be empty.");
        }

        var width = ParseDimension(Width, "Width", RequestDefaults.DefaultWidth, problems);
        var height = ParseDimension(Height, "Height", RequestDefaults.DefaultHeight, problems);
        var seed = ParseSeed(Seed, preset.Seed, problems);

        return problems.Count == 0
            ? new RequestBuildResult(preset with { Width = width, Height = height, Seed = seed }, outputPath, [])
            : new RequestBuildResult(null, outputPath, problems.ToImmutable());
    }

    private static int ParseDimension(string text, string field, int fallback, ImmutableArray<string>.Builder problems)
    {
        var value = text.Trim();
        if (value.Length == 0)
        {
            return fallback;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            problems.Add($"{field} must be a whole number, but was '{value}'.");
            return fallback;
        }

        if (parsed < 1)
        {
            problems.Add($"{field} must be at least 1, but was '{value}'.");
            return fallback;
        }

        return parsed;
    }

    private static ulong ParseSeed(string text, ulong fallback, ImmutableArray<string>.Builder problems)
    {
        var value = text.Trim();
        if (value.Length == 0)
        {
            return fallback;
        }

        if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        problems.Add($"Seed must be a whole number from 0 upwards, but was '{value}'.");
        return fallback;
    }
}

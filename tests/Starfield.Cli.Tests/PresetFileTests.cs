using System.IO.Compression;
using System.Text.Json;
using Imaging.Core.Colors;
using Imaging.Core.Noise;
using Starfield.Cli.Presets;
using Starfield.Core.Options;

namespace Starfield.Cli.Tests;

/// <summary>Checks that a preset survives a trip through the file format without losing anything.</summary>
public sealed class PresetFileTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "starfield-preset-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void SaveThenLoad_PreservesEverySetting()
    {
        var original = StarfieldPresets.Default() with
        {
            Width = 7000,
            Height = 900,
            Seed = 99UL,
            SeamlessX = false,
            Background = "#010203",
            ToneMapping = ToneMappingCurve.Reinhard,
            Exposure = 1.25f,
            DitherStrength = 0.5f,
            BandHeight = 48,
            MaxDegreeOfParallelism = 2,
            MaxWorkingSetBytes = 8192,
            CompressionLevel = CompressionLevel.SmallestSize,
        };

        var path = Path.Combine(_directory, "preset.json");
        PresetFile.Save(path, original);
        var loaded = PresetFile.Load(path);

        Assert.Equal(original.Width, loaded.Width);
        Assert.Equal(original.Height, loaded.Height);
        Assert.Equal(original.Seed, loaded.Seed);
        Assert.Equal(original.SeamlessX, loaded.SeamlessX);
        Assert.Equal(original.Background, loaded.Background);
        Assert.Equal(original.ToneMapping, loaded.ToneMapping);
        Assert.Equal(original.Exposure, loaded.Exposure);
        Assert.Equal(original.DitherStrength, loaded.DitherStrength);
        Assert.Equal(original.BandHeight, loaded.BandHeight);
        Assert.Equal(original.MaxDegreeOfParallelism, loaded.MaxDegreeOfParallelism);
        Assert.Equal(original.MaxWorkingSetBytes, loaded.MaxWorkingSetBytes);
        Assert.Equal(original.CompressionLevel, loaded.CompressionLevel);
        Assert.Empty(loaded.Validate());
    }

    [Fact]
    public void SaveThenLoad_PreservesTheNebulaAndItsColourRamp()
    {
        var original = StarfieldPresets.Default();
        var path = Path.Combine(_directory, "nebula.json");

        PresetFile.Save(path, original);
        var loaded = PresetFile.Load(path).Nebula;

        Assert.Equal(original.Nebula.Intensity, loaded.Intensity);
        Assert.Equal(original.Nebula.DensityContrast, loaded.DensityContrast);
        Assert.Equal(original.Nebula.Structure.Shape, loaded.Structure.Shape);
        Assert.Equal(original.Nebula.Structure.Octaves, loaded.Structure.Octaves);
        Assert.Equal(original.Nebula.Palettes.Count, loaded.Palettes.Count);
        Assert.Equal(original.Nebula.Palettes[2].Name, loaded.Palettes[2].Name);
        Assert.Equal(original.Nebula.Palettes[2].ColorStops[2].Color, loaded.Palettes[2].ColorStops[2].Color);
        Assert.Equal(original.Nebula.Palettes[2].ColorStops[2].Position, loaded.Palettes[2].ColorStops[2].Position);
    }

    [Fact]
    public void SaveThenLoad_PreservesEveryStarLayer()
    {
        var original = StarfieldPresets.Default();
        var path = Path.Combine(_directory, "layers.json");

        PresetFile.Save(path, original);
        var loaded = PresetFile.Load(path).StarLayers;

        Assert.Equal(original.StarLayers.Count, loaded.Count);

        for (var index = 0; index < loaded.Count; index++)
        {
            Assert.Equal(original.StarLayers[index], loaded[index]);
        }

        Assert.Equal(original.StarLayers[^1].FalloffExponent, loaded[^1].FalloffExponent);
        Assert.Equal(original.StarLayers[^1].ClusteringResponse, loaded[^1].ClusteringResponse);
    }

    [Fact]
    public void SaveThenLoad_PreservesTheClusteringSettings()
    {
        var original = StarfieldPresets.Default();
        var path = Path.Combine(_directory, "clustering.json");

        PresetFile.Save(path, original);
        var loaded = PresetFile.Load(path).Clustering;

        Assert.Equal(original.Clustering.Floor, loaded.Floor);
        Assert.Equal(original.Clustering.BandStrength, loaded.BandStrength);
        Assert.Equal(original.Clustering.BandWidth, loaded.BandWidth);
        Assert.Equal(original.Clustering.ClumpStrength, loaded.ClumpStrength);
        Assert.Equal(original.Clustering.DustStrength, loaded.DustStrength);
        Assert.Equal(original.Clustering.HazeIntensity, loaded.HazeIntensity);
        Assert.Equal(original.Clustering.HazeColor, loaded.HazeColor);
        Assert.Equal(original.Clustering.Dust.Shape, loaded.Dust.Shape);
        Assert.Equal(original.Clustering.Clumping.Octaves, loaded.Clumping.Octaves);
    }

    [Fact]
    public void Save_WritesEnumsByName()
    {
        var path = Path.Combine(_directory, "names.json");
        PresetFile.Save(path, StarfieldPresets.Default() with { ToneMapping = ToneMappingCurve.Filmic });

        var json = File.ReadAllText(path);

        Assert.Contains("\"Filmic\"", json, StringComparison.Ordinal);
        Assert.Contains("\"Ridged\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_AcceptsCommentsAndTrailingCommas()
    {
        var path = Path.Combine(_directory, "hand-edited.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            path,
                          /*lang=json*/
                          """
            {
              // A hand-edited preset.
              "Width": 640,
              "Height": 480,
              "Nebula": { "Shape": null, "Intensity": 0.25 },
            }
            """);

        var loaded = PresetFile.Load(path);

        Assert.Equal(640, loaded.Width);
        Assert.Equal(480, loaded.Height);
        Assert.Equal(0.25f, loaded.Nebula.Intensity);
    }

    [Fact]
    public void Load_FillsOmittedSettingsWithTheirDefaults()
    {
        var path = Path.Combine(_directory, "sparse.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, /*lang=json,strict*/ """{ "Seed": 5 }""");

        var loaded = PresetFile.Load(path);
        var defaults = new StarfieldOptions();

        Assert.Equal(5UL, loaded.Seed);
        Assert.Equal(defaults.Width, loaded.Width);
        Assert.Equal(defaults.Background, loaded.Background);
        Assert.Equal(FractalNoiseShape.Ridged, loaded.Nebula.Structure.Shape);
    }

    [Fact]
    public void Load_OfMalformedJson_Throws()
    {
        var path = Path.Combine(_directory, "broken.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(path, "{ not json");

        Assert.Throws<JsonException>(() => PresetFile.Load(path));
    }

    [Fact]
    public void Load_OfAMissingFile_Throws()
    {
        Directory.CreateDirectory(_directory);

        Assert.Throws<FileNotFoundException>(() => PresetFile.Load(Path.Combine(_directory, "absent.json")));
    }

    [Fact]
    public void Save_CreatesTheDirectoryItNeeds()
    {
        var path = Path.Combine(_directory, "nested", "deeper", "preset.json");

        PresetFile.Save(path, StarfieldPresets.Default());

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Save_WithoutOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => PresetFile.Save(Path.Combine(_directory, "x.json"), null!));
}

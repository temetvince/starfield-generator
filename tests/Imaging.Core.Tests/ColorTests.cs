using Imaging.Core.Colors;

namespace Imaging.Core.Tests;

/// <summary>Checks the colour pipeline: encoding, temperature tints, ramps and tone curves.</summary>
public sealed class ColorTests
{
    [Fact]
    public void Srgb_RoundTripsEveryStoredByte()
    {
        for (var stored = 0; stored <= 255; stored++)
        {
            var linear = Srgb.ByteToLinear((byte)stored);
            Assert.Equal(stored, Srgb.Quantise(Srgb.FromLinear(linear), 0.0f));
        }
    }

    [Fact]
    public void Srgb_ClampsOutOfRangeInput()
    {
        Assert.Equal(0, Srgb.Quantise(-1.0f, 0.0f));
        Assert.Equal(255, Srgb.Quantise(2.0f, 0.0f));
        Assert.Equal(0.0f, Srgb.FromLinear(-0.5f));
    }

    [Fact]
    public void Srgb_DitherShiftsQuantisationByAtMostOneStep()
    {
        var exact = Srgb.Quantise(0.5f, 0.0f);

        Assert.InRange(Srgb.Quantise(0.5f, 0.9f), exact, (byte)(exact + 1));
        Assert.InRange(Srgb.Quantise(0.5f, -0.9f), (byte)(exact - 1), exact);
    }

    [Theory]
    [InlineData(1000.0f)]
    [InlineData(5500.0f)]
    [InlineData(12000.0f)]
    [InlineData(40000.0f)]
    public void Blackbody_NormalisesItsBrightestChannel(float kelvin)
    {
        var colour = Blackbody.FromTemperature(kelvin);

        Assert.Equal(1.0f, colour.MaxComponent, 4);
    }

    [Fact]
    public void Blackbody_RunsWarmWhenCoolAndCoolWhenHot()
    {
        var cool = Blackbody.FromTemperature(2000.0f);
        var hot = Blackbody.FromTemperature(20000.0f);

        Assert.True(cool.R > cool.B, "A cool star should be red-dominant.");
        Assert.True(hot.B > hot.R, "A hot star should be blue-dominant.");
    }

    [Fact]
    public void Blackbody_ClampsOutsideItsDefinedRange()
    {
        Assert.Equal(Blackbody.FromTemperature(Blackbody.MinimumKelvin), Blackbody.FromTemperature(10.0f));
        Assert.Equal(Blackbody.FromTemperature(Blackbody.MaximumKelvin), Blackbody.FromTemperature(999999.0f));
    }

    [Theory]
    [InlineData(1.0f, 1.0f, 1.0f)]
    [InlineData(0.0f, 0.0f, 0.0f)]
    [InlineData(0.8f, 0.1f, 0.05f)]
    [InlineData(0.02f, 0.3f, 0.6f)]
    public void Oklab_RoundTripsThroughLinearLight(float red, float green, float blue)
    {
        var colour = new LinearRgb(red, green, blue);

        var back = Oklab.FromLinear(colour).ToLinear();

        Assert.Equal(colour.R, back.R, 4);
        Assert.Equal(colour.G, back.G, 4);
        Assert.Equal(colour.B, back.B, 4);
    }

    [Fact]
    public void Oklab_PutsWhiteAtFullLightnessOnTheNeutralAxis()
    {
        var white = Oklab.FromLinear(LinearRgb.White);

        Assert.Equal(1.0f, white.L, 3);
        Assert.Equal(0.0f, white.Chroma, 3);
    }

    [Theory]
    [InlineData(0.0f)]
    [InlineData(1.0f)]
    [InlineData(-2.0f)]
    public void LinearRgb_RotateHue_ByWholeTurnsChangesNothing(float turns)
    {
        var colour = new LinearRgb(0.2f, 0.5f, 0.9f);

        var rotated = colour.RotateHue(turns);

        Assert.Equal(colour.R, rotated.R, 3);
        Assert.Equal(colour.G, rotated.G, 3);
        Assert.Equal(colour.B, rotated.B, 3);
    }

    [Fact]
    public void LinearRgb_RotateHue_ByAHalfTurnGivesTheOppositeHue()
    {
        var rotated = new LinearRgb(0.5f, 0.1f, 0.1f).RotateHue(0.5f);

        Assert.True(rotated.G > rotated.R && rotated.B > rotated.R, $"Red did not become cyan: {rotated}");
    }

    [Fact]
    public void LinearRgb_RotateHue_LeavesGreyAloneAndKeepsLightnessAndChroma()
    {
        var grey = LinearRgb.FromLevel(0.4f);
        var rotatedGrey = grey.RotateHue(0.37f);

        Assert.Equal(grey.R, rotatedGrey.R, 3);
        Assert.Equal(grey.G, rotatedGrey.G, 3);
        Assert.Equal(grey.B, rotatedGrey.B, 3);

        var colour = new LinearRgb(0.3f, 0.4f, 0.25f);
        var rotated = Oklab.FromLinear(colour.RotateHue(0.21f));
        var original = Oklab.FromLinear(colour);

        Assert.NotEqual(colour, colour.RotateHue(0.21f));
        Assert.Equal(original.L, rotated.L, 3);
        Assert.Equal(original.Chroma, rotated.Chroma, 3);
    }

    [Fact]
    public void LinearRgb_RotateHue_NeverProducesANegativeComponent()
    {
        for (var step = 0; step < 24; step++)
        {
            var rotated = new LinearRgb(1.0f, 0.0f, 0.0f).RotateHue(step / 24.0f);

            Assert.True(rotated.R >= 0.0f && rotated.G >= 0.0f && rotated.B >= 0.0f, $"Step {step} went negative.");
        }
    }

    [Fact]
    public void HexColor_RoundTripsThroughLinearLight()
    {
        Assert.Equal("#7A5FFF", HexColor.ToHex(HexColor.Parse("#7A5FFF")));
        Assert.Equal("#000000", HexColor.ToHex(HexColor.Parse("#000000")));
        Assert.Equal("#FFFFFF", HexColor.ToHex(HexColor.Parse("#FFFFFF")));
    }

    [Fact]
    public void HexColor_AcceptsShorthandAndAMissingHash()
    {
        Assert.Equal(HexColor.Parse("#AABBCC"), HexColor.Parse("abc"));
        Assert.Equal(HexColor.Parse("#112233"), HexColor.Parse("112233"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void HexColor_RejectsMalformedText(string? text)
    {
        Assert.False(HexColor.TryParse(text, out _));
        Assert.Throws<FormatException>(() => HexColor.Parse(text));
    }

    [Fact]
    public void ColorGradient_ClampsBeyondItsEndStops()
    {
        var gradient = new ColorGradient(
        [
            new GradientStop(0.25f, new LinearRgb(1.0f, 0.0f, 0.0f)),
            new GradientStop(0.75f, new LinearRgb(0.0f, 0.0f, 1.0f)),
        ]);

        Assert.Equal(new LinearRgb(1.0f, 0.0f, 0.0f), gradient.Sample(0.0f));
        Assert.Equal(new LinearRgb(0.0f, 0.0f, 1.0f), gradient.Sample(1.0f));
    }

    [Fact]
    public void ColorGradient_InterpolatesBetweenStopsRegardlessOfInputOrder()
    {
        var gradient = new ColorGradient(
        [
            new GradientStop(1.0f, LinearRgb.White),
            new GradientStop(0.0f, LinearRgb.Black),
        ]);

        var middle = gradient.Sample(0.5f);

        Assert.Equal(0.5f, middle.R, 5);
        Assert.Equal(0.5f, middle.G, 5);
        Assert.Equal(0.5f, middle.B, 5);
        Assert.Equal(2, gradient.StopCount);
    }

    [Fact]
    public void ColorGradient_WithNoStops_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ColorGradient([]));
        Assert.Throws<ArgumentNullException>(() => new ColorGradient(null!));
    }

    [Theory]
    [InlineData(ToneMappingCurve.Clamp)]
    [InlineData(ToneMappingCurve.Reinhard)]
    [InlineData(ToneMappingCurve.Exponential)]
    [InlineData(ToneMappingCurve.Filmic)]
    public void ToneMapper_KeepsEveryChannelDisplayable(ToneMappingCurve curve)
    {
        foreach (var level in new[] { 0.0f, 0.01f, 0.5f, 1.0f, 12.0f, 1000.0f })
        {
            var mapped = ToneMapper.Apply(LinearRgb.FromLevel(level), curve, 1.0f);

            Assert.InRange(mapped.R, 0.0f, 1.0f);
            Assert.InRange(mapped.G, 0.0f, 1.0f);
            Assert.InRange(mapped.B, 0.0f, 1.0f);
        }
    }

    [Theory]
    [InlineData(ToneMappingCurve.Reinhard)]
    [InlineData(ToneMappingCurve.Exponential)]
    [InlineData(ToneMappingCurve.Filmic)]
    public void ToneMapper_NeverDarkensAsInputBrightens(ToneMappingCurve curve)
    {
        var previous = -1.0f;

        for (var level = 0.0f; level < 20.0f; level += 0.05f)
        {
            var mapped = ToneMapper.Apply(LinearRgb.FromLevel(level), curve, 1.0f).G;
            Assert.True(mapped >= previous, $"{curve} dipped at {level}.");
            previous = mapped;
        }
    }

    [Fact]
    public void ToneMapper_WithZeroExposure_ProducesBlack() => Assert.Equal(LinearRgb.Black, ToneMapper.Apply(LinearRgb.FromLevel(50.0f), ToneMappingCurve.Filmic, 0.0f));

    [Fact]
    public void ToneMapper_WithNegativeExposure_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ToneMapper.Apply(LinearRgb.White, ToneMappingCurve.Filmic, -1.0f));
    }

    [Fact]
    public void LinearRgb_AddsAndScalesComponentWise()
    {
        var first = new LinearRgb(0.1f, 0.2f, 0.3f);
        var second = new LinearRgb(0.4f, 0.5f, 0.6f);

        AssertClose(new LinearRgb(0.5f, 0.7f, 0.9f), LinearRgb.Add(first, second));
        AssertClose(new LinearRgb(0.2f, 0.4f, 0.6f), LinearRgb.Multiply(first, 2.0f));
        AssertClose(new LinearRgb(0.04f, 0.1f, 0.18f), LinearRgb.Multiply(first, second));
    }

    [Fact]
    public void WithSaturation_AtZero_LeavesOnlyLuminance()
    {
        var colour = new LinearRgb(0.9f, 0.3f, 0.1f);
        var grey = colour.WithSaturation(0.0f);

        Assert.Equal(grey.R, grey.G, 5);
        Assert.Equal(grey.G, grey.B, 5);
        Assert.Equal(colour.Luminance, grey.Luminance, 5);
    }

    private static void AssertClose(LinearRgb expected, LinearRgb actual)
    {
        Assert.Equal(expected.R, actual.R, 5);
        Assert.Equal(expected.G, actual.G, 5);
        Assert.Equal(expected.B, actual.B, 5);
    }
}

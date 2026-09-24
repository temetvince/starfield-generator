using static System.FormattableString;

namespace Starfield.Core.Options;

/// <summary>
/// The diffraction spikes drawn on the brightest stars of a layer.
/// </summary>
/// <remarks>
/// Spikes are what a camera's aperture blades do to a point source. They are applied only above a
/// brightness threshold, because putting them on every faint star reads as noise rather than as glare.
/// </remarks>
public sealed record SpikeOptions
{
    /// <summary>Gets whether any spikes are drawn.</summary>
    /// <value><see langword="false"/> disables the whole effect for the layer.</value>
    public bool Enabled { get; init; }

    /// <summary>Gets how many arms each spiked star has.</summary>
    /// <value>
    /// Between 2 and 8, spread evenly around the star. Four gives the familiar cross; six reads as a
    /// hexagonal aperture.
    /// </value>
    public int Arms { get; init; } = 4;

    /// <summary>Gets the arm length as a multiple of the star's glow radius.</summary>
    /// <value>Greater than zero. Around 2 to 4 looks cinematic without smearing the frame.</value>
    public float LengthScale { get; init; } = 2.5f;

    /// <summary>Gets how bright an arm is relative to the star's core.</summary>
    /// <value>In <c>(0, 1]</c>. Values above about <c>0.3</c> start to overwhelm the star itself.</value>
    public float Intensity { get; init; } = 0.14f;

    /// <summary>Gets the half-width of an arm, in pixels.</summary>
    /// <value>Greater than zero. Below about <c>0.5</c> the arms alias into dotted lines.</value>
    public float Thickness { get; init; } = 0.7f;

    /// <summary>Gets the base rotation of the arms, in degrees.</summary>
    /// <value>Any angle. Zero puts the first arm along the positive horizontal axis.</value>
    public float AngleDegrees { get; init; }

    /// <summary>Gets how much each star's rotation varies from the base angle, in degrees.</summary>
    /// <value>
    /// Not negative. A little jitter stops a field of spiked stars from looking stamped from one
    /// template; zero keeps every star aligned, as a single physical aperture would.
    /// </value>
    public float AngleJitterDegrees { get; init; } = 6.0f;

    /// <summary>Gets the fraction of a layer's brightness range above which stars get spikes.</summary>
    /// <value>
    /// In <c>[0, 1]</c>, measured against the layer's own minimum and maximum intensity. At <c>0.8</c>
    /// only the top fifth of the brightness range is spiked.
    /// </value>
    public float BrightnessThreshold { get; init; } = 0.8f;

    /// <summary>Reports why these options cannot be used, if they cannot.</summary>
    /// <returns>A human-readable list of problems, empty when the options are valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Arms is < 2 or > 8)
        {
            problems.Add(Invariant($"Spike Arms must be between 2 and 8 but was {Arms}."));
        }

        if (!(LengthScale > 0.0f))
        {
            problems.Add(Invariant($"Spike LengthScale must be greater than 0 but was {LengthScale}."));
        }

        if (Intensity is <= 0.0f or > 1.0f)
        {
            problems.Add(Invariant($"Spike Intensity must be in (0, 1] but was {Intensity}."));
        }

        if (!(Thickness > 0.0f))
        {
            problems.Add(Invariant($"Spike Thickness must be greater than 0 but was {Thickness}."));
        }

        if (!(AngleJitterDegrees >= 0.0f))
        {
            problems.Add(Invariant($"Spike AngleJitterDegrees must not be negative but was {AngleJitterDegrees}."));
        }

        if (BrightnessThreshold is < 0.0f or > 1.0f || float.IsNaN(BrightnessThreshold))
        {
            problems.Add(Invariant($"Spike BrightnessThreshold must be in [0, 1] but was {BrightnessThreshold}."));
        }

        return problems;
    }
}

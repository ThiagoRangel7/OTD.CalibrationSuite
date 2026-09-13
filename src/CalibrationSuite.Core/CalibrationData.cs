using System.Text.Json;
using System.Text.Json.Serialization;

namespace CalibrationSuite.Core;

public sealed class CalibrationException(string message) : Exception(message);

public readonly record struct Point2(
    [property: JsonRequired] double X, [property: JsonRequired] double Y)
{
    [JsonIgnore] public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
    public static Point2 operator +(Point2 a, Point2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Point2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2 operator *(Point2 a, double b) => new(a.X * b, a.Y * b);
    public static double Dot(Point2 a, Point2 b) => a.X * b.X + a.Y * b.Y;
    public static double Cross(Point2 a, Point2 b) => a.X * b.Y - a.Y * b.X;
}

public sealed record SourceGeometry(
    [property: JsonRequired] double MaxX, [property: JsonRequired] double MaxY,
    [property: JsonRequired] double WidthMm, [property: JsonRequired] double HeightMm)
{
    public void Validate()
    {
        if (!new[] { MaxX, MaxY, WidthMm, HeightMm }.All(v => double.IsFinite(v) && v > 0))
            throw new CalibrationException("Digitizer geometry must be finite and positive.");
    }

    public bool Matches(SourceGeometry other) => Close(MaxX, other.MaxX) && Close(MaxY, other.MaxY)
        && Close(WidthMm, other.WidthMm) && Close(HeightMm, other.HeightMm);
    private static bool Close(double a, double b) => Math.Abs(a - b) <= 1e-6 * Math.Max(Math.Abs(a), Math.Abs(b));
}

public sealed record RequestedPose([property: JsonRequired] double AltitudeDeg, double? AzimuthDeg = null);
public sealed record CalibrationSample(
    [property: JsonRequired] Point2 Position, [property: JsonRequired] Point2 Tilt,
    RequestedPose? RequestedPose = null);
public sealed record CalibrationNode([property: JsonRequired] CalibrationSample[] Samples);
public sealed record CalibrationGrid(
    [property: JsonRequired] double[] PreTransformRowCoordinates,
    [property: JsonRequired] double[] PreTransformColumnCoordinates);

// These are editable serialization models. Preparation copies all evaluator inputs.
public sealed record CalibrationData
{
    public required string Format { get; init; }
    public required int Version { get; init; }
    public required string Mode { get; init; }
    public SourceGeometry? SourceGeometry { get; init; }
    public CalibrationGrid? Grid { get; init; }
    public CalibrationNode[][]? Nodes { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static CalibrationData Identity => new() { Format = "CalibrationSuite", Version = 2, Mode = "identity" };

    public static CalibrationData Parse(string json)
    {
        try
        {
            // Check the version before binding the measured schema so legacy payloads get a useful diagnostic.
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("version", out var version)
                && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out int number)
                && number == 1)
                throw new CalibrationException("Version 1 requires recalibration. Keep the original data until replacement or reset.");
            return JsonSerializer.Deserialize<CalibrationData>(json, JsonOptions)
                ?? throw new CalibrationException("Calibration data must be an object.");
        }
        catch (JsonException ex) { throw new CalibrationException($"Invalid calibration JSON: {ex.Message}"); }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}

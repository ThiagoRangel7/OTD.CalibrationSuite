using Xunit;
using System.Text.Json.Nodes;
using CalibrationSuite.Core;
using static CalibrationSuite.Tests.CoreFixtures;

namespace CalibrationSuite.Tests;

public class CalibrationDataTests
{
    [Fact]
    public void CompactRoundTripRetainsMeasurementsAndGuidanceWithoutDerivedErrors()
    {
        var data = Data();
        data.Nodes![0][0].Samples[2] = new(new(152, 153), new(1, 2), new(89));
        string json = data.ToJson();
        Assert.DoesNotContain('\n', json);
        Assert.DoesNotContain("error", json);
        Assert.DoesNotContain("azimuthDeg", json);
        var roundTrip = CalibrationData.Parse(json);
        Assert.Equal(data.Nodes[0][0].Samples[2], roundTrip.Nodes![0][0].Samples[2]);
        Assert.False(PreparedCalibration.Prepare(roundTrip, Geometry).IsIdentity);
        Assert.Equal("{\"format\":\"CalibrationSuite\",\"version\":2,\"mode\":\"identity\"}", CalibrationData.Identity.ToJson());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("{\"format\":\"CalibrationSuite\",\"version\":2,\"mode\":\"identity\"}")]
    public void EmptyAndIdentityDataArePassThrough(string json)
    {
        var prepared = PreparedCalibration.Load(json, Geometry, out var diagnostic);
        Assert.True(prepared.IsIdentity); Assert.Null(diagnostic);
        Assert.Equal(new Point2(-20, 2000), prepared.Correct(new(-20, 2000), new(32, 9)));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"2\"}")]
    [InlineData("{\"format\":\"CalibrationSuite\",\"version\":2.5,\"mode\":\"identity\"}")]
    [InlineData("{\"format\":\"CalibrationSuite\",\"version\":3,\"mode\":\"identity\"}")]
    [InlineData("{\"format\":\"calibrationsuite\",\"version\":2,\"mode\":\"identity\"}")]
    [InlineData("{\"Format\":\"CalibrationSuite\",\"version\":2,\"mode\":\"identity\"}")]
    [InlineData("{\"format\":\"CalibrationSuite\",\"version\":2,\"mode\":\"Measured\"}")]
    [InlineData("{\"format\":\"CalibrationSuite\",\"version\":2,\"mode\":\"measured\"}")]
    public void MalformedOrUnsupportedDataFallsBackWithDiagnostic(string json)
    {
        Assert.True(PreparedCalibration.Load(json, Geometry, out var diagnostic).IsIdentity);
        Assert.False(string.IsNullOrWhiteSpace(diagnostic));
    }

    [Fact]
    public void LegacyDataIsRejectedWithoutRewritingIt()
    {
        const string original = "{\"version\":1,\"coefficients\":[1,2,3]}";
        Assert.True(PreparedCalibration.Load(original, Geometry, out var diagnostic).IsIdentity);
        Assert.Contains("Version 1 requires recalibration", diagnostic);
        Assert.Equal("{\"version\":1,\"coefficients\":[1,2,3]}", original);
    }

    [Theory]
    [InlineData("sourceGeometry")]
    [InlineData("grid")]
    [InlineData("nodes")]
    [InlineData("sourceGeometry.maxX")]
    [InlineData("grid.preTransformRowCoordinates")]
    [InlineData("nodes.0.0.samples")]
    [InlineData("nodes.0.0.samples.0.position")]
    [InlineData("nodes.0.0.samples.0.position.x")]
    [InlineData("nodes.0.0.samples.0.tilt.y")]
    public void RequiredMeasuredFieldsCannotDefaultToZero(string path)
    {
        var root = JsonNode.Parse(Data().ToJson())!;
        var pieces = path.Split('.');
        var parent = root;
        foreach (string part in pieces[..^1]) parent = int.TryParse(part, out int index) ? parent[index]! : parent[part]!;
        parent.AsObject().Remove(pieces[^1]);
        Assert.True(PreparedCalibration.Load(root.ToJsonString(), Geometry, out var diagnostic).IsIdentity);
        Assert.NotNull(diagnostic);
    }

    [Theory]
    [InlineData("sourceGeometry")]
    [InlineData("grid")]
    [InlineData("nodes")]
    public void ExplicitNullMeasuredSectionsAreRejected(string field)
    {
        var root = JsonNode.Parse(Data().ToJson())!; root[field] = null;
        Assert.True(PreparedCalibration.Load(root.ToJsonString(), Geometry, out var diagnostic).IsIdentity);
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void UnknownOptionalPropertiesAndGuidanceDoNotChangeEvaluation()
    {
        var root = JsonNode.Parse(Data().ToJson())!;
        root["futureOptionalMetadata"] = new JsonObject { ["value"] = 42 };
        var sample = root["nodes"]![0]![0]!["samples"]![0]!;
        sample["requestedPose"] = new JsonObject { ["altitudeDeg"] = 12, ["azimuthDeg"] = 57 };
        var prepared = PreparedCalibration.Load(root.ToJsonString(), Geometry, out var diagnostic);
        Assert.Null(diagnostic); Near(new(498, 497), prepared.Correct(new(500, 500), new(13, -21)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void AllGeometryFieldsUseRelativeTolerance(int field)
    {
        SourceGeometry Change(double factor) => field switch
        {
            0 => Geometry with { MaxX = Geometry.MaxX * factor },
            1 => Geometry with { MaxY = Geometry.MaxY * factor },
            2 => Geometry with { WidthMm = Geometry.WidthMm * factor },
            _ => Geometry with { HeightMm = Geometry.HeightMm * factor }
        };
        Assert.False(PreparedCalibration.Prepare(Data(), Change(1 + 0.5e-6)).IsIdentity);
        Assert.Throws<CalibrationException>(() => PreparedCalibration.Prepare(Data(), Change(1 + 2e-6)));
    }

    [Fact]
    public void GeometryToleranceDoesNotRescaleOrRejectRecordedGridEndpoints()
    {
        var prepared = PreparedCalibration.Prepare(Data(rows: [0, 1000], columns: [0, 1000]),
            Geometry with { MaxX = 999.9995, MaxY = 999.9995 });
        Near(new(998, 997), prepared.Correct(new(1000, 1000), default));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"NaN\"")]
    [InlineData("1e999")]
    public void InvalidNumericCoordinatesReturnDiagnostics(string value)
    {
        var root = JsonNode.Parse(Data().ToJson())!;
        root["nodes"]![0]![0]!["samples"]![0]!["tilt"]!["x"] = JsonNode.Parse(value);
        Assert.True(PreparedCalibration.Load(root.ToJsonString(), Geometry, out var diagnostic).IsIdentity);
        Assert.NotNull(diagnostic);
    }

    [Fact]
    public void InvalidGridDimensionsSamplesAndFiniteValuesAreRejectedWithNodeLocation()
    {
        foreach (double[] axis in new double[][] { [], [10], [10, 10], [30, 20], [-1, 50], [10, 1001], [10, double.NaN] })
            Assert.Throws<CalibrationException>(() => PreparedCalibration.Prepare(Data(rows: axis), Geometry));
        Assert.Throws<CalibrationException>(() => PreparedCalibration.Prepare(Data() with { Nodes = [] }, Geometry));
        Assert.Throws<CalibrationException>(() => PreparedCalibration.Prepare(Data() with { SourceGeometry = Geometry with { WidthMm = 0 } }, Geometry));
        var data = Data(); data.Nodes![1][0].Samples[0] = new(new(double.PositiveInfinity, 3), new(-40, -40));
        Assert.Contains("Node [1][0]", Assert.Throws<CalibrationException>(() => PreparedCalibration.Prepare(data, Geometry)).Message);
    }

    [Fact]
    public void PreparedSnapshotIsUnaffectedByEditsToSourceArrays()
    {
        var data = Data(); var prepared = PreparedCalibration.Prepare(data, Geometry);
        data.Grid!.PreTransformRowCoordinates[0] = 800;
        data.Nodes![0][0].Samples[0] = new(new(999, 999), new(999, 999));
        data.Nodes[1] = [];
        Near(new(498, 497), prepared.Correct(new(500, 500), new(13, -21)));
    }
}

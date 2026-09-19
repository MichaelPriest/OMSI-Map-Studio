using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSplineTests
{
    [Fact]
    public void ReadSplines_ExtractsBasePlacementAndCurveData()
    {
        const string source =
            "[spline]\r\n" +
            "0\r\n" +
            "Splines\\Roads\\street.sli\r\n" +
            "120\r\n" +
            "-1\r\n" +
            "121\r\n" +
            "10.5\r\n" +
            "1.25\r\n" +
            "20.75\r\n" +
            "90\r\n" +
            "50\r\n" +
            "-200\r\n" +
            "2\r\n" +
            "4\r\n" +
            "0\r\n" +
            "0\r\n";

        var document =
            OmsiConfigParser.Parse(source);

        var spline =
            Assert.Single(
                OmsiTileReader.ReadSplines(
                    document));

        Assert.Equal(
            "Splines\\Roads\\street.sli",
            spline.SplinePath);

        Assert.Equal(
            120,
            spline.SplineId);

        Assert.Equal(
            -1,
            spline.PreviousSplineId);

        Assert.Equal(
            121,
            spline.NextSplineId);

        Assert.Equal(
            10.5,
            spline.X);

        Assert.Equal(
            1.25,
            spline.Z);

        Assert.Equal(
            20.75,
            spline.Y);

        Assert.Equal(
            90,
            spline.Rotation);

        Assert.Equal(
            50,
            spline.Length);

        Assert.Equal(
            -200,
            spline.Radius);

        Assert.Equal(
            2,
            spline.GradientStart);

        Assert.Equal(
            4,
            spline.GradientEnd);

        Assert.False(
            spline.IsHeightSpline);

        Assert.Equal(
            0,
            spline.SourceSectionOrdinal);

        Assert.Equal(
            source,
            document.ToText());
    }

    [Fact]
    public void SplineEditor_ChangesOnlyEditableNumericLines()
    {
        const string source =
            "[spline]\r\n" +
            "0\r\n" +
            "Splines\\Roads\\street.sli\r\n" +
            "120\r\n" +
            "-1\r\n" +
            "121\r\n" +
            "10.5\r\n" +
            "1.25\r\n" +
            "20.75\r\n" +
            "90\r\n" +
            "50\r\n" +
            "-200\r\n" +
            "2\r\n" +
            "4\r\n" +
            "future-extra\r\n" +
            "[future_section]\r\n" +
            "keep-exactly\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileSplineEditor.ApplyTransforms(
                document,
                [
                    new(
                        0,
                        @"Splines\Roads\street.sli",
                        120,
                        -1,
                        121,
                        false,
                        11,
                        2,
                        21,
                        95,
                        55,
                        -180,
                        3,
                        5)
                ]);

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            1,
            result.AppliedEdits);

        Assert.Contains(
            "120\r\n-1\r\n121\r\n",
            text);

        Assert.Contains(
            "11\r\n2\r\n21\r\n95\r\n55\r\n-180\r\n3\r\n5\r\n",
            text);

        Assert.Contains(
            "future-extra\r\n",
            text);

        Assert.Contains(
            "[future_section]\r\nkeep-exactly\r\n",
            text);
    }

    [Fact]
    public void SplineEditor_RefusesChangedLinks()
    {
        var document =
            OmsiConfigParser.Parse(
                "[spline]\n0\nSplines\\A.sli\n5\n-1\n6\n0\n0\n0\n0\n10\n0\n0\n0\n");

        Assert.Throws<InvalidDataException>(
            () =>
                OmsiTileSplineEditor
                    .ApplyTransforms(
                        document,
                        [
                            new(
                                0,
                                @"Splines\A.sli",
                                5,
                                -1,
                                99,
                                false,
                                0,
                                0,
                                0,
                                0,
                                10,
                                0,
                                0,
                                0)
                        ]));
    }

    [Fact]
    public void SplineInserter_AppendsDetachedCopyAndPreservesContent()
    {
        const string source =
            "[version]\r\n14\r\n" +
            "[future_section]\r\nkeep-me\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileSplineInserter.Append(
                document,
                new OmsiNewPlacedSpline(
                    "0",
                    @"Splines\Roads\street.sli",
                    121,
                    -1,
                    -1,
                    12.5,
                    1.25,
                    22.75,
                    45,
                    50,
                    -200,
                    2,
                    4,
                    false,
                    ["future-extra"]));

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            0,
            result.SourceSectionOrdinal);

        Assert.StartsWith(
            source,
            text);

        Assert.Contains(
            "[spline]\r\n0\r\n" +
            "Splines\\Roads\\street.sli\r\n" +
            "121\r\n-1\r\n-1\r\n" +
            "12.5\r\n1.25\r\n22.75\r\n" +
            "45\r\n50\r\n-200\r\n2\r\n4\r\n" +
            "future-extra\r\n",
            text);
    }

    [Fact]
    public void SplineInserter_UsesHeightKeyword()
    {
        var document =
            OmsiConfigParser.Parse(
                "[version]\n14\n");

        var result =
            OmsiTileSplineInserter.Append(
                document,
                new OmsiNewPlacedSpline(
                    "0",
                    @"Splines\Roads\hill.sli",
                    2,
                    -1,
                    -1,
                    0,
                    0,
                    0,
                    0,
                    30,
                    0,
                    5,
                    5,
                    true,
                    ["1.5"]));

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Contains(
            "[spline_h]\n",
            text);
    }

    [Fact]
    public void SplineDeleter_RemovesDetachedSplineAndPreservesComments()
    {
        const string source =
            "[spline]\r\n" +
            "0\r\n" +
            "# keep-inside\r\n" +
            "Splines\\Roads\\street.sli\r\n" +
            "121\r\n-1\r\n-1\r\n" +
            "12.5\r\n1.25\r\n22.75\r\n" +
            "45\r\n50\r\n-200\r\n2\r\n4\r\n" +
            "future-extra\r\n" +
            "# keep-after-data\r\n" +
            "[future_section]\r\nkeep-me\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileSplineDeleter.Remove(
                document,
                0,
                @"Splines\Roads\street.sli",
                121,
                -1,
                -1,
                false);

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            1,
            result.DeletedSplines);

        Assert.DoesNotContain(
            "[spline]",
            text);

        Assert.DoesNotContain(
            "future-extra",
            text);

        Assert.Contains(
            "# keep-inside\r\n",
            text);

        Assert.Contains(
            "# keep-after-data\r\n",
            text);

        Assert.Contains(
            "[future_section]\r\nkeep-me\r\n",
            text);
    }

    [Fact]
    public void SplineDeleter_RemovesExactLinkedSplineAfterGraphValidation()
    {
        var document =
            OmsiConfigParser.Parse(
                "[spline]\n0\nSplines\\A.sli\n5\n4\n6\n0\n0\n0\n0\n10\n0\n0\n0\n");

        var result =
            OmsiTileSplineDeleter.Remove(
                document,
                0,
                @"Splines\A.sli",
                5,
                4,
                6,
                false);

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.DoesNotContain(
            "[spline]",
            text);
    }

    [Fact]
    public void SplineLinkPlanner_DisconnectsBothNeighborsForDeletion()
    {
        var states =
            new Dictionary<
                int,
                OmsiSplineLinkState>
            {
                [4] = new(
                    4,
                    -1,
                    5),
                [5] = new(
                    5,
                    4,
                    6),
                [6] = new(
                    6,
                    5,
                    -1)
            };

        var plan =
            OmsiSplineLinkPlanner.Plan(
                states,
                5,
                4,
                6,
                -1,
                -1);

        Assert.Equal(
            -1,
            plan[4].NextSplineId);

        Assert.Equal(
            -1,
            plan[6].PreviousSplineId);

        Assert.Equal(
            new OmsiSplineLinkTarget(
                -1,
                -1),
            plan[5]);
    }

    [Fact]
    public void SplineLinkPlanner_RelinksBothOldAndNewNeighbors()
    {
        var states =
            new Dictionary<
                int,
                OmsiSplineLinkState>
            {
                [10] = new(
                    10,
                    8,
                    12),
                [8] = new(
                    8,
                    -1,
                    10),
                [12] = new(
                    12,
                    10,
                    -1),
                [20] = new(
                    20,
                    -1,
                    -1),
                [22] = new(
                    22,
                    -1,
                    -1)
            };

        var plan =
            OmsiSplineLinkPlanner.Plan(
                states,
                10,
                8,
                12,
                20,
                22);

        Assert.Equal(
            new OmsiSplineLinkTarget(
                20,
                22),
            plan[10]);

        Assert.Equal(
            -1,
            plan[8].NextSplineId);

        Assert.Equal(
            -1,
            plan[12].PreviousSplineId);

        Assert.Equal(
            10,
            plan[20].NextSplineId);

        Assert.Equal(
            10,
            plan[22].PreviousSplineId);
    }

    [Fact]
    public void SplineLinkPlanner_RefusesBusyTarget()
    {
        var states =
            new Dictionary<
                int,
                OmsiSplineLinkState>
            {
                [10] = new(
                    10,
                    -1,
                    -1),
                [20] = new(
                    20,
                    -1,
                    99)
            };

        Assert.Throws<InvalidDataException>(
            () =>
                OmsiSplineLinkPlanner.Plan(
                    states,
                    10,
                    -1,
                    -1,
                    20,
                    -1));
    }

    [Fact]
    public void SplineLinkEditor_ChangesOnlyLinkLines()
    {
        const string source =
            "[spline]\r\n" +
            "0\r\n" +
            "Splines\\A.sli\r\n" +
            "10\r\n-1\r\n-1\r\n" +
            "1\r\n2\r\n3\r\n4\r\n" +
            "5\r\n6\r\n7\r\n8\r\n" +
            "future-extra\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileSplineLinkEditor
                .ApplyLinks(
                    document,
                    [
                        new(
                            0,
                            @"Splines\A.sli",
                            10,
                            -1,
                            -1,
                            false,
                            20,
                            30)
                    ]);

        var text =
            System.Text.Encoding.UTF8
                .GetString(
                    result.Bytes);

        Assert.Equal(
            1,
            result.AppliedEdits);

        Assert.Contains(
            "10\r\n20\r\n30\r\n" +
            "1\r\n2\r\n3\r\n4\r\n" +
            "5\r\n6\r\n7\r\n8\r\n" +
            "future-extra\r\n",
            text);
    }

    [Fact]
    public void ReadSplines_RecognizesHeightSpline()
    {
        const string source =
            "[spline_h]\n" +
            "0\n" +
            "Splines\\Roads\\hill.sli\n" +
            "1\n" +
            "-1\n" +
            "-1\n" +
            "0\n" +
            "2\n" +
            "0\n" +
            "0\n" +
            "30\n" +
            "0\n" +
            "5\n" +
            "5\n" +
            "1.5\n";

        var document =
            OmsiConfigParser.Parse(source);

        var spline =
            Assert.Single(
                OmsiTileReader.ReadSplines(
                    document));

        Assert.True(
            spline.IsHeightSpline);

        Assert.Equal(
            "1.5",
            Assert.Single(
                spline.ExtraValues));
    }
}

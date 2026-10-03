using MapStudio.Core.Generation.Terrain;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioElevationGridReaderTests
{
    [Fact]
    public void ReadsDelimitedGrid()
    {
        var grid =
            new MapStudioElevationGridReader()
                .ReadDelimited(
                    [
                        "10, 20, 30",
                        "40, 50, 60",
                        "70, 80, 90"
                    ]);

        Assert.Equal(
            3,
            grid.Rows);

        Assert.Equal(
            3,
            grid.Columns);

        Assert.Equal(
            9,
            grid.SampleCount);

        Assert.Equal(
            10,
            grid.MinimumElevation);

        Assert.Equal(
            90,
            grid.MaximumElevation);

        Assert.Equal(
            "delimited",
            grid.SourceFormat);
    }

    [Fact]
    public void ReadsEsriAsciiGrid()
    {
        var grid =
            new MapStudioElevationGridReader()
                .ReadEsriAscii(
                    [
                        "ncols 3",
                        "nrows 2",
                        "xllcorner 100",
                        "yllcorner 200",
                        "cellsize 10",
                        "NODATA_value -9999",
                        "1 2 3",
                        "4 5 6"
                    ]);

        Assert.Equal(
            2,
            grid.Rows);

        Assert.Equal(
            3,
            grid.Columns);

        Assert.Equal(
            new double[]
            {
                1,
                2,
                3,
                4,
                5,
                6
            },
            grid.Elevations);

        Assert.Equal(
            "esri-ascii",
            grid.SourceFormat);
    }

    [Fact]
    public void RejectsNoDataSamples()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioElevationGridReader()
                        .ReadEsriAscii(
                            [
                                "ncols 2",
                                "nrows 2",
                                "xllcorner 0",
                                "yllcorner 0",
                                "cellsize 1",
                                "NODATA_value -9999",
                                "1 2",
                                "3 -9999"
                            ]));
    }

    [Fact]
    public void RejectsIrregularDelimitedRows()
    {
        Assert.Throws<
            InvalidDataException>(
                () =>
                    new MapStudioElevationGridReader()
                        .ReadDelimited(
                            [
                                "1,2,3",
                                "4,5"
                            ]));
    }
}

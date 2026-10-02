using System.Globalization;
using System.Text;
using MapStudio.Core.Omsi.Indexing;
using MapStudio.Core.Omsi.Structures;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class MapStudioOmsiJunctionResolverTests
{
    [Fact]
    public async Task ResolverFitsOriginalFourArmJunctionFromRealPathGeometry()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "mapstudio-stock-junction-resolver-" + Guid.NewGuid().ToString("N"));

        try
        {
            var relativePath =
                @"Sceneryobjects\Kreuz_MC\Kreuz_Test_4arm.sco";

            WriteStockJunction(root, relativePath, [0, 90, 180, 270], 8.0);

            var fullPath = Path.Combine(
                root,
                relativePath.Replace('\\', Path.DirectorySeparatorChar));

            var entry = new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                new FileInfo(fullPath).Length,
                File.GetLastWriteTimeUtc(fullPath).Ticks);

            var matches = await new MapStudioOmsiJunctionResolver()
                .ResolveAsync(
                    root,
                    [entry],
                    [
                        new MapStudioOmsiJunctionTarget(
                            42,
                            [
                                new(1, 12, 7, 30),
                                new(2, 102, 7, 30),
                                new(3, 192, 7, 30),
                                new(4, 282, 7, 30)
                            ])
                    ]);

            var match = Assert.Single(matches);

            Assert.Equal(42, match.Key);
            Assert.Equal(relativePath, match.Value.RelativePath);
            Assert.InRange(match.Value.RotationDegrees, 11.5, 12.5);
            Assert.Equal(4, match.Value.MouthCount);
            Assert.Equal(4, match.Value.TrimDistanceBySegmentId.Count);

            Assert.All(
                match.Value.TrimDistanceBySegmentId.Values,
                trim => Assert.InRange(trim, 7.5, 8.6));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ResolverFitsOriginalThreeArmJunctionFromRealPathGeometry()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "mapstudio-stock-junction-three-arm-" +
            Guid.NewGuid().ToString("N"));

        try
        {
            var relativePath =
                @"Sceneryobjects\Kreuz_RUE\Einm_Test_3arm.sco";

            WriteStockJunction(
                root,
                relativePath,
                [0, 90, 270],
                8.0);

            var fullPath = Path.Combine(
                root,
                relativePath.Replace(
                    '\\',
                    Path.DirectorySeparatorChar));

            var entry = new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                new FileInfo(fullPath).Length,
                File.GetLastWriteTimeUtc(fullPath).Ticks);

            var matches =
                await new MapStudioOmsiJunctionResolver()
                    .ResolveAsync(
                        root,
                        [entry],
                        [
                            new MapStudioOmsiJunctionTarget(
                                77,
                                [
                                    new(10, 17, 7, 30),
                                    new(11, 107, 7, 30),
                                    new(12, 287, 7, 30)
                                ])
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                77,
                match.Key);

            Assert.Equal(
                relativePath,
                match.Value.RelativePath);

            Assert.InRange(
                match.Value.RotationDegrees,
                16.5,
                17.5);

            Assert.Equal(
                3,
                match.Value.MouthCount);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task ResolverKeepsCloseArmsSeparateUsingPathTangents()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "mapstudio-stock-junction-close-arms-" +
            Guid.NewGuid().ToString("N"));

        try
        {
            var relativePath =
                @"Sceneryobjects\Kreuz_MC\Kreuz_Test_5arm.sco";

            WriteStockJunction(
                root,
                relativePath,
                [0, 45, 135, 225, 315],
                8.0);

            var fullPath = Path.Combine(
                root,
                relativePath.Replace(
                    '\\',
                    Path.DirectorySeparatorChar));

            var entry = new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                new FileInfo(fullPath).Length,
                File.GetLastWriteTimeUtc(fullPath).Ticks);

            var matches =
                await new MapStudioOmsiJunctionResolver()
                    .ResolveAsync(
                        root,
                        [entry],
                        [
                            new MapStudioOmsiJunctionTarget(
                                88,
                                [
                                    new(20, 7, 7, 30),
                                    new(21, 52, 7, 30),
                                    new(22, 142, 7, 30),
                                    new(23, 232, 7, 30),
                                    new(24, 322, 7, 30)
                                ])
                        ]);

            var match =
                Assert.Single(
                    matches);

            Assert.Equal(
                88,
                match.Key);

            Assert.Equal(
                5,
                match.Value.MouthCount);

            Assert.InRange(
                match.Value.RotationDegrees,
                6.5,
                7.5);

            Assert.Equal(
                5,
                match.Value.TrimDistanceBySegmentId.Count);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(
                    root,
                    recursive:
                        true);
            }
        }
    }

    [Fact]
    public async Task ResolverRejectsThreeArmAssetForFourArmTarget()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "mapstudio-stock-junction-arity-" + Guid.NewGuid().ToString("N"));

        try
        {
            var relativePath =
                @"Sceneryobjects\Kreuz_RUE\Einm_Test_3arm.sco";

            WriteStockJunction(root, relativePath, [0, 90, 270], 8.0);

            var fullPath = Path.Combine(
                root,
                relativePath.Replace('\\', Path.DirectorySeparatorChar));

            var entry = new OmsiAssetIndexEntry(
                relativePath,
                OmsiAssetKind.SceneryObject,
                new FileInfo(fullPath).Length,
                File.GetLastWriteTimeUtc(fullPath).Ticks);

            var matches = await new MapStudioOmsiJunctionResolver()
                .ResolveAsync(
                    root,
                    [entry],
                    [
                        new MapStudioOmsiJunctionTarget(
                            7,
                            [
                                new(1, 0, 7, 30),
                                new(2, 90, 7, 30),
                                new(3, 180, 7, 30),
                                new(4, 270, 7, 30)
                            ])
                    ]);

            Assert.Empty(matches);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    internal static void WriteStockJunction(
        string root,
        string relativePath,
        IReadOnlyList<double> angles,
        double radius)
    {
        var fullPath = Path.Combine(
            root,
            relativePath.Replace('\\', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        var builder = new StringBuilder(
            "[friendlyname]\r\nSynthetic stock junction\r\n\r\n");

        foreach (var angle in angles)
        {
            foreach (var lateralOffset in new[] { -1.75, 1.75 })
            {
                var radians = angle * Math.PI / 180.0;
                var x =
                    Math.Sin(radians) * radius +
                    Math.Cos(radians) * lateralOffset;
                var y =
                    Math.Cos(radians) * radius -
                    Math.Sin(radians) * lateralOffset;
                var rotation = (angle + 180.0) % 360.0;

                builder.AppendLine("[path]");
                builder.AppendLine(x.ToString("0.######", CultureInfo.InvariantCulture));
                builder.AppendLine(y.ToString("0.######", CultureInfo.InvariantCulture));
                builder.AppendLine("0");
                builder.AppendLine(rotation.ToString("0.######", CultureInfo.InvariantCulture));
                builder.AppendLine("0");
                builder.AppendLine((radius * 0.55).ToString("0.######", CultureInfo.InvariantCulture));
                builder.AppendLine("0");
                builder.AppendLine("0");
                builder.AppendLine("0");
                builder.AppendLine("3");
                builder.AppendLine("0");
                builder.AppendLine("0");
                builder.AppendLine();
            }
        }

        builder.AppendLine("[mesh]");
        builder.AppendLine("junction.o3d");

        File.WriteAllText(fullPath, builder.ToString(), Encoding.ASCII);
    }
}

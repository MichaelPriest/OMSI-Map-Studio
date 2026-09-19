using System.Text;
using MapStudio.Core.IO;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiSafeEditingTests
{
    [Fact]
    public void ObjectEditor_ChangesOnlyTransformLines()
    {
        const string source =
            "[version]\r\n14\r\n" +
            "[object]\r\n" +
            "0\r\n" +
            "Sceneryobjects\\Pack\\House.sco\r\n" +
            "77\r\n" +
            "10.5\r\n" +
            "20.25\r\n" +
            "1\r\n" +
            "90\r\n" +
            "0\r\n" +
            "0\r\n" +
            "future-extra\r\n" +
            "[future_section]\r\n" +
            "keep-exactly\r\n";

        var document =
            OmsiConfigParser.Parse(
                source);

        var result =
            OmsiTileObjectEditor
                .ApplyTransforms(
                    document,
                    [
                        new(
                            SourceSectionOrdinal: 0,
                            SceneryObjectPath:
                                @"Sceneryobjects\Pack\House.sco",
                            ObjectId: 77,
                            X: 11.75,
                            Y: 22.5,
                            Z: 1.25,
                            Rotation: 135,
                            Pitch: 2.5,
                            Bank: -1.5)
                    ]);

        var text =
            Encoding.UTF8.GetString(
                result.Bytes);

        Assert.Equal(
            1,
            result.AppliedEdits);

        Assert.Contains(
            "[future_section]\r\nkeep-exactly\r\n",
            text);

        Assert.Contains(
            "future-extra\r\n",
            text);

        Assert.Contains(
            "11.75\r\n22.5\r\n1.25\r\n135\r\n2.5\r\n-1.5\r\n",
            text);
    }

    [Fact]
    public void ObjectEditor_RefusesChangedSourceIdentity()
    {
        var document =
            OmsiConfigParser.Parse(
                "[object]\n" +
                "0\n" +
                "Sceneryobjects\\A.sco\n" +
                "5\n" +
                "0\n0\n0\n0\n0\n0\n");

        Assert.Throws<InvalidDataException>(
            () =>
                OmsiTileObjectEditor
                    .ApplyTransforms(
                        document,
                        [
                            new(
                                0,
                                @"Sceneryobjects\B.sco",
                                5,
                                1,
                                2,
                                3,
                                4,
                                5,
                                6)
                        ]));
    }

    [Fact]
    public async Task SafeTransaction_CreatesBackupAndReplacesTarget()
    {
        var root =
            Path.Combine(
                Path.GetTempPath(),
                $"mapstudio-save-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            root);

        var target =
            Path.Combine(
                root,
                "tile_0_0.map");

        var backup =
            Path.Combine(
                root,
                ".mapstudio-backups",
                "test",
                "tile_0_0.map");

        try
        {
            await File.WriteAllTextAsync(
                target,
                "before",
                new UTF8Encoding(false));

            await SafeFileTransaction
                .WriteAllAsync(
                    [
                        new(
                            target,
                            backup,
                            Encoding.UTF8.GetBytes(
                                "after"))
                    ]);

            Assert.Equal(
                "after",
                await File.ReadAllTextAsync(
                    target));

            Assert.Equal(
                "before",
                await File.ReadAllTextAsync(
                    backup));
        }
        finally
        {
            Directory.Delete(
                root,
                recursive: true);
        }
    }

    [Fact]
    public void TileReader_AssignsStableObjectSectionOrdinals()
    {
        var document =
            OmsiConfigParser.Parse(
                "[object]\n0\nSceneryobjects\\A.sco\n1\n0\n0\n0\n0\n0\n0\n" +
                "[object]\n0\nSceneryobjects\\B.sco\n2\n0\n0\n0\n0\n0\n0\n");

        var objects =
            OmsiTileReader.ReadObjects(
                document);

        Assert.Equal(
            0,
            objects[0]
                .SourceSectionOrdinal);

        Assert.Equal(
            1,
            objects[1]
                .SourceSectionOrdinal);
    }
}

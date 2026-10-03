using System.Text;
using MapStudio.Core.Omsi.Config;
using MapStudio.Core.Omsi.Maps;
using Xunit;

namespace MapStudio.Core.Tests;

public sealed class OmsiTileWaterMarkerEditorTests
{
    [Fact]
    public void EnsurePresentAddsSingleWaterSection()
    {
        var document =
            OmsiConfigParser.Parse(
                "[version]\r\n14\r\n\r\n" +
                "[terrain]\r\n\r\n");

        var first =
            OmsiTileWaterMarkerEditor
                .EnsurePresent(
                    document);

        var parsed =
            OmsiConfigParser
                .ParseBytes(
                    first);

        Assert.Single(
            parsed.FindSections(
                "water"));

        var second =
            OmsiTileWaterMarkerEditor
                .EnsurePresent(
                    parsed);

        var parsedAgain =
            OmsiConfigParser
                .ParseBytes(
                    second);

        Assert.Single(
            parsedAgain.FindSections(
                "water"));
    }

    [Fact]
    public void RemovePreservesFollowingSections()
    {
        var document =
            OmsiConfigParser.Parse(
                "[version]\r\n14\r\n\r\n" +
                "[water]\r\n\r\n" +
                "# water comment\r\n\r\n" +
                "[object]\r\n0\r\n" +
                "Sceneryobjects\\Test\\test.sco\r\n" +
                "1\r\n0\r\n0\r\n0\r\n0\r\n0\r\n0\r\n");

        var bytes =
            OmsiTileWaterMarkerEditor
                .Remove(
                    document);

        var parsed =
            OmsiConfigParser
                .ParseBytes(
                    bytes);

        Assert.Null(
            parsed.FindFirstSection(
                "water"));

        var objectSection =
            Assert.Single(
                parsed.FindSections(
                    "object"));

        Assert.Equal(
            "Sceneryobjects\\Test\\test.sco",
            objectSection
                .DataLines
                .Skip(1)
                .First());
    }

    [Fact]
    public void EditingMarkerPreservesUtf16Bom()
    {
        var encoding =
            new UnicodeEncoding(
                bigEndian:
                    false,
                byteOrderMark:
                    true,
                throwOnInvalidBytes:
                    true);

        var body =
            encoding.GetBytes(
                "[version]\r\n14\r\n");

        var preamble =
            encoding.GetPreamble();

        var source =
            new byte[
                preamble.Length +
                body.Length];

        Buffer.BlockCopy(
            preamble,
            0,
            source,
            0,
            preamble.Length);

        Buffer.BlockCopy(
            body,
            0,
            source,
            preamble.Length,
            body.Length);

        var document =
            OmsiConfigParser
                .ParseBytes(
                    source);

        var bytes =
            OmsiTileWaterMarkerEditor
                .EnsurePresent(
                    document);

        Assert.True(
            bytes.AsSpan()
                .StartsWith(
                    preamble));

        var parsed =
            OmsiConfigParser
                .ParseBytes(
                    bytes);

        Assert.True(
            parsed.HasByteOrderMark);

        Assert.Equal(
            Encoding.Unicode
                .CodePage,
            parsed.TextEncoding
                .CodePage);

        Assert.NotNull(
            parsed.FindFirstSection(
                "water"));
    }
}

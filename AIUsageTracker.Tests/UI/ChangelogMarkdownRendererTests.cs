// <copyright file="ChangelogMarkdownRendererTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Windows.Documents;
using System.Windows.Media;
using AIUsageTracker.UI.Slim.Services;

namespace AIUsageTracker.Tests.UI;

public sealed class ChangelogMarkdownRendererTests
{
    // ── GetHeaderLevel ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("# Title", 1)]
    [InlineData("## Section", 2)]
    [InlineData("### Sub", 3)]
    [InlineData("#### Deep", 4)]
    public void GetHeaderLevel_ReturnsCorrectLevel(string line, int expected)
    {
        Assert.Equal(expected, ChangelogMarkdownRenderer.GetHeaderLevel(line));
    }

    [Theory]
    [InlineData("No hashes")]
    [InlineData("#NoSpace")]
    [InlineData("")]
    [InlineData("##NoSpace")]
    public void GetHeaderLevel_ReturnsZero_WhenNotAHeader(string line)
    {
        Assert.Equal(0, ChangelogMarkdownRenderer.GetHeaderLevel(line));
    }

    // ── TryParseNumberedItem ──────────────────────────────────────────────────

    [Fact]
    public void TryParseNumberedItem_ReturnsTrueAndExtractsNumber()
    {
        var result = ChangelogMarkdownRenderer.TryParseNumberedItem("3. Do the thing", out var number, out var content);

        Assert.True(result);
        Assert.Equal(3, number);
        Assert.Equal("Do the thing", content);
    }

    [Fact]
    public void TryParseNumberedItem_ReturnsFalse_ForBulletItem()
    {
        var result = ChangelogMarkdownRenderer.TryParseNumberedItem("- Not numbered", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseNumberedItem_ReturnsFalse_WhenPrefixNotNumeric()
    {
        var result = ChangelogMarkdownRenderer.TryParseNumberedItem("abc. Text", out _, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseNumberedItem_ReturnsFalse_WhenContentIsBlank()
    {
        var result = ChangelogMarkdownRenderer.TryParseNumberedItem("1.   ", out _, out _);
        Assert.False(result);
    }

    // ── TryCreateHyperlink ────────────────────────────────────────────────────

    [Fact]
    public void TryCreateHyperlink_ReturnsTrueForValidMarkdownLink()
    {
        var result = ChangelogMarkdownRenderer.TryCreateHyperlink(
            "[Claude](https://claude.ai)", out var hyperlink);

        Assert.True(result);
        Assert.NotNull(hyperlink);
        Assert.Equal(new Uri("https://claude.ai"), hyperlink.NavigateUri);
    }

    [Fact]
    public void TryCreateHyperlink_ReturnsFalse_ForPlainText()
    {
        var result = ChangelogMarkdownRenderer.TryCreateHyperlink("plain text", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryCreateHyperlink_ReturnsFalse_ForRelativeUrl()
    {
        var result = ChangelogMarkdownRenderer.TryCreateHyperlink("[link](/relative)", out _);
        Assert.False(result);
    }

    // ── BuildDocument (WPF — requires STA thread) ─────────────────────────────

    [Fact]
    public void BuildDocument_ReturnsEmptyMessage_ForNullOrWhitespace()
    {
        string? runText = null;
        int? blockCount = null;
        Exception? ex = null;

        var thread = new Thread(() =>
        {
            try
            {
                var renderer = new ChangelogMarkdownRenderer((_, fallback) => fallback);
                var doc = renderer.BuildDocument(string.Empty);
                blockCount = doc.Blocks.Count;
                var paragraph = doc.Blocks.OfType<Paragraph>().Single();
                runText = paragraph.Inlines.OfType<Run>().Single().Text;
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null) throw new Exception("STA thread threw", ex);

        Assert.Equal(1, blockCount);
        Assert.Contains("No changelog", runText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildDocument_ParsesHeadingBulletAndParagraph()
    {
        const string markdown = """
            # Release 1.0

            A new version.

            - Fix one
            - Fix two
            """;

        int? totalBlocks = null;
        double? headingFontSize = null;
        Exception? ex = null;

        var thread = new Thread(() =>
        {
            try
            {
                var renderer = new ChangelogMarkdownRenderer((_, fallback) => fallback);
                var doc = renderer.BuildDocument(markdown);
                totalBlocks = doc.Blocks.Count;
                headingFontSize = doc.Blocks.OfType<Paragraph>().First().FontSize;
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null) throw new Exception("STA thread threw", ex);

        Assert.Equal(22, headingFontSize); // h1 = 22
        Assert.Equal(4, totalBlocks);     // heading + paragraph + 2 bullets
    }

    [Fact]
    public void BuildDocument_ParsesFencedCodeBlock()
    {
        const string markdown = """
            Some intro.

            ```
            var x = 1;
            ```
            """;

        int? blockCount = null;
        string? lastFontFamilySource = null;
        Exception? ex = null;

        var thread = new Thread(() =>
        {
            try
            {
                var renderer = new ChangelogMarkdownRenderer((_, fallback) => fallback);
                var doc = renderer.BuildDocument(markdown);
                blockCount = doc.Blocks.Count;
                lastFontFamilySource = doc.Blocks.OfType<Paragraph>().Last().FontFamily.Source;
            }
            catch (Exception e)
            {
                ex = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (ex != null) throw new Exception("STA thread threw", ex);

        Assert.Equal(2, blockCount); // intro paragraph + code block
        Assert.Equal("Consolas", lastFontFamilySource);
    }
}

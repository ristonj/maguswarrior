using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MagusWarrior.Tests.Unit;

public class I18nTest {
    // AppContext.BaseDirectory = tests/bin/Debug/net9.0/ — go up 4 to reach project root
    private static readonly string CsvPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../data/strings/ui_strings.csv"));

    [Fact]
    public void CsvHasCorrectHeader() {
        Assert.True(File.Exists(CsvPath), $"Expected CSV at {CsvPath}");
        string firstLine = File.ReadLines(CsvPath).First();
        Assert.Equal("keys,en", firstLine);
    }

    [Fact]
    public void CsvContainsPlaceholderTitleKey() {
        Assert.True(File.Exists(CsvPath), $"Expected CSV at {CsvPath}");
        string? matchLine = File.ReadLines(CsvPath)
            .FirstOrDefault(l => l.StartsWith("ui.placeholder_menu.title,"));
        Assert.NotNull(matchLine);
        string value = matchLine!.Substring("ui.placeholder_menu.title,".Length);
        Assert.False(string.IsNullOrWhiteSpace(value));
    }
}

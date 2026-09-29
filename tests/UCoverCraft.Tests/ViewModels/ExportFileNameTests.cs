using UCoverCraft.App.ViewModels;

namespace UCoverCraft.Tests.ViewModels;

public class ExportFileNameTests
{
    [Theory]
    [InlineData("LAB REPORT")]
    [InlineData("Thesis Report")]
    [InlineData("Smart Campus Navigation")]
    [InlineData("Lab Report v1.2")]
    [InlineData(".hidden notes")]
    [InlineData("COM10")]
    [InlineData("CONSOLE")]
    [InlineData("CON report")]
    [InlineData("AUXILIARY")]
    [InlineData("LPT0")]
    public void SanitizeFileName_KeepsValidTitlesUnchanged(string title)
    {
        Assert.Equal(title, MainViewModel.SanitizeFileName(title));
    }

    [Theory]
    [InlineData("Report: Part/1", "Report Part1")]
    [InlineData("A<B>C|D?E*F\"G", "ABCDEFG")]
    [InlineData("file\tname", "filename")]
    [InlineData("n/a\\b", "nab")]
    public void SanitizeFileName_RemovesInvalidCharacters(string title, string expected)
    {
        Assert.Equal(expected, MainViewModel.SanitizeFileName(title));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("///")]
    [InlineData("...")]
    [InlineData(". . .")]
    public void SanitizeFileName_FallsBackToCoverPage_WhenNothingUsableRemains(string title)
    {
        Assert.Equal("CoverPage", MainViewModel.SanitizeFileName(title));
    }

    [Theory]
    [InlineData("CON", "_CON")]
    [InlineData("con", "_con")]
    [InlineData("PRN", "_PRN")]
    [InlineData("AUX", "_AUX")]
    [InlineData("NUL", "_NUL")]
    [InlineData("COM1", "_COM1")]
    [InlineData("COM5", "_COM5")]
    [InlineData("COM9", "_COM9")]
    [InlineData("LPT1", "_LPT1")]
    [InlineData("LPT9", "_LPT9")]
    [InlineData("nul", "_nul")]
    [InlineData("CON.", "_CON")]
    [InlineData(" CON ", "_CON")]
    [InlineData("NUL . ", "_NUL")]
    [InlineData("con.txt", "_con.txt")]
    public void SanitizeFileName_HandlesReservedDeviceNames(string title, string expected)
    {
        Assert.Equal(expected, MainViewModel.SanitizeFileName(title));
    }

    [Theory]
    [InlineData("Report.", "Report")]
    [InlineData("Report..", "Report")]
    [InlineData("Report ", "Report")]
    [InlineData("Report. ", "Report")]
    [InlineData("  Report.  ", "Report")]
    [InlineData("Thesis. . .", "Thesis")]
    [InlineData("a.", "a")]
    public void SanitizeFileName_TrimsTrailingDotsAndSpaces(string title, string expected)
    {
        Assert.Equal(expected, MainViewModel.SanitizeFileName(title));
    }

    [Fact]
    public void SanitizeFileName_HandlesCombinedHazards()
    {
        Assert.Equal("_CON", MainViewModel.SanitizeFileName("  CON: .  "));
    }
}

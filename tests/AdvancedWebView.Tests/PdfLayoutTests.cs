namespace Mafesoft.Maui.Controls.Tests;

public class PdfLayoutTests
{
    [Theory]
    [InlineData(WebPaperSize.A4, 210, 297)]
    [InlineData(WebPaperSize.A3, 297, 420)]
    [InlineData(WebPaperSize.A5, 148, 210)]
    [InlineData(WebPaperSize.Letter, 215.9, 279.4)]
    [InlineData(WebPaperSize.Legal, 215.9, 355.6)]
    public void Paper_sizes_in_portrait(WebPaperSize paper, double widthMm, double heightMm)
    {
        var (width, height) = PdfLayout.PageSizeInches(paper, WebPageOrientation.Portrait);

        Assert.Equal(widthMm, width * PdfLayout.MillimetersPerInch, 3);
        Assert.Equal(heightMm, height * PdfLayout.MillimetersPerInch, 3);
    }

    [Fact]
    public void Landscape_swaps_width_and_height()
    {
        var portrait = PdfLayout.PageSizeInches(WebPaperSize.A4, WebPageOrientation.Portrait);
        var landscape = PdfLayout.PageSizeInches(WebPaperSize.A4, WebPageOrientation.Landscape);

        Assert.Equal(portrait.Height, landscape.Width);
        Assert.Equal(portrait.Width, landscape.Height);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(1000, 92.3)] // capped: (210 mm - 1 in) / 2, so an inch of the page stays printable
    public void Margin_is_clamped(double marginMm, double expectedMm)
    {
        var margin = PdfLayout.MarginInches(new WebPdfOptions { MarginMillimeters = marginMm });

        Assert.Equal(expectedMm, margin * PdfLayout.MillimetersPerInch, 1);
    }

    [Fact]
    public void Defaults_are_a4_portrait_with_10_mm_margins()
    {
        var options = new WebPdfOptions();

        Assert.Equal(WebPaperSize.A4, options.PaperSize);
        Assert.Equal(WebPageOrientation.Portrait, options.Orientation);
        Assert.Equal(10, options.MarginMillimeters);
    }
}

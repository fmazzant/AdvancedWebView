namespace Mafesoft.Maui.Controls
{
    /// <summary>Paper size of <see cref="WebPdfOptions"/>.</summary>
    public enum WebPaperSize
    {
        /// <summary>ISO A4, 210 × 297 mm.</summary>
        A4,

        /// <summary>ISO A3, 297 × 420 mm.</summary>
        A3,

        /// <summary>ISO A5, 148 × 210 mm.</summary>
        A5,

        /// <summary>US Letter, 8.5 × 11 in.</summary>
        Letter,

        /// <summary>US Legal, 8.5 × 14 in.</summary>
        Legal,
    }

    /// <summary>Page orientation of <see cref="WebPdfOptions"/>.</summary>
    public enum WebPageOrientation
    {
        /// <summary>Taller than wide.</summary>
        Portrait,

        /// <summary>Wider than tall.</summary>
        Landscape,
    }

    /// <summary>How <c>AdvancedWebView.ExportPdfAsync</c> lays out the page, as when printing it.</summary>
    public sealed class WebPdfOptions
    {
        /// <summary>The paper size. Defaults to <see cref="WebPaperSize.A4"/>.</summary>
        public WebPaperSize PaperSize { get; set; } = WebPaperSize.A4;

        /// <summary>The orientation. Defaults to <see cref="WebPageOrientation.Portrait"/>.</summary>
        public WebPageOrientation Orientation { get; set; } = WebPageOrientation.Portrait;

        /// <summary>The margin on every side, in millimeters. Defaults to 10. The page's <c>@page</c> CSS can change it.</summary>
        public double MarginMillimeters { get; set; } = 10;
    }

    /// <summary>Page geometry for the PDF export. Platform-independent.</summary>
    internal static class PdfLayout
    {
        public const double MillimetersPerInch = 25.4;

        /// <summary>Width and height of the page in inches, with the orientation applied.</summary>
        public static (double Width, double Height) PageSizeInches(WebPaperSize paper, WebPageOrientation orientation)
        {
            var (width, height) = paper switch
            {
                WebPaperSize.A3 => (297 / MillimetersPerInch, 420 / MillimetersPerInch),
                WebPaperSize.A5 => (148 / MillimetersPerInch, 210 / MillimetersPerInch),
                WebPaperSize.Letter => (8.5, 11d),
                WebPaperSize.Legal => (8.5, 14d),
                _ => (210 / MillimetersPerInch, 297 / MillimetersPerInch),
            };
            return orientation == WebPageOrientation.Landscape ? (height, width) : (width, height);
        }

        /// <summary>The margin in inches: not negative, and leaving at least an inch of printable page in each direction.</summary>
        public static double MarginInches(WebPdfOptions options)
        {
            var (width, height) = PageSizeInches(options.PaperSize, options.Orientation);
            var margin = double.IsFinite(options.MarginMillimeters) ? Math.Max(0, options.MarginMillimeters) / MillimetersPerInch : 0;
            return Math.Min(margin, (Math.Min(width, height) - 1) / 2);
        }
    }
}

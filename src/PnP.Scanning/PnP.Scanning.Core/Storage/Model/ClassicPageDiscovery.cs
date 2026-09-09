using Microsoft.EntityFrameworkCore;

namespace PnP.Scanning.Core.Storage
{
    /// <summary>
    /// One lossless ASPX observation made by the Classic page scan. This table is the discovery
    /// denominator; <see cref="ClassicPage"/> remains the selected Classic classification output.
    /// </summary>
    [Index(nameof(ScanId), [nameof(SiteUrl), nameof(WebUrl), nameof(PageUrl)], IsUnique = true)]
    internal sealed class ClassicPageDiscovery : BaseScanResult
    {
        public string OutputVersion { get; set; }

        public string PageUrl { get; set; }

        public string PageName { get; set; }

        public string PageType { get; set; }

        public string ListUrl { get; set; }

        public string ListTitle { get; set; }

        public Guid ListId { get; set; }

        public bool LibraryHidden { get; set; }

        public string ObservationState { get; set; }

        public string SelectionState { get; set; }

        public bool? HomePage { get; set; }

        public string WelcomePageStatus { get; set; }
    }
}

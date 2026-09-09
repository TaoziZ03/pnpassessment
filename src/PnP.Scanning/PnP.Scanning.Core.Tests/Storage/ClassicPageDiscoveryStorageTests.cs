using CsvHelper;
using CsvHelper.Configuration;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PnP.Scanning.Core.Scanners;
using PnP.Scanning.Core.Services;
using PnP.Scanning.Core.Storage;
using PnP.Scanning.Core.Tests.Fixtures;
using System.Globalization;
using Xunit;

namespace PnP.Scanning.Core.Tests.Storage
{
    public sealed class ClassicPageDiscoveryStorageTests : IClassFixture<ScanContextFixture>
    {
        private readonly ScanContextFixture fixture;

        public ClassicPageDiscoveryStorageTests(ScanContextFixture fixture) => this.fixture = fixture;

        [Fact]
        public async Task Migration_persists_separate_observation_selection_and_partial_state()
        {
            var scanId = Guid.NewGuid();
            const string siteUrl = "https://contoso.sharepoint.com/sites/classic-discovery";
            const string pageUrl = "/sites/classic-discovery/SitePages/Home.aspx";
            using (var context = fixture.CreateContext())
            {
                context.ClassicPageDiscoveries.Add(new ClassicPageDiscovery
                {
                    ScanId = scanId,
                    SiteUrl = siteUrl,
                    WebUrl = "/",
                    OutputVersion = ClassicAspxDiscoveryContract.OutputVersion,
                    PageUrl = pageUrl,
                    PageName = "Home",
                    PageType = PageScanComponent.WikiPage,
                    ListUrl = "/sites/classic-discovery/SitePages",
                    ListTitle = "Site Pages",
                    ListId = Guid.NewGuid(),
                    LibraryHidden = true,
                    ObservationState = ClassicAspxDiscoveryContract.Observed,
                    SelectionState = ClassicAspxDiscoveryContract.NotEvaluated,
                    HomePage = null,
                    WelcomePageStatus = ClassicAspxDiscoveryContract.WelcomePageDenied,
                });
                context.ClassicWebSummaries.Add(new ClassicWebSummary
                {
                    ScanId = scanId,
                    SiteUrl = siteUrl,
                    WebUrl = "/",
                    PageDiscoveryOutputVersion = ClassicAspxDiscoveryContract.OutputVersion,
                    PageDiscoveryState = ClassicAspxDiscoveryContract.Partial,
                    PageDiscoveryGapCodes = ClassicAspxDiscoveryContract.WelcomePageDeniedGap,
                    WelcomePageStatus = ClassicAspxDiscoveryContract.WelcomePageDenied,
                    WelcomePageEvidence = "UnauthorizedAccessException;correlation=synthetic",
                    AllAspxObserved = 1,
                    PageSelectionNotEvaluated = 1,
                    HiddenAspxObserved = 1,
                });
                await context.SaveChangesAsync();
            }

            using (var context = fixture.CreateContext())
            {
                var row = await context.ClassicPageDiscoveries.SingleAsync(item => item.ScanId == scanId);
                row.LibraryHidden.Should().BeTrue();
                row.SelectionState.Should().Be(ClassicAspxDiscoveryContract.NotEvaluated);
                row.HomePage.Should().BeNull();

                var summary = await context.ClassicWebSummaries.SingleAsync(item => item.ScanId == scanId);
                summary.PageDiscoveryState.Should().Be(ClassicAspxDiscoveryContract.Partial);
                summary.AllAspxObserved.Should().Be(1);
                summary.PageSelectionNotEvaluated.Should().Be(1);
            }
        }

        [Fact]
        public async Task Csv_exports_versioned_classic_aspx_discovery_rows()
        {
            var scanId = Guid.NewGuid();
            using (var context = fixture.CreateContext())
            {
                context.ClassicPageDiscoveries.Add(new ClassicPageDiscovery
                {
                    ScanId = scanId,
                    SiteUrl = "https://contoso.sharepoint.com/sites/csv",
                    WebUrl = "/",
                    OutputVersion = ClassicAspxDiscoveryContract.OutputVersion,
                    PageUrl = "/sites/csv/SitePages/Home.aspx",
                    PageType = PageScanComponent.WikiPage,
                    ListId = Guid.NewGuid(),
                    LibraryHidden = true,
                    ObservationState = ClassicAspxDiscoveryContract.Observed,
                    SelectionState = ClassicAspxDiscoveryContract.Selected,
                    HomePage = true,
                    WelcomePageStatus = ClassicAspxDiscoveryContract.WelcomePageSuccess,
                });
                await context.SaveChangesAsync();
            }

            string directory = Path.Combine(Path.GetTempPath(), "classic-aspx-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                using (var context = fixture.CreateContext())
                {
                    await ReportManager.ExportClassicReportDataAsync(context, scanId, directory,
                        new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "," });
                }

                string path = Path.Combine(directory, "classicaspxdiscovery.csv");
                File.Exists(path).Should().BeTrue();
                using var reader = new StreamReader(path);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
                var row = csv.GetRecords<ClassicPageDiscovery>().Single();
                row.OutputVersion.Should().Be(ClassicAspxDiscoveryContract.OutputVersion);
                row.ObservationState.Should().Be(ClassicAspxDiscoveryContract.Observed);
                row.SelectionState.Should().Be(ClassicAspxDiscoveryContract.Selected);
                row.LibraryHidden.Should().BeTrue();
                row.HomePage.Should().BeTrue();
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PnP.Scanning.Core.Scanners;
using PnP.Scanning.Core.Storage;
using Xunit;

namespace PnP.Scanning.Core.Tests.Scanners
{
    public class ClassicScannerPostScanningTests
    {
        [Fact]
        public async Task PostScanning_AllWebsFailed_PreservesFailureWithoutInventingSiteSummary()
        {
            var scanId = Guid.NewGuid();
            try
            {
                using (var context = new ScanContext(scanId))
                {
                    await context.Database.MigrateAsync();
                    context.Webs.Add(new Web
                    {
                        ScanId = scanId, SiteUrl = "https://contoso.sharepoint.com/sites/failed",
                        WebUrl = "/", Status = SiteWebStatus.Failed, Error = "Initialization failed",
                    });
                    await context.SaveChangesAsync();
                }

                await NewScanner(scanId).PostScanningAsync();

                using var reopened = new ScanContext(scanId);
                reopened.ClassicSiteSummaries.Should().BeEmpty();
                reopened.ClassicWebSummaries.Should().BeEmpty();
                reopened.Webs.Single().Status.Should().Be(SiteWebStatus.Failed);
                reopened.Webs.Single().Error.Should().Be("Initialization failed");
            }
            finally
            {
                DeleteTestDatabase(scanId);
            }
        }

        [Fact]
        public async Task PostScanning_SuccessfulWebs_PreservesCountsAndWeightedMappingAverage()
        {
            var scanId = Guid.NewGuid();
            const string site = "https://contoso.sharepoint.com/sites/collected";
            try
            {
                using (var context = new ScanContext(scanId))
                {
                    await context.Database.MigrateAsync();
                    context.ClassicWebSummaries.AddRange(
                        new ClassicWebSummary
                        {
                            ScanId = scanId, SiteUrl = site, WebUrl = "/", Template = "STS#0",
                            ClassicPages = 2, ClassicWikiPages = 2, AggregatedRemediationCodes = "PG1",
                        },
                        new ClassicWebSummary
                        {
                            ScanId = scanId, SiteUrl = site, WebUrl = "/sub", Template = "CMSPUBLISHING#0",
                            ClassicPages = 1, ClassicPublishingPages = 1, AggregatedRemediationCodes = "PG2",
                        });
                    context.ClassicPages.AddRange(
                        NewPage(scanId, site, "/", "/SitePages/a.aspx", 100),
                        NewPage(scanId, site, "/", "/SitePages/b.aspx", 0),
                        NewPage(scanId, site, "/sub", "/sub/Pages/c.aspx", 50));
                    await context.SaveChangesAsync();
                }

                await NewScanner(scanId).PostScanningAsync();

                using var reopened = new ScanContext(scanId);
                var summary = reopened.ClassicSiteSummaries.Single();
                summary.ClassicPages.Should().Be(3);
                summary.ClassicWikiPages.Should().Be(2);
                summary.ClassicPublishingPages.Should().Be(1);
                summary.PagesWithWebParts.Should().Be(3);
                summary.MappableWebPartPages.Should().Be(1);
                summary.UnmappedWebPartPages.Should().Be(2);
                summary.AvgMappingPercentage.Should().Be(50);
                summary.SubWebCount.Should().Be(1);
                summary.RootWebTemplate.Should().Be("STS#0");
                summary.SubWebTemplates.Should().Be("CMSPUBLISHING#0");
                summary.AggregatedRemediationCodes.Split(',').Should().BeEquivalentTo(new[] { "PG1", "PG2" });
            }
            finally
            {
                DeleteTestDatabase(scanId);
            }
        }

        private static ClassicScanner NewScanner(Guid scanId) => new(null, null, null, scanId, "", "/", "STS#0",
            new ClassicOptions { Pages = true, SkipUsageInformation = true });

        private static void DeleteTestDatabase(Guid scanId)
        {
            var testRoot = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            var directory = Path.GetFullPath(StorageManager.GetScanDataFolder(scanId));
            if (!directory.StartsWith(testRoot, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(directory) != scanId.ToString())
            {
                throw new InvalidOperationException("Test database directory is outside the test output.");
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(directory, recursive: true);
        }

        private static ClassicPage NewPage(Guid scanId, string site, string web, string page, double percentage) => new()
        {
            ScanId = scanId, SiteUrl = site, WebUrl = web, PageUrl = page,
            PageName = Path.GetFileName(page), PageType = "WikiPage", WebPartCount = 1, MappingPercentage = percentage,
        };
    }
}

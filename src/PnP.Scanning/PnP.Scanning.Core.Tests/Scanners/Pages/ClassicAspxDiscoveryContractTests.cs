using FluentAssertions;
using PnP.Core.Model.SharePoint;
using PnP.Scanning.Core.Scanners;
using PnP.Scanning.Core.Storage;
using Xunit;

namespace PnP.Scanning.Core.Tests.Scanners.Pages
{
    public sealed class ClassicAspxDiscoveryContractTests
    {
        [Fact]
        public void Visible_wiki_home_and_non_home_keep_observed_and_selected_denominators()
        {
            var welcomePage = ClassicAspxDiscoveryContract.FromValue("SitePages/Home.aspx");
            var rows = Rows(hidden: false, homePageOnly: false, welcomePage);

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(row => row.ObservationState == ClassicAspxDiscoveryContract.Observed);
            rows.Should().OnlyContain(row => row.SelectionState == ClassicAspxDiscoveryContract.Selected);
            rows.Single(row => row.PageUrl.EndsWith("Home.aspx")).HomePage.Should().BeTrue();
            rows.Single(row => row.PageUrl.EndsWith("About.aspx")).HomePage.Should().BeFalse();

            var summary = ClassicAspxDiscoveryContract.Summarize(rows, welcomePage, homePageOnly: false);
            summary.AllAspxObserved.Should().Be(2);
            summary.ClassicSelected.Should().Be(2);
            summary.State.Should().Be(ClassicAspxDiscoveryContract.Complete);
        }

        [Fact]
        public void Hidden_page_library_remains_in_observed_and_selected_denominators()
        {
            var welcomePage = ClassicAspxDiscoveryContract.FromValue("SitePages/Home.aspx");
            var rows = Rows(hidden: true, homePageOnly: false, welcomePage);

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(row => row.LibraryHidden);
            rows.Should().OnlyContain(row => row.SelectionState == ClassicAspxDiscoveryContract.Selected);
            ClassicAspxDiscoveryContract.Summarize(rows, welcomePage, false).HiddenAspxObserved.Should().Be(2);
        }

        [Fact]
        public void Homepage_only_selects_home_and_records_non_home_as_explicit_exclusion()
        {
            var welcomePage = ClassicAspxDiscoveryContract.FromValue("SitePages/Home.aspx");
            var rows = Rows(hidden: false, homePageOnly: true, welcomePage);

            rows.Should().ContainSingle(row => row.SelectionState == ClassicAspxDiscoveryContract.Selected && row.HomePage == true);
            rows.Should().ContainSingle(row => row.SelectionState == ClassicAspxDiscoveryContract.ExcludedHomePageOnly && row.HomePage == false);
            var summary = ClassicAspxDiscoveryContract.Summarize(rows, welcomePage, true);
            summary.AllAspxObserved.Should().Be(2);
            summary.ClassicSelected.Should().Be(1);
            summary.ClassicExcluded.Should().Be(1);
        }

        [Fact]
        public async Task Welcome_page_success_empty_is_distinct_and_uses_default_aspx_fallback()
        {
            var resolution = await PageScanComponent.ResolveWelcomePageAsync(() => Task.FromResult<string>(null));

            resolution.Status.Should().Be(ClassicAspxDiscoveryContract.WelcomePageEmpty);
            resolution.IsAvailable.Should().BeTrue();
            ClassicAspxDiscoveryContract.EvaluateSelection(
                "/sites/team/default.aspx", PageScanComponent.WebPartPage, resolution, homePageOnly: true)
                .Should().Be(new ClassicPageSelection(ClassicAspxDiscoveryContract.Selected, true));
        }

        [Fact]
        public async Task Welcome_page_denied_is_partial_normally_and_failed_for_homepage_only()
        {
            var denied = await PageScanComponent.ResolveWelcomePageAsync(
                () => Task.FromException<string>(new UnauthorizedAccessException("Access denied")));
            var rows = Rows(hidden: false, homePageOnly: true, denied);

            denied.Status.Should().Be(ClassicAspxDiscoveryContract.WelcomePageDenied);
            denied.Evidence.Should().Contain(nameof(UnauthorizedAccessException));
            rows.Should().OnlyContain(row => row.SelectionState == ClassicAspxDiscoveryContract.NotEvaluated && row.HomePage == null);
            ClassicAspxDiscoveryContract.Summarize(rows, denied, false).State.Should().Be(ClassicAspxDiscoveryContract.Partial);
            var failed = ClassicAspxDiscoveryContract.Summarize(rows, denied, true);
            failed.State.Should().Be(ClassicAspxDiscoveryContract.Failed);
            failed.GapCodes.Should().Be(ClassicAspxDiscoveryContract.WelcomePageDeniedGap);
        }

        [Fact]
        public async Task Welcome_page_error_is_not_collapsed_to_empty()
        {
            var error = await PageScanComponent.ResolveWelcomePageAsync(
                () => Task.FromException<string>(new InvalidOperationException("synthetic failure")));

            error.Status.Should().Be(ClassicAspxDiscoveryContract.WelcomePageError);
            error.IsAvailable.Should().BeFalse();
            ClassicAspxDiscoveryContract.Summarize(Array.Empty<ClassicPageDiscovery>(), error, false)
                .Should().Match<ClassicPageDiscoverySummary>(summary =>
                    summary.State == ClassicAspxDiscoveryContract.Partial &&
                    summary.GapCodes == ClassicAspxDiscoveryContract.WelcomePageErrorGap);
        }

        [Fact]
        public void Hidden_filter_is_bypassed_only_for_page_discovery_libraries()
        {
            ScannerBase.ShouldIncludeLoadedList(true, "/SitePages/Forms/AllPages.aspx", ListTemplateType.WebPageLibrary, true)
                .Should().BeTrue();
            ScannerBase.ShouldIncludeLoadedList(true, "/SitePages/Forms/AllPages.aspx", ListTemplateType.WebPageLibrary, false)
                .Should().BeFalse();
            ScannerBase.ShouldIncludeLoadedList(true, "/Lists/Hidden/AllItems.aspx", ListTemplateType.GenericList, true)
                .Should().BeFalse();
            ScannerBase.ShouldIncludeLoadedList(false, "/_catalogs/masterpage/Forms/AllItems.aspx", ListTemplateType.DocumentLibrary, true)
                .Should().BeFalse();
        }

        [Fact]
        public void Page_query_is_recursive_exact_aspx_and_paged()
        {
            string query = PageScanComponent.PageQuery(new List<string>());

            query.Should().Contain("Scope='RecursiveAll'");
            query.Should().Contain("<Eq>");
            query.Should().Contain("<Value Type='text'>aspx</Value>");
            query.Should().Contain("<RowLimit Paged='TRUE'>1000</RowLimit>");
            PageScanComponent.IsAspxPage("/Pages/UPPER.ASPX").Should().BeTrue();
            PageScanComponent.IsAspxPage("/Pages/not-a-page.aspx.bak").Should().BeFalse();
        }

        [Fact]
        public void Modern_negative_control_is_observed_but_not_classic_selected()
        {
            var resolution = ClassicAspxDiscoveryContract.FromValue("SitePages/Home.aspx");
            var selection = ClassicAspxDiscoveryContract.EvaluateSelection(
                "/SitePages/Modern.aspx", PageScanComponent.ModernPage, resolution, homePageOnly: false);

            selection.State.Should().Be(ClassicAspxDiscoveryContract.ExcludedModern);
        }

        private static List<ClassicPageDiscovery> Rows(
            bool hidden,
            bool homePageOnly,
            WelcomePageResolution welcomePage)
        {
            return new[] { "/sites/team/SitePages/Home.aspx", "/sites/team/SitePages/About.aspx" }
                .Select(pageUrl =>
                {
                    var selection = ClassicAspxDiscoveryContract.EvaluateSelection(
                        pageUrl, PageScanComponent.WikiPage, welcomePage, homePageOnly);
                    return new ClassicPageDiscovery
                    {
                        OutputVersion = ClassicAspxDiscoveryContract.OutputVersion,
                        PageUrl = pageUrl,
                        PageType = PageScanComponent.WikiPage,
                        LibraryHidden = hidden,
                        ObservationState = ClassicAspxDiscoveryContract.Observed,
                        SelectionState = selection.State,
                        HomePage = selection.IsHomePage,
                        WelcomePageStatus = welcomePage.Status,
                    };
                })
                .ToList();
        }
    }
}

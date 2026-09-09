using Microsoft.SharePoint.Client;
using PnP.Scanning.Core.Storage;

namespace PnP.Scanning.Core.Scanners
{
    /// <summary>
    /// Versioned bridge between lossless ASPX observation and the narrower Classic retirement
    /// classification. Observation and selection are deliberately separate: an authorized readable
    /// ASPX stays in the denominator even when a Classic-only policy does not select it.
    /// </summary>
    internal static class ClassicAspxDiscoveryContract
    {
        internal const string OutputVersion = "classic-aspx-discovery-output/v1";

        internal const string Observed = "observed";
        internal const string Selected = "selected";
        internal const string ExcludedHomePageOnly = "excluded_homepageonly";
        internal const string ExcludedModern = "excluded_modern";
        internal const string ExcludedHidden = "excluded_hidden";
        internal const string Denied = "denied";
        internal const string Error = "error";
        internal const string Unsupported = "unsupported";
        internal const string NotEvaluated = "not_evaluated";

        internal const string Complete = "complete";
        internal const string Partial = "partial";
        internal const string Failed = "failed";

        internal const string WelcomePageSuccess = "success";
        internal const string WelcomePageEmpty = "success_empty";
        internal const string WelcomePageDenied = Denied;
        internal const string WelcomePageError = Error;

        internal const string WelcomePageDeniedGap = "welcome_page_denied";
        internal const string WelcomePageErrorGap = "welcome_page_error";

        internal static ClassicPageSelection EvaluateSelection(
            string pageUrl,
            string pageType,
            WelcomePageResolution welcomePage,
            bool homePageOnly)
        {
            bool isModern = string.Equals(pageType, PageScanComponent.ModernPage, StringComparison.Ordinal);
            if (!welcomePage.IsAvailable)
            {
                return homePageOnly
                    ? new ClassicPageSelection(NotEvaluated, null)
                    : new ClassicPageSelection(isModern ? ExcludedModern : Selected, null);
            }

            bool isHomePage = HomePageDetector.IsHomePage(pageUrl, welcomePage.Value);
            if (isModern)
            {
                return new ClassicPageSelection(ExcludedModern, isHomePage);
            }

            if (homePageOnly && !isHomePage)
            {
                return new ClassicPageSelection(ExcludedHomePageOnly, false);
            }

            return new ClassicPageSelection(Selected, isHomePage);
        }

        internal static WelcomePageResolution FromValue(string value) => string.IsNullOrEmpty(value)
            ? new WelcomePageResolution(WelcomePageEmpty, string.Empty, null)
            : new WelcomePageResolution(WelcomePageSuccess, value, null);

        internal static WelcomePageResolution FromException(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            bool denied = IsAccessDenied(exception);
            var evidence = new List<string> { exception.GetType().Name };
            if (exception is ServerException serverException)
            {
                evidence.Add($"serverErrorCode={serverException.ServerErrorCode}");
                if (!string.IsNullOrWhiteSpace(serverException.ServerErrorTraceCorrelationId))
                {
                    evidence.Add($"correlation={serverException.ServerErrorTraceCorrelationId}");
                }
            }

            return new WelcomePageResolution(
                denied ? WelcomePageDenied : WelcomePageError,
                null,
                string.Join(";", evidence));
        }

        internal static ClassicPageDiscoverySummary Summarize(
            IReadOnlyCollection<ClassicPageDiscovery> observations,
            WelcomePageResolution welcomePage,
            bool homePageOnly)
        {
            observations ??= Array.Empty<ClassicPageDiscovery>();
            bool unavailable = !welcomePage.IsAvailable;
            string state = unavailable ? (homePageOnly ? Failed : Partial) : Complete;
            string gapCode = welcomePage.Status switch
            {
                WelcomePageDenied => WelcomePageDeniedGap,
                WelcomePageError => WelcomePageErrorGap,
                _ => null,
            };

            return new ClassicPageDiscoverySummary(
                OutputVersion,
                state,
                gapCode,
                welcomePage.Status,
                welcomePage.Evidence,
                homePageOnly,
                observations.Count,
                observations.Count(row => row.SelectionState == Selected),
                observations.Count(row => row.SelectionState is ExcludedHomePageOnly or ExcludedModern or ExcludedHidden),
                observations.Count(row => row.SelectionState == NotEvaluated),
                observations.Count(row => row.LibraryHidden));
        }

        private static bool IsAccessDenied(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
            {
                if (current is UnauthorizedAccessException or ServerUnauthorizedAccessException)
                {
                    return true;
                }

                if (current is ServerException serverException && serverException.ServerErrorCode == -2147024891)
                {
                    return true;
                }

                if (current is HttpRequestException httpException &&
                    httpException.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                {
                    return true;
                }

                if (current.Message.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                    current.Message.Contains("403", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed record WelcomePageResolution(string Status, string Value, string Evidence)
    {
        internal bool IsAvailable => Status is ClassicAspxDiscoveryContract.WelcomePageSuccess or
            ClassicAspxDiscoveryContract.WelcomePageEmpty;
    }

    internal sealed record ClassicPageSelection(string State, bool? IsHomePage);

    internal sealed record ClassicPageDiscoverySummary(
        string OutputVersion,
        string State,
        string GapCodes,
        string WelcomePageStatus,
        string WelcomePageEvidence,
        bool HomePageOnly,
        int AllAspxObserved,
        int ClassicSelected,
        int ClassicExcluded,
        int SelectionNotEvaluated,
        int HiddenAspxObserved);

    internal sealed class WelcomePageUnavailableException : InvalidOperationException
    {
        internal WelcomePageUnavailableException(WelcomePageResolution resolution)
            : base($"WelcomePage evaluation is {resolution.Status}; --homepageonly cannot produce a complete selection.")
        {
        }
    }
}

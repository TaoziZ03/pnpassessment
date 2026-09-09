# classicwebsummaries.csv file details

## Summary

This csv file contains one row per assessed web with roll-up information. It is shared by all classic assessment components; this page documents the columns relevant to the classic **pages** assessment (page counts and page-modernization-readiness roll-ups). The page-readiness columns are computed only over pages that actually carry web parts.

## Columns

The following page-relevant columns are included (the file also contains roll-up columns for the other classic components such as lists, workflows and add-ins):

Column|Description
------|-----------
Template | The web template
ClassicPages | Total number of classic pages found in this web
ClassicWikiPages | Number of classic wiki pages
ClassicASPXPages | Number of classic ASPX pages
ClassicBlogPages | Number of classic blog pages
ClassicWebPartPages | Number of classic web part pages
ClassicPublishingPages | Number of classic publishing pages
ModernPages | Number of modern pages found in this web
PageDiscoveryOutputVersion | Version of the Classic ASPX observation/selection projection
PageDiscoveryState | `complete`, `partial`, or `failed`
PageDiscoveryGapCodes | Stable gap code such as `welcome_page_denied` or `welcome_page_error`
WelcomePageStatus | `success`, `success_empty`, `denied`, or `error`
WelcomePageEvidence | Non-secret exception type/server error/correlation evidence for denied or error results
HomePageOnly | True when `--homepageonly` was requested
AllAspxObserved | Number of ASPX rows observed before Classic selection
ClassicSelected | Number of observed rows selected into Classic processing
ClassicExcluded | Number of observed rows explicitly excluded by Classic selection policy
PageSelectionNotEvaluated | Number of rows whose home-page selection could not be evaluated
HiddenAspxObserved | Number of observed ASPX rows from hidden page libraries
PagesWithWebParts | Number of classic pages that carry at least one web part
MappableWebPartPages | Number of pages (with web parts) whose web parts are fully mappable (mapping percentage of 100)
UnmappedWebPartPages | Number of pages (with web parts) that have at least one unmapped web part (mapping percentage below 100)
AvgMappingPercentage | The average mapping percentage across the pages that carry web parts
UncustomizedHomePages | Number of uncustomized (default) home pages found in this web
AggregatedRemediationCodes | The aggregated remediation codes for this web
ScanId | Id of the assessment
SiteUrl | Fully qualified site collection URL
WebUrl | Relative URL of this web

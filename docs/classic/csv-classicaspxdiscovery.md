# classicaspxdiscovery.csv file details

## Summary

This CSV is the versioned ASPX observation denominator for the Classic pages assessment. It contains
one row for every readable `.aspx` observed in a supported page library before the Classic-only and
`--homepageonly` selection rules run. A hidden library is recorded as provenance and is not silently
removed.

`classicpages.csv` remains the selected Classic retirement-classification output. Compare the two files
when you need to distinguish observed, selected, explicitly excluded, or not-evaluated pages.

## Columns

Column|Description
------|-----------
OutputVersion | Discovery projection contract version. The initial version is `classic-aspx-discovery-output/v1`
PageUrl | Server-relative physical page locator
PageName | Page display name, or the file leaf when no title is present
PageType | Classification result, including Classic types and the Modern negative control
ListUrl | Server-relative URL of the source page library
ListTitle | Source page library title
ListId | Stable source page library ID
LibraryHidden | Source library `Hidden` state; hidden readable page libraries remain in this output
ObservationState | `observed` for a readable ASPX row
SelectionState | `selected`, `excluded_homepageonly`, `excluded_modern`, or `not_evaluated`
HomePage | True/false when WelcomePage was available; empty when home-page evaluation was unavailable
WelcomePageStatus | `success`, `success_empty`, `denied`, or `error`
ScanId | Assessment ID
SiteUrl | Fully qualified site collection URL
WebUrl | Relative URL of the web

When `WelcomePageStatus` is `denied` or `error`, normal page discovery continues with explicit partial
state and `HomePage` remains unknown. With `--homepageonly`, selection is not evaluated and the web fails
after evidence is persisted; the assessment does not report a successful zero-row result.

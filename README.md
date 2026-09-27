# Financial Tracker

## Tagged releases

Pushing a tag in the form `vMAJOR.MINOR.PATCH` starts the release workflow for
iOS, Android, and Windows. All three artifacts receive the version from the tag
and the same GitHub Actions run number as their build number.

For example, `v3.2.22` produces apps that display `3.2.22 (<run number>)`.
Development builds that are not produced by this workflow use the fallback
version declared in `FinancialTracker.csproj`.

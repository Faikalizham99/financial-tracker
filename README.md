# Financial Tracker

## Tagged releases

Pushing a tag in the form `vMAJOR.MINOR.PATCH` starts the release workflow for
iOS, Android, and Windows. All three artifacts receive the version from the tag
and the same GitHub Actions run number as their build number.

For example, `v3.2.22` produces apps that display `3.2.22 (<run number>)`.
Development builds that are not produced by this workflow use the fallback
version declared in `FinancialTracker.csproj`.

## iOS notification transaction inbox

On iOS 27, Financial Tracker exposes a **Capture Pending Transaction** action
to Shortcuts. Use it after a Notification automation trigger and map any
available Source App, Title, Subtitle, Message, and Received At values into the
action. Amount and Description are optional overrides when a Shortcut extracts
them explicitly.

The action never writes to the private Financial Tracker database. It stores a
deduplicated capture in `Library/FinancialTracker/pending-transactions.db3`
inside the shared App Group. When Financial Tracker opens or resumes, the
Transactions page shows a review banner. A capture can be approved, edited in
the normal transaction form, or rejected. Only approval writes a final
transaction.

The app, WidgetKit extension, and App Intents extension must all be signed with
the same authorized App Group. A signing service must preserve and re-sign both
embedded extensions for capture and widgets to work.

# Assets Feature Plan

Status: Planning only. No Assets feature implementation has started.

## Purpose

Add an Assets page that records one portfolio snapshot per calendar month. Each
snapshot has a user-selected entry date and one value for every configured
asset. Opening a month that already has a snapshot edits the existing snapshot
instead of creating a duplicate.

Asset snapshots are independent from income and expense transactions and must
not affect transaction totals.

## Asset catalog

The asset list and icon files will be supplied by the user and configured in
code, following the existing category and payment-method catalog pattern. The
app does not need an asset-management screen.

Create a shared `AssetCatalog` whose entries contain:

- A stable internal key
- Display name
- Icon asset
- Asset type, such as Bank, Investment, Retirement, E-wallet, or Other
- Display order
- Whether the asset is included in the Accessible Assets total
- Whether the asset is active

Stable keys preserve history when an asset is renamed. An asset that is no
longer used should be marked inactive rather than deleted so its historical
values remain readable.

When a new catalog asset is introduced, it appears in future entry forms. If it
has no value in the comparison snapshot, the UI displays `New` instead of
calculating a misleading increase from zero.

## Navigation and page structure

Add Assets as a fourth primary navigation destination:

`Home - Transaction - Assets - Settings`

The Assets page contains:

1. A month selector with previous and next controls.
2. An Add Snapshot action when the selected month has no entry, or Edit
   Snapshot when it does.
3. A portfolio overview showing:
   - Total assets
   - Total change in currency and percentage
   - Previous and current snapshot dates
   - Accessible assets and their change
4. An asset comparison list showing:
   - Asset name and icon
   - Previous value
   - Current value
   - Increase or decrease in currency
   - Percentage change where a valid previous value exists
5. A compact six- or twelve-month portfolio trend.
6. Useful insights such as the largest holding, biggest increase, and biggest
   decrease.

Desktop can use a four-column comparison table. Phone layouts should use
compact responsive rows or cards without horizontal scrolling.

Use teal/positive styling for increases, pink/red/negative styling for
decreases, and muted styling for unchanged values. Follow the app's existing
light/dark themes, accent resources, card styling, animation patterns, safe
areas, and loading skeleton behavior.

## Monthly snapshot editor

The snapshot editor should:

- Restrict the entry date to a date inside the selected calendar month.
- Default sensibly to today for the current month.
- Render every active asset from `AssetCatalog` in catalog order.
- Provide a numeric amount field for every asset.
- Treat a blank value as incomplete and `0.00` as a valid value.
- Update the portfolio total while the user enters values.
- Offer a Copy Previous Snapshot action to prefill existing values.
- Require all active assets to have a value before saving.
- Load the saved date and values when editing an existing month.
- Save the complete snapshot atomically.

Changing the date within the same month edits the same snapshot. The editor
must not silently move a snapshot into another calendar month.

## Database design

Add two SQLite tables through a safe schema migration.

### AssetSnapshots

- `Id`
- Calendar month key
- Entry date
- Currency code
- Created timestamp
- Updated timestamp

The calendar month key must have a unique database constraint so duplicate
monthly snapshots are impossible even if Save is triggered more than once.

### AssetSnapshotValues

- `Id`
- Snapshot ID
- Asset key
- Amount in minor currency units

Add a unique constraint on Snapshot ID plus Asset Key. Store monetary values as
integer minor units, consistent with transactions. Save the snapshot and all
of its values in one SQLite transaction to prevent partial data.

Deleting or replacing configured catalog entries must never cascade-delete
historical snapshot values.

## Currency rules

Save the selected currency code with every snapshot. Display historical values
using the snapshot's stored currency rather than relabeling them when the app's
current currency changes.

Only calculate comparisons between compatible currencies. Automatic foreign
exchange conversion is outside the first release.

## Comparison and insight rules

- Compare the selected snapshot with the latest earlier recorded snapshot.
- Always show the two actual entry dates being compared.
- If there is no earlier snapshot, present the selected snapshot as a baseline
  and omit change percentages.
- If an asset has no earlier value, display `New`.
- If an asset existed earlier but is inactive now, preserve it in historical
  views and handle its removal explicitly rather than hiding its effect.
- Total change is current total minus previous total.
- Percentage change is total change divided by the previous total when the
  previous total is greater than zero.
- Editing or inserting an older snapshot automatically changes calculations
  for later snapshots; calculated values should not be permanently stored.

## Suggested code organization

- `Models/AssetCatalogItem.cs`
- `Models/AssetSnapshotRecord.cs`
- `Models/AssetSnapshotValueRecord.cs`
- `Models/AssetComparisonItem.cs`
- `Services/AssetCatalog.cs`
- `Services/AssetPortfolioService.cs`
- `Views/AssetsView.xaml`
- `Views/AssetsView.xaml.cs`
- `Views/AssetSnapshotEditorView.xaml`
- `Views/AssetSnapshotEditorView.xaml.cs`

Keep asset loading and calculation logic outside `MainPage` where practical.
`MainPage` should host navigation and coordinate refresh events, while the
Assets view and portfolio service own asset-specific behavior.

Reuse the shared calendar date picker and money-formatting helpers. Asset data
should have its own cache and loading flow so transaction loading does not
delay Assets navigation.

## Implementation order

1. Receive the asset names, icon files, types, display order, and Accessible
   Assets inclusion rules.
2. Add the asset catalog and icon resources.
3. Add the database migration, records, constraints, and atomic repository
   operations.
4. Add portfolio calculation and comparison services.
5. Build the monthly snapshot add/edit experience.
6. Build the responsive Assets overview, comparison list, trend, and empty
   state.
7. Add Assets to navigation and add an Assets loading skeleton.
8. Validate Windows, iOS, and Android behavior in light and dark themes.

## Validation checklist

- Only one snapshot can exist per calendar month.
- Repeated Save actions cannot create duplicates.
- Editing preserves the snapshot identity and updates all values atomically.
- Entry dates cannot leave the selected month.
- Blank and zero values behave differently.
- Copy Previous Snapshot handles new and inactive assets correctly.
- Missing months compare against the latest earlier snapshot and label it
  clearly.
- Added assets show `New` rather than a false gain from zero.
- Historical records survive catalog renames and inactive assets.
- Currency symbols come from the stored snapshot currency.
- Totals, changes, percentages, and accessible totals are correct.
- Phone layouts do not require horizontal scrolling.
- Keyboard, safe-area, scrolling, theme switching, and animations work on iOS
  and Android as well as Windows.

## Deferred ideas

- Liabilities and net worth
- Multiple currencies with exchange-rate conversion
- Investment cost basis and returns
- Property valuation history
- Financial goals and targets
- Exporting portfolio reports

import AppIntents
import SwiftUI
import WidgetKit

private enum AssetWidgetSettings {
    static let appGroupIdentifiers = [
        "group.com.faikalizham.financial-tracker",
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5"
    ]
    static let snapshotFileName = "financial_tracker_asset_widget.json"
    static let kwspPreferenceFileName =
        "financial_tracker_asset_widget_include_kwsp.txt"
    static let selectedMonthFileName =
        "financial_tracker_asset_widget_selected_month.txt"
    static let amountsHiddenKey =
        "financial_tracker_asset_widget_amounts_hidden"
    static let widgetKind = "FinancialTrackerAssetsWidget"
    static let assetsUrl = URL(
        string: "com.faikalizham.financial-tracker://assets"
    )!
}

private enum AssetWidgetAppearance {
    static let background = Color(red: 0.985, green: 0.972, blue: 0.948)
    static let accent = Color(red: 0.392, green: 0.847, blue: 0.894)
    static let ink = Color(red: 0.095, green: 0.082, blue: 0.180)
    static let secondaryInk = Color(red: 0.390, green: 0.370, blue: 0.440)
    static let divider = Color(red: 0.885, green: 0.860, blue: 0.825)
    static let positive = Color(red: 0.075, green: 0.550, blue: 0.400)
    static let negative = Color(red: 0.820, green: 0.230, blue: 0.340)

    static func changeColor(_ direction: Int) -> Color {
        if direction > 0 { return positive }
        if direction < 0 { return negative }
        return secondaryInk
    }

    static func assetColor(_ key: String) -> Color {
        switch key {
        case "ambank": return Color(red: 0.93, green: 0.11, blue: 0.14)
        case "asb": return Color(red: 0.18, green: 0.31, blue: 0.62)
        case "bank_islam": return Color(red: 0.83, green: 0.08, blue: 0.35)
        case "cash": return Color(red: 0.55, green: 0.42, blue: 0.24)
        case "cimb": return Color(red: 0.47, green: 0.00, blue: 0.11)
        case "gxbank": return Color(red: 0.48, green: 0.17, blue: 0.75)
        case "kwsp": return Color(red: 0.65, green: 0.48, blue: 0.00)
        case "luno": return Color(red: 0.06, green: 0.16, blue: 0.34)
        case "maybank": return Color(red: 0.96, green: 0.76, blue: 0.00)
        case "moomoo": return Color(red: 1.00, green: 0.42, blue: 0.00)
        case "ryt_bank": return Color(red: 0.32, green: 0.40, blue: 0.91)
        case "standard_chartered": return Color(red: 0.18, green: 0.68, blue: 0.00)
        case "touch_n_go_ewallet": return Color(red: 0.00, green: 0.45, blue: 0.81)
        case "versa": return Color(red: 0.08, green: 0.60, blue: 0.61)
        case "wahed": return Color(red: 0.89, green: 0.70, blue: 0.25)
        default: return secondaryInk
        }
    }
}

private struct AssetWidgetSummary: Codable {
    let totalText: String
    let changeText: String
    let accessibleText: String
    let accessibleChangeText: String
    let changeDirection: Int
    let accessibleChangeDirection: Int
}

private struct AssetWidgetItem: Codable, Identifiable {
    let key: String
    let name: String
    let previousText: String
    let currentText: String
    let changeText: String
    let changeDirection: Int
    let isKwsp: Bool

    var id: String { key }
}

private struct AssetWidgetMonth: Codable, Identifiable {
    let monthKey: Int
    let monthText: String
    let hasSnapshot: Bool
    let comparisonDateText: String
    let withKwsp: AssetWidgetSummary
    let withoutKwsp: AssetWidgetSummary
    let assets: [AssetWidgetItem]

    var id: Int { monthKey }

    func summary(includeKwsp: Bool) -> AssetWidgetSummary {
        includeKwsp ? withKwsp : withoutKwsp
    }
}

private struct AssetWidgetSnapshot: Codable {
    let version: Int
    let currentMonthKey: Int
    let months: [AssetWidgetMonth]
    let updatedAtUnixSeconds: Int64

    static var placeholder: AssetWidgetSnapshot {
        let calendar = Calendar.current
        let components = calendar.dateComponents([.year, .month], from: Date())
        let key = (components.year ?? 2026) * 100 + (components.month ?? 1)
        let month = AssetWidgetMonth(
            monthKey: key,
            monthText: Date().formatted(.dateTime.month(.wide).year()),
            hasSnapshot: false,
            comparisonDateText: "Open the app to publish assets",
            withKwsp: .empty,
            withoutKwsp: .empty,
            assets: []
        )
        return AssetWidgetSnapshot(
            version: 1,
            currentMonthKey: key,
            months: [month],
            updatedAtUnixSeconds: Int64(Date().timeIntervalSince1970)
        )
    }
}

private extension AssetWidgetSummary {
    static var empty: AssetWidgetSummary {
        AssetWidgetSummary(
            totalText: "RM 0.00",
            changeText: "—",
            accessibleText: "RM 0.00",
            accessibleChangeText: "—",
            changeDirection: 0,
            accessibleChangeDirection: 0
        )
    }
}

private enum AssetWidgetLoadState {
    case loaded
    case appGroupUnavailable
    case snapshotMissing
    case snapshotUnreadable
    case invalidSnapshot

    var message: String {
        switch self {
        case .loaded: return ""
        case .appGroupUnavailable: return "Sync unavailable"
        case .snapshotMissing: return "Open Financial Tracker to sync assets"
        case .snapshotUnreadable: return "Asset snapshot could not be read"
        case .invalidSnapshot: return "Update Financial Tracker"
        }
    }
}

private struct AssetWidgetLoadResult {
    let snapshot: AssetWidgetSnapshot
    let state: AssetWidgetLoadState
}

private enum AssetWidgetStorage {
    static func loadSnapshot() -> AssetWidgetLoadResult {
        guard let containerUrl = availableContainer()?.url else {
            return failure(.appGroupUnavailable)
        }
        let url = containerUrl.appendingPathComponent(
            AssetWidgetSettings.snapshotFileName,
            isDirectory: false
        )
        guard FileManager.default.fileExists(atPath: url.path) else {
            return failure(.snapshotMissing)
        }
        guard let data = try? Data(contentsOf: url) else {
            return failure(.snapshotUnreadable)
        }
        guard let snapshot = try? JSONDecoder().decode(
            AssetWidgetSnapshot.self,
            from: data
        ), snapshot.version == 1 else {
            return failure(.invalidSnapshot)
        }
        return AssetWidgetLoadResult(snapshot: snapshot, state: .loaded)
    }

    static func loadIncludeKwsp() -> Bool {
        guard let url = availableContainer()?.url else { return true }
        let preferenceUrl = url.appendingPathComponent(
            AssetWidgetSettings.kwspPreferenceFileName,
            isDirectory: false
        )
        guard let text = try? String(contentsOf: preferenceUrl, encoding: .utf8)
        else { return true }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased() != "false"
    }

    @discardableResult
    static func writeIncludeKwsp(_ value: Bool) -> Bool {
        guard let url = availableContainer()?.url else { return false }
        do {
            try (value ? "true" : "false").write(
                to: url.appendingPathComponent(
                    AssetWidgetSettings.kwspPreferenceFileName,
                    isDirectory: false
                ),
                atomically: true,
                encoding: .utf8
            )
            return true
        } catch {
            return false
        }
    }

    static func loadSelectedMonthKey(fallback: Int) -> Int {
        guard let url = availableContainer()?.url,
              let text = try? String(
                contentsOf: url.appendingPathComponent(
                    AssetWidgetSettings.selectedMonthFileName,
                    isDirectory: false
                ),
                encoding: .utf8
              ),
              let value = Int(text.trimmingCharacters(in: .whitespacesAndNewlines))
        else { return fallback }
        return value
    }

    @discardableResult
    static func writeSelectedMonthKey(_ value: Int) -> Bool {
        guard let url = availableContainer()?.url else { return false }
        do {
            try String(value).write(
                to: url.appendingPathComponent(
                    AssetWidgetSettings.selectedMonthFileName,
                    isDirectory: false
                ),
                atomically: true,
                encoding: .utf8
            )
            return true
        } catch {
            return false
        }
    }

    static func loadAmountsHidden() -> Bool {
        guard let container = availableContainer(),
              let defaults = UserDefaults(suiteName: container.identifier)
        else { return false }
        return defaults.bool(forKey: AssetWidgetSettings.amountsHiddenKey)
    }

    @discardableResult
    static func toggleAmountsHidden() -> Bool {
        guard let container = availableContainer(),
              let defaults = UserDefaults(suiteName: container.identifier)
        else { return false }
        let value = !defaults.bool(forKey: AssetWidgetSettings.amountsHiddenKey)
        defaults.set(value, forKey: AssetWidgetSettings.amountsHiddenKey)
        return value
    }

    private static func availableContainer() -> (identifier: String, url: URL)? {
        for identifier in AssetWidgetSettings.appGroupIdentifiers {
            if let url = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) {
                return (identifier, url)
            }
        }
        return nil
    }

    private static func failure(_ state: AssetWidgetLoadState) -> AssetWidgetLoadResult {
        AssetWidgetLoadResult(snapshot: .placeholder, state: state)
    }
}

@available(iOS 17.0, *)
struct RefreshAssetsWidgetIntent: AppIntent {
    static var title: LocalizedStringResource = "Refresh Assets Widget"
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        WidgetCenter.shared.reloadTimelines(ofKind: AssetWidgetSettings.widgetKind)
        return .result()
    }
}

@available(iOS 17.0, *)
struct ToggleAssetKwspIntent: AppIntent {
    static var title: LocalizedStringResource = "Include or Exclude KWSP"
    static var openAppWhenRun = false

    @Parameter(title: "Currently Includes KWSP")
    var currentValue: Bool

    init() { currentValue = true }
    init(currentValue: Bool) { self.currentValue = currentValue }

    func perform() async throws -> some IntentResult {
        AssetWidgetStorage.writeIncludeKwsp(!currentValue)
        WidgetCenter.shared.reloadTimelines(ofKind: AssetWidgetSettings.widgetKind)
        return .result()
    }
}

@available(iOS 17.0, *)
struct ToggleAssetPrivacyIntent: AppIntent {
    static var title: LocalizedStringResource = "Hide or Show Asset Values"
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        AssetWidgetStorage.toggleAmountsHidden()
        WidgetCenter.shared.reloadTimelines(ofKind: AssetWidgetSettings.widgetKind)
        return .result()
    }
}

@available(iOS 17.0, *)
struct SelectAssetWidgetMonthIntent: AppIntent {
    static var title: LocalizedStringResource = "Select Asset Month"
    static var openAppWhenRun = false

    @Parameter(title: "Month Key")
    var monthKey: Int

    init() { monthKey = 0 }
    init(monthKey: Int) { self.monthKey = monthKey }

    func perform() async throws -> some IntentResult {
        AssetWidgetStorage.writeSelectedMonthKey(monthKey)
        WidgetCenter.shared.reloadTimelines(ofKind: AssetWidgetSettings.widgetKind)
        return .result()
    }
}

private struct FinancialTrackerAssetsEntry: TimelineEntry {
    let date: Date
    let snapshot: AssetWidgetSnapshot
    let selectedMonthKey: Int
    let includeKwsp: Bool
    let amountsHidden: Bool
    let loadState: AssetWidgetLoadState

    var selectedMonth: AssetWidgetMonth {
        snapshot.months.first(where: { $0.monthKey == selectedMonthKey })
            ?? snapshot.months.first(where: { $0.monthKey == snapshot.currentMonthKey })
            ?? snapshot.months.last
            ?? AssetWidgetSnapshot.placeholder.months[0]
    }

    var selectedIndex: Int {
        snapshot.months.firstIndex(where: { $0.monthKey == selectedMonth.monthKey }) ?? 0
    }

    var previousMonthKey: Int? {
        let index = selectedIndex - 1
        return index >= 0 ? snapshot.months[index].monthKey : nil
    }

    var nextMonthKey: Int? {
        let index = selectedIndex + 1
        return index < snapshot.months.count ? snapshot.months[index].monthKey : nil
    }
}

private struct FinancialTrackerAssetsProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinancialTrackerAssetsEntry {
        makeEntry(loadResult: AssetWidgetLoadResult(
            snapshot: .placeholder,
            state: .loaded
        ))
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialTrackerAssetsEntry) -> Void
    ) {
        completion(context.isPreview
            ? placeholder(in: context)
            : makeEntry(loadResult: AssetWidgetStorage.loadSnapshot()))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialTrackerAssetsEntry>) -> Void
    ) {
        let entry = makeEntry(loadResult: AssetWidgetStorage.loadSnapshot())
        completion(Timeline(
            entries: [entry],
            policy: .after(Date().addingTimeInterval(30 * 60))
        ))
    }

    private func makeEntry(
        loadResult: AssetWidgetLoadResult
    ) -> FinancialTrackerAssetsEntry {
        let snapshot = loadResult.snapshot
        return FinancialTrackerAssetsEntry(
            date: Date(),
            snapshot: snapshot,
            selectedMonthKey: AssetWidgetStorage.loadSelectedMonthKey(
                fallback: snapshot.currentMonthKey
            ),
            includeKwsp: AssetWidgetStorage.loadIncludeKwsp(),
            amountsHidden: AssetWidgetStorage.loadAmountsHidden(),
            loadState: loadResult.state
        )
    }
}

private struct AssetWidgetBrandMark: View {
    var body: some View {
        Image(systemName: "building.columns.fill")
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(AssetWidgetAppearance.ink)
            .frame(width: 30, height: 30)
            .background(
                RoundedRectangle(cornerRadius: 9, style: .continuous)
                    .fill(AssetWidgetAppearance.accent.opacity(0.18))
            )
    }
}

private struct FinancialTrackerAssetsView: View {
    let entry: FinancialTrackerAssetsEntry

    private var month: AssetWidgetMonth { entry.selectedMonth }
    private var summary: AssetWidgetSummary {
        month.summary(includeKwsp: entry.includeKwsp)
    }
    private var rows: [AssetWidgetItem] {
        entry.includeKwsp ? month.assets : month.assets.filter { !$0.isKwsp }
    }

    var body: some View {
        if #available(iOS 17.0, *) {
            content
                .widgetURL(AssetWidgetSettings.assetsUrl)
                .containerBackground(AssetWidgetAppearance.background, for: .widget)
        } else {
            content
                .padding()
                .background(AssetWidgetAppearance.background)
                .widgetURL(AssetWidgetSettings.assetsUrl)
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 6) {
            header
            monthSelector

            if month.hasSnapshot {
                summaryRow
                Divider().overlay(AssetWidgetAppearance.divider)
                tableHeader
                assetRows
            } else {
                noSnapshot
            }

            Spacer(minLength: 0)
            Text(entry.loadState == .loaded
                ? "Refreshed \(entry.date.formatted(date: .omitted, time: .shortened))"
                : entry.loadState.message)
                .font(.system(size: 8))
                .foregroundStyle(AssetWidgetAppearance.secondaryInk)
                .frame(maxWidth: .infinity, alignment: .trailing)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var header: some View {
        HStack(spacing: 8) {
            AssetWidgetBrandMark()
            VStack(alignment: .leading, spacing: 1) {
                Text("Financial Tracker Assets")
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .foregroundStyle(AssetWidgetAppearance.ink)
                    .lineLimit(1)
                    .minimumScaleFactor(0.76)
                Text(entry.includeKwsp ? "With KWSP" : "Without KWSP")
                    .font(.caption2)
                    .foregroundStyle(AssetWidgetAppearance.secondaryInk)
            }
            Spacer(minLength: 2)
            kwspButton
            addSnapshotLink
            privacyButton
            refreshButton
        }
    }

    @ViewBuilder
    private var kwspButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: ToggleAssetKwspIntent(currentValue: entry.includeKwsp)) {
                Image(systemName: "person.crop.circle.badge.checkmark")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(entry.includeKwsp
                        ? Color.white
                        : AssetWidgetAppearance.secondaryInk)
                    .frame(width: 34, height: 27)
                    .background(Capsule().fill(entry.includeKwsp
                        ? AssetWidgetAppearance.accent
                        : AssetWidgetAppearance.secondaryInk.opacity(0.10)))
            }
            .buttonStyle(.plain)
            .accessibilityLabel(entry.includeKwsp ? "Exclude KWSP" : "Include KWSP")
        }
    }

    private var addSnapshotLink: some View {
        Link(destination: URL(
            string: "com.faikalizham.financial-tracker://add-asset-snapshot?month=\(month.monthKey)"
        )!) {
            Image(systemName: "plus")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(AssetWidgetAppearance.ink)
                .frame(width: 27, height: 27)
                .background(Circle().fill(AssetWidgetAppearance.accent.opacity(0.16)))
        }
        .accessibilityLabel("Add or edit asset snapshot")
    }

    @ViewBuilder
    private var privacyButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: ToggleAssetPrivacyIntent()) {
                Image(systemName: entry.amountsHidden ? "eye.slash" : "eye")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(AssetWidgetAppearance.secondaryInk)
                    .frame(width: 27, height: 27)
                    .background(Circle().fill(
                        AssetWidgetAppearance.secondaryInk.opacity(0.10)
                    ))
            }
            .buttonStyle(.plain)
        }
    }

    @ViewBuilder
    private var refreshButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: RefreshAssetsWidgetIntent()) {
                Image(systemName: "arrow.clockwise")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(AssetWidgetAppearance.accent)
                    .frame(width: 27, height: 27)
                    .background(Circle().fill(AssetWidgetAppearance.accent.opacity(0.16)))
            }
            .buttonStyle(.plain)
        }
    }

    private var monthSelector: some View {
        HStack(spacing: 4) {
            monthButton(entry.previousMonthKey, systemName: "chevron.left")
            Image(systemName: "calendar")
            Text(month.monthText)
                .lineLimit(1)
                .minimumScaleFactor(0.75)
            monthButton(entry.nextMonthKey, systemName: "chevron.right")
            Spacer(minLength: 4)
            Text(month.comparisonDateText)
                .font(.system(size: 8))
                .foregroundStyle(AssetWidgetAppearance.secondaryInk)
                .lineLimit(1)
                .minimumScaleFactor(0.72)
        }
        .font(.system(size: 11, weight: .semibold, design: .rounded))
        .foregroundStyle(AssetWidgetAppearance.secondaryInk)
    }

    @ViewBuilder
    private func monthButton(_ key: Int?, systemName: String) -> some View {
        if let key = key {
            if #available(iOS 17.0, *) {
                Button(intent: SelectAssetWidgetMonthIntent(monthKey: key)) {
                    Image(systemName: systemName)
                        .font(.system(size: 9, weight: .bold))
                        .frame(width: 20, height: 20)
                        .background(Circle().fill(
                            AssetWidgetAppearance.secondaryInk.opacity(0.08)
                        ))
                }
                .buttonStyle(.plain)
            } else {
                disabledMonthButton(systemName)
            }
        } else {
            disabledMonthButton(systemName)
        }
    }

    private func disabledMonthButton(_ systemName: String) -> some View {
        Image(systemName: systemName)
            .font(.system(size: 9, weight: .bold))
            .foregroundStyle(AssetWidgetAppearance.secondaryInk.opacity(0.24))
            .frame(width: 20, height: 20)
    }

    private var summaryRow: some View {
        HStack(spacing: 18) {
            summaryMetric(
                title: "TOTAL ASSETS",
                amount: summary.totalText,
                change: summary.changeText,
                direction: summary.changeDirection
            )
            Rectangle()
                .fill(AssetWidgetAppearance.divider)
                .frame(width: 1, height: 38)
            summaryMetric(
                title: "ACCESSIBLE ASSETS",
                amount: summary.accessibleText,
                change: summary.accessibleChangeText,
                direction: summary.accessibleChangeDirection
            )
        }
    }

    private func summaryMetric(
        title: String,
        amount: String,
        change: String,
        direction: Int
    ) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(title)
                .font(.system(size: 8, weight: .semibold))
                .tracking(0.55)
                .foregroundStyle(AssetWidgetAppearance.secondaryInk)
            Text(displayed(amount))
                .font(.system(size: 17, weight: .bold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(AssetWidgetAppearance.ink)
                .lineLimit(1)
                .minimumScaleFactor(0.62)
            Text(displayed(change))
                .font(.system(size: 8.5, weight: .semibold))
                .monospacedDigit()
                .foregroundStyle(AssetWidgetAppearance.changeColor(direction))
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var tableHeader: some View {
        HStack(spacing: 3) {
            tableHeaderText("ASSET")
                .frame(maxWidth: .infinity, alignment: .leading)
            tableHeaderText("PREVIOUS")
                .frame(width: 68, alignment: .trailing)
            tableHeaderText("CURRENT")
                .frame(width: 68, alignment: .trailing)
            tableHeaderText("CHANGE")
                .frame(width: 70, alignment: .trailing)
        }
        .foregroundStyle(AssetWidgetAppearance.secondaryInk)
    }

    private func tableHeaderText(_ title: String) -> Text {
        Text(title)
            .font(.system(size: 7.5, weight: .semibold))
            .tracking(0.35)
    }

    private var assetRows: some View {
        VStack(spacing: 1) {
            ForEach(rows) { item in
                HStack(spacing: 3) {
                    HStack(spacing: 4) {
                        Circle()
                            .fill(AssetWidgetAppearance.assetColor(item.key))
                            .frame(width: 4, height: 4)
                        Text(item.name)
                            .font(.system(size: 8.5, weight: .semibold))
                            .foregroundStyle(AssetWidgetAppearance.ink)
                            .lineLimit(1)
                            .minimumScaleFactor(0.65)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)

                    valueText(item.previousText).frame(width: 68, alignment: .trailing)
                    valueText(item.currentText).frame(width: 68, alignment: .trailing)
                    valueText(item.changeText)
                        .foregroundStyle(AssetWidgetAppearance.changeColor(
                            item.changeDirection
                        ))
                        .frame(width: 70, alignment: .trailing)
                }
                .frame(height: 12)
            }
        }
    }

    private func valueText(_ text: String) -> some View {
        Text(displayed(text))
            .font(.system(size: 8, weight: .medium))
            .monospacedDigit()
            .foregroundStyle(AssetWidgetAppearance.secondaryInk)
            .lineLimit(1)
            .minimumScaleFactor(0.58)
    }

    private var noSnapshot: some View {
        VStack(spacing: 8) {
            Spacer(minLength: 16)
            Image(systemName: "building.columns")
                .font(.system(size: 28, weight: .light))
                .foregroundStyle(AssetWidgetAppearance.accent)
            Text("No snapshot for \(month.monthText)")
                .font(.system(.headline, design: .rounded).weight(.semibold))
                .foregroundStyle(AssetWidgetAppearance.ink)
            Text("Tap + to add this month’s asset values.")
                .font(.caption)
                .foregroundStyle(AssetWidgetAppearance.secondaryInk)
        }
        .frame(maxWidth: .infinity)
    }

    private func displayed(_ value: String) -> String {
        entry.amountsHidden && value != "—" ? "••••" : value
    }
}

struct FinancialTrackerAssetsWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(
            kind: AssetWidgetSettings.widgetKind,
            provider: FinancialTrackerAssetsProvider()
        ) { entry in
            FinancialTrackerAssetsView(entry: entry)
        }
        .configurationDisplayName("Financial Tracker Assets")
        .description("Compare every asset with your previous monthly snapshot.")
        .supportedFamilies([.systemLarge])
    }
}

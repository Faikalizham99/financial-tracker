import AppIntents
import SwiftUI
import WidgetKit

private enum WidgetSettings {
    static let appGroupIdentifiers = [
        "group.com.faikalizham.financial-tracker",
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5"
    ]
    static let snapshotFileName = "financial_tracker_widget.json"
    static let investmentPreferenceFileName =
        "financial_tracker_widget_include_investment.txt"
    static let amountsHiddenKey = "financial_tracker_widget_amounts_hidden"
    static let widgetKind = "FinancialTrackerWidget"
}

private enum WidgetAppearance {
    static let background = Color(red: 0.985, green: 0.972, blue: 0.948)
    static let accent = Color(red: 0.392, green: 0.847, blue: 0.894)
    static let ink = Color(red: 0.095, green: 0.082, blue: 0.180)
    static let secondaryInk = Color(red: 0.390, green: 0.370, blue: 0.440)
    static let divider = Color(red: 0.885, green: 0.860, blue: 0.825)
    static let positive = Color(red: 0.075, green: 0.550, blue: 0.400)
    static let negative = Color(red: 0.820, green: 0.230, blue: 0.340)
}

private struct FinancialTrackerSummary: Codable {
    let availableText: String
    let incomeText: String
    let expenseText: String
    let hasBudget: Bool
    let budgetSpentText: String
    let budgetLimitText: String
    let budgetUsageText: String
    let budgetRemainingText: String
    let budgetProgress: Double
}

private struct FinancialTrackerSnapshot: Codable {
    let version: Int
    let monthText: String
    let availableText: String
    let incomeText: String
    let expenseText: String
    let transactionCount: Int
    let hasBudget: Bool?
    let budgetSpentText: String?
    let budgetLimitText: String?
    let budgetUsageText: String?
    let budgetRemainingText: String?
    let budgetProgress: Double?
    let includeInvestment: Bool?
    let withInvestment: FinancialTrackerSummary?
    let withoutInvestment: FinancialTrackerSummary?
    let updatedAtUnixSeconds: Int64

    static var placeholder: FinancialTrackerSnapshot {
        FinancialTrackerSnapshot(
            version: 3,
            monthText: Date().formatted(.dateTime.month(.wide).year()),
            availableText: "RM 0.00",
            incomeText: "RM 0.00",
            expenseText: "RM 0.00",
            transactionCount: 0,
            hasBudget: true,
            budgetSpentText: "RM 350.00",
            budgetLimitText: "RM 2,750.00",
            budgetUsageText: "12.7% USED",
            budgetRemainingText: "RM 2,400.00 left",
            budgetProgress: 0.127,
            includeInvestment: true,
            withInvestment: previewSummary,
            withoutInvestment: previewSummary,
            updatedAtUnixSeconds: Int64(Date().timeIntervalSince1970)
        )
    }

    private static var previewSummary: FinancialTrackerSummary {
        FinancialTrackerSummary(
            availableText: "RM 745.00",
            incomeText: "RM 1,045.00",
            expenseText: "RM 300.00",
            hasBudget: true,
            budgetSpentText: "RM 255.00",
            budgetLimitText: "RM 2,500.00",
            budgetUsageText: "10.2% USED",
            budgetRemainingText: "RM 2,245.00 left",
            budgetProgress: 0.102
        )
    }

    func summary(includeInvestment: Bool) -> FinancialTrackerSummary {
        let legacySummary = FinancialTrackerSummary(
            availableText: availableText,
            incomeText: incomeText,
            expenseText: expenseText,
            hasBudget: hasBudget ?? false,
            budgetSpentText: budgetSpentText ?? "RM 0.00",
            budgetLimitText: budgetLimitText ?? "RM 0.00",
            budgetUsageText: budgetUsageText ?? "0% USED",
            budgetRemainingText: budgetRemainingText ?? "RM 0.00 left",
            budgetProgress: budgetProgress ?? 0
        )

        return includeInvestment
            ? (withInvestment ?? legacySummary)
            : (withoutInvestment ?? legacySummary)
    }
}

private enum WidgetSnapshotLoadState {
    case loaded
    case appGroupUnavailable
    case snapshotMissing
    case snapshotUnreadable
    case invalidSnapshot
    case unsupportedVersion

    var message: String {
        switch self {
        case .loaded:
            return ""
        case .appGroupUnavailable:
            return "No App Group"
        case .snapshotMissing:
            return "Open app to sync"
        case .snapshotUnreadable:
            return "Snapshot unreadable"
        case .invalidSnapshot:
            return "Invalid snapshot"
        case .unsupportedVersion:
            return "Update required"
        }
    }
}

private struct WidgetSnapshotLoadResult {
    let snapshot: FinancialTrackerSnapshot
    let state: WidgetSnapshotLoadState

    static var placeholder: WidgetSnapshotLoadResult {
        WidgetSnapshotLoadResult(
            snapshot: .placeholder,
            state: .loaded
        )
    }
}

private enum SharedWidgetStorage {
    static func loadSnapshot() -> WidgetSnapshotLoadResult {
        guard let containerUrl = availableContainerUrl() else {
            return failure(.appGroupUnavailable)
        }

        let snapshotUrl = containerUrl.appendingPathComponent(
            WidgetSettings.snapshotFileName,
            isDirectory: false
        )
        guard FileManager.default.fileExists(atPath: snapshotUrl.path) else {
            return failure(.snapshotMissing)
        }

        guard let data = try? Data(contentsOf: snapshotUrl) else {
            return failure(.snapshotUnreadable)
        }

        guard let snapshot = try? JSONDecoder().decode(
            FinancialTrackerSnapshot.self,
            from: data
        ) else {
            return failure(.invalidSnapshot)
        }

        guard (1...3).contains(snapshot.version) else {
            return failure(.unsupportedVersion)
        }

        return WidgetSnapshotLoadResult(
            snapshot: snapshot,
            state: .loaded
        )
    }

    private static func availableContainerUrl() -> URL? {
        availableContainer()?.url
    }

    static func loadAmountsHidden() -> Bool {
        guard let identifier = availableContainer()?.identifier,
              let defaults = UserDefaults(suiteName: identifier) else {
            return false
        }

        return defaults.bool(forKey: WidgetSettings.amountsHiddenKey)
    }

    static func loadIncludeInvestment(fallback: Bool) -> Bool {
        guard let containerUrl = availableContainerUrl() else {
            return fallback
        }

        let preferenceUrl = containerUrl.appendingPathComponent(
            WidgetSettings.investmentPreferenceFileName,
            isDirectory: false
        )
        guard let storedValue = try? String(
            contentsOf: preferenceUrl,
            encoding: .utf8
        ) else {
            return fallback
        }

        switch storedValue.trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased() {
        case "true":
            return true
        case "false":
            return false
        default:
            return fallback
        }
    }

    @discardableResult
    static func writeIncludeInvestment(_ includeInvestment: Bool) -> Bool {
        guard let containerUrl = availableContainerUrl() else {
            return false
        }

        let preferenceUrl = containerUrl.appendingPathComponent(
            WidgetSettings.investmentPreferenceFileName,
            isDirectory: false
        )
        do {
            try (includeInvestment ? "true" : "false").write(
                to: preferenceUrl,
                atomically: true,
                encoding: .utf8
            )
            return true
        } catch {
            return false
        }
    }

    @discardableResult
    static func toggleAmountsHidden() -> Bool {
        guard let identifier = availableContainer()?.identifier,
              let defaults = UserDefaults(suiteName: identifier) else {
            return false
        }

        let updatedValue = !defaults.bool(
            forKey: WidgetSettings.amountsHiddenKey
        )
        defaults.set(updatedValue, forKey: WidgetSettings.amountsHiddenKey)
        return updatedValue
    }

    private static func availableContainer() -> (identifier: String, url: URL)? {
        for identifier in WidgetSettings.appGroupIdentifiers {
            if let containerUrl = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) {
                return (identifier, containerUrl)
            }
        }

        return nil
    }

    private static func failure(
        _ state: WidgetSnapshotLoadState
    ) -> WidgetSnapshotLoadResult {
        WidgetSnapshotLoadResult(
            snapshot: .placeholder,
            state: state
        )
    }
}

@available(iOS 17.0, *)
struct RefreshFinancialTrackerIntent: AppIntent {
    static var title: LocalizedStringResource = "Refresh Financial Tracker"
    static var description = IntentDescription(
        "Reload the latest Financial Tracker widget snapshot."
    )
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        WidgetCenter.shared.reloadTimelines(ofKind: WidgetSettings.widgetKind)
        return .result()
    }
}

@available(iOS 17.0, *)
struct ToggleFinancialPrivacyIntent: AppIntent {
    static var title: LocalizedStringResource = "Hide or Show Financial Values"
    static var description = IntentDescription(
        "Toggle the visibility of monetary values in Financial Tracker widgets."
    )
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        SharedWidgetStorage.toggleAmountsHidden()
        WidgetCenter.shared.reloadTimelines(ofKind: WidgetSettings.widgetKind)
        return .result()
    }
}

@available(iOS 17.0, *)
struct ToggleInvestmentInclusionIntent: AppIntent {
    static var title: LocalizedStringResource = "Include or Exclude Investment"
    static var description = IntentDescription(
        "Toggle whether investment transactions are included in Financial Tracker totals."
    )
    static var openAppWhenRun = false

    @Parameter(title: "Currently Includes Investment")
    var currentValue: Bool

    init() {
        currentValue = true
    }

    init(currentValue: Bool) {
        self.currentValue = currentValue
    }

    func perform() async throws -> some IntentResult {
        SharedWidgetStorage.writeIncludeInvestment(!currentValue)
        WidgetCenter.shared.reloadTimelines(ofKind: WidgetSettings.widgetKind)
        return .result()
    }
}

private struct FinancialTrackerEntry: TimelineEntry {
    let date: Date
    let snapshot: FinancialTrackerSnapshot
    let loadState: WidgetSnapshotLoadState
    let amountsHidden: Bool
    let includeInvestment: Bool

    var summary: FinancialTrackerSummary {
        snapshot.summary(includeInvestment: includeInvestment)
    }

    var refreshedTimeText: String {
        date.formatted(date: .omitted, time: .shortened)
    }

    var refreshedText: String {
        "Refreshed \(refreshedTimeText)"
    }
}

private struct FinancialTrackerProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinancialTrackerEntry {
        FinancialTrackerEntry(
            date: Date(),
            snapshot: .placeholder,
            loadState: .loaded,
            amountsHidden: false,
            includeInvestment: true
        )
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialTrackerEntry) -> Void
    ) {
        let result = context.isPreview
            ? WidgetSnapshotLoadResult.placeholder
            : SharedWidgetStorage.loadSnapshot()
        completion(FinancialTrackerEntry(
            date: Date(),
            snapshot: result.snapshot,
            loadState: result.state,
            amountsHidden: SharedWidgetStorage.loadAmountsHidden(),
            includeInvestment: SharedWidgetStorage.loadIncludeInvestment(
                fallback: result.snapshot.includeInvestment ?? true
            )
        ))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialTrackerEntry>) -> Void
    ) {
        let now = Date()
        let result = SharedWidgetStorage.loadSnapshot()
        let entry = FinancialTrackerEntry(
            date: now,
            snapshot: result.snapshot,
            loadState: result.state,
            amountsHidden: SharedWidgetStorage.loadAmountsHidden(),
            includeInvestment: SharedWidgetStorage.loadIncludeInvestment(
                fallback: result.snapshot.includeInvestment ?? true
            )
        )
        let nextFallbackRefresh = Calendar.current.date(
            byAdding: .minute,
            value: 15,
            to: now
        ) ?? now.addingTimeInterval(15 * 60)
        completion(Timeline(
            entries: [entry],
            policy: .after(nextFallbackRefresh)
        ))
    }
}

private struct FinancialTrackerWidgetView: View {
    @Environment(\.widgetFamily) private var family

    let entry: FinancialTrackerEntry

    var body: some View {
        if #available(iOS 17.0, *) {
            content
                .containerBackground(WidgetAppearance.background, for: .widget)
        } else {
            content
                .padding()
                .background(WidgetAppearance.background)
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: family == .systemLarge ? 12 : 10) {
            header

            switch family {
            case .systemSmall:
                smallSummary
            case .systemLarge:
                largeSummary
            default:
                mediumSummary
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }

    private var header: some View {
        HStack(spacing: 8) {
            Image("financial_tracker_widget_icon")
                .resizable()
                .scaledToFill()
                .frame(width: 32, height: 32)
                .clipShape(
                    RoundedRectangle(cornerRadius: 9, style: .continuous)
                )

            VStack(alignment: .leading, spacing: 1) {
                Text("Financial Tracker")
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .foregroundStyle(WidgetAppearance.ink)
                    .lineLimit(1)

                if family != .systemSmall {
                    Text(family == .systemLarge
                        ? (entry.includeInvestment
                            ? "With investment"
                            : "Without investment")
                        : entry.snapshot.monthText)
                        .font(.caption2)
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .lineLimit(1)
                }
            }

            Spacer(minLength: 4)

            if family == .systemLarge {
                investmentButton
            }

            if family == .systemMedium {
                privacyButton
            }

            refreshButton
        }
    }

    @ViewBuilder
    private var refreshButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: RefreshFinancialTrackerIntent()) {
                Image(systemName: "arrow.clockwise")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(WidgetAppearance.accent)
                    .frame(width: 28, height: 28)
                    .background(
                        Circle()
                            .fill(WidgetAppearance.accent.opacity(0.16))
                    )
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Refresh widget")
        }
    }

    @ViewBuilder
    private var privacyButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: ToggleFinancialPrivacyIntent()) {
                Image(systemName: entry.amountsHidden ? "eye.slash" : "eye")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(WidgetAppearance.secondaryInk)
                    .frame(width: 28, height: 28)
                    .background(
                        Circle()
                            .fill(WidgetAppearance.secondaryInk.opacity(0.10))
                    )
            }
            .buttonStyle(.plain)
            .accessibilityLabel(
                entry.amountsHidden ? "Show financial values" : "Hide financial values"
            )
        } else {
            Image(systemName: "eye")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(WidgetAppearance.secondaryInk)
                .frame(width: 28, height: 28)
        }
    }

    @ViewBuilder
    private var investmentButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: ToggleInvestmentInclusionIntent(
                currentValue: entry.includeInvestment
            )) {
                Image(systemName: "chart.line.uptrend.xyaxis")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(
                        entry.includeInvestment
                            ? Color.white
                            : WidgetAppearance.secondaryInk
                    )
                    .frame(width: 36, height: 28)
                    .background(
                        Capsule()
                            .fill(entry.includeInvestment
                                ? WidgetAppearance.accent
                                : WidgetAppearance.secondaryInk.opacity(0.10))
                    )
            }
            .buttonStyle(.plain)
            .accessibilityLabel(entry.includeInvestment
                ? "Exclude investment from totals"
                : "Include investment in totals")
        } else {
            Image(systemName: "chart.line.uptrend.xyaxis")
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(WidgetAppearance.secondaryInk)
                .frame(width: 36, height: 28)
        }
    }

    private var smallSummary: some View {
        VStack(alignment: .leading, spacing: 4) {
            Spacer(minLength: 0)

            Text("AVAILABLE")
                .font(.caption2.weight(.semibold))
                .tracking(0.8)
                .foregroundStyle(WidgetAppearance.secondaryInk)

            Text(displayedAmount(entry.summary.availableText))
                .font(.system(.title3, design: .rounded).weight(.bold))
                .monospacedDigit()
                .foregroundStyle(WidgetAppearance.ink)
                .lineLimit(1)
                .minimumScaleFactor(0.58)

            Text(activityText)
                .font(.caption2)
                .foregroundStyle(WidgetAppearance.secondaryInk)
                .lineLimit(1)
                .minimumScaleFactor(0.75)
        }
    }

    private var mediumSummary: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: 1) {
                    Text("AVAILABLE")
                        .font(.caption2.weight(.semibold))
                        .tracking(0.8)
                        .foregroundStyle(WidgetAppearance.secondaryInk)

                    Text(displayedAmount(entry.summary.availableText))
                        .font(.system(.title2, design: .rounded).weight(.bold))
                        .monospacedDigit()
                        .foregroundStyle(WidgetAppearance.ink)
                        .lineLimit(1)
                        .minimumScaleFactor(0.65)
                }

                Spacer(minLength: 10)

                Text(entry.loadState == .loaded
                    ? entry.refreshedText
                    : entry.loadState.message)
                    .font(.caption2)
                    .foregroundStyle(WidgetAppearance.secondaryInk)
                    .lineLimit(1)
            }

            HStack(spacing: 10) {
                metric(
                    title: "INCOME",
                    value: displayedAmount(entry.summary.incomeText),
                    color: WidgetAppearance.positive
                )
                metric(
                    title: "EXPENSE",
                    value: displayedAmount(entry.summary.expenseText),
                    color: WidgetAppearance.negative
                )
            }
        }
    }

    private var largeSummary: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack(alignment: .top, spacing: 12) {
                VStack(alignment: .leading, spacing: 6) {
                    Label(entry.snapshot.monthText, systemImage: "calendar")
                        .font(.system(.subheadline, design: .rounded).weight(.semibold))
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .lineLimit(1)

                    Text(displayedAmount(entry.summary.availableText))
                        .font(.system(.largeTitle, design: .rounded).weight(.semibold))
                        .monospacedDigit()
                        .foregroundStyle(WidgetAppearance.ink)
                        .lineLimit(1)
                        .minimumScaleFactor(0.62)

                    HStack(spacing: 5) {
                        Text("Available for \(entry.snapshot.monthText)")
                            .font(.caption)
                            .foregroundStyle(WidgetAppearance.secondaryInk)
                            .lineLimit(1)

                        privacyButton
                    }
                }

                Spacer(minLength: 8)

                VStack(alignment: .trailing, spacing: 3) {
                    Text("TOTAL TRANSACTIONS")
                        .font(.system(size: 8, weight: .semibold))
                        .tracking(0.6)
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .lineLimit(1)

                    Text(entry.snapshot.transactionCount.formatted())
                        .font(.system(.title2, design: .rounded).weight(.bold))
                        .monospacedDigit()
                        .foregroundStyle(WidgetAppearance.ink)
                }
            }

            Divider()
                .overlay(WidgetAppearance.divider)

            HStack(spacing: 16) {
                largeMetric(
                    title: "TOTAL INCOME",
                    value: displayedAmount(entry.summary.incomeText),
                    symbol: "arrow.up.right",
                    color: WidgetAppearance.positive
                )

                Rectangle()
                    .fill(WidgetAppearance.divider)
                    .frame(width: 1, height: 38)

                largeMetric(
                    title: "TOTAL EXPENSE",
                    value: displayedAmount(entry.summary.expenseText),
                    symbol: "arrow.down.right",
                    color: WidgetAppearance.negative
                )
            }

            Divider()
                .overlay(WidgetAppearance.divider)

            budgetSummary

            Spacer(minLength: 0)

            Text(entry.loadState == .loaded
                ? entry.refreshedText
                : entry.loadState.message)
                .font(.caption2)
                .foregroundStyle(WidgetAppearance.secondaryInk)
                .frame(maxWidth: .infinity, alignment: .trailing)
                .lineLimit(1)
        }
    }

    private func largeMetric(
        title: String,
        value: String,
        symbol: String,
        color: Color
    ) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title)
                .font(.system(size: 9, weight: .semibold))
                .tracking(0.7)
                .foregroundStyle(WidgetAppearance.secondaryInk)

            HStack(spacing: 5) {
                Text(value)
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(WidgetAppearance.ink)
                    .lineLimit(1)
                    .minimumScaleFactor(0.58)

                Image(systemName: symbol)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(color)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    @ViewBuilder
    private var budgetSummary: some View {
        if entry.summary.hasBudget {
            VStack(alignment: .leading, spacing: 7) {
                HStack(alignment: .firstTextBaseline) {
                    Text("NET SPENDING / BUDGET")
                        .font(.system(size: 9, weight: .semibold))
                        .tracking(0.7)
                        .foregroundStyle(WidgetAppearance.secondaryInk)

                    Spacer(minLength: 8)

                    Text(entry.amountsHidden
                        ? "•••"
                        : entry.summary.budgetUsageText)
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(WidgetAppearance.accent)
                        .lineLimit(1)
                }

                Text(entry.amountsHidden
                    ? "•••••• / ••••••"
                    : "\(entry.summary.budgetSpentText) / \(entry.summary.budgetLimitText)")
                    .font(.system(.subheadline, design: .rounded).weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(WidgetAppearance.ink)
                    .lineLimit(1)
                    .minimumScaleFactor(0.62)

                ProgressView(
                    value: entry.amountsHidden
                        ? 0
                        : min(max(entry.summary.budgetProgress, 0), 1)
                )
                .tint(WidgetAppearance.accent)

                Text(entry.amountsHidden
                    ? "••••••"
                    : entry.summary.budgetRemainingText)
                    .font(.caption2)
                    .foregroundStyle(WidgetAppearance.secondaryInk)
                    .frame(maxWidth: .infinity, alignment: .trailing)
                    .lineLimit(1)
            }
        } else {
            HStack(spacing: 8) {
                Image(systemName: "chart.bar.doc.horizontal")
                    .foregroundStyle(WidgetAppearance.accent)

                VStack(alignment: .leading, spacing: 2) {
                    Text("No monthly budget")
                        .font(.subheadline.weight(.semibold))
                        .foregroundStyle(WidgetAppearance.ink)
                    Text("Set a budget in Financial Tracker to see progress here.")
                        .font(.caption2)
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .lineLimit(2)
                }
            }
        }
    }

    private func metric(title: String, value: String, color: Color) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(title)
                .font(.caption2.weight(.semibold))
                .foregroundStyle(WidgetAppearance.secondaryInk)

            Text(value)
                .font(.system(.subheadline, design: .rounded).weight(.semibold))
                .monospacedDigit()
                .foregroundStyle(color)
                .lineLimit(1)
                .minimumScaleFactor(0.65)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func displayedAmount(_ value: String) -> String {
        entry.amountsHidden ? "••••••" : value
    }

    private var activityText: String {
        guard entry.loadState == .loaded else {
            return entry.loadState.message
        }

        switch entry.snapshot.transactionCount {
        case 0:
            return "No activity \u{00B7} \(entry.refreshedTimeText)"
        case 1:
            return "1 tx \u{00B7} \(entry.refreshedTimeText)"
        default:
            return "\(entry.snapshot.transactionCount) tx \u{00B7} \(entry.refreshedTimeText)"
        }
    }
}

@main
struct FinancialTrackerWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(
            kind: WidgetSettings.widgetKind,
            provider: FinancialTrackerProvider()
        ) { entry in
            FinancialTrackerWidgetView(entry: entry)
        }
        .configurationDisplayName("Financial Tracker")
        .description("See this month’s balance, income, and expenses.")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge])
    }
}

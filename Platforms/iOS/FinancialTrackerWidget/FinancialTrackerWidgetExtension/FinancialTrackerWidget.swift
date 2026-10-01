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
    static let selectedMonthFileName =
        "financial_tracker_widget_selected_month.txt"
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

private struct FinancialTrackerMonthSnapshot: Codable {
    let monthKey: Int
    let monthText: String
    let transactionCount: Int
    let withInvestment: FinancialTrackerSummary
    let withoutInvestment: FinancialTrackerSummary

    func summary(includeInvestment: Bool) -> FinancialTrackerSummary {
        includeInvestment ? withInvestment : withoutInvestment
    }
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
    let currentMonthKey: Int?
    let months: [FinancialTrackerMonthSnapshot]?
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
            currentMonthKey: previewMonth.monthKey,
            months: [previewMonth],
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

    private static var previewMonth: FinancialTrackerMonthSnapshot {
        let components = Calendar.current.dateComponents(
            [.year, .month],
            from: Date()
        )
        let monthKey = (components.year ?? 2000) * 100 +
            (components.month ?? 1)
        return FinancialTrackerMonthSnapshot(
            monthKey: monthKey,
            monthText: Date().formatted(.dateTime.month(.wide).year()),
            transactionCount: 9,
            withInvestment: previewSummary,
            withoutInvestment: previewSummary
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

    var resolvedCurrentMonthKey: Int {
        currentMonthKey ?? months?.last?.monthKey ?? 0
    }

    func month(for monthKey: Int) -> FinancialTrackerMonthSnapshot? {
        months?.first(where: { $0.monthKey == monthKey })
    }

    func resolvedSelectedMonthKey(_ requestedMonthKey: Int) -> Int {
        month(for: requestedMonthKey) == nil
            ? resolvedCurrentMonthKey
            : requestedMonthKey
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

        guard (1...4).contains(snapshot.version) else {
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

    static func loadSelectedMonthKey(fallback: Int) -> Int {
        guard let containerUrl = availableContainerUrl() else {
            return fallback
        }

        let selectionUrl = containerUrl.appendingPathComponent(
            WidgetSettings.selectedMonthFileName,
            isDirectory: false
        )
        guard let storedValue = try? String(
            contentsOf: selectionUrl,
            encoding: .utf8
        ),
        let monthKey = Int(storedValue.trimmingCharacters(
            in: .whitespacesAndNewlines
        )) else {
            return fallback
        }

        return monthKey
    }

    @discardableResult
    static func writeSelectedMonthKey(_ monthKey: Int) -> Bool {
        guard let containerUrl = availableContainerUrl() else {
            return false
        }

        let selectionUrl = containerUrl.appendingPathComponent(
            WidgetSettings.selectedMonthFileName,
            isDirectory: false
        )
        do {
            try String(monthKey).write(
                to: selectionUrl,
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

@available(iOS 17.0, *)
struct SelectWidgetMonthIntent: AppIntent {
    static var title: LocalizedStringResource = "Select Financial Month"
    static var description = IntentDescription(
        "Show a different month in the Financial Tracker widget."
    )
    static var openAppWhenRun = false

    @Parameter(title: "Month Key")
    var monthKey: Int

    init() {
        monthKey = 0
    }

    init(monthKey: Int) {
        self.monthKey = monthKey
    }

    func perform() async throws -> some IntentResult {
        SharedWidgetStorage.writeSelectedMonthKey(monthKey)
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
    let selectedMonthKey: Int

    var currentMonth: FinancialTrackerMonthSnapshot? {
        snapshot.month(for: snapshot.resolvedCurrentMonthKey)
    }

    var selectedMonth: FinancialTrackerMonthSnapshot? {
        snapshot.month(for: selectedMonthKey) ?? currentMonth
    }

    var previousMonthKey: Int? {
        adjacentMonthKey(offset: -1)
    }

    var nextMonthKey: Int? {
        adjacentMonthKey(offset: 1)
    }

    private func adjacentMonthKey(offset: Int) -> Int? {
        guard let months = snapshot.months,
              let selectedIndex = months.firstIndex(where: {
                  $0.monthKey == selectedMonthKey
              }) else {
            return nil
        }

        let adjacentIndex = selectedIndex + offset
        guard months.indices.contains(adjacentIndex) else {
            return nil
        }

        return months[adjacentIndex].monthKey
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
            includeInvestment: true,
            selectedMonthKey: FinancialTrackerSnapshot.placeholder
                .resolvedCurrentMonthKey
        )
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialTrackerEntry) -> Void
    ) {
        let result = context.isPreview
            ? WidgetSnapshotLoadResult.placeholder
            : SharedWidgetStorage.loadSnapshot()
        let requestedMonthKey = SharedWidgetStorage.loadSelectedMonthKey(
            fallback: result.snapshot.resolvedCurrentMonthKey
        )
        completion(FinancialTrackerEntry(
            date: Date(),
            snapshot: result.snapshot,
            loadState: result.state,
            amountsHidden: SharedWidgetStorage.loadAmountsHidden(),
            includeInvestment: SharedWidgetStorage.loadIncludeInvestment(
                fallback: result.snapshot.includeInvestment ?? true
            ),
            selectedMonthKey: result.snapshot.resolvedSelectedMonthKey(
                requestedMonthKey
            )
        ))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialTrackerEntry>) -> Void
    ) {
        let now = Date()
        let result = SharedWidgetStorage.loadSnapshot()
        let requestedMonthKey = SharedWidgetStorage.loadSelectedMonthKey(
            fallback: result.snapshot.resolvedCurrentMonthKey
        )
        let entry = FinancialTrackerEntry(
            date: now,
            snapshot: result.snapshot,
            loadState: result.state,
            amountsHidden: SharedWidgetStorage.loadAmountsHidden(),
            includeInvestment: SharedWidgetStorage.loadIncludeInvestment(
                fallback: result.snapshot.includeInvestment ?? true
            ),
            selectedMonthKey: result.snapshot.resolvedSelectedMonthKey(
                requestedMonthKey
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

private struct WidgetBrandMark: View {
    var body: some View {
        ZStack {
            RoundedRectangle(cornerRadius: 9, style: .continuous)
                .fill(WidgetAppearance.accent.opacity(0.18))

            RoundedRectangle(cornerRadius: 3, style: .continuous)
                .stroke(WidgetAppearance.ink, lineWidth: 1.6)
                .frame(width: 20, height: 14)
                .offset(y: 2)

            RoundedRectangle(cornerRadius: 2, style: .continuous)
                .fill(WidgetAppearance.background)
                .overlay(
                    RoundedRectangle(cornerRadius: 2, style: .continuous)
                        .stroke(WidgetAppearance.ink, lineWidth: 1.4)
                )
                .frame(width: 7, height: 5)
                .offset(x: 7, y: 2)

            Path { path in
                path.move(to: CGPoint(x: 7, y: 21))
                path.addLine(to: CGPoint(x: 12, y: 17))
                path.addLine(to: CGPoint(x: 16, y: 19))
                path.addLine(to: CGPoint(x: 24, y: 10))
            }
            .stroke(
                WidgetAppearance.positive,
                style: StrokeStyle(
                    lineWidth: 2,
                    lineCap: .round,
                    lineJoin: .round
                )
            )

            Circle()
                .fill(WidgetAppearance.positive)
                .frame(width: 3.5, height: 3.5)
                .position(x: 24, y: 10)
        }
        .frame(width: 32, height: 32)
        .accessibilityHidden(true)
    }
}

private struct FinancialTrackerWidgetView: View {
    @Environment(\.widgetFamily) private var family

    let entry: FinancialTrackerEntry

    private var activeMonth: FinancialTrackerMonthSnapshot? {
        family == .systemLarge ? entry.selectedMonth : entry.currentMonth
    }

    private var activeSummary: FinancialTrackerSummary {
        activeMonth?.summary(includeInvestment: entry.includeInvestment) ??
            entry.snapshot.summary(includeInvestment: entry.includeInvestment)
    }

    private var activeMonthText: String {
        activeMonth?.monthText ?? entry.snapshot.monthText
    }

    private var activeTransactionCount: Int {
        activeMonth?.transactionCount ?? entry.snapshot.transactionCount
    }

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
            WidgetBrandMark()

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

            Text(displayedAmount(activeSummary.availableText))
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

                    Text(displayedAmount(activeSummary.availableText))
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
                    value: displayedAmount(activeSummary.incomeText),
                    color: WidgetAppearance.positive
                )
                metric(
                    title: "EXPENSE",
                    value: displayedAmount(activeSummary.expenseText),
                    color: WidgetAppearance.negative
                )
            }
        }
    }

    private var monthLabel: some View {
        Label(activeMonthText, systemImage: "calendar")
            .font(.system(.subheadline, design: .rounded).weight(.semibold))
            .foregroundStyle(WidgetAppearance.secondaryInk)
            .lineLimit(1)
    }

    @ViewBuilder
    private var monthSelector: some View {
        HStack(spacing: 3) {
            monthArrowButton(
                targetMonthKey: entry.previousMonthKey,
                systemName: "chevron.left",
                accessibilityLabel: "Show previous month"
            )

            if #available(iOS 17.0, *) {
                Button(intent: SelectWidgetMonthIntent(
                    monthKey: entry.snapshot.resolvedCurrentMonthKey
                )) {
                    monthLabel
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Return to current month")
            } else {
                monthLabel
            }

            monthArrowButton(
                targetMonthKey: entry.nextMonthKey,
                systemName: "chevron.right",
                accessibilityLabel: "Show next month"
            )
        }
    }

    @ViewBuilder
    private func monthArrowButton(
        targetMonthKey: Int?,
        systemName: String,
        accessibilityLabel: String
    ) -> some View {
        if let targetMonthKey = targetMonthKey {
            if #available(iOS 17.0, *) {
                Button(intent: SelectWidgetMonthIntent(monthKey: targetMonthKey)) {
                    Image(systemName: systemName)
                        .font(.system(size: 10, weight: .bold))
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .frame(width: 22, height: 24)
                        .background(
                            Circle()
                                .fill(WidgetAppearance.secondaryInk.opacity(0.08))
                        )
                }
                .buttonStyle(.plain)
                .accessibilityLabel(accessibilityLabel)
            } else {
                Image(systemName: systemName)
                    .font(.system(size: 10, weight: .bold))
                    .foregroundStyle(WidgetAppearance.secondaryInk)
                    .frame(width: 22, height: 24)
            }
        } else {
            Image(systemName: systemName)
                .font(.system(size: 10, weight: .bold))
                .foregroundStyle(WidgetAppearance.secondaryInk.opacity(0.24))
                .frame(width: 22, height: 24)
        }
    }

    private var largeSummary: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack(alignment: .top, spacing: 12) {
                VStack(alignment: .leading, spacing: 6) {
                    monthSelector

                    Text(displayedAmount(activeSummary.availableText))
                        .font(.system(.largeTitle, design: .rounded).weight(.semibold))
                        .monospacedDigit()
                        .foregroundStyle(WidgetAppearance.ink)
                        .lineLimit(1)
                        .minimumScaleFactor(0.62)

                    HStack(spacing: 5) {
                        Text(activeTransactionCount == 0
                            ? "No activity for \(activeMonthText)"
                            : "Available for \(activeMonthText)")
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

                    Text(activeTransactionCount.formatted())
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
                    value: displayedAmount(activeSummary.incomeText),
                    symbol: "arrow.up.right",
                    color: WidgetAppearance.positive
                )

                Rectangle()
                    .fill(WidgetAppearance.divider)
                    .frame(width: 1, height: 38)

                largeMetric(
                    title: "TOTAL EXPENSE",
                    value: displayedAmount(activeSummary.expenseText),
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
        if activeSummary.hasBudget {
            VStack(alignment: .leading, spacing: 7) {
                HStack(alignment: .firstTextBaseline) {
                    Text("NET SPENDING / BUDGET")
                        .font(.system(size: 9, weight: .semibold))
                        .tracking(0.7)
                        .foregroundStyle(WidgetAppearance.secondaryInk)

                    Spacer(minLength: 8)

                    Text(entry.amountsHidden
                        ? "•••"
                        : activeSummary.budgetUsageText)
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(WidgetAppearance.accent)
                        .lineLimit(1)
                }

                Text(entry.amountsHidden
                    ? "•••••• / ••••••"
                    : "\(activeSummary.budgetSpentText) / \(activeSummary.budgetLimitText)")
                    .font(.system(.subheadline, design: .rounded).weight(.semibold))
                    .monospacedDigit()
                    .foregroundStyle(WidgetAppearance.ink)
                    .lineLimit(1)
                    .minimumScaleFactor(0.62)

                ProgressView(
                    value: entry.amountsHidden
                        ? 0
                        : min(max(activeSummary.budgetProgress, 0), 1)
                )
                .tint(WidgetAppearance.accent)

                Text(entry.amountsHidden
                    ? "••••••"
                    : activeSummary.budgetRemainingText)
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

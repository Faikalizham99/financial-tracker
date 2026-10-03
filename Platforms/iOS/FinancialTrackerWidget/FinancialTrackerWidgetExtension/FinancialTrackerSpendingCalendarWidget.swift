import AppIntents
import SwiftUI
import WidgetKit

private enum SpendingCalendarSettings {
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
        "financial_tracker_calendar_selected_month.txt"
    static let amountsHiddenKey = "financial_tracker_widget_amounts_hidden"
    static let widgetKind = "FinancialTrackerSpendingCalendarWidget"
    static let mainWidgetKind = "FinancialTrackerWidget"
}

private typealias CalendarAppearance = SharedWidgetAppearance

private struct CalendarSummary: Codable {
    let expenseText: String
    let hasBudget: Bool
    let budgetLimitText: String
    let budgetLimitMinor: Int64?
}

private struct CalendarDaySnapshot: Codable {
    let day: Int
    let transactionCount: Int
    let withInvestmentExpenseMinor: Int64
    let withoutInvestmentExpenseMinor: Int64

    func expenseMinor(includeInvestment: Bool) -> Int64 {
        includeInvestment
            ? withInvestmentExpenseMinor
            : withoutInvestmentExpenseMinor
    }
}

private struct CalendarMonthSnapshot: Codable {
    let monthKey: Int
    let monthText: String
    let transactionCount: Int
    let withInvestment: CalendarSummary
    let withoutInvestment: CalendarSummary
    let days: [CalendarDaySnapshot]?

    func summary(includeInvestment: Bool) -> CalendarSummary {
        includeInvestment ? withInvestment : withoutInvestment
    }
}

private struct CalendarFinancialSnapshot: Codable {
    let version: Int
    let includeInvestment: Bool?
    let currentMonthKey: Int?
    let months: [CalendarMonthSnapshot]?
    let updatedAtUnixSeconds: Int64

    var resolvedCurrentMonthKey: Int {
        currentMonthKey ?? months?.last?.monthKey ?? 0
    }

    func month(for monthKey: Int) -> CalendarMonthSnapshot? {
        months?.first(where: { $0.monthKey == monthKey })
    }

    func resolvedSelectedMonthKey(_ requestedMonthKey: Int) -> Int {
        month(for: requestedMonthKey) == nil
            ? resolvedCurrentMonthKey
            : requestedMonthKey
    }

    static var placeholder: CalendarFinancialSnapshot {
        let calendar = Calendar.current
        let now = Date()
        let components = calendar.dateComponents([.year, .month], from: now)
        let year = components.year ?? 2026
        let month = components.month ?? 1
        let monthKey = (year * 100) + month
        let start = calendar.date(
            from: DateComponents(year: year, month: month, day: 1)
        ) ?? now
        let dayCount = calendar.range(of: .day, in: .month, for: start)?.count ?? 30
        let days = (1...dayCount).map { day in
            CalendarDaySnapshot(
                day: day,
                transactionCount: day.isMultiple(of: 4) ? 2 : 0,
                withInvestmentExpenseMinor: day.isMultiple(of: 4)
                    ? Int64(day * 735)
                    : 0,
                withoutInvestmentExpenseMinor: day.isMultiple(of: 4)
                    ? Int64(day * 735)
                    : 0
            )
        }
        let summary = CalendarSummary(
            expenseText: "RM 1,240.00",
            hasBudget: true,
            budgetLimitText: "RM 2,750.00",
            budgetLimitMinor: 275_000
        )
        let monthSnapshot = CalendarMonthSnapshot(
            monthKey: monthKey,
            monthText: start.formatted(.dateTime.month(.wide).year()),
            transactionCount: 14,
            withInvestment: summary,
            withoutInvestment: summary,
            days: days
        )
        return CalendarFinancialSnapshot(
            version: 5,
            includeInvestment: true,
            currentMonthKey: monthKey,
            months: [monthSnapshot],
            updatedAtUnixSeconds: Int64(now.timeIntervalSince1970)
        )
    }
}

private enum CalendarLoadState: Equatable {
    case loaded
    case appGroupUnavailable
    case snapshotMissing
    case snapshotUnreadable
    case invalidSnapshot
    case unsupportedVersion

    var message: String {
        switch self {
        case .loaded: return ""
        case .appGroupUnavailable: return "No App Group"
        case .snapshotMissing: return "Open app to sync"
        case .snapshotUnreadable: return "Snapshot unreadable"
        case .invalidSnapshot: return "Invalid snapshot"
        case .unsupportedVersion: return "Update required"
        }
    }
}

private struct CalendarLoadResult {
    let snapshot: CalendarFinancialSnapshot
    let state: CalendarLoadState

    static var placeholder: CalendarLoadResult {
        CalendarLoadResult(snapshot: .placeholder, state: .loaded)
    }
}

private enum CalendarWidgetStorage {
    static func loadSnapshot() -> CalendarLoadResult {
        guard let containerUrl = availableContainer()?.url else {
            return failure(.appGroupUnavailable)
        }
        let snapshotUrl = containerUrl.appendingPathComponent(
            SpendingCalendarSettings.snapshotFileName,
            isDirectory: false
        )
        guard FileManager.default.fileExists(atPath: snapshotUrl.path) else {
            return failure(.snapshotMissing)
        }
        guard let data = try? Data(contentsOf: snapshotUrl) else {
            return failure(.snapshotUnreadable)
        }
        guard let snapshot = try? JSONDecoder().decode(
            CalendarFinancialSnapshot.self,
            from: data
        ) else {
            return failure(.invalidSnapshot)
        }
        guard (4...5).contains(snapshot.version) else {
            return failure(.unsupportedVersion)
        }
        return CalendarLoadResult(snapshot: snapshot, state: .loaded)
    }

    static func loadAmountsHidden() -> Bool {
        guard let identifier = availableContainer()?.identifier,
              let defaults = UserDefaults(suiteName: identifier) else {
            return false
        }
        return defaults.bool(forKey: SpendingCalendarSettings.amountsHiddenKey)
    }

    @discardableResult
    static func toggleAmountsHidden() -> Bool {
        guard let identifier = availableContainer()?.identifier,
              let defaults = UserDefaults(suiteName: identifier) else {
            return false
        }
        let updatedValue = !defaults.bool(
            forKey: SpendingCalendarSettings.amountsHiddenKey
        )
        defaults.set(
            updatedValue,
            forKey: SpendingCalendarSettings.amountsHiddenKey
        )
        return updatedValue
    }

    static func loadIncludeInvestment(fallback: Bool) -> Bool {
        guard let containerUrl = availableContainer()?.url else {
            return fallback
        }
        let preferenceUrl = containerUrl.appendingPathComponent(
            SpendingCalendarSettings.investmentPreferenceFileName,
            isDirectory: false
        )
        guard let storedValue = try? String(
            contentsOf: preferenceUrl,
            encoding: .utf8
        ) else {
            return fallback
        }
        switch storedValue
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .lowercased() {
        case "true": return true
        case "false": return false
        default: return fallback
        }
    }

    static func loadSelectedMonthKey(fallback: Int) -> Int {
        guard let containerUrl = availableContainer()?.url else {
            return fallback
        }
        let selectionUrl = containerUrl.appendingPathComponent(
            SpendingCalendarSettings.selectedMonthFileName,
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
        guard let containerUrl = availableContainer()?.url else {
            return false
        }
        let selectionUrl = containerUrl.appendingPathComponent(
            SpendingCalendarSettings.selectedMonthFileName,
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

    private static func availableContainer() -> (
        identifier: String,
        url: URL
    )? {
        for identifier in SpendingCalendarSettings.appGroupIdentifiers {
            if let containerUrl = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) {
                return (identifier, containerUrl)
            }
        }
        return nil
    }

    private static func failure(_ state: CalendarLoadState) -> CalendarLoadResult {
        CalendarLoadResult(snapshot: .placeholder, state: state)
    }
}

@available(iOS 17.0, *)
struct RefreshSpendingCalendarIntent: AppIntent {
    static var title: LocalizedStringResource = "Refresh Spending Calendar"
    static var description = IntentDescription(
        "Reload the latest Financial Tracker spending calendar."
    )
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        WidgetCenter.shared.reloadTimelines(
            ofKind: SpendingCalendarSettings.widgetKind
        )
        return .result()
    }
}

@available(iOS 17.0, *)
struct ToggleSpendingCalendarPrivacyIntent: AppIntent {
    static var title: LocalizedStringResource = "Hide or Show Calendar Values"
    static var description = IntentDescription(
        "Toggle monetary values in Financial Tracker widgets."
    )
    static var openAppWhenRun = false

    func perform() async throws -> some IntentResult {
        CalendarWidgetStorage.toggleAmountsHidden()
        WidgetCenter.shared.reloadTimelines(
            ofKind: SpendingCalendarSettings.widgetKind
        )
        WidgetCenter.shared.reloadTimelines(
            ofKind: SpendingCalendarSettings.mainWidgetKind
        )
        return .result()
    }
}

@available(iOS 17.0, *)
struct SelectSpendingCalendarMonthIntent: AppIntent {
    static var title: LocalizedStringResource = "Select Calendar Month"
    static var description = IntentDescription(
        "Show a different month in the Financial Tracker spending calendar."
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
        CalendarWidgetStorage.writeSelectedMonthKey(monthKey)
        WidgetCenter.shared.reloadTimelines(
            ofKind: SpendingCalendarSettings.widgetKind
        )
        return .result()
    }
}

private struct SpendingCalendarEntry: TimelineEntry {
    let date: Date
    let snapshot: CalendarFinancialSnapshot
    let loadState: CalendarLoadState
    let selectedMonthKey: Int
    let amountsHidden: Bool
    let includeInvestment: Bool

    var selectedMonth: CalendarMonthSnapshot? {
        snapshot.month(for: selectedMonthKey) ??
            snapshot.month(for: snapshot.resolvedCurrentMonthKey)
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
        return months.indices.contains(adjacentIndex)
            ? months[adjacentIndex].monthKey
            : nil
    }
}

private struct SpendingCalendarProvider: TimelineProvider {
    func placeholder(in context: Context) -> SpendingCalendarEntry {
        makeEntry(result: .placeholder)
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (SpendingCalendarEntry) -> Void
    ) {
        completion(makeEntry(
            result: context.isPreview
                ? .placeholder
                : CalendarWidgetStorage.loadSnapshot()
        ))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<SpendingCalendarEntry>) -> Void
    ) {
        let entry = makeEntry(result: CalendarWidgetStorage.loadSnapshot())
        let refreshDate = Calendar.current.date(
            byAdding: .minute,
            value: 15,
            to: Date()
        ) ?? Date().addingTimeInterval(900)
        completion(Timeline(entries: [entry], policy: .after(refreshDate)))
    }

    private func makeEntry(
        result: CalendarLoadResult
    ) -> SpendingCalendarEntry {
        CalendarAppearance.reload()
        let selectedMonthKey = result.snapshot.resolvedSelectedMonthKey(
            CalendarWidgetStorage.loadSelectedMonthKey(
                fallback: result.snapshot.resolvedCurrentMonthKey
            )
        )
        return SpendingCalendarEntry(
            date: Date(),
            snapshot: result.snapshot,
            loadState: result.state,
            selectedMonthKey: selectedMonthKey,
            amountsHidden: CalendarWidgetStorage.loadAmountsHidden(),
            includeInvestment: CalendarWidgetStorage.loadIncludeInvestment(
                fallback: result.snapshot.includeInvestment ?? true
            )
        )
    }
}

private enum CalendarSpendingLevel: Equatable {
    case none
    case below
    case near
    case over
}

private struct SpendingCalendarWidgetView: View {
    let entry: SpendingCalendarEntry

    private let weekdayLabels = ["M", "T", "W", "T", "F", "S", "S"]
    private let columns = Array(
        repeating: GridItem(.flexible(), spacing: 4),
        count: 7
    )

    private var month: CalendarMonthSnapshot? {
        entry.selectedMonth
    }

    private var summary: CalendarSummary? {
        month?.summary(includeInvestment: entry.includeInvestment)
    }

    private var monthDate: Date {
        let monthKey = month?.monthKey ?? entry.snapshot.resolvedCurrentMonthKey
        return Calendar.current.date(from: DateComponents(
            year: monthKey / 100,
            month: monthKey % 100,
            day: 1
        )) ?? Date()
    }

    private var dayCount: Int {
        Calendar.current.range(of: .day, in: .month, for: monthDate)?.count ?? 30
    }

    private var leadingBlankCount: Int {
        let weekday = Calendar.current.component(.weekday, from: monthDate)
        return (weekday + 5) % 7
    }

    private var dailyAllowanceMinor: Double {
        guard summary?.hasBudget == true,
              let budgetMinor = summary?.budgetLimitMinor,
              budgetMinor > 0 else {
            return 0
        }
        return Double(budgetMinor) / Double(dayCount)
    }

    var body: some View {
        if #available(iOS 17.0, *) {
            content
                .containerBackground(CalendarAppearance.background, for: .widget)
        } else {
            content
                .padding()
                .background(CalendarAppearance.background)
        }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 7) {
            header
            monthSelector
            summaryRow
            weekdayRow
            calendarGrid
            footer
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    private var header: some View {
        HStack(spacing: 8) {
            Image(systemName: "calendar.badge.clock")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(CalendarAppearance.accent)
                .frame(width: 30, height: 30)
                .background(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(CalendarAppearance.accent.opacity(0.16))
                )

            VStack(alignment: .leading, spacing: 1) {
                Text("Spending Calendar")
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .foregroundStyle(CalendarAppearance.ink)
                    .lineLimit(1)
                Text(entry.includeInvestment
                    ? "With investment"
                    : "Without investment")
                    .font(.caption2)
                    .foregroundStyle(CalendarAppearance.secondaryInk)
            }

            Spacer(minLength: 4)
            privacyButton
            refreshButton
        }
    }

    @ViewBuilder
    private var privacyButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: ToggleSpendingCalendarPrivacyIntent()) {
                Image(systemName: entry.amountsHidden ? "eye.slash" : "eye")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.secondaryInk)
                    .frame(width: 28, height: 28)
                    .background(
                        Circle().fill(
                            CalendarAppearance.secondaryInk.opacity(0.10)
                        )
                    )
            }
            .buttonStyle(.plain)
        }
    }

    @ViewBuilder
    private var refreshButton: some View {
        if #available(iOS 17.0, *) {
            Button(intent: RefreshSpendingCalendarIntent()) {
                Image(systemName: "arrow.clockwise")
                    .font(.system(size: 12, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.accent)
                    .frame(width: 28, height: 28)
                    .background(
                        Circle().fill(CalendarAppearance.accent.opacity(0.16))
                    )
            }
            .buttonStyle(.plain)
        }
    }

    private var monthSelector: some View {
        HStack(spacing: 5) {
            monthArrow(
                targetMonthKey: entry.previousMonthKey,
                systemName: "chevron.left"
            )
            Spacer(minLength: 2)
            Text(month?.monthText ?? "Spending calendar")
                .font(.system(.subheadline, design: .rounded).weight(.semibold))
                .foregroundStyle(CalendarAppearance.ink)
                .lineLimit(1)
            Spacer(minLength: 2)
            monthArrow(
                targetMonthKey: entry.nextMonthKey,
                systemName: "chevron.right"
            )
        }
    }

    @ViewBuilder
    private func monthArrow(
        targetMonthKey: Int?,
        systemName: String
    ) -> some View {
        if let targetMonthKey = targetMonthKey {
            if #available(iOS 17.0, *) {
                Button(intent: SelectSpendingCalendarMonthIntent(
                    monthKey: targetMonthKey
                )) {
                    monthArrowImage(systemName: systemName, enabled: true)
                }
                .buttonStyle(.plain)
            } else {
                monthArrowImage(systemName: systemName, enabled: false)
            }
        } else {
            monthArrowImage(systemName: systemName, enabled: false)
        }
    }

    private func monthArrowImage(
        systemName: String,
        enabled: Bool
    ) -> some View {
        Image(systemName: systemName)
            .font(.system(size: 10, weight: .bold))
            .foregroundStyle(
                CalendarAppearance.secondaryInk.opacity(enabled ? 1 : 0.24)
            )
            .frame(width: 24, height: 24)
            .background(
                Circle().fill(CalendarAppearance.secondaryInk.opacity(0.08))
            )
    }

    private var summaryRow: some View {
        HStack(spacing: 10) {
            summaryMetric(
                title: "SPENT",
                value: hiddenValue(summary?.expenseText ?? "RM 0.00"),
                color: CalendarAppearance.negative
            )
            Divider().overlay(CalendarAppearance.divider)
            summaryMetric(
                title: "BUDGET",
                value: hiddenValue(summary?.budgetLimitText ?? "No budget"),
                color: CalendarAppearance.ink
            )
            Divider().overlay(CalendarAppearance.divider)
            summaryMetric(
                title: "DAILY",
                value: entry.amountsHidden
                    ? "••••"
                    : dailyAllowanceText,
                color: CalendarAppearance.accent
            )
        }
        .frame(height: 34)
    }

    private func summaryMetric(
        title: String,
        value: String,
        color: Color
    ) -> some View {
        VStack(alignment: .leading, spacing: 1) {
            Text(title)
                .font(.system(size: 8, weight: .semibold))
                .tracking(0.7)
                .foregroundStyle(CalendarAppearance.secondaryInk)
            Text(value)
                .font(.system(size: 12, weight: .semibold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(color)
                .lineLimit(1)
                .minimumScaleFactor(0.65)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var weekdayRow: some View {
        HStack(spacing: 4) {
            ForEach(weekdayLabels.indices, id: \.self) { index in
                Text(weekdayLabels[index])
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.secondaryInk)
                    .frame(maxWidth: .infinity)
            }
        }
    }

    private var calendarGrid: some View {
        LazyVGrid(columns: columns, spacing: 4) {
            ForEach(0..<42, id: \.self) { index in
                let day = index - leadingBlankCount + 1
                if day >= 1 && day <= dayCount {
                    dayLink(day: day)
                } else {
                    Color.clear.frame(height: 35)
                }
            }
        }
    }

    private func dayLink(day: Int) -> some View {
        Link(destination: transactionUrl(day: day)) {
            dayCell(day: day)
        }
        .buttonStyle(.plain)
    }

    private func dayCell(day: Int) -> some View {
        let snapshot = month?.days?.first(where: { $0.day == day })
        let amountMinor = snapshot?.expenseMinor(
            includeInvestment: entry.includeInvestment
        ) ?? 0
        let level = spendingLevel(amountMinor: amountMinor)
        let isToday = Calendar.current.isDateInToday(dayDate(day: day))

        return VStack(spacing: 1) {
            Text(String(day))
                .font(.system(size: 9, weight: .semibold, design: .rounded))
                .foregroundStyle(CalendarAppearance.ink)
            Text(entry.amountsHidden
                ? (amountMinor > 0 ? "••" : "")
                : compactAmount(amountMinor))
                .font(.system(size: 7, weight: .semibold, design: .rounded))
                .monospacedDigit()
                .foregroundStyle(levelColor(level))
                .lineLimit(1)
                .minimumScaleFactor(0.55)
        }
        .frame(maxWidth: .infinity, minHeight: 35)
        .background(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(levelColor(level).opacity(level == .none ? 0.06 : 0.14))
        )
        .overlay(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .stroke(
                    isToday ? CalendarAppearance.accent : Color.clear,
                    lineWidth: 1.5
                )
        )
    }

    private var footer: some View {
        HStack(spacing: 12) {
            if entry.loadState != .loaded {
                Text(entry.loadState.message)
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.negative)
            } else if month?.days == nil {
                Text("Open app to sync calendar")
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.secondaryInk)
            } else if summary?.hasBudget == true {
                legendDot(color: CalendarAppearance.positive, text: "Below")
                legendDot(color: warningColor, text: "Near")
                legendDot(color: CalendarAppearance.negative, text: "Over")
            } else {
                Text("Set a monthly budget to enable spending colours")
                    .font(.system(size: 8, weight: .semibold))
                    .foregroundStyle(CalendarAppearance.secondaryInk)
            }
            Spacer(minLength: 2)
        }
    }

    private func legendDot(color: Color, text: String) -> some View {
        HStack(spacing: 3) {
            Circle().fill(color).frame(width: 5, height: 5)
            Text(text)
                .font(.system(size: 8, weight: .medium))
                .foregroundStyle(CalendarAppearance.secondaryInk)
        }
    }

    private var warningColor: Color {
        Color(red: 0.91, green: 0.61, blue: 0.10)
    }

    private var dailyAllowanceText: String {
        guard dailyAllowanceMinor > 0 else { return "No budget" }
        return formatMinor(Int64(dailyAllowanceMinor.rounded()))
    }

    private func spendingLevel(amountMinor: Int64) -> CalendarSpendingLevel {
        guard amountMinor > 0 else { return .none }
        guard dailyAllowanceMinor > 0 else { return .none }
        let ratio = Double(amountMinor) / dailyAllowanceMinor
        if ratio > 1 { return .over }
        if ratio >= 0.75 { return .near }
        return .below
    }

    private func levelColor(_ level: CalendarSpendingLevel) -> Color {
        switch level {
        case .none: return CalendarAppearance.secondaryInk
        case .below: return CalendarAppearance.positive
        case .near: return warningColor
        case .over: return CalendarAppearance.negative
        }
    }

    private func hiddenValue(_ value: String) -> String {
        entry.amountsHidden ? "••••••" : value
    }

    private func compactAmount(_ minor: Int64) -> String {
        guard minor > 0 else { return "" }
        let amount = Double(minor) / 100
        let symbol = CalendarAppearance.currencySymbol
        if amount >= 1_000 {
            return String(format: "%@%.1fk", symbol, amount / 1_000)
        }
        return String(format: "%@%.0f", symbol, amount)
    }

    private func formatMinor(_ minor: Int64) -> String {
        String(
            format: "%@ %.2f",
            CalendarAppearance.currencySymbol,
            Double(minor) / 100
        )
    }

    private func dayDate(day: Int) -> Date {
        let monthKey = month?.monthKey ?? entry.snapshot.resolvedCurrentMonthKey
        return Calendar.current.date(from: DateComponents(
            year: monthKey / 100,
            month: monthKey % 100,
            day: day
        )) ?? monthDate
    }

    private func transactionUrl(day: Int) -> URL {
        let formatter = DateFormatter()
        formatter.calendar = Calendar(identifier: .gregorian)
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.dateFormat = "yyyy-MM-dd"
        let dateText = formatter.string(from: dayDate(day: day))
        return URL(
            string: "com.faikalizham.financial-tracker://transactions?date=\(dateText)"
        )!
    }
}

struct FinancialTrackerSpendingCalendarWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(
            kind: SpendingCalendarSettings.widgetKind,
            provider: SpendingCalendarProvider()
        ) { entry in
            SpendingCalendarWidgetView(entry: entry)
        }
        .configurationDisplayName("Spending Calendar")
        .description("See daily spending against your monthly budget.")
        .supportedFamilies([.systemLarge])
    }
}

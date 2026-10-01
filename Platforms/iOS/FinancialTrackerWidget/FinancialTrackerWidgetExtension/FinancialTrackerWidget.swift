import SwiftUI
import WidgetKit

private enum WidgetSettings {
    static let appGroupIdentifier = "group.com.faikalizham.financial-tracker"
    static let snapshotFileName = "financial_tracker_widget.json"
    static let widgetKind = "FinancialTrackerWidget"
}

private enum WidgetAppearance {
    static let background = Color(red: 0.985, green: 0.972, blue: 0.948)
    static let accent = Color(red: 0.392, green: 0.847, blue: 0.894)
    static let ink = Color(red: 0.095, green: 0.082, blue: 0.180)
    static let secondaryInk = Color(red: 0.390, green: 0.370, blue: 0.440)
    static let positive = Color(red: 0.075, green: 0.550, blue: 0.400)
    static let negative = Color(red: 0.820, green: 0.230, blue: 0.340)
}

private struct FinancialTrackerSnapshot: Codable {
    let version: Int
    let monthText: String
    let availableText: String
    let incomeText: String
    let expenseText: String
    let transactionCount: Int
    let updatedAtUnixSeconds: Int64

    static var placeholder: FinancialTrackerSnapshot {
        FinancialTrackerSnapshot(
            version: 1,
            monthText: Date().formatted(.dateTime.month(.wide).year()),
            availableText: "RM 0.00",
            incomeText: "RM 0.00",
            expenseText: "RM 0.00",
            transactionCount: 0,
            updatedAtUnixSeconds: Int64(Date().timeIntervalSince1970)
        )
    }

    var updatedText: String {
        let updatedAt = Date(
            timeIntervalSince1970: TimeInterval(updatedAtUnixSeconds)
        )
        return "Updated \(updatedAt.formatted(date: .omitted, time: .shortened))"
    }
}

private enum SharedWidgetStorage {
    static func loadSnapshot() -> FinancialTrackerSnapshot {
        guard
            let containerUrl = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: WidgetSettings.appGroupIdentifier
            )
        else {
            return .placeholder
        }

        let snapshotUrl = containerUrl.appendingPathComponent(
            WidgetSettings.snapshotFileName,
            isDirectory: false
        )
        guard
            let data = try? Data(contentsOf: snapshotUrl),
            let snapshot = try? JSONDecoder().decode(
                FinancialTrackerSnapshot.self,
                from: data
            ),
            snapshot.version == 1
        else {
            return .placeholder
        }

        return snapshot
    }
}

private struct FinancialTrackerEntry: TimelineEntry {
    let date: Date
    let snapshot: FinancialTrackerSnapshot
}

private struct FinancialTrackerProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinancialTrackerEntry {
        FinancialTrackerEntry(
            date: Date(),
            snapshot: .placeholder
        )
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialTrackerEntry) -> Void
    ) {
        completion(FinancialTrackerEntry(
            date: Date(),
            snapshot: context.isPreview
                ? .placeholder
                : SharedWidgetStorage.loadSnapshot()
        ))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialTrackerEntry>) -> Void
    ) {
        let now = Date()
        let entry = FinancialTrackerEntry(
            date: now,
            snapshot: SharedWidgetStorage.loadSnapshot()
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
        VStack(alignment: .leading, spacing: family == .systemSmall ? 8 : 10) {
            header

            if family == .systemSmall {
                smallSummary
            } else {
                mediumSummary
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }

    private var header: some View {
        HStack(spacing: 8) {
            ZStack {
                Circle()
                    .fill(WidgetAppearance.accent.opacity(0.20))

                Image(systemName: "chart.line.uptrend.xyaxis")
                    .font(.system(size: 15, weight: .semibold))
                    .foregroundStyle(WidgetAppearance.accent)
            }
            .frame(width: 32, height: 32)

            VStack(alignment: .leading, spacing: 1) {
                Text("Financial Tracker")
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .foregroundStyle(WidgetAppearance.ink)
                    .lineLimit(1)

                if family != .systemSmall {
                    Text(entry.snapshot.monthText)
                        .font(.caption2)
                        .foregroundStyle(WidgetAppearance.secondaryInk)
                        .lineLimit(1)
                }
            }
        }
    }

    private var smallSummary: some View {
        VStack(alignment: .leading, spacing: 4) {
            Spacer(minLength: 0)

            Text("AVAILABLE")
                .font(.caption2.weight(.semibold))
                .tracking(0.8)
                .foregroundStyle(WidgetAppearance.secondaryInk)

            Text(entry.snapshot.availableText)
                .font(.system(.title3, design: .rounded).weight(.bold))
                .monospacedDigit()
                .foregroundStyle(WidgetAppearance.ink)
                .lineLimit(1)
                .minimumScaleFactor(0.58)

            Text(activityText)
                .font(.caption2)
                .foregroundStyle(WidgetAppearance.secondaryInk)
                .lineLimit(1)
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

                    Text(entry.snapshot.availableText)
                        .font(.system(.title2, design: .rounded).weight(.bold))
                        .monospacedDigit()
                        .foregroundStyle(WidgetAppearance.ink)
                        .lineLimit(1)
                        .minimumScaleFactor(0.65)
                }

                Spacer(minLength: 10)

                Text(entry.snapshot.updatedText)
                    .font(.caption2)
                    .foregroundStyle(WidgetAppearance.secondaryInk)
                    .lineLimit(1)
            }

            HStack(spacing: 10) {
                metric(
                    title: "INCOME",
                    value: entry.snapshot.incomeText,
                    color: WidgetAppearance.positive
                )
                metric(
                    title: "EXPENSE",
                    value: entry.snapshot.expenseText,
                    color: WidgetAppearance.negative
                )
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

    private var activityText: String {
        switch entry.snapshot.transactionCount {
        case 0:
            return "No activity yet"
        case 1:
            return "1 transaction"
        default:
            return "\(entry.snapshot.transactionCount) transactions"
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
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

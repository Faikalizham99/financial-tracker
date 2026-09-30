import SwiftUI
import WidgetKit

private enum WidgetAppearance {
    static let background = Color(red: 0.985, green: 0.972, blue: 0.948)
    static let accent = Color(red: 0.392, green: 0.847, blue: 0.894)
    static let ink = Color(red: 0.095, green: 0.082, blue: 0.180)
    static let secondaryInk = Color(red: 0.390, green: 0.370, blue: 0.440)
}

private struct FinancialTrackerEntry: TimelineEntry {
    let date: Date
}

private struct FinancialTrackerProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinancialTrackerEntry {
        FinancialTrackerEntry(date: Date())
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialTrackerEntry) -> Void
    ) {
        completion(FinancialTrackerEntry(date: Date()))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialTrackerEntry>) -> Void
    ) {
        let entry = FinancialTrackerEntry(date: Date())
        completion(Timeline(entries: [entry], policy: .never))
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
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                ZStack {
                    Circle()
                        .fill(WidgetAppearance.accent.opacity(0.20))

                    Image(systemName: "chart.line.uptrend.xyaxis")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(WidgetAppearance.accent)
                }
                .frame(width: 34, height: 34)

                if family != .systemSmall {
                    Text("Financial Tracker")
                        .font(.system(.headline, design: .rounded).weight(.semibold))
                        .foregroundStyle(WidgetAppearance.ink)
                        .lineLimit(1)
                }
            }

            Spacer(minLength: 0)

            Text(family == .systemSmall ? "Financial\nTracker" : "Widget test")
                .font(.system(family == .systemSmall ? .title3 : .title2, design: .rounded).weight(.bold))
                .foregroundStyle(WidgetAppearance.ink)
                .lineLimit(2)
                .minimumScaleFactor(0.75)

            HStack(spacing: 6) {
                Circle()
                    .fill(WidgetAppearance.accent)
                    .frame(width: 7, height: 7)

                Text("Ready")
                    .font(.system(.caption, design: .rounded).weight(.medium))
                    .foregroundStyle(WidgetAppearance.secondaryInk)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }
}

@main
struct FinancialTrackerWidget: Widget {
    private let kind = "FinancialTrackerWidget"

    var body: some WidgetConfiguration {
        StaticConfiguration(kind: kind, provider: FinancialTrackerProvider()) { entry in
            FinancialTrackerWidgetView(entry: entry)
        }
        .configurationDisplayName("Financial Tracker")
        .description("A quick Financial Tracker status.")
        .supportedFamilies([.systemSmall, .systemMedium])
    }
}

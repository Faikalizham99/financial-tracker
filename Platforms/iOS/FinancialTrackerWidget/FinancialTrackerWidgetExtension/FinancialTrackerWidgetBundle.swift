import SwiftUI
import WidgetKit

@main
struct FinancialTrackerWidgetBundle: WidgetBundle {
    var body: some Widget {
        FinancialTrackerWidget()
        FinancialTrackerAssetsWidget()
        FinancialCalculatorWidget()
        FinancialTrackerSpendingCalendarWidget()
    }
}

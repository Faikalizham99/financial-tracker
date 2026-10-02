import AppIntents
import Foundation
import SwiftUI
import WidgetKit

private enum CalculatorWidgetSettings {
    static let appGroupIdentifiers = [
        "group.com.faikalizham.financial-tracker",
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5"
    ]
    static let stateFileName = "financial_tracker_calculator_widget.json"
    static let widgetKind = "FinancialTrackerCalculatorWidget"
}

private struct CalculatorWidgetState: Codable {
    var currentInput = "0"
    var accumulator: Decimal?
    var pendingOperator: String?
    var isWaitingForOperand = false
    var expressionText = ""
    var hasError = false

    static let initial = CalculatorWidgetState()
}

private enum CalculatorWidgetStorage {
    static func load() -> CalculatorWidgetState {
        guard let url = stateUrl(),
              let data = try? Data(contentsOf: url),
              let state = try? JSONDecoder().decode(
                CalculatorWidgetState.self,
                from: data
              ) else {
            return .initial
        }
        return state
    }

    @discardableResult
    static func save(_ state: CalculatorWidgetState) -> Bool {
        guard let url = stateUrl(),
              let data = try? JSONEncoder().encode(state) else {
            return false
        }
        do {
            try data.write(to: url, options: .atomic)
            return true
        } catch {
            return false
        }
    }

    private static func stateUrl() -> URL? {
        for identifier in CalculatorWidgetSettings.appGroupIdentifiers {
            if let containerUrl = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) {
                return containerUrl.appendingPathComponent(
                    CalculatorWidgetSettings.stateFileName,
                    isDirectory: false
                )
            }
        }
        return nil
    }
}

private enum CalculatorEngine {
    private static let maximumInputDigits = 12
    private static let maximumDisplayFractionDigits = 6

    static func apply(_ key: String, to state: inout CalculatorWidgetState) {
        switch key {
        case "clear":
            state = .initial
        case "backspace":
            backspace(&state)
        case "sign":
            toggleSign(&state)
        case "percent":
            applyPercent(&state)
        case "+", "-", "*", "/":
            selectOperator(key, state: &state)
        case "equals":
            complete(&state)
        case ".":
            appendDecimal(&state)
        default:
            if key.count == 1 && key.first?.isNumber == true {
                appendDigit(key, state: &state)
            }
        }
    }

    static func displayText(_ state: CalculatorWidgetState) -> String {
        if state.hasError { return "Error" }
        return groupedText(decimal(from: state.currentInput))
    }

    static func transactionAmountText(_ state: CalculatorWidgetState) -> String? {
        guard !state.hasError else { return nil }
        let value = decimal(from: state.currentInput)
        let positiveValue = value < 0 ? -value : value
        guard positiveValue > 0 else { return nil }
        return plainText(positiveValue, maximumFractionDigits: 2)
    }

    private static func appendDigit(
        _ digit: String,
        state: inout CalculatorWidgetState
    ) {
        resetErrorIfNeeded(&state)
        if state.isWaitingForOperand {
            if state.pendingOperator == nil {
                state.expressionText = ""
            }
            state.currentInput = digit
            state.isWaitingForOperand = false
            updateExpression(&state)
            return
        }

        let digitCount = state.currentInput.filter(\.isNumber).count
        if digitCount >= maximumInputDigits { return }
        if let decimalIndex = state.currentInput.firstIndex(of: "."),
           state.currentInput.distance(
               from: state.currentInput.index(after: decimalIndex),
               to: state.currentInput.endIndex
           ) >= maximumDisplayFractionDigits {
            return
        }
        state.currentInput = state.currentInput == "0"
            ? digit
            : state.currentInput + digit
        updateExpression(&state)
    }

    private static func appendDecimal(_ state: inout CalculatorWidgetState) {
        resetErrorIfNeeded(&state)
        if state.isWaitingForOperand {
            if state.pendingOperator == nil {
                state.expressionText = ""
            }
            state.currentInput = "0."
            state.isWaitingForOperand = false
            updateExpression(&state)
            return
        }
        guard !state.currentInput.contains(".") else { return }
        state.currentInput += "."
        updateExpression(&state)
    }

    private static func backspace(_ state: inout CalculatorWidgetState) {
        if state.hasError {
            state = .initial
            return
        }
        if state.isWaitingForOperand, state.pendingOperator != nil {
            state.currentInput = plainText(state.accumulator ?? 0)
            state.accumulator = nil
            state.pendingOperator = nil
            state.isWaitingForOperand = false
            state.expressionText = ""
            return
        }
        if state.isWaitingForOperand {
            state.isWaitingForOperand = false
            state.expressionText = ""
        }
        if state.currentInput.count <= 1 ||
            (state.currentInput.hasPrefix("-") && state.currentInput.count == 2) {
            state.currentInput = "0"
        } else {
            state.currentInput.removeLast()
        }
        updateExpression(&state)
    }

    private static func toggleSign(_ state: inout CalculatorWidgetState) {
        resetErrorIfNeeded(&state)
        if state.isWaitingForOperand {
            if state.pendingOperator != nil {
                state.currentInput = "0"
            }
            state.isWaitingForOperand = false
        }
        let value = decimal(from: state.currentInput)
        state.currentInput = plainText(-value)
        updateExpression(&state)
    }

    private static func applyPercent(_ state: inout CalculatorWidgetState) {
        resetErrorIfNeeded(&state)
        if state.isWaitingForOperand, state.pendingOperator == nil {
            state.isWaitingForOperand = false
        }
        guard !state.isWaitingForOperand else { return }
        let enteredValue = decimal(from: state.currentInput)
        let percentage: Decimal
        if let accumulator = state.accumulator,
           let operation = state.pendingOperator,
           operation == "+" || operation == "-" {
            percentage = accumulator * enteredValue / 100
        } else {
            percentage = enteredValue / 100
        }
        state.currentInput = plainText(percentage)
        updateExpression(&state, rightSuffix: "%")
    }

    private static func selectOperator(
        _ operation: String,
        state: inout CalculatorWidgetState
    ) {
        resetErrorIfNeeded(&state)
        let currentValue = decimal(from: state.currentInput)
        if let pending = state.pendingOperator,
           let accumulator = state.accumulator,
           !state.isWaitingForOperand {
            guard let result = calculate(accumulator, currentValue, pending) else {
                showError(&state)
                return
            }
            state.currentInput = plainText(result)
            state.accumulator = result
        } else if state.accumulator == nil || state.pendingOperator == nil {
            state.accumulator = currentValue
        }
        state.pendingOperator = operation
        state.isWaitingForOperand = true
        state.expressionText =
            "\(groupedText(state.accumulator ?? currentValue)) \(symbol(operation))"
    }

    private static func complete(_ state: inout CalculatorWidgetState) {
        guard !state.hasError,
              !state.isWaitingForOperand,
              let operation = state.pendingOperator,
              let accumulator = state.accumulator else { return }
        let right = decimal(from: state.currentInput)
        guard let result = calculate(accumulator, right, operation) else {
            showError(&state)
            return
        }
        state.expressionText =
            "\(groupedText(accumulator)) \(symbol(operation)) " +
            "\(groupedText(right)) ="
        state.currentInput = plainText(result)
        state.accumulator = nil
        state.pendingOperator = nil
        state.isWaitingForOperand = true
    }

    private static func calculate(
        _ left: Decimal,
        _ right: Decimal,
        _ operation: String
    ) -> Decimal? {
        let result: Decimal
        switch operation {
        case "+": result = left + right
        case "-": result = left - right
        case "*": result = left * right
        case "/":
            guard right != 0 else { return nil }
            result = left / right
        default: return left
        }
        return rounded(result)
    }

    private static func updateExpression(
        _ state: inout CalculatorWidgetState,
        rightSuffix: String = ""
    ) {
        guard let operation = state.pendingOperator,
              let accumulator = state.accumulator else { return }
        state.expressionText =
            "\(groupedText(accumulator)) \(symbol(operation)) " +
            "\(groupedText(decimal(from: state.currentInput)))\(rightSuffix)"
    }

    private static func resetErrorIfNeeded(_ state: inout CalculatorWidgetState) {
        if state.hasError { state = .initial }
    }

    private static func showError(_ state: inout CalculatorWidgetState) {
        state = .initial
        state.hasError = true
        state.expressionText = "Cannot divide by zero"
    }

    private static func decimal(from text: String) -> Decimal {
        Decimal(
            string: text.trimmingCharacters(in: CharacterSet(charactersIn: ".")),
            locale: Locale(identifier: "en_US_POSIX")
        ) ?? 0
    }

    private static func rounded(_ value: Decimal) -> Decimal {
        var source = value
        var result = Decimal()
        NSDecimalRound(
            &result,
            &source,
            maximumDisplayFractionDigits,
            .bankers
        )
        return result
    }

    private static func plainText(
        _ value: Decimal,
        maximumFractionDigits: Int = 6
    ) -> String {
        formatter(
            usesGroupingSeparator: false,
            maximumFractionDigits: maximumFractionDigits
        ).string(from: NSDecimalNumber(decimal: rounded(value))) ?? "0"
    }

    private static func groupedText(_ value: Decimal) -> String {
        formatter(
            usesGroupingSeparator: true,
            maximumFractionDigits: maximumDisplayFractionDigits
        ).string(from: NSDecimalNumber(decimal: rounded(value))) ?? "0"
    }

    private static func formatter(
        usesGroupingSeparator: Bool,
        maximumFractionDigits: Int
    ) -> NumberFormatter {
        let formatter = NumberFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.numberStyle = .decimal
        formatter.usesGroupingSeparator = usesGroupingSeparator
        formatter.minimumFractionDigits = 0
        formatter.maximumFractionDigits = maximumFractionDigits
        return formatter
    }

    private static func symbol(_ operation: String) -> String {
        switch operation {
        case "*": return "×"
        case "/": return "÷"
        case "-": return "−"
        default: return "+"
        }
    }
}

@available(iOS 17.0, *)
struct CalculatorKeyIntent: AppIntent {
    static var title: LocalizedStringResource = "Calculator Key"
    static var openAppWhenRun = false

    @Parameter(title: "Key")
    var key: String

    init() { key = "clear" }
    init(key: String) { self.key = key }

    func perform() async throws -> some IntentResult {
        var state = CalculatorWidgetStorage.load()
        CalculatorEngine.apply(key, to: &state)
        CalculatorWidgetStorage.save(state)
        WidgetCenter.shared.reloadTimelines(
            ofKind: CalculatorWidgetSettings.widgetKind
        )
        return .result()
    }
}

private struct FinancialCalculatorEntry: TimelineEntry {
    let date: Date
    let state: CalculatorWidgetState
}

private struct FinancialCalculatorProvider: TimelineProvider {
    func placeholder(in context: Context) -> FinancialCalculatorEntry {
        FinancialCalculatorEntry(date: Date(), state: .initial)
    }

    func getSnapshot(
        in context: Context,
        completion: @escaping (FinancialCalculatorEntry) -> Void
    ) {
        SharedWidgetAppearance.reload()
        completion(FinancialCalculatorEntry(
            date: Date(),
            state: context.isPreview ? .initial : CalculatorWidgetStorage.load()
        ))
    }

    func getTimeline(
        in context: Context,
        completion: @escaping (Timeline<FinancialCalculatorEntry>) -> Void
    ) {
        SharedWidgetAppearance.reload()
        let entry = FinancialCalculatorEntry(
            date: Date(),
            state: CalculatorWidgetStorage.load()
        )
        completion(Timeline(
            entries: [entry],
            policy: .after(Date().addingTimeInterval(30 * 60))
        ))
    }
}

private struct CalculatorKey: Identifiable {
    let id: String
    let label: String
    let systemImage: String?
    let role: Role

    enum Role {
        case number
        case utility
        case operation
        case equals
        case destructive
    }

    init(
        _ id: String,
        label: String? = nil,
        systemImage: String? = nil,
        role: Role
    ) {
        self.id = id
        self.label = label ?? id
        self.systemImage = systemImage
        self.role = role
    }
}

private struct FinancialCalculatorView: View {
    let entry: FinancialCalculatorEntry

    private let keyRows: [[CalculatorKey]] = [
        [
            CalculatorKey("clear", label: "AC", role: .destructive),
            CalculatorKey("sign", label: "±", role: .utility),
            CalculatorKey("percent", label: "%", role: .utility),
            CalculatorKey("/", label: "÷", role: .operation)
        ],
        [
            CalculatorKey("7", role: .number),
            CalculatorKey("8", role: .number),
            CalculatorKey("9", role: .number),
            CalculatorKey("*", label: "×", role: .operation)
        ],
        [
            CalculatorKey("4", role: .number),
            CalculatorKey("5", role: .number),
            CalculatorKey("6", role: .number),
            CalculatorKey("-", label: "−", role: .operation)
        ],
        [
            CalculatorKey("1", role: .number),
            CalculatorKey("2", role: .number),
            CalculatorKey("3", role: .number),
            CalculatorKey("+", role: .operation)
        ],
        [
            CalculatorKey("0", role: .number),
            CalculatorKey(
                "backspace",
                systemImage: "delete.left",
                role: .utility
            ),
            CalculatorKey(".", role: .number),
            CalculatorKey("equals", label: "=", role: .equals)
        ]
    ]

    var body: some View {
        if #available(iOS 17.0, *) {
            content
                .containerBackground(
                    SharedWidgetAppearance.background,
                    for: .widget
                )
        } else {
            content
                .padding()
                .background(SharedWidgetAppearance.background)
        }
    }

    private var content: some View {
        VStack(spacing: 8) {
            header
            display
            keypad
            addTransactionLink
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }

    private var header: some View {
        HStack(spacing: 8) {
            Image(systemName: "function")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(SharedWidgetAppearance.accent)
                .frame(width: 30, height: 30)
                .background(
                    RoundedRectangle(cornerRadius: 9, style: .continuous)
                        .fill(SharedWidgetAppearance.accent.opacity(0.16))
                )
            VStack(alignment: .leading, spacing: 1) {
                Text("Financial Calculator")
                    .font(.system(.headline, design: .rounded).weight(.semibold))
                    .foregroundStyle(SharedWidgetAppearance.ink)
                Text("Tap = to finish · operators calculate chains")
                    .font(.system(size: 9))
                    .foregroundStyle(SharedWidgetAppearance.secondaryInk)
            }
            Spacer(minLength: 4)
            Text(SharedWidgetAppearance.currencySymbol)
                .font(.system(size: 11, weight: .bold, design: .rounded))
                .foregroundStyle(SharedWidgetAppearance.accent)
                .padding(.horizontal, 9)
                .frame(height: 26)
                .background(Capsule().fill(
                    SharedWidgetAppearance.accent.opacity(0.14)
                ))
        }
    }

    private var display: some View {
        VStack(alignment: .trailing, spacing: 1) {
            Text(entry.state.expressionText.isEmpty
                ? "Ready"
                : entry.state.expressionText)
                .font(.system(size: 10, weight: .medium))
                .foregroundStyle(entry.state.hasError
                    ? SharedWidgetAppearance.negative
                    : SharedWidgetAppearance.secondaryInk)
                .lineLimit(1)
                .minimumScaleFactor(0.65)
                .frame(maxWidth: .infinity, alignment: .trailing)
            HStack(alignment: .firstTextBaseline, spacing: 7) {
                Text(SharedWidgetAppearance.currencySymbol)
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundStyle(SharedWidgetAppearance.secondaryInk)
                Text(CalculatorEngine.displayText(entry.state))
                    .font(.system(size: 30, weight: .bold, design: .rounded))
                    .monospacedDigit()
                    .foregroundStyle(entry.state.hasError
                        ? SharedWidgetAppearance.negative
                        : SharedWidgetAppearance.ink)
                    .lineLimit(1)
                    .minimumScaleFactor(0.42)
            }
            .frame(maxWidth: .infinity, alignment: .trailing)
        }
        .padding(.horizontal, 12)
        .padding(.vertical, 6)
        .background(
            RoundedRectangle(cornerRadius: 13, style: .continuous)
                .fill(SharedWidgetAppearance.controlSurface)
        )
    }

    private var keypad: some View {
        VStack(spacing: 5) {
            ForEach(Array(keyRows.enumerated()), id: \.offset) { _, row in
                HStack(spacing: 6) {
                    ForEach(row) { key in
                        keyButton(key)
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func keyButton(_ key: CalculatorKey) -> some View {
        if #available(iOS 17.0, *) {
            Button(intent: CalculatorKeyIntent(key: key.id)) {
                keyLabel(key)
            }
            .buttonStyle(.plain)
        } else {
            keyLabel(key).opacity(0.55)
        }
    }

    private func keyLabel(_ key: CalculatorKey) -> some View {
        Group {
            if let systemImage = key.systemImage {
                Image(systemName: systemImage)
            } else {
                Text(key.label)
            }
        }
        .font(.system(size: 17, weight: .semibold, design: .rounded))
        .foregroundStyle(keyForeground(key.role))
        .frame(maxWidth: .infinity)
        .frame(height: 38)
        .background(
            RoundedRectangle(cornerRadius: 10, style: .continuous)
                .fill(keyBackground(key.role))
        )
    }

    private func keyForeground(_ role: CalculatorKey.Role) -> Color {
        switch role {
        case .equals: return SharedWidgetAppearance.accentForeground
        case .operation: return SharedWidgetAppearance.accent
        case .destructive: return SharedWidgetAppearance.negative
        default: return SharedWidgetAppearance.ink
        }
    }

    private func keyBackground(_ role: CalculatorKey.Role) -> Color {
        switch role {
        case .equals: return SharedWidgetAppearance.accent
        case .operation: return SharedWidgetAppearance.accent.opacity(0.16)
        case .destructive: return SharedWidgetAppearance.negative.opacity(0.12)
        case .utility: return SharedWidgetAppearance.secondaryInk.opacity(0.10)
        case .number: return SharedWidgetAppearance.controlSurface
        }
    }

    private var addTransactionLink: some View {
        Link(destination: addTransactionUrl) {
            HStack(spacing: 6) {
                Image(systemName: "plus.circle.fill")
                Text("Add result as transaction")
            }
            .font(.system(size: 11, weight: .semibold))
            .foregroundStyle(SharedWidgetAppearance.accent)
            .frame(maxWidth: .infinity)
            .frame(height: 28)
            .background(
                Capsule().fill(SharedWidgetAppearance.accent.opacity(0.12))
            )
        }
    }

    private var addTransactionUrl: URL {
        guard let amount = CalculatorEngine.transactionAmountText(entry.state)
        else {
            return URL(
                string: "com.faikalizham.financial-tracker://add-transaction"
            )!
        }
        return URL(
            string: "com.faikalizham.financial-tracker://add-transaction?amount=\(amount)"
        )!
    }
}

struct FinancialCalculatorWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(
            kind: CalculatorWidgetSettings.widgetKind,
            provider: FinancialCalculatorProvider()
        ) { entry in
            FinancialCalculatorView(entry: entry)
        }
        .configurationDisplayName("Financial Calculator")
        .description("Calculate money and send the result to a transaction.")
        .supportedFamilies([.systemLarge])
    }
}

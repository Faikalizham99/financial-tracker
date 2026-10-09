import AppIntents
import CryptoKit
import Foundation
import SQLite3

private enum PendingInboxConfiguration {
    static let appGroupIdentifiers = [
        "group.com.faikalizham.financial-tracker",
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5",
    ]
    static let databaseFileName = "pending-transactions.db3"
}

private enum PendingInboxError: LocalizedError {
    case appGroupUnavailable
    case database(Int32)

    var errorDescription: String? {
        switch self {
        case .appGroupUnavailable:
            "The shared inbox is unavailable. Check Financial Tracker’s App Group signing."
        case .database:
            "The pending transaction could not be saved. Please try again."
        }
    }
}

private struct PendingSuggestion: Sendable {
    let amountMinor: Int64?
    let currencyCode: String
    let description: String
    let type: String
    let category: String
    let paymentMethod: String
    let status: String
}

private final class PendingInboxDatabase {
    private var handle: OpaquePointer?
    private let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)

    init() throws {
        let fileURL = try Self.databaseURL()
        let directoryURL = fileURL.deletingLastPathComponent()
        try FileManager.default.createDirectory(
            at: directoryURL,
            withIntermediateDirectories: true
        )
        try? FileManager.default.setAttributes(
            [.protectionKey: FileProtectionType.completeUntilFirstUserAuthentication],
            ofItemAtPath: directoryURL.path
        )

        let flags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX
        guard sqlite3_open_v2(fileURL.path, &handle, flags, nil) == SQLITE_OK else {
            let code = handle.map(sqlite3_extended_errcode) ?? SQLITE_CANTOPEN
            if let handle { sqlite3_close(handle) }
            handle = nil
            throw PendingInboxError.database(code)
        }

        sqlite3_busy_timeout(handle, 5_000)
        try execute("PRAGMA journal_mode=WAL")
        try execute("PRAGMA synchronous=FULL")
        try execute(
            """
            CREATE TABLE IF NOT EXISTS PendingTransactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CaptureKey TEXT NOT NULL,
                SourceApp TEXT,
                NotificationTitle TEXT,
                NotificationSubtitle TEXT,
                NotificationMessage TEXT,
                AmountMinor INTEGER,
                CurrencyCode TEXT,
                SuggestedDescription TEXT,
                SuggestedType TEXT,
                SuggestedCategory TEXT,
                SuggestedPaymentMethod TEXT,
                ReceivedAtUnixMs INTEGER NOT NULL,
                CreatedAtUnixMs INTEGER NOT NULL,
                ParseStatus TEXT
            )
            """
        )
        try execute(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_PendingTransactions_CaptureKey " +
            "ON PendingTransactions (CaptureKey)"
        )
    }

    deinit {
        if let handle { sqlite3_close(handle) }
    }

    func save(
        captureKey: String,
        source: String,
        title: String,
        subtitle: String,
        message: String,
        suggestion: PendingSuggestion,
        receivedAtUnixMs: Int64
    ) throws -> Bool {
        try execute("BEGIN IMMEDIATE")
        do {
            let sql =
                "INSERT OR IGNORE INTO PendingTransactions (" +
                "CaptureKey,SourceApp,NotificationTitle,NotificationSubtitle," +
                "NotificationMessage,AmountMinor,CurrencyCode,SuggestedDescription," +
                "SuggestedType,SuggestedCategory,SuggestedPaymentMethod," +
                "ReceivedAtUnixMs,CreatedAtUnixMs,ParseStatus) " +
                "VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?)"
            var statement: OpaquePointer?
            guard sqlite3_prepare_v2(handle, sql, -1, &statement, nil) == SQLITE_OK,
                  let statement else {
                throw databaseError()
            }
            defer { sqlite3_finalize(statement) }

            try bind(captureKey, statement, 1)
            try bind(source, statement, 2)
            try bind(title, statement, 3)
            try bind(subtitle, statement, 4)
            try bind(message, statement, 5)
            if let amount = suggestion.amountMinor {
                sqlite3_bind_int64(statement, 6, amount)
            } else {
                sqlite3_bind_null(statement, 6)
            }
            try bind(suggestion.currencyCode, statement, 7)
            try bind(suggestion.description, statement, 8)
            try bind(suggestion.type, statement, 9)
            try bind(suggestion.category, statement, 10)
            try bind(suggestion.paymentMethod, statement, 11)
            sqlite3_bind_int64(statement, 12, receivedAtUnixMs)
            sqlite3_bind_int64(
                statement,
                13,
                Int64(Date().timeIntervalSince1970 * 1_000)
            )
            try bind(suggestion.status, statement, 14)

            guard sqlite3_step(statement) == SQLITE_DONE else {
                throw databaseError()
            }
            let inserted = sqlite3_changes(handle) > 0
            try execute("COMMIT")
            return inserted
        } catch {
            try? execute("ROLLBACK")
            throw error
        }
    }

    private static func databaseURL() throws -> URL {
        for identifier in PendingInboxConfiguration.appGroupIdentifiers {
            if let container = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) {
                return container
                    .appendingPathComponent("Library/FinancialTracker", isDirectory: true)
                    .appendingPathComponent(
                        PendingInboxConfiguration.databaseFileName,
                        isDirectory: false
                    )
            }
        }
        throw PendingInboxError.appGroupUnavailable
    }

    private func execute(_ sql: String) throws {
        guard sqlite3_exec(handle, sql, nil, nil, nil) == SQLITE_OK else {
            throw databaseError()
        }
    }

    private func bind(_ value: String, _ statement: OpaquePointer, _ index: Int32) throws {
        let result = value.withCString {
            sqlite3_bind_text(statement, index, $0, -1, transient)
        }
        guard result == SQLITE_OK else { throw PendingInboxError.database(result) }
    }

    private func databaseError() -> PendingInboxError {
        guard let handle else { return .database(SQLITE_CANTOPEN) }
        return .database(sqlite3_extended_errcode(handle))
    }
}

private enum NotificationTransactionParser {
    static func parse(
        source: String,
        title: String,
        subtitle: String,
        message: String,
        explicitAmount: Double?,
        explicitDescription: String?
    ) -> PendingSuggestion {
        let combined = [title, subtitle, message]
            .filter { !$0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            .joined(separator: " · ")
        let searchable = "\(source) \(combined)".lowercased()
        let amountMinor = explicitAmount.flatMap(toMinorUnits) ?? extractAmount(combined)
        let isIncome = containsAny(
            searchable,
            ["credited", "received", "cashback", "refund", "salary", "deposit"]
        ) && !containsAny(searchable, ["debited", "spent", "paid", "purchase"])

        let type = isIncome ? "Income" : "Expense"
        let category = suggestedCategory(searchable, isIncome: isIncome)
        let payment = suggestedPaymentMethod(searchable)
        let description = clean(explicitDescription) ?? merchantDescription(
            title: title,
            message: message,
            source: source
        )
        return PendingSuggestion(
            amountMinor: amountMinor,
            currencyCode: "MYR",
            description: description,
            type: type,
            category: category,
            paymentMethod: payment,
            status: amountMinor == nil ? "NeedsAmount" : "Parsed"
        )
    }

    static func captureKey(
        supplied: String?,
        source: String,
        title: String,
        subtitle: String,
        message: String,
        receivedAt: Date
    ) -> String {
        if let supplied = clean(supplied) { return supplied }
        let minuteBucket = Int64(receivedAt.timeIntervalSince1970 / 60)
        let payload = [source, title, subtitle, message, String(minuteBucket)]
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines).lowercased() }
            .joined(separator: "|")
        let digest = SHA256.hash(data: Data(payload.utf8))
        return digest.map { String(format: "%02x", $0) }.joined()
    }

    private static func extractAmount(_ text: String) -> Int64? {
        let patterns = [
            #"(?i)(?:RM|MYR)\s*([0-9][0-9,]*(?:\.[0-9]{1,2})?)"#,
            #"(?i)(?:amount|paid|spent|purchase|payment)\D{0,12}([0-9][0-9,]*\.[0-9]{2})"#,
        ]
        for pattern in patterns {
            guard let regex = try? NSRegularExpression(pattern: pattern),
                  let match = regex.firstMatch(
                    in: text,
                    range: NSRange(text.startIndex..., in: text)
                  ),
                  match.numberOfRanges > 1,
                  let range = Range(match.range(at: 1), in: text) else {
                continue
            }
            let value = String(text[range]).replacingOccurrences(of: ",", with: "")
            if let decimal = Decimal(string: value), decimal > 0 {
                return NSDecimalNumber(decimal: decimal * 100).int64Value
            }
        }
        return nil
    }

    private static func toMinorUnits(_ value: Double) -> Int64? {
        guard value.isFinite, value > 0 else { return nil }
        return Int64((value * 100).rounded())
    }

    private static func merchantDescription(
        title: String,
        message: String,
        source: String
    ) -> String {
        let candidates = [title, message, source]
        for candidate in candidates {
            let trimmed = candidate.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !trimmed.isEmpty else { continue }
            let withoutAmount = trimmed.replacingOccurrences(
                of: #"(?i)(?:RM|MYR)\s*[0-9][0-9,]*(?:\.[0-9]{1,2})?"#,
                with: "",
                options: .regularExpression
            )
            let cleaned = withoutAmount
                .replacingOccurrences(of: #"\s+"#, with: " ", options: .regularExpression)
                .trimmingCharacters(in: .whitespacesAndNewlines.union(.punctuationCharacters))
            if !cleaned.isEmpty { return String(cleaned.prefix(120)) }
        }
        return "Notification transaction"
    }

    private static func suggestedPaymentMethod(_ text: String) -> String {
        if containsAny(text, ["touch 'n go", "touch n go", "tng ewallet"]) {
            return "Touch N Go eWallet"
        }
        if text.contains("amex") { return "AMEX Maybank" }
        if text.contains("visa maybank") { return "VISA Maybank" }
        if text.contains("maybank") { return "Maybank" }
        if text.contains("cimb") { return "CIMB Bank" }
        if text.contains("bank islam") { return "Bank Islam" }
        if text.contains("gxbank") { return "GXBank" }
        if text.contains("ambank") { return "AmBank" }
        if text.contains("standard chartered") { return "Standard Chartered" }
        if text.contains("ryt") { return "Ryt Bank" }
        return "Others"
    }

    private static func suggestedCategory(_ text: String, isIncome: Bool) -> String {
        if isIncome {
            if text.contains("salary") { return "Salary" }
            if text.contains("cashback") { return "Cashback" }
            if text.contains("refund") { return "Refund" }
            if text.contains("bonus") { return "Bonus" }
            return "Others"
        }
        if containsAny(text, ["food", "restaurant", "cafe", "coffee", "lunch", "dinner"]) {
            return "Food & Drinks"
        }
        if containsAny(text, ["grocery", "groceries", "supermarket"]) { return "Groceries" }
        if containsAny(text, ["petrol", "fuel", "grab", "transport", "parking", "toll"]) {
            return "Transportation"
        }
        if containsAny(text, ["bill", "utility", "electric", "unifi", "internet"]) {
            return "Bills & Utilities"
        }
        if containsAny(text, ["subscription", "netflix", "spotify", "icloud"]) {
            return "Subscription"
        }
        if containsAny(text, ["pharmacy", "clinic", "hospital", "health"]) { return "Health" }
        if containsAny(text, ["shop", "purchase", "store", "mall"]) { return "Shopping" }
        return "Others"
    }

    private static func containsAny(_ text: String, _ values: [String]) -> Bool {
        values.contains { text.contains($0) }
    }

    private static func clean(_ value: String?) -> String? {
        guard let value else { return nil }
        let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}

struct CapturePendingTransactionIntent: AppIntent {
    static var title: LocalizedStringResource = "Capture Pending Transaction"
    static var description = IntentDescription(
        "Save notification details to Financial Tracker for review. This never creates a transaction automatically."
    )
    static var openAppWhenRun = false

    @Parameter(title: "Source App") var sourceApp: String?
    @Parameter(title: "Title") var notificationTitle: String?
    @Parameter(title: "Subtitle") var notificationSubtitle: String?
    @Parameter(title: "Message") var notificationMessage: String?
    @Parameter(title: "Received At") var receivedAt: Date?
    @Parameter(title: "Amount", description: "Optional amount override if your Shortcut extracts it.") var amount: Double?
    @Parameter(title: "Description", description: "Optional merchant or description override.") var transactionDescription: String?
    @Parameter(title: "Capture ID", description: "Optional stable ID used to prevent duplicate captures.") var captureID: String?

    static var parameterSummary: some ParameterSummary {
        Summary("Capture transaction from \(\.$sourceApp)") {
            \.$notificationTitle
            \.$notificationSubtitle
            \.$notificationMessage
            \.$receivedAt
            \.$amount
            \.$transactionDescription
            \.$captureID
        }
    }

    func perform() async throws -> some IntentResult & ProvidesDialog & ReturnsValue<String> {
        let source = sourceApp?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let title = notificationTitle?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let subtitle = notificationSubtitle?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let message = notificationMessage?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
        let date = receivedAt ?? Date()
        let suggestion = NotificationTransactionParser.parse(
            source: source,
            title: title,
            subtitle: subtitle,
            message: message,
            explicitAmount: amount,
            explicitDescription: transactionDescription
        )
        let key = NotificationTransactionParser.captureKey(
            supplied: captureID,
            source: source,
            title: title,
            subtitle: subtitle,
            message: message,
            receivedAt: date
        )

        let inserted = try await Task.detached(priority: .userInitiated) {
            let database = try PendingInboxDatabase()
            return try database.save(
                captureKey: key,
                source: source,
                title: title,
                subtitle: subtitle,
                message: message,
                suggestion: suggestion,
                receivedAtUnixMs: Int64(date.timeIntervalSince1970 * 1_000)
            )
        }.value

        if inserted {
            return .result(
                value: key,
                dialog: "Added to Financial Tracker’s pending transactions."
            )
        }
        return .result(
            value: key,
            dialog: "This notification is already pending."
        )
    }
}

@main
struct FinancialTrackerIntentsExtension: AppIntentsExtension {}

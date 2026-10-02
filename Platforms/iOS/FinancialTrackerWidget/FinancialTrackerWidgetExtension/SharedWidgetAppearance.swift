import Foundation
import SwiftUI
import UIKit

enum SharedWidgetAppearance {
    private struct Snapshot: Codable {
        let version: Int
        let theme: String
        let accentColorHex: String
        let currencySymbol: String?

        static let fallback = Snapshot(
            version: 1,
            theme: "Light",
            accentColorHex: "#5044E4",
            currencySymbol: "RM"
        )
    }

    private static let appGroupIdentifiers = [
        "group.com.faikalizham.financial-tracker",
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5"
    ]
    private static let snapshotFileName =
        "financial_tracker_widget_appearance.json"
    private static let synchronization = NSLock()
    private static var snapshot = loadSnapshot()

    static func reload() {
        let loadedSnapshot = loadSnapshot()
        synchronization.lock()
        snapshot = loadedSnapshot
        synchronization.unlock()
    }

    static var background: Color {
        adaptive(light: "#FAF7F1", dark: "#0A0A0C")
    }

    static var accent: Color {
        Color(uiColor: color(from: currentSnapshot().accentColorHex))
    }

    static var accentForeground: Color {
        let accentColor = color(from: currentSnapshot().accentColorHex)
        let components = accentColor.rgbComponents
        let luminance = (0.299 * components.red) +
            (0.587 * components.green) +
            (0.114 * components.blue)
        return Color(uiColor: color(from:
            luminance > 0.62 ? "#17142D" : "#FFFFFF"))
    }

    static var ink: Color {
        adaptive(light: "#17142D", dark: "#FBF7F0")
    }

    static var secondaryInk: Color {
        adaptive(light: "#686273", dark: "#BBB4C7")
    }

    static var divider: Color {
        adaptive(light: "#E9E3DB", dark: "#29292D")
    }

    static var controlSurface: Color {
        adaptive(light: "#F1EDE7", dark: "#202023")
    }

    static var currencySymbol: String {
        if let symbol = currentSnapshot().currencySymbol?
            .trimmingCharacters(in: .whitespacesAndNewlines),
           !symbol.isEmpty {
            return symbol
        }
        return "RM"
    }

    static var positive: Color {
        adaptive(light: "#147C5B", dark: "#72D7B2")
    }

    static var negative: Color {
        adaptive(light: "#C54558", dark: "#FF8D9C")
    }

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

    private static func adaptive(light: String, dark: String) -> Color {
        let preference = currentSnapshot().theme
        if preference == "Dark" {
            return Color(uiColor: color(from: dark))
        }
        if preference == "Light" {
            return Color(uiColor: color(from: light))
        }

        return Color(uiColor: UIColor { traits in
            color(from: traits.userInterfaceStyle == .dark ? dark : light)
        })
    }

    private static func currentSnapshot() -> Snapshot {
        synchronization.lock()
        defer { synchronization.unlock() }
        return snapshot
    }

    private static func loadSnapshot() -> Snapshot {
        for identifier in appGroupIdentifiers {
            guard let containerUrl = FileManager.default.containerURL(
                forSecurityApplicationGroupIdentifier: identifier
            ) else { continue }
            let fileUrl = containerUrl.appendingPathComponent(
                snapshotFileName,
                isDirectory: false
            )
            guard let data = try? Data(contentsOf: fileUrl),
                  let decoded = try? JSONDecoder().decode(Snapshot.self, from: data),
                  decoded.version == 1 else { continue }
            return decoded
        }
        return .fallback
    }

    private static func color(from hex: String) -> UIColor {
        let cleaned = hex.trimmingCharacters(in: .whitespacesAndNewlines)
            .trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        guard cleaned.count == 6,
              let value = UInt64(cleaned, radix: 16) else {
            return UIColor(red: 0.314, green: 0.267, blue: 0.894, alpha: 1)
        }
        return UIColor(
            red: CGFloat((value >> 16) & 0xFF) / 255,
            green: CGFloat((value >> 8) & 0xFF) / 255,
            blue: CGFloat(value & 0xFF) / 255,
            alpha: 1
        )
    }
}

private extension UIColor {
    var rgbComponents: (red: CGFloat, green: CGFloat, blue: CGFloat) {
        var red: CGFloat = 0
        var green: CGFloat = 0
        var blue: CGFloat = 0
        var alpha: CGFloat = 0
        getRed(&red, green: &green, blue: &blue, alpha: &alpha)
        return (red, green, blue)
    }
}

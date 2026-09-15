using Microsoft.Maui.Handlers;

namespace FinancialTracker.Helpers;

public static class EntryChrome
{
    public static void RemoveNativeBorder(IEntryHandler handler)
    {
#if WINDOWS
        var textBox = handler.PlatformView;
        var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.Transparent);
        var noBorder = new Microsoft.UI.Xaml.Thickness(0);

        textBox.UseSystemFocusVisuals = false;
        textBox.BorderThickness = noBorder;
        textBox.BorderBrush = transparent;
        textBox.Background = transparent;
        textBox.Resources["TextControlBorderThemeThickness"] = noBorder;
        textBox.Resources["TextControlBorderThemeThicknessFocused"] = noBorder;
        textBox.Resources["TextControlBorderBrush"] = transparent;
        textBox.Resources["TextControlBorderBrushPointerOver"] = transparent;
        textBox.Resources["TextControlBorderBrushFocused"] = transparent;
        textBox.Resources["TextControlBorderBrushDisabled"] = transparent;
        textBox.Resources["TextControlBackground"] = transparent;
        textBox.Resources["TextControlBackgroundPointerOver"] = transparent;
        textBox.Resources["TextControlBackgroundFocused"] = transparent;
        textBox.Resources["TextControlBackgroundDisabled"] = transparent;
#elif ANDROID
        var editText = handler.PlatformView;
        editText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
            Android.Graphics.Color.Transparent);
        editText.SetBackgroundColor(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
        var textField = handler.PlatformView;
        textField.BorderStyle = UIKit.UITextBorderStyle.None;
        textField.BackgroundColor = UIKit.UIColor.Clear;
        textField.Layer.BorderWidth = 0;
        textField.Layer.CornerRadius = 0;
#endif
    }
}

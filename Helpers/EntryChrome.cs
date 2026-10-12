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

    public static void RemoveNativeBorder(IEditorHandler handler)
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
        var textView = handler.PlatformView;
        textView.BackgroundColor = UIKit.UIColor.Clear;
        textView.Layer.BorderWidth = 0;
        textView.Layer.CornerRadius = 0;
#endif
    }

    public static void RemoveNativeBorder(IPickerHandler handler)
    {
#if WINDOWS
        var comboBox = handler.PlatformView;
        var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            Microsoft.UI.Colors.Transparent);
        var noBorder = new Microsoft.UI.Xaml.Thickness(0);

        comboBox.UseSystemFocusVisuals = false;
        comboBox.BorderThickness = noBorder;
        comboBox.BorderBrush = transparent;
        comboBox.Background = transparent;
        comboBox.Resources["ComboBoxBorderThemeThickness"] = noBorder;
        comboBox.Resources["ComboBoxBorderBrush"] = transparent;
        comboBox.Resources["ComboBoxBorderBrushPointerOver"] = transparent;
        comboBox.Resources["ComboBoxBorderBrushPressed"] = transparent;
        comboBox.Resources["ComboBoxBorderBrushFocused"] = transparent;
        comboBox.Resources["ComboBoxBackground"] = transparent;
        comboBox.Resources["ComboBoxBackgroundPointerOver"] = transparent;
        comboBox.Resources["ComboBoxBackgroundPressed"] = transparent;
#elif ANDROID
        var picker = handler.PlatformView;
        picker.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
            Android.Graphics.Color.Transparent);
        picker.SetBackgroundColor(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
        var picker = handler.PlatformView;
        picker.BorderStyle = UIKit.UITextBorderStyle.None;
        picker.BackgroundColor = UIKit.UIColor.Clear;
        picker.Layer.BorderWidth = 0;
        picker.Layer.CornerRadius = 0;
#endif
    }
}

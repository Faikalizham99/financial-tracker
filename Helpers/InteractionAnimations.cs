namespace FinancialTracker.Helpers;

public static class InteractionAnimations
{
    private const string PressScaleAnimationName = "InteractionPressScale";
    private const string PressOpacityAnimationName = "InteractionPressOpacity";
    private const uint PressDuration = 45;
    private const uint ReleaseDuration = 90;
    private const double PressedScale = 0.965;
    private const double PressedOpacity = 0.82;

    private static readonly BindableProperty IsFeedbackAttachedProperty =
        BindableProperty.CreateAttached(
            "IsFeedbackAttached",
            typeof(bool),
            typeof(InteractionAnimations),
            false);

    private static readonly BindableProperty IsPressedProperty =
        BindableProperty.CreateAttached(
            "IsPressed",
            typeof(bool),
            typeof(InteractionAnimations),
            false);

    private static readonly BindableProperty RestingScaleProperty =
        BindableProperty.CreateAttached(
            "RestingScale",
            typeof(double),
            typeof(InteractionAnimations),
            1d);

    private static readonly BindableProperty RestingOpacityProperty =
        BindableProperty.CreateAttached(
            "RestingOpacity",
            typeof(double),
            typeof(InteractionAnimations),
            1d);

    public static void AttachPressFeedback(View element)
    {
        if (GetIsFeedbackAttached(element) || !IsInteractive(element))
        {
            return;
        }

        element.SetValue(IsFeedbackAttachedProperty, true);
        if (element is Button button)
        {
            button.Pressed += OnButtonPressed;
            button.Released += OnButtonReleased;
            return;
        }

        if (element is ImageButton imageButton)
        {
            imageButton.Pressed += OnButtonPressed;
            imageButton.Released += OnButtonReleased;
            return;
        }

        foreach (var tapGesture in element.GestureRecognizers.OfType<TapGestureRecognizer>())
        {
            tapGesture.Tapped += OnTapCompleted;
        }

        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerPressed += OnPointerPressed;
        pointerGesture.PointerReleased += OnPointerReleased;
        pointerGesture.PointerExited += OnPointerExited;
        element.GestureRecognizers.Add(pointerGesture);
    }

    public static async Task PulseAsync(object? sender)
    {
        var element = ResolveVisualElement(sender);
        if (element is null || !element.IsEnabled)
        {
            return;
        }

        if (GetIsFeedbackAttached(element))
        {
            return;
        }

        element.CancelAnimations();
        await element.ScaleToAsync(0.96, 55, Easing.CubicOut);
        await element.ScaleToAsync(1, 105, Easing.CubicOut);
    }

    private static VisualElement? ResolveVisualElement(object? sender) =>
        sender switch
        {
            SwipeItemView swipeItem => swipeItem.Content as VisualElement ?? swipeItem,
            VisualElement element => element,
            GestureRecognizer gesture => gesture.Parent as VisualElement,
            _ => null
        };

    private static bool IsInteractive(View element) =>
        element is Button or ImageButton ||
        element.GestureRecognizers.OfType<TapGestureRecognizer>().Any();

    private static bool GetIsFeedbackAttached(BindableObject element) =>
        (bool)element.GetValue(IsFeedbackAttachedProperty);

    private static bool GetIsPressed(BindableObject element) =>
        (bool)element.GetValue(IsPressedProperty);

    private static void SetIsPressed(BindableObject element, bool value) =>
        element.SetValue(IsPressedProperty, value);

    private static void OnButtonPressed(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            ShowPressedState(element);
        }
    }

    private static void OnButtonReleased(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            ShowReleasedState(element);
        }
    }

    private static void OnPointerPressed(object? sender, PointerEventArgs e)
    {
        if (sender is PointerGestureRecognizer { Parent: VisualElement element })
        {
            ShowPressedState(element);
        }
    }

    private static void OnPointerReleased(object? sender, PointerEventArgs e)
    {
        if (sender is PointerGestureRecognizer { Parent: VisualElement element })
        {
            ShowReleasedState(element);
        }
    }

    private static void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is PointerGestureRecognizer { Parent: VisualElement element })
        {
            ShowReleasedState(element);
        }
    }

    private static void OnTapCompleted(object? sender, TappedEventArgs e)
    {
        if (sender is TapGestureRecognizer { Parent: VisualElement element } &&
            GetIsPressed(element))
        {
            ShowReleasedState(element);
        }
    }

    private static void ShowPressedState(VisualElement element)
    {
        if (!element.IsEnabled || GetIsPressed(element))
        {
            return;
        }

        SetIsPressed(element, true);
        if (element is BoxView)
        {
            element.SetValue(RestingOpacityProperty, element.Opacity);
            AnimateOpacity(
                element,
                element.Opacity * PressedOpacity,
                PressDuration,
                Easing.CubicOut);
        }
        else
        {
            element.SetValue(RestingScaleProperty, element.Scale);
            AnimateScale(
                element,
                element.Scale * PressedScale,
                PressDuration,
                Easing.CubicOut);
        }
    }

    private static void ShowReleasedState(VisualElement element)
    {
        if (!GetIsPressed(element))
        {
            return;
        }

        SetIsPressed(element, false);
        if (element is BoxView)
        {
            AnimateOpacity(
                element,
                (double)element.GetValue(RestingOpacityProperty),
                ReleaseDuration,
                Easing.CubicOut);
        }
        else
        {
            AnimateScale(
                element,
                (double)element.GetValue(RestingScaleProperty),
                ReleaseDuration,
                Easing.CubicOut);
        }
    }

    private static void AnimateScale(
        VisualElement element,
        double target,
        uint duration,
        Easing easing)
    {
        element.AbortAnimation(PressScaleAnimationName);
        new Animation(
                value => element.Scale = value,
                element.Scale,
                target,
                easing)
            .Commit(
                element,
                PressScaleAnimationName,
                length: duration);
    }

    private static void AnimateOpacity(
        VisualElement element,
        double target,
        uint duration,
        Easing easing)
    {
        element.AbortAnimation(PressOpacityAnimationName);
        new Animation(
                value => element.Opacity = value,
                element.Opacity,
                target,
                easing)
            .Commit(
                element,
                PressOpacityAnimationName,
                length: duration);
    }
}

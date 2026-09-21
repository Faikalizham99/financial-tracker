namespace FinancialTracker.Helpers;

public static class InteractionAnimations
{
    private const string PressDepthAnimationName = "InteractionPressDepth";
    private const uint PressDuration = 60;
    private const uint ReleaseDuration = 105;
    private const double PressedScale = 0.95;
    private const double PressedTranslationY = 2;

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

    private static readonly BindableProperty RestingTranslationYProperty =
        BindableProperty.CreateAttached(
            "RestingTranslationY",
            typeof(double),
            typeof(InteractionAnimations),
            0d);

    private static readonly BindableProperty PointerPressObservedProperty =
        BindableProperty.CreateAttached(
            "PointerPressObserved",
            typeof(bool),
            typeof(InteractionAnimations),
            false);

    private static readonly BindableProperty IsFallbackPulseRunningProperty =
        BindableProperty.CreateAttached(
            "IsFallbackPulseRunning",
            typeof(bool),
            typeof(InteractionAnimations),
            false);

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

    public static Task PulseAsync(object? sender)
    {
        var element = ResolveVisualElement(sender);
        if (element is null || !CanAnimate(element))
        {
            return Task.CompletedTask;
        }

        if (GetIsFeedbackAttached(element) &&
            (element is Button or ImageButton ||
             GetPointerPressObserved(element) ||
             GetIsPressed(element)))
        {
            return Task.CompletedTask;
        }

        return RunFallbackPulseAsync(element);
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
        element is not BoxView &&
        (element is Button or ImageButton ||
         element.GestureRecognizers.OfType<TapGestureRecognizer>().Any());

    private static bool CanAnimate(VisualElement element) =>
        element is not BoxView && element.IsEnabled;

    private static bool GetIsFeedbackAttached(BindableObject element) =>
        (bool)element.GetValue(IsFeedbackAttachedProperty);

    private static bool GetIsPressed(BindableObject element) =>
        (bool)element.GetValue(IsPressedProperty);

    private static void SetIsPressed(BindableObject element, bool value) =>
        element.SetValue(IsPressedProperty, value);

    private static bool GetPointerPressObserved(BindableObject element) =>
        (bool)element.GetValue(PointerPressObservedProperty);

    private static void SetPointerPressObserved(BindableObject element, bool value) =>
        element.SetValue(PointerPressObservedProperty, value);

    private static bool GetIsFallbackPulseRunning(BindableObject element) =>
        (bool)element.GetValue(IsFallbackPulseRunningProperty);

    private static void SetIsFallbackPulseRunning(BindableObject element, bool value) =>
        element.SetValue(IsFallbackPulseRunningProperty, value);

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
            SetPointerPressObserved(element, true);
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
            SetPointerPressObserved(element, false);
            ShowReleasedState(element);
        }
    }

    private static void OnTapCompleted(object? sender, TappedEventArgs e)
    {
        if (sender is not TapGestureRecognizer { Parent: VisualElement element } ||
            GetIsFallbackPulseRunning(element))
        {
            return;
        }

        var pointerPressObserved = GetPointerPressObserved(element);
        SetPointerPressObserved(element, false);

        if (GetIsPressed(element))
        {
            ShowReleasedState(element);
        }

        if (!pointerPressObserved)
        {
            _ = RunFallbackPulseAsync(element);
        }
    }

    private static void ShowPressedState(VisualElement element)
    {
        if (!CanAnimate(element) ||
            GetIsPressed(element) ||
            GetIsFallbackPulseRunning(element))
        {
            return;
        }

        CaptureRestingTransform(element);
        SetIsPressed(element, true);
        AnimateTransform(
            element,
            GetRestingScale(element) * PressedScale,
            GetRestingTranslationY(element) + PressedTranslationY,
            PressDuration,
            Easing.CubicOut);
    }

    private static void ShowReleasedState(VisualElement element)
    {
        if (!GetIsPressed(element))
        {
            return;
        }

        SetIsPressed(element, false);
        AnimateTransform(
            element,
            GetRestingScale(element),
            GetRestingTranslationY(element),
            ReleaseDuration,
            Easing.CubicOut);
    }

    private static async Task RunFallbackPulseAsync(VisualElement element)
    {
        if (!CanAnimate(element) || GetIsFallbackPulseRunning(element))
        {
            return;
        }

        SetIsFallbackPulseRunning(element, true);
        CaptureRestingTransform(element);
        SetIsPressed(element, true);

        var restingScale = GetRestingScale(element);
        var restingTranslationY = GetRestingTranslationY(element);
        element.AbortAnimation(PressDepthAnimationName);
        element.Scale = restingScale * PressedScale;
        element.TranslationY = restingTranslationY + PressedTranslationY;

        try
        {
            await Task.Delay((int)PressDuration);
            SetIsPressed(element, false);
            AnimateTransform(
                element,
                restingScale,
                restingTranslationY,
                ReleaseDuration,
                Easing.CubicOut);
            await Task.Delay((int)ReleaseDuration);
        }
        finally
        {
            element.AbortAnimation(PressDepthAnimationName);
            element.Scale = restingScale;
            element.TranslationY = restingTranslationY;
            SetIsPressed(element, false);
            SetIsFallbackPulseRunning(element, false);
        }
    }

    private static void CaptureRestingTransform(VisualElement element)
    {
        element.SetValue(RestingScaleProperty, element.Scale);
        element.SetValue(RestingTranslationYProperty, element.TranslationY);
    }

    private static double GetRestingScale(BindableObject element) =>
        (double)element.GetValue(RestingScaleProperty);

    private static double GetRestingTranslationY(BindableObject element) =>
        (double)element.GetValue(RestingTranslationYProperty);

    private static void AnimateTransform(
        VisualElement element,
        double targetScale,
        double targetTranslationY,
        uint duration,
        Easing easing)
    {
        element.AbortAnimation(PressDepthAnimationName);
        var startScale = element.Scale;
        var startTranslationY = element.TranslationY;
        new Animation(
                progress =>
                {
                    element.Scale = startScale + ((targetScale - startScale) * progress);
                    element.TranslationY = startTranslationY +
                        ((targetTranslationY - startTranslationY) * progress);
                },
                0,
                1,
                easing)
            .Commit(
                element,
                PressDepthAnimationName,
                length: duration);
    }
}

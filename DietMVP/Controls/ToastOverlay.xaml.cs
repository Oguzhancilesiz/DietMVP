namespace DietMVP.Controls;
public partial class ToastOverlay : ContentView
{
    public ToastOverlay() { InitializeComponent(); }

    public async Task ShowAsync(string text, int durationMs = 2000)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        IsVisible = true;
        ToastText.Text = text;
        ToastHost.Opacity = 0; ToastHost.TranslationY = 40;

        await Task.WhenAll(
            ToastHost.FadeTo(1, 160),
            ToastHost.TranslateTo(0, 0, 160, Easing.CubicOut)
        );

        await Task.Delay(durationMs);

        await Task.WhenAll(
            ToastHost.FadeTo(0, 160),
            ToastHost.TranslateTo(0, 40, 160, Easing.CubicIn)
        );

        IsVisible = false;
    }
}

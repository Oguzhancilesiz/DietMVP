using System.Windows.Input;

namespace DietMVP.Controls;

public partial class LoadingOverlay : ContentView
{
    public LoadingOverlay()
    {
        InitializeComponent();
    }

    // Bindable Text
    public static readonly BindableProperty TextProperty =
        BindableProperty.Create(nameof(Text), typeof(string), typeof(LoadingOverlay),
            "Yükleniyor...", propertyChanged: (b, o, n) =>
            {
                if (b is LoadingOverlay lo && lo.Lbl != null)
                    lo.Lbl.Text = n?.ToString();
            });

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public async Task RunAsync(string text, Func<Task> work)
    {
        Text = text;
        IsVisible = true;
        await Task.WhenAll(Card.FadeTo(1, 140), Card.TranslateTo(0, 0, 140, Easing.CubicOut));

        try { await work(); }
        finally
        {
            await Task.WhenAll(Card.FadeTo(0, 140), Card.TranslateTo(0, 40, 140, Easing.CubicIn));
            IsVisible = false;
        }
    }

    // Basit Show/Hide da lazým olursa:
    public async Task ShowAsync(string? text = null)
    {
        if (!string.IsNullOrWhiteSpace(text)) Text = text;
        IsVisible = true;
        await Task.WhenAll(Card.FadeTo(1, 140), Card.TranslateTo(0, 0, 140, Easing.CubicOut));
    }

    public async Task HideAsync()
    {
        await Task.WhenAll(Card.FadeTo(0, 140), Card.TranslateTo(0, 40, 140, Easing.CubicIn));
        IsVisible = false;
    }
}

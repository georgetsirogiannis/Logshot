using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using Logshot.Services;

namespace Logshot.Behaviors;

public class AutocorrectBehavior
{
    public static readonly AttachedProperty<bool> EnableProperty =
        AvaloniaProperty.RegisterAttached<AutocorrectBehavior, TextBox, bool>("Enable");

    static AutocorrectBehavior()
    {
        EnableProperty.Changed.AddClassHandler<TextBox>(HandleEnableChanged);
    }

    public static void SetEnable(AvaloniaObject element, bool value) => element.SetValue(EnableProperty, value);
    public static bool GetEnable(AvaloniaObject element) => element.GetValue(EnableProperty);

    private static void HandleEnableChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            textBox.KeyUp += TextBox_KeyUp;
            textBox.LostFocus += TextBox_LostFocus;
        }
        else
        {
            textBox.KeyUp -= TextBox_KeyUp;
            textBox.LostFocus -= TextBox_LostFocus;
        }
    }

    private static void TextBox_KeyUp(object? sender, KeyEventArgs e)
    {
        // Trigger instantly when user presses Space or Enter
        if (e.Key == Key.Space || e.Key == Key.Enter)
        {
            if (sender is TextBox tb) ApplyAutocorrect(tb);
        }
    }

    private static void TextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        // Also trigger if they click away immediately after finishing a word
        if (sender is TextBox tb) ApplyAutocorrect(tb);
    }

    private static void ApplyAutocorrect(TextBox textBox)
    {
        if (!AutocorrectionManager.Instance.IsEnabled || string.IsNullOrEmpty(textBox.Text)) return;

        string text = textBox.Text;
        int caret = textBox.CaretIndex;
        if (caret == 0) return;

        // O(1) performance: We only look backwards from the caret to find the last typed word
        int end = caret - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end])) end--;
        if (end < 0) return;

        int start = end;
        while (start >= 0 && !char.IsWhiteSpace(text[start]) && !char.IsPunctuation(text[start])) start--;
        start++;

        if (start <= end)
        {
            string word = text.Substring(start, end - start + 1);
            if (AutocorrectionManager.Instance.GetActivePairs().TryGetValue(word, out string? correctWord))
            {
                string newText = text.Substring(0, start) + correctWord + text.Substring(end + 1);
                int lengthDiff = correctWord.Length - word.Length;

                textBox.Text = newText;
                textBox.CaretIndex = Math.Max(0, Math.Min(newText.Length, caret + lengthDiff));
            }
        }
    }
}
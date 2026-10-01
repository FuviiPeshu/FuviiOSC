using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace FuviiOSC.Common;

public partial class TextBoxNumber
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(TextBoxNumber),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public TextBoxNumber()
    {
        InitializeComponent();
    }
}

public sealed class TextBoxNumberValidationRule : ValidationRule
{
    public override ValidationResult Validate(object value, CultureInfo cultureInfo)
    {
        if (value is not string text || text.Length == 0) return ValidationResult.ValidResult;

        foreach (char character in text)
        {
            if ((character >= '0' && character <= '9') || character is '.' or ',') continue;

            return new ValidationResult(false, "Only numbers, '.' and ',' are allowed.");
        }

        return ValidationResult.ValidResult;
    }
}
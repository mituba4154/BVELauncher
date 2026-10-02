using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BVELauncher.App.Services;

internal static class NumberBoxAutomation
{
    public static void Attach(NumberBox number, string id, string name)
    {
        number.Loaded += (_, _) =>
        {
            var input = FindTextBox(number);
            if (input == null) return;
            AutomationProperties.SetAutomationId(input, id); AutomationProperties.SetName(input, name);
        };
    }
    private static TextBox? FindTextBox(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox text) return text;
            var found = FindTextBox(child); if (found != null) return found;
        }
        return null;
    }
}

using System.Text.Json;
using System.Windows;

namespace Sub2Bar.Windows.Views;

public partial class ServerResponseWindow : Window
{
    public ServerResponseWindow(string requestPath, string response)
    {
        InitializeComponent();
        PathText.Text = requestPath;
        ResponseText.Text = PrettyPrint(response);
    }

    private static string PrettyPrint(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return response;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(ResponseText.Text);
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}


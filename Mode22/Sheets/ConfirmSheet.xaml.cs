using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mode22.Sheets;

public partial class ConfirmSheet : UserControl
{
    public ConfirmSheet(string title, string message, string confirmText = "Удалить")
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            ConfirmButton.Focus();
        }), DispatcherPriority.Input);
    }

    public event EventHandler? Confirmed;
    public event EventHandler? Cancelled;

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Cancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}

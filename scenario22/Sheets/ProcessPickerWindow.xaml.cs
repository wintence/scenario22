using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Scenario22.Models;
using Scenario22.Services;

namespace Scenario22.Sheets;

public partial class ProcessPickerWindow : Window
{
    private readonly bool _forClosing;
    private List<ProcessRow> _rows = new();

    public ProcessPickerWindow(bool forClosing)
    {
        InitializeComponent();
        _forClosing = forClosing;
        Loaded += Window_Loaded;
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    public string ProcessName { get; private set; } = string.Empty;

    public string ProcessPath { get; private set; } = string.Empty;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        SetStatus(Strings.T("L_ProcLoading"), visible: true);
        try
        {
            var rows = await Task.Run(() => ProcessService.GetRunningProcesses()
                .Select(x => new ProcessRow(x, _forClosing))
                .ToList());

            _rows = rows;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, visible: true);
        }
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _rows
            : _rows.Where(x => x.Matches(query)).ToList();

        ProcessList.ItemsSource = filtered;
        ProcessList.SelectedItem = filtered.FirstOrDefault(x => x.CanPick);
        CountText.Text = Strings.T("L_ProcCountFmt", filtered.Count);

        var empty = filtered.Count == 0;
        ProcessList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (empty)
        {
            SetStatus(Strings.T("L_ProcEmpty"), visible: true);
        }
        else
        {
            StatusText.Visibility = Visibility.Collapsed;
        }
    }

    private void SetStatus(string message, bool visible)
    {
        StatusText.Text = message;
        StatusText.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            ProcessList.Visibility = Visibility.Collapsed;
            CountText.Text = string.Empty;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_rows.Count > 0)
        {
            ApplyFilter();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _rows = new List<ProcessRow>();
        await LoadAsync();
    }

    private void ProcessList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        AcceptSelection();
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        AcceptSelection();
    }

    private void AcceptSelection()
    {
        if (ProcessList.SelectedItem is not ProcessRow row || !row.CanPick)
        {
            return;
        }

        ProcessName = row.Name;
        ProcessPath = row.Path;
        DialogResult = true;
    }

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.T("L_FileDialogProcess"),
            Filter = Strings.T("L_FilterProcess"),
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        ProcessPath = dialog.FileName;
        ProcessName = ScenarioAction.NormalizeProcessName(dialog.FileName);
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            _ = LoadAsync();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private sealed class ProcessRow
    {
        public ProcessRow(RunningProcessInfo info, bool forClosing)
        {
            Name = info.Name;
            Path = info.Path;
            Title = info.Title;
            IsProtected = info.IsProtected;
            Icon = info.Icon;
            CanPick = forClosing
                ? !info.IsProtected && !string.IsNullOrWhiteSpace(info.Name)
                : !string.IsNullOrWhiteSpace(info.Path);
        }

        public string Name { get; }

        public string Path { get; }

        public string Title { get; }

        public bool IsProtected { get; }

        public ImageSource? Icon { get; }

        public bool CanPick { get; }

        public bool Matches(string query)
        {
            return Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                || Title.Contains(query, StringComparison.OrdinalIgnoreCase);
        }
    }
}

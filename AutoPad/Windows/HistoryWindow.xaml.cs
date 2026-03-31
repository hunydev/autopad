using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using AutoPad.Models;
using AutoPad.Services;
using WpfClipboard = System.Windows.Clipboard;

namespace AutoPad.Windows;

public partial class HistoryWindow : Window
{
    private readonly ClipboardHistoryService _historyService;

    public HistoryWindow(ClipboardHistoryService historyService)
    {
        InitializeComponent();
        _historyService = historyService;

        // 아이콘 설정
        Icon = IconHelper.CreateAppIconImageSource(32);

        // 다크 모드 제목 표시줄
        SourceInitialized += (s, e) =>
            ThemeHelper.ApplyDarkTitleBar(this, true);

        ApplyLocalization();
        RefreshList();
    }

    private void ApplyLocalization()
    {
        Title = Loc.HistoryTitle;
        HeaderText.Text = Loc.HistoryHeader;
        SearchBox.Tag = Loc.HistorySearch; // placeholder
        ClearButton.Content = Loc.HistoryClearAll;
    }

    private void RefreshList(string? filter = null)
    {
        IReadOnlyList<ClipboardHistoryItem> items = _historyService.Items;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            items = items.Where(i =>
                (i.Preview?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.TextContent?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = items;
        CountText.Text = Loc.HistoryCount(items.Count);
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshList(SearchBox.Text);
    }

    private void Item_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ClipboardHistoryItem item)
        {
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("DkContextMenu")
            };

            var menuItemStyle = (Style)FindResource("DkMenuItem");

            var copyMenuItem = new MenuItem { Header = Loc.BtnCopy, Style = menuItemStyle };
            copyMenuItem.Click += (s, args) => CopyAndClose(item);
            menu.Items.Add(copyMenuItem);

            var editMenuItem = new MenuItem { Header = Loc.BtnEdit, Style = menuItemStyle };
            editMenuItem.Click += (s, args) => EditAndClose(item);
            menu.Items.Add(editMenuItem);

            menu.PlacementTarget = fe;
            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void CopyAndClose(ClipboardHistoryItem item)
    {
        EditWindow.SuppressClipboardMonitor = true;
        try
        {
            switch (item.Type)
            {
                case ClipboardHistoryItemType.Text when item.TextContent != null:
                    WpfClipboard.SetText(item.TextContent);
                    break;
                case ClipboardHistoryItemType.Image when item.ImagePath != null:
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(item.ImagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    WpfClipboard.SetImage(bitmap);
                    break;
                case ClipboardHistoryItemType.File when item.FilePath != null:
                    var files = new System.Collections.Specialized.StringCollection { item.FilePath };
                    WpfClipboard.SetFileDropList(files);
                    break;
            }
        }
        finally
        {
            Dispatcher.InvokeAsync(() =>
            {
                EditWindow.SuppressClipboardMonitor = false;
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        Close();
    }

    private void EditAndClose(ClipboardHistoryItem item)
    {
        var isDarkMode = App.SettingsService?.Settings.IsDarkMode ?? true;

        switch (item.Type)
        {
            case ClipboardHistoryItemType.Text when item.TextContent != null:
                new EditWindow(item.TextContent, isDarkMode).Show();
                break;
            case ClipboardHistoryItemType.Image when item.ImagePath != null:
                new EditWindow(item.ImagePath, isFilePath: true, isDarkMode).Show();
                break;
            case ClipboardHistoryItemType.File when item.FilePath != null:
                new EditWindow(item.FilePath, isFilePath: true, isDarkMode).Show();
                break;
        }

        Close();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            Loc.HistoryClearConfirm,
            "AutoPad",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _historyService.Clear();
            RefreshList();
        }
    }
}

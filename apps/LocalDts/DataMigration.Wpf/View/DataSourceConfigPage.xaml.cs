using System.Windows.Controls;
using System.Windows.Input;
using DataMigration.Wpf.ViewModel;

namespace DataMigration.Wpf;

public partial class DataSourceConfigPage : Page
{
    public DataSourceConfigPage()
    {
        InitializeComponent();
        // DataContext will be set by NavigationService
    }

    private async void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DataSourceConfigViewModel viewModel)
        {
            var textBlock = sender as TextBlock;
            if (textBlock != null && textBlock.Text != null)
            {
                await viewModel.PreviewTableCommand.ExecuteAsync(textBlock.Text);
            }
        }
    }
}
using DataMigration.Wpf.ViewModel;
using System.Windows.Controls;

namespace DataMigration.Wpf;

public partial class TaskConfigPage : Page
{
    public TaskConfigPage()
    {
        InitializeComponent();
    }

    private async void SourceTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModel.TaskConfigViewModel viewModel)
        {
            await viewModel.LoadSourceColumnsCommand.ExecuteAsync(null);
        }
    }

    private async void TargetTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is ViewModel.TaskConfigViewModel viewModel)
        {
            await viewModel.LoadTargetColumnsCommand.ExecuteAsync(null);
        }
    }
}
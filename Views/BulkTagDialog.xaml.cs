using System.Windows;

namespace PrintVault3D.Views;

/// <summary>
/// Dialog for adding tags to multiple models.
/// </summary>
public partial class BulkTagDialog : Window
{
    public string Tags { get; private set; } = string.Empty;

    public BulkTagDialog()
    {
        InitializeComponent();
        TagsTextBox.Focus();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        Tags = TagsTextBox.Text.Trim();
        
        if (string.IsNullOrWhiteSpace(Tags))
        {
            System.Windows.MessageBox.Show("Lütfen en az bir etiket girin.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

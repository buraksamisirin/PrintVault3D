using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrintVault3D.Models;

namespace PrintVault3D.Views;

public partial class LinkGcodeDialog : Window
{
    private readonly IEnumerable<Gcode> _allGcodes;
    public Gcode? SelectedGcode { get; private set; }

    public LinkGcodeDialog(IEnumerable<Gcode> unlinkedGcodes)
    {
        InitializeComponent();
        
        _allGcodes = unlinkedGcodes ?? Enumerable.Empty<Gcode>();
        GcodesList.ItemsSource = _allGcodes;

        // Focus search box on load
        Loaded += (s, e) => 
        {
            SearchBox.Focus();
        };
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchBox.Text?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(query))
        {
            GcodesList.ItemsSource = _allGcodes;
        }
        else
        {
            GcodesList.ItemsSource = _allGcodes.Where(g => 
                (g.OriginalFileName != null && g.OriginalFileName.ToLowerInvariant().Contains(query)));
        }
        
        // Reset selection if the filtered list doesn't contain the selected item
        if (GcodesList.SelectedItem == null)
        {
            LinkButton.IsEnabled = false;
        }
    }

    private void GcodesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SelectedGcode = GcodesList.SelectedItem as Gcode;
        LinkButton.IsEnabled = SelectedGcode != null;
    }

    private void LinkButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGcode != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

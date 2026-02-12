using System.Windows;

namespace PrintVault3D.Helpers;

/// <summary>
/// Attached properties for drag and drop visual feedback.
/// </summary>
public static class DragDropHelper
{
    #region IsDragOver Attached Property

    public static readonly DependencyProperty IsDragOverProperty =
        DependencyProperty.RegisterAttached(
            "IsDragOver",
            typeof(bool),
            typeof(DragDropHelper),
            new PropertyMetadata(false));

    public static bool GetIsDragOver(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsDragOverProperty);
    }

    public static void SetIsDragOver(DependencyObject obj, bool value)
    {
        obj.SetValue(IsDragOverProperty, value);
    }

    #endregion

    #region DragCount Attached Property (for badge display)

    public static readonly DependencyProperty DragCountProperty =
        DependencyProperty.RegisterAttached(
            "DragCount",
            typeof(int),
            typeof(DragDropHelper),
            new PropertyMetadata(0));

    public static int GetDragCount(DependencyObject obj)
    {
        return (int)obj.GetValue(DragCountProperty);
    }

    public static void SetDragCount(DependencyObject obj, int value)
    {
        obj.SetValue(DragCountProperty, value);
    }

    #endregion
}

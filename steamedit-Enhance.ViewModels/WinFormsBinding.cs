#nullable enable
using System.ComponentModel;
using System.Windows.Forms;

namespace steamedit_Enhance.ViewModels;

internal static class WinFormsBinding
{
    public static Binding Bind(
        Control control, string propertyName,
        INotifyPropertyChanged dataSource, string dataMember,
        bool formattingEnabled = true,
        DataSourceUpdateMode updateMode = DataSourceUpdateMode.OnPropertyChanged)
    {
        var binding = new Binding(propertyName, dataSource, dataMember, formattingEnabled, updateMode);
        control.DataBindings.Add(binding);
        return binding;
    }

    public static Binding BindText(
        Control control,
        INotifyPropertyChanged dataSource, string dataMember,
        DataSourceUpdateMode updateMode = DataSourceUpdateMode.OnPropertyChanged)
    {
        return Bind(control, "Text", dataSource, dataMember, true, updateMode);
    }

    public static Binding BindEnabled(
        Control control,
        INotifyPropertyChanged dataSource, string dataMember)
    {
        return Bind(control, "Enabled", dataSource, dataMember);
    }

    public static Binding BindChecked(
        Control control,
        INotifyPropertyChanged dataSource, string dataMember,
        DataSourceUpdateMode updateMode = DataSourceUpdateMode.OnPropertyChanged)
    {
        return Bind(control, "Checked", dataSource, dataMember, true, updateMode);
    }
}

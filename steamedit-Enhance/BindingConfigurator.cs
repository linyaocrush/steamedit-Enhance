#nullable enable
using System.Windows.Forms;
using steamedit_Enhance.ViewModels;

namespace steamedit_Enhance;

internal static class BindingConfigurator
{
	public static void Configure(
		PropertyEditorViewModel pe,
		ClearableTextBox textGameName,
		ClearableTextBox textSortAs,
		CheckBox checkHidden,
		CheckBox checkCompleted,
		ComboBox listControllerSupport,
		ComboBox listVR,
		ComboBox listEntryType)
	{
		textGameName.DataBindings.Add(nameof(TextBox.Text), pe, nameof(pe.GameName), false, DataSourceUpdateMode.OnPropertyChanged);
		textSortAs.DataBindings.Add(nameof(TextBox.Text), pe, nameof(pe.SortAs), false, DataSourceUpdateMode.OnPropertyChanged);
		checkHidden.DataBindings.Add(nameof(CheckBox.Checked), pe, nameof(pe.IsHidden), false, DataSourceUpdateMode.OnPropertyChanged);
		checkCompleted.DataBindings.Add(nameof(CheckBox.Checked), pe, nameof(pe.IsCompleted), false, DataSourceUpdateMode.OnPropertyChanged);
		listControllerSupport.DataBindings.Add(nameof(ComboBox.SelectedIndex), pe, nameof(pe.ControllerSupportIndex), false, DataSourceUpdateMode.OnPropertyChanged);
		listVR.DataBindings.Add(nameof(ComboBox.SelectedIndex), pe, nameof(pe.VRIndex), false, DataSourceUpdateMode.OnPropertyChanged);
		textGameName.DataBindings.Add(nameof(ClearableTextBox.ClearXVisible), pe, nameof(pe.GameNameClearXVisible), false, DataSourceUpdateMode.OnPropertyChanged);
		textSortAs.DataBindings.Add(nameof(ClearableTextBox.ClearXVisible), pe, nameof(pe.SortAsClearXVisible), false, DataSourceUpdateMode.OnPropertyChanged);
		listEntryType.DataBindings.Add(nameof(ComboBox.SelectedItem), pe, nameof(pe.EntryType), false, DataSourceUpdateMode.OnPropertyChanged);

		// SortAs 颜色通过 PropertyChanged 事件同步
		pe.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(PropertyEditorViewModel.HasSortAs))
				textSortAs.ForeColor = pe.HasSortAs
					? System.Drawing.Color.FromKnownColor(System.Drawing.KnownColor.WindowText)
					: System.Drawing.Color.Gray;
		};

		// ComboBox 键盘拦截（阻止翻页按键改变选中项）
		AttachKeyDownInterceptor(listControllerSupport);
		AttachKeyDownInterceptor(listVR);
	}

	private static void AttachKeyDownInterceptor(ComboBox cb)
	{
		cb.KeyDown += (s, e) =>
		{
			if ((uint)(e.KeyCode - 33) <= 1u)
			{
				e.Handled = true;
				e.SuppressKeyPress = true;
				cb.DroppedDown = false;
			}
		};
	}
}

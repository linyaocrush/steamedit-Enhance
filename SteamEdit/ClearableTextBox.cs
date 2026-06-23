#nullable enable
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SteamEdit;

internal class ClearableTextBox : TextBox
{
	private readonly Label lblClearX;

	public bool AutoClearX { get; set; } = true;

	public bool ClearXVisible
	{
		get
		{
			return lblClearX.Visible;
		}
		set
		{
			lblClearX.Visible = value;
		}
	}

	public event HandledEventHandler? ClearXClicked;

	public ClearableTextBox()
	{
		base.Resize += ClearableTextBox_Resize;
		base.TextChanged += ClearableTextBox_TextChanged;
		lblClearX = new Label
		{
			Location = new Point(100, 0),
			AutoSize = true,
			Text = "x",
			ForeColor = Color.Gray,
			Visible = false,
			Font = new Font("Tahoma", 8.25f),
			Cursor = Cursors.Arrow,
			BackColor = Color.Transparent
		};
		base.Controls.Add(lblClearX);
		lblClearX.Click += LblClearX_Click;
		lblClearX.BringToFront();
	}

	private void LblClearX_Click(object? sender, EventArgs e)
	{
		HandledEventArgs e2 = new HandledEventArgs(defaultHandledValue: false);
		this.ClearXClicked?.Invoke(this, e2);
		if (!e2.Handled)
		{
			Text = string.Empty;
		}
	}

	private void ClearableTextBox_TextChanged(object? sender, EventArgs e)
	{
		if (AutoClearX)
		{
			lblClearX.Visible = !string.IsNullOrEmpty(Text);
		}
	}

	private void ClearableTextBox_Resize(object? sender, EventArgs e)
	{
		lblClearX.Location = new Point(base.Width - lblClearX.Width - 3, (base.Height - lblClearX.Height) / 2 - 3);
	}
}

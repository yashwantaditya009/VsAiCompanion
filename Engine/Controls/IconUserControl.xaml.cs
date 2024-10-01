﻿using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Controls;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace JocysCom.VS.AiCompanion.Engine.Controls
{
	/// <summary>
	/// Interaction logic for IconUserControl.xaml
	/// </summary>
	public partial class IconUserControl : UserControl
	{
		public IconUserControl()
		{
			InitializeComponent();
			if (ControlsHelper.IsDesignMode(this))
				return;
			UpdateButtons();
		}

		ISettingsListFileItem _item;

		public void BindData(ISettingsListFileItem item = null)
		{
			_item = item;
			DataContext = item;
			UpdateButtons();
		}

		private void UpdateButtons()
		{
			// Edit button is always visible if icon is not set.
			IconEditButton.Visibility = _item?.Icon == null
				? Visibility.Visible
				: Visibility.Hidden;
		}

		System.Windows.Forms.OpenFileDialog _OpenFileDialog = new System.Windows.Forms.OpenFileDialog();

		public void Edit()
		{
			string contents = null;
			// Check if CTRL+C is pressed.
			if (Keyboard.Modifiers != ModifierKeys.Control)
			{
				// Get SVG file content from clipboard.
				contents = Clipboard.GetText();
			}
			else
			{
				var dialog = _OpenFileDialog;
				dialog.SupportMultiDottedExtensions = true;
				JocysCom.ClassLibrary.Controls.DialogHelper.AddFilter(dialog, ".svg");
				JocysCom.ClassLibrary.Controls.DialogHelper.AddFilter(dialog);
				dialog.FilterIndex = 1;
				dialog.RestoreDirectory = true;
				dialog.Title = "Open " + JocysCom.ClassLibrary.Files.Mime.GetFileDescription(".svg");
				var result = dialog.ShowDialog();
				if (result != System.Windows.Forms.DialogResult.OK)
					return;
				contents = System.IO.File.ReadAllText(dialog.FileNames[0]);
			}
			if (string.IsNullOrEmpty(contents))
				_item?.SetIcon(contents);
		}

		private void UserControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			//Edit();
		}

		private void IconEditButton_Click(object sender, RoutedEventArgs e)
		{
			Edit();
		}

		private void Grid_MouseEnter(object sender, MouseEventArgs e)
		{
			IconEditButton.Visibility = Visibility.Visible;
		}

		private void Grid_MouseLeave(object sender, MouseEventArgs e)
		{
			UpdateButtons();
		}

		private void This_Loaded(object sender, RoutedEventArgs e)
		{
			if (ControlsHelper.AllowLoad(this))
			{
				AppHelper.InitHelp(this);
				//UiPresetsManager.InitControl(this, true);
				UiPresetsManager.AddControls(this);
			}
		}
	}
}

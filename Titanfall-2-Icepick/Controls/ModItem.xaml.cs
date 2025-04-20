﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Icepick.Controls
{
	/// <summary>
	/// Interaction logic for ModItem.xaml
	/// </summary>
	public partial class ModItem : UserControl
	{
		public enum StatusIconType
		{
			Ok,
			Warning,
			Error,
			Update
		}

		public static string GetIcon( StatusIconType icon )
		{
			switch ( icon )
			{
				default:
				case StatusIconType.Warning:
					return "error.png";
				case StatusIconType.Ok:
					return "accept.png";
				case StatusIconType.Error:
					return "exclamation.png";
				case StatusIconType.Update:
					return "connect.png";
			}
		}

		public ModItem()
		{
			InitializeComponent();
		}

		public ModItem( Mods.TitanfallMod mod )
		{
			InitializeComponent();
			Mod = mod;

			ModName = mod.Definition?.Name;
			ModDescription = mod.Definition?.Description;
			ModEnabled = mod.Enabled;
			if ( !string.IsNullOrEmpty( mod.ImagePath ) )
			{
				ModImage = System.IO.Path.Combine( AppDomain.CurrentDomain.BaseDirectory, mod.ImagePath );
			}

			UpdateTooltipAndStatus();
		}

		public Mods.TitanfallMod Mod { get; set; }

		public string ModName
		{
			set
			{
				ModNameLabel.Content = string.IsNullOrWhiteSpace(value) ? "Warning: Unnamed Mod " + System.IO.Path.GetFileName( Mod.Directory ) : value;
			}
			get
			{
				return (string) ModNameLabel.Content;
			}
		}

		public string ModDescription
		{
			set
			{
				ModDescriptionLabel.Content = string.IsNullOrWhiteSpace( value ) ? "Warning: Missing description." : value;
			}
			get
			{
				return (string) ModDescriptionLabel.Content;
			}
		}

		public bool ModEnabled
		{
			set
			{
				if (value)
				{
					Opacity = 1.0;
					ModStatusImage.Visibility = Visibility.Visible;
				}
				else
				{
					Opacity = 0.5;
					ModStatusImage.Visibility = Visibility.Hidden;
				}
				EnabledMenuItem.IsChecked = value;
				Mod.Enabled = value;
			}
		}

		public string ModImage
		{
			set
			{
				ModDisplayImage.Source = new BitmapImage( new Uri( value ) );
			}
		}

		public ImageSource ModImageSource
		{
			get
			{
				return ModDisplayImage.Source;
			}
		}

		public StatusIconType Icon
		{
			set
			{
				BitmapImage logo = new BitmapImage();
				logo.BeginInit();
				logo.UriSource = new Uri( "pack://application:,,,/Titanfall-2-Icepick;component/Images/" + GetIcon( value ) );
				logo.EndInit();
				ModStatusImage.Source = logo;
			}
		}

		private void ShowInExplorer_Click( object sender, RoutedEventArgs e )
		{
			string path = System.IO.Path.Combine( AppDomain.CurrentDomain.BaseDirectory, Mod.Directory );
			System.Diagnostics.Process.Start( path );
		}

		private void ViewDetails_Click( object sender, RoutedEventArgs e )
		{
			ModDetailsWindow details = new ModDetailsWindow( this );
			details.Show();
		}

		private void PackageMod_Click( object sender, RoutedEventArgs e )
		{
			string errorMessage = Mods.ModDatabase.PackageMod( Mod.Directory );
			if ( errorMessage == null )
			{
				Mods.ModDatabase.ShowFolder(Mods.ModDatabase.ModsDirectory);
			}
			else
			{
				MessageBox.Show( $"Could not package mod.\n{errorMessage}", "Package Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
			}
		}

		private void DeleteMod_Click(object sender, RoutedEventArgs e)
		{
			var name = Mod.Definition?.Name;
			if (string.IsNullOrWhiteSpace(name))
			{
				name = System.IO.Path.GetFileName(Mod.Directory);
			}

			var result = MessageBox.Show($"Do you want to delete \"{name}\"?", "Delete Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (result == MessageBoxResult.Yes)
			{
				try
				{
					Mods.ModDatabase.DeleteMod(Mod.Directory);
				}
				catch (Exception err)
				{
					MessageBox.Show($"Could not delete mod.\n{err.Message}", "Deletion Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				}
			}
		}

		private void ToggleMod_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ModEnabled = Mods.ModDatabase.ToggleMod(Mod.Directory);
			}
			catch (Exception err)
			{
				MessageBox.Show($"Could not {(Mod.Enabled ? "disable" : "enable")} mod.\n{err.Message}", "Toggle Mod Error", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			}
		}

		private void UpdateTooltipAndStatus()
		{
			var ErrorsList = Mod.GetErrors();
			var WarningsList = Mod.GetWarnings();

			if ( ErrorsList.Count > 0 || WarningsList.Count > 0 )
			{
				Icon = ErrorsList.Count > 0 ? StatusIconType.Error : StatusIconType.Warning;
				TooltipHeader.Text = "Action Required";
				TooltipText.Text = "";
				foreach ( string error in Mod.GetErrors() )
				{
					TooltipText.Text += TooltipText.Text == "" ? "" : "\n";
					TooltipText.Text += error;
				}
				foreach ( string warning in Mod.GetWarnings() )
				{
					TooltipText.Text += TooltipText.Text == "" ? "" : "\n";
					TooltipText.Text += warning;
				}
				return;
			}

			Icon = StatusIconType.Ok;
			TooltipHeader.Text = "All good";
			TooltipText.Text = "This mod is setup correctly";
		}
	}
}

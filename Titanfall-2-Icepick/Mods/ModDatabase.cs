﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Icepick.Mods
{
	public static class ModDatabase
	{
		public enum ModImportType
		{
			Invalid,
			Mod,
			Save
		}

		public const string ModsDirectory = @"data\mods";
		public const string SavesDirectory = @"data\saves";
		public const string DisabledFileName = "disabled";
		private const string ArchiveExtension = ".zip";

		public delegate void ModDatabaseDelegate();
		public delegate void TitanfallModDelegate(TitanfallMod Mod);
		public delegate void ImportModDelegate(bool success, ModImportType importType, string message);

		public static event ModDatabaseDelegate OnStartedLoadingMods;
		public static event TitanfallModDelegate OnModLoaded;
		public static event ModDatabaseDelegate OnFinishedLoadingMods;
		public static event ImportModDelegate OnFinishedImportingMod;
		public static event ModDatabaseDelegate OnModsChanged;

		public static List<TitanfallMod> LoadedMods = new List<TitanfallMod>();

		public static void ShowFolder(string directory)
		{
			string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, directory);
			if (!Directory.Exists(path))
			{
				MessageBox.Show($"The directory '{path}' is missing!", "Missing Directory", MessageBoxButton.OK, MessageBoxImage.Exclamation);
				return;
			}
			System.Diagnostics.Process.Start(path);
		}

		public static void ClearDatabase()
		{
			LoadedMods.Clear();
		}

		public static void LoadAll()
		{
			if (OnStartedLoadingMods != null)
			{
				OnStartedLoadingMods();
			}

			string modsFullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory);

			if (Directory.Exists(modsFullDirectory))
			{
				foreach (var modPath in Directory.GetDirectories(modsFullDirectory))
				{
					TitanfallMod newMod = new TitanfallMod(modPath);
					string modDirectory = Path.GetFileName(newMod.Directory);
					if (!modDirectory.StartsWith("."))
					{
						LoadedMods.Add(newMod);
						if (OnModLoaded != null)
						{
							OnModLoaded(newMod);
						}
					}
				}
			}

			if (OnFinishedLoadingMods != null)
			{
				OnFinishedLoadingMods();
			}
		}

		public static void AttemptImportMod(string path)
		{
			if (Path.GetExtension(path) == ArchiveExtension)
			{
				string modsFullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory);
				Console.WriteLine(modsFullDirectory);

				// Check if a mod already exists
				string modFolderName = Path.GetFileNameWithoutExtension(path);
				string destinationFolder = Path.Combine(modsFullDirectory, modFolderName);
				if (Directory.Exists(destinationFolder))
				{
					MessageBoxResult overwriteResult = MessageBox.Show($"'{modFolderName}' already exists in your mods folder.\n\nDo you wish to overwrite it?", "Overwrite Mod?", MessageBoxButton.YesNo);
					if (overwriteResult == MessageBoxResult.Yes)
					{
						Directory.Delete(destinationFolder, true);
					}
					else
					{
						if (OnFinishedImportingMod != null)
						{
							OnFinishedImportingMod(false, ModImportType.Invalid, $"A mod already exists in folder '{modFolderName}'!");
						}
						return;
					}
				}

				try
				{
					bool foundModDefinition = false;
					bool foundSaveFile = false;

					ZipArchive zip = ZipFile.OpenRead(path);
					foreach (var entry in zip.Entries)
					{
						string[] parts = entry.Name.Split('.');
						if (parts.Length > 2 && entry.Name.EndsWith(".txt"))
						{
							foundSaveFile = true;
						}

						if (entry.Name == TitanfallMod.ModDocumentFile)
						{
							foundModDefinition = true;
						}

					}

					if (foundModDefinition)
					{
						// Extract mod to the mods folder
						ZipFile.ExtractToDirectory(path, destinationFolder);

						if (File.Exists(Path.Combine(destinationFolder, DisabledFileName)))
						{
							File.Delete(Path.Combine(destinationFolder, DisabledFileName));
						}

						if (OnFinishedImportingMod != null)
						{
							OnFinishedImportingMod(true, ModImportType.Mod, $"{modFolderName} imported successfully!");
						}
					}
					else if (foundSaveFile)
					{
						// Extract saves to the saves folder
						destinationFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SavesDirectory);
						ZipFile.ExtractToDirectory(path, destinationFolder);

						if (OnFinishedImportingMod != null)
						{
							OnFinishedImportingMod(true, ModImportType.Save, $"{modFolderName} imported to saves successfully!");
						}
					}
					else
					{
						throw new Exception("Mod was not a valid mod, nor a save file.");
					}
				}
				catch (Exception e)
				{
					if (OnFinishedImportingMod != null)
					{
						OnFinishedImportingMod(false, ModImportType.Invalid, $"An exception occurred while importing mod '{modFolderName}', {e.Message}");
					}
					return;
				}
			}
		}

		public static string PackageMod(string path)
		{
			string exportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory, Path.GetFileName(path)) + ArchiveExtension;
			string modDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
			string disabledFilePath = Path.Combine(modDirectory, DisabledFileName);
			string errorMessage = null;

			try
			{
				bool isDisabled = File.Exists(disabledFilePath);
				if (isDisabled)
				{
					// prevent packaging the disabled status file
					File.Delete(disabledFilePath);
				}

				ZipFile.CreateFromDirectory(modDirectory, exportPath);

				if (isDisabled)
				{
					// recreate the disabled status file if applicable
					File.Create(disabledFilePath).Close();
				}
			}
			catch (Exception e)
			{
				errorMessage = e.Message;
			}

			return errorMessage;
		}

		public static bool ToggleMod(string path)
		{
			string disabledFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory, Path.GetFileName(path), DisabledFileName);

			if (File.Exists(disabledFilePath))
			{
				File.Delete(disabledFilePath);
				return true;
			}

			File.Create(disabledFilePath).Close();
			return false;
		}

		public static void DeleteMod(string path)
		{
			Directory.Delete(path, true);
		}

		// From https://stackoverflow.com/a/29491927
		private static FileSystemEventHandler Debounce(FileSystemEventHandler func, int milliseconds = 500)
		{
			CancellationTokenSource cancelTokenSource = null;

			return (object sender, FileSystemEventArgs e) =>
			{
				cancelTokenSource?.Cancel();
				cancelTokenSource = new CancellationTokenSource();

				Task.Delay(milliseconds, cancelTokenSource.Token)
					.ContinueWith(t =>
					{
						if (!t.IsCanceled)
						{
							func(sender, e);
						}
					}, TaskScheduler.Default);
			};
		}

		public static void WatchModsFolder()
		{
			var modsFullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory);
			var watcher = new FileSystemWatcher(modsFullDirectory);
			watcher.NotifyFilter = NotifyFilters.Attributes
								 | NotifyFilters.CreationTime
								 | NotifyFilters.DirectoryName
								 | NotifyFilters.FileName
								 | NotifyFilters.LastWrite;
			watcher.Changed += Debounce(OnChanged);
			watcher.Created += Debounce(OnChanged);
			watcher.Deleted += Debounce(OnChanged);
			watcher.Renamed += OnRenamed;

			watcher.Filter = "*";
			watcher.IncludeSubdirectories = true;
			watcher.EnableRaisingEvents = true;
		}

		private static bool ShouldReloadMods(string fullPath)
		{
			if (fullPath.Contains("mod.json") || fullPath.Contains("disabled"))
			{
				return true;
			}

			var modsFullDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModsDirectory);
			DirectoryInfo modDir = new DirectoryInfo(modsFullDirectory);
			DirectoryInfo changeDir = new DirectoryInfo(fullPath);
			if (changeDir.Parent.FullName == modDir.FullName)
			{
				return true;
			}

			return false;
		}

		private static void OnChanged(object sender, FileSystemEventArgs e)
		{
			if (ShouldReloadMods(e.FullPath))
			{
				if (OnModsChanged != null)
				{
					OnModsChanged();
				}
			}
		}

		private static void OnRenamed(object sender, RenamedEventArgs e)
		{
			if (ShouldReloadMods(e.FullPath) || ShouldReloadMods(e.OldFullPath))
			{
				if (OnModsChanged != null)
				{
					OnModsChanged();
				}
			}
		}
	}
}

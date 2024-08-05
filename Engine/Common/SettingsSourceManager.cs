﻿using JocysCom.ClassLibrary;
using JocysCom.ClassLibrary.Configuration;
using JocysCom.ClassLibrary.Runtime;
using JocysCom.VS.AiCompanion.Plugins.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace JocysCom.VS.AiCompanion.Engine
{
	public static class SettingsSourceManager
	{

		public static void ResetSettings(bool confirm = false)
		{
			if (confirm && !AppHelper.AllowReset("All Settings", "Please note that this will reset all services, models, templates and tasks!"))
				return;
			try
			{
				var zip = GetSettingsZip();
				// Load data from a single XML file.
				var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
				// Load data from multiple XML files.
				var zipTasks = GetItemsFromZip(zip, Global.TasksName, Global.Tasks);
				var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
				var zipLists = GetItemsFromZip(zip, Global.ListsName, Global.Lists);
				var zipEmbeddings = GetItemsFromZip(zip, Global.EmbeddingsName, Global.Embeddings);
				var zipAppSettings = zipAppDataItems[0];
				PreventWriteToNewerFiles(false);
				RemoveToReplace(Global.Tasks, zipTasks, x => x.Name);
				RemoveToReplace(Global.Templates, zipTemplates, x => x.Name);
				RemoveToReplace(Global.Lists, zipLists, x => x.Name);
				RemoveToReplace(Global.Embeddings, zipEmbeddings, x => x.Name);
				ResetServicesAndModels();
				ResetPrompts(zip);
				ResetVoices(zip);
				// Add user settings.
				Global.Embeddings.Add(zipEmbeddings.ToArray());
				Global.Lists.Add(zipLists.ToArray());
				Global.Templates.Add(zipTemplates.ToArray());
				Global.Tasks.Add(zipTasks.ToArray());
				// Copy other settings.
				RuntimeHelper.CopyProperties(zipAppSettings, Global.AppSettings, true);
				var settings = Global.AppSettings.PanelSettingsList.ToArray();
				foreach (var setting in settings)
				{
					var zipSetting = zipAppSettings.PanelSettingsList.FirstOrDefault(x => x.ItemType == setting.ItemType);
					if (zipSetting == null)
						continue;
					RuntimeHelper.CopyProperties(zipSetting, setting, true);
				}
				// Save settings.
				Global.SaveSettings();
				PreventWriteToNewerFiles(true);
				Global.RaiseOnAiServicesUpdated();
				Global.RaiseOnAiModelsUpdated();
				Global.RaiseOnTasksUpdated();
				Global.RaiseOnTemplatesUpdated();
				Global.RaiseOnListsUpdated();
			}
			catch (Exception ex)
			{
				Global.ShowError("ResetSettings() error: " + ex.Message);
			}
		}

		/// <summary>
		/// Allow overwriting newer files when saving current settings to the disk.
		/// </summary>
		static void PreventWriteToNewerFiles(bool enabled)
		{
			// Separate files.
			Global.Assistants.PreventWriteToNewerFiles = enabled;
			Global.Embeddings.PreventWriteToNewerFiles = enabled;
			Global.FineTunings.PreventWriteToNewerFiles = enabled;
			Global.Lists.PreventWriteToNewerFiles = enabled;
			Global.Tasks.PreventWriteToNewerFiles = enabled;
			Global.Templates.PreventWriteToNewerFiles = enabled;
			Global.UiPresets.PreventWriteToNewerFiles = enabled;
			// Single file.
			Global.AppData.PreventWriteToNewerFiles = enabled;
			Global.PromptItems.PreventWriteToNewerFiles = enabled;
			Global.Voices.PreventWriteToNewerFiles = enabled;
		}

		public static Guid OpenAiServiceId
			=> AppHelper.GetGuid(nameof(AiService), OpenAiName);
		// Must be string constant or OpenAiServiceId property will get empty string.

		public const string OpenAiName = "Open AI";

		#region Reset App Settings

		/// <summary>
		/// Does not reset AiServices and AiModels.
		/// </summary>
		public static void ResetAppSettings(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
			if (zipAppDataItems == null)
				return;
			var zipAppSettings = zipAppDataItems[0];
			// Reset all app settings except of services, list of models and other reference types.
			JocysCom.ClassLibrary.Runtime.RuntimeHelper.CopyProperties(zipAppSettings, Global.AppSettings, true);
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		/// <summary>Reset Prompts</summary>
		public static void ResetPrompts(ZipStorer zip = null)
			=> ResetItems(zip, Global.PromptItems, Global.PromptItemsName, x => x.Name);

		/// <summary>Reset Voices</summary>
		public static void ResetVoices(ZipStorer zip = null)
			=> ResetItems(zip, Global.Voices, Global.VociesName, x => x.Name);

		#endregion

		#region Reset Services and Models

		/// <summary>Reset Services and Models</summary>
		public static void ResetServicesAndModels(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			// ---
			var zipAppDataItems = GetItemsFromZip(zip, Global.AppDataName, Global.AppData);
			var zipServices = zipAppDataItems[0].AiServices;
			var zipModels = zipAppDataItems[0].AiModels;
			// Remove Services and Models
			var zipServiceNames = zipServices.Select(t => t.Name.ToLower()).ToList();
			var servicesToRemove = Global.AppSettings.AiServices.Where(x => zipServiceNames.Contains(x.Name.ToLower())).ToArray();
			foreach (var service in servicesToRemove)
			{
				var modelsToRemove = Global.AppSettings.AiModels.Where(x => x.AiServiceId == service.Id).ToArray();
				foreach (var model in modelsToRemove)
					Global.AppSettings.AiModels.Remove(model);
				Global.AppSettings.AiServices.Remove(service);
			}
			// Add Services and Models
			foreach (var item in zipServices)
				Global.AppSettings.AiServices.Add(item);
			foreach (var item in zipModels)
				Global.AppSettings.AiModels.Add(item);
			// ---
			if (closeZip)
				zip.Close();
		}

		#endregion

		#region Lists

		public static string[] GetRequiredLists()
		{
			return new string[] {
				"Prompts"
			};
		}


		/// <summary>Reset Lists</summary>
		public static void ResetLists(ZipStorer zip = null)
			=> ResetItems(zip, Global.Lists, Global.ListsName, x => x.Name);

		public static void ResetUiPresets(ZipStorer zip = null)
			=> ResetItems(zip, Global.UiPresets, Global.UiPresetsName, x => x.Name);

		/// <summary>Reset Lists</summary>
		public static void ResetEmbeddings(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			ResetItems(zip, Global.Embeddings, Global.EmbeddingsName, x => x.Name);
			ResetOtherItems(zip, Global.Embeddings, Global.EmbeddingsName);
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		/// <summary>
		/// Reset non-XML items.
		/// </summary>
		private static void ResetOtherItems<T>(ZipStorer zip, SettingsData<T> data, string name) where T : SettingsFileItem
		{
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(name) && !x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
			foreach (var entry in entries)
			{
				var path = Path.Combine(Global.AppData.XmlFile.Directory.FullName, entry.FilenameInZip);
				bool isDirectory = entry.FilenameInZip.EndsWith("/");
				if (isDirectory)
				{
					var di = new DirectoryInfo(path);
					if (!di.Exists)
						di.Create();
				}
				else
				{
					var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
					var fi = new FileInfo(path);
					if (!fi.Directory.Exists)
						fi.Directory.Create();
					if (File.Exists(path))
						File.Delete(path);
					File.WriteAllBytes(path, bytes);
				}
			}
		}

		/// <summary>
		/// Reset XML items.
		/// </summary>
		private static void ResetItems<T>(ZipStorer zip, SettingsData<T> data, string name, Func<T, string> propertySelector)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			// Update Lists
			var zipItems = GetItemsFromZip(zip, name, data);
			RemoveToReplace(data, zipItems, propertySelector);
			data.Add(zipItems.ToArray());
			// Close zip.
			if (closeZip)
				zip.Close();
		}

		public static int CheckRequiredItems(IList<EmbeddingsItem> items, ZipStorer zip = null)
		{
			return 0;
		}

		public static int CheckRequiredItems(IList<ListInfo> items, ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return 0;
			// ---
			var required = GetRequiredLists();
			var current = items.Select(x => x.Name).ToArray();
			var missing = required.Except(current).ToArray();
			// If all templates exist then return.
			if (missing.Length == 0)
				return 0;
			var zipItems = GetItemsFromZip(zip, Global.ListsName, Global.Lists, missing);
			foreach (var zipItem in zipItems)
				items.Add(zipItem);
			// ---
			if (closeZip)
				zip.Close();
			return missing.Length;
		}

		#endregion

		#region Reset Templates

		public const string TemplateGenerateTitleTaskName = "® System - Generate Title";
		public const string TemplateFormatMessageTaskName = "® System - Format Message";
		public const string TemplatePluginApprovalTaskName = "® System - Plugin Approval";
		public const string TempalteListsUpdateUserProfile = "Lists - Update User Profile";
		public const string TemplateAIChatPersonalized = "AI - Chat - Personalized";

		public const string TemplatePlugin_Model_TextToAudio = "® System - Text-To-Audio";
		public const string TemplatePlugin_Model_AudioToText = "® System - Audio-To-Text";
		public const string TemplatePlugin_Model_VideoToText = "® System - Video-To-Text";
		public const string TemplatePlugin_Model_TextToVideo = "® System - Text-To-Video";

		public static string[] GetRequiredTemplates()
		{
			return new string[] {
				TemplateGenerateTitleTaskName,
				TemplateFormatMessageTaskName,
				TemplatePluginApprovalTaskName,
				TempalteListsUpdateUserProfile,
				TemplateAIChatPersonalized,
			};
		}

		public static int CheckRequiredTemplates(IList<TemplateItem> items, ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return 0;
			// ---
			var required = GetRequiredTemplates();
			var current = items.Select(x => x.Name).ToArray();
			var missing = required.Except(current).ToArray();
			// If all templates exist then return.
			if (missing.Length == 0)
				return 0;
			var zipItems = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates, missing);
			foreach (var zipItem in zipItems)
				items.Add(zipItem);
			// ---
			if (closeZip)
				zip.Close();
			return missing.Length;
		}

		public static void ResetTasks(ZipStorer zip = null)
		{
		}

		public static void ResetTemplates(ZipStorer zip = null)
		{
			bool closeZip;
			if (closeZip = zip == null)
				zip = GetSettingsZip();
			if (zip == null)
				return;
			// ---
			var data = Global.Templates;
			var zipTemplates = GetItemsFromZip(zip, Global.TemplatesName, Global.Templates);
			if (zipTemplates.Count == 0)
				return;
			var items = data.Items.ToArray();
			foreach (var item in items)
			{
				var error = data.DeleteItem(item);
				if (!string.IsNullOrEmpty(error))
					Global.ShowError(error);
			}
			data.PreventWriteToNewerFiles = false;
			data.Add(zipTemplates.ToArray());
			data.Save();
			data.PreventWriteToNewerFiles = true;
			// ---
			if (closeZip)
				zip.Close();
		}

		/// <summary>
		/// Get items from the zip. entry pattern: filenameInZipStartsWith*.xml
		/// </summary>
		public static List<T> GetItemsFromZip<T>(ZipStorer zip, string filenameInZipStartsWith, SettingsData<T> data, params string[] names)
		{
			var list = new List<T>();
			var entries = zip.ReadCentralDir()
				.Where(x => x.FilenameInZip.StartsWith(filenameInZipStartsWith) && x.FilenameInZip.EndsWith(".xml"))
				.ToArray();
			foreach (var entry in entries)
			{
				// If names supplied then get only named templates.
				if (names?.Length > 0)
				{
					var nameInZip = Path.GetFileNameWithoutExtension(entry.FilenameInZip);
					if (!names.Contains(nameInZip))
						continue;
				}
				var bytes = AppHelper.ExtractFile(zip, entry.FilenameInZip);
				if (data.UseSeparateFiles)
				{
					var itemFile = data.DeserializeItem(bytes, false);
					list.Add(itemFile);
				}
				else
				{
					var dataFile = data.DeserializeData(bytes, false);
					list.AddRange(dataFile.Items);
				}
			}
			return list;
		}

		#endregion

		#region General Methods

		private static void RemoveToReplace<T>(SettingsData<T> data, IList<T> items, Func<T, string> propertySelector)
		{
			// Remove lists which will be replaced.
			var names = items.Select(t => propertySelector(t).ToLower()).ToList();
			var itemsToRemove = data.Items.Where(x => names.Contains(propertySelector(x).ToLower())).ToArray();
			data.Remove(itemsToRemove);
		}

		public static ZipStorer GetSettingsZip()
		{
			ZipStorer zip = null;
			// Check if settings zip file with the same name as the executable exists.
			var settingsFile = $"{AssemblyInfo.Entry.ModuleBasePath}.Settings.zip";
			if (File.Exists(settingsFile))
			{
				// Use external file.
				zip = ZipStorer.Open(settingsFile, FileAccess.Read);
			}
			else if (Global.AppSettings.IsEnterprise)
			{
				// Use external URL or local file specified by the user.
				var path = JocysCom.ClassLibrary.Configuration.AssemblyInfo.ExpandPath(Global.AppSettings.ConfigurationUrl);
				var isUrl = Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.Scheme != Uri.UriSchemeFile;
				if (isUrl)
					zip = GetZipFromUrl(path);
				else if (File.Exists(path))
					zip = ZipStorer.Open(path, FileAccess.Read);
			}
			else
			{
				// Use embedded resource.
				zip = AppHelper.GetZip("Resources.Settings.zip", typeof(Global).Assembly);
			}
			if (zip == null)
				return null;
			return zip;
		}

		public static ZipStorer GetZipFromUrl(string url)
		{
			var docItem = Helper.RunSynchronously(async () => await Web.DownloadContentAuthenticated(url));
			if (!string.IsNullOrEmpty(docItem.Error))
			{
				Global.ShowError($"{nameof(GetZipFromUrl)} error: {docItem.Error}");
				return null;
			}
			var zipBytes = docItem.GetDataBinary();
			var ms = new MemoryStream(zipBytes);
			try
			{
				// Open an existing zip file for reading.
				var zip = ZipStorer.Open(ms, FileAccess.Read);
				return zip;
			}
			catch (Exception ex)
			{
				Global.ShowError($"{nameof(GetZipFromUrl)} error: {ex.Message}");
				return null;
			}
		}

		#endregion


	}
}

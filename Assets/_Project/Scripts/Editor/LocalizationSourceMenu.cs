using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.CSV;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace _Project.Scripts.Editor
{
	public static class LocalizationSourceMenu
	{
		private const string SOURCE_ASSET_PATH = "Assets/_Project/Localization/Sources/Localization_Source.csv";

		private static readonly Regex _tableEntryRegex =
			new(@"(?m)^[ \t]*- m_Id: (?<id>\d+)\r?\n", RegexOptions.Compiled);

		private static readonly Regex _localizedValueRegex =
			new(@"(?ms)^(?<prefix>[ \t]+m_Localized:[ \t]*)""(?<value>(?:\\.|[^""\\])*)""", RegexOptions.Compiled);

		private static readonly Regex _unicodeEscapeRegex =
			new Regex(@"\\u(?<code>[0-9a-fA-F]{4})", RegexOptions.Compiled);

		[MenuItem("Artemis/Localization/Open Localization Source")]
		private static void OpenLocalizationSource()
		{
			string sourcePath = GetSourcePath();
			string revealPath = File.Exists(sourcePath) ? sourcePath : Path.GetDirectoryName(sourcePath);
			EditorUtility.RevealInFinder(revealPath);
		}

		[MenuItem("Artemis/Localization/Select Localization Source CSV")]
		private static void SelectLocalizationSource()
		{
			string selectedPath = EditorUtility.OpenFilePanel("Select Localization CSV", GetSourceDirectory(), "csv");

			if (string.IsNullOrEmpty(selectedPath))
			{
				return;
			}

			try
			{
				string sourcePath = GetSourcePath();

				if (!string.Equals(Path.GetFullPath(selectedPath), sourcePath, StringComparison.OrdinalIgnoreCase))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(sourcePath));
					File.Copy(selectedPath, sourcePath, true);
				}

				AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
				Debug.Log($"[Localization] Source replaced with '{selectedPath}'.");
			}
			catch (Exception exception)
			{
				Debug.LogError($"[Localization] Failed to replace source with '{selectedPath}'. {exception}");
				EditorUtility.DisplayDialog("Localization Source Replacement Failed", exception.Message, "OK");
			}
		}

		[MenuItem("Artemis/Localization/Import Localization Source")]
		private static void ImportLocalizationSource()
		{
			string sourcePath = GetSourcePath();

			if (!File.Exists(sourcePath))
			{
				EditorUtility.DisplayDialog(
					"Localization Source Not Found",
					$"Select a CSV file first. Expected file: {SOURCE_ASSET_PATH}",
					"OK");

				return;
			}

			try
			{
				StringTableCollection collection = FindSourceCollection();

				Dictionary<string, string> tableSnapshots = SnapshotTableFiles(collection);

				using (StreamReader reader = new StreamReader(sourcePath, Encoding.UTF8, true))
				{
					Csv.ImportInto(reader, collection, true, null, false);
				}

				AssetDatabase.SaveAssets();
				int normalizedEntries = NormalizeChangedTableEntries(collection, tableSnapshots);

				if (normalizedEntries > 0)
				{
					Debug.Log(
						$"[Localization] Normalized escaped Unicode in {normalizedEntries} changed table entries.");
				}

				Debug.Log($"[Localization] Imported '{SOURCE_ASSET_PATH}' into '{collection.TableCollectionName}'.");
			}
			catch (Exception exception)
			{
				Debug.LogError($"[Localization] Failed to import '{SOURCE_ASSET_PATH}'. {exception}");
				EditorUtility.DisplayDialog("Localization Import Failed", exception.Message, "OK");
			}
		}

		private static string GetSourcePath()
		{
			string projectPath = Directory.GetParent(Application.dataPath).FullName;
			return Path.Combine(projectPath, SOURCE_ASSET_PATH);
		}

		private static string GetSourceDirectory()
		{
			return Path.GetDirectoryName(GetSourcePath());
		}

		private static StringTableCollection FindSourceCollection()
		{
			var collections = LocalizationEditorSettings.GetStringTableCollections();

			if (collections.Count == 0)
			{
				throw new InvalidOperationException("No string table collections were found in the project.");
			}

			if (collections.Count == 1)
			{
				return collections[0];
			}

			StringBuilder collectionNames = new StringBuilder();

			for (int i = 0; i < collections.Count; i++)
			{
				if (i > 0)
				{
					collectionNames.Append(", ");
				}

				collectionNames.Append(collections[i].TableCollectionName);
			}

			throw new InvalidOperationException(
				$"Multiple string table collections were found ({collectionNames}). " +
				"The CSV source must be assigned to one collection before import.");
		}

		private static string ReadProjectFile(string assetPath)
		{
			string filePath = GetProjectFilePath(assetPath);
			return File.Exists(filePath) ? File.ReadAllText(filePath) : null;
		}

		private static string GetProjectFilePath(string assetPath)
		{
			string projectPath = Directory.GetParent(Application.dataPath).FullName;
			return Path.Combine(projectPath, assetPath);
		}

		private static Dictionary<string, string> SnapshotTableFiles(StringTableCollection collection)
		{
			Dictionary<string, string> snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			foreach (StringTable table in collection.StringTables)
			{
				string tablePath = AssetDatabase.GetAssetPath(table);

				if (!string.IsNullOrEmpty(tablePath))
				{
					snapshots[tablePath] = ReadProjectFile(tablePath);
				}
			}

			return snapshots;
		}

		private static int NormalizeChangedTableEntries(
			StringTableCollection collection,
			Dictionary<string, string> beforeTables)
		{
			int normalizedEntries = 0;

			foreach (StringTable table in collection.StringTables)
			{
				string tablePath = AssetDatabase.GetAssetPath(table);

				if (string.IsNullOrEmpty(tablePath))
				{
					continue;
				}

				bool isNewTable = !beforeTables.TryGetValue(tablePath, out string beforeText);
				normalizedEntries += NormalizeTableFile(tablePath, beforeText, isNewTable);
			}

			return normalizedEntries;
		}

		private static int NormalizeTableFile(string assetPath, string beforeText, bool normalizeAllEntries)
		{
			if (string.IsNullOrEmpty(beforeText) && !normalizeAllEntries)
			{
				return 0;
			}

			string tablePath = GetProjectFilePath(assetPath);

			if (!File.Exists(tablePath))
			{
				return 0;
			}

			string afterText = File.ReadAllText(tablePath);

			Dictionary<string, string> beforeEntries = string.IsNullOrEmpty(beforeText)
				? new Dictionary<string, string>()
				: GetTableEntries(beforeText);

			MatchCollection afterMatches = _tableEntryRegex.Matches(afterText);
			StringBuilder normalizedText = new StringBuilder(afterText.Length);
			int previousEnd = 0;
			int normalizedEntries = 0;

			for (int i = 0; i < afterMatches.Count; i++)
			{
				Match match = afterMatches[i];
				int entryEnd = i + 1 < afterMatches.Count ? afterMatches[i + 1].Index : afterText.Length;
				string entry = afterText.Substring(match.Index, entryEnd - match.Index);
				string id = match.Groups["id"].Value;

				if (normalizeAllEntries
				    || !beforeEntries.TryGetValue(id, out string beforeEntry)
				    || beforeEntry != entry)
				{
					string normalizedEntry = NormalizeLocalizedValue(entry);

					if (normalizedEntry != entry)
					{
						normalizedText.Append(afterText, previousEnd, match.Index - previousEnd);
						normalizedText.Append(normalizedEntry);
						previousEnd = entryEnd;
						normalizedEntries++;
					}
				}
			}

			if (normalizedEntries == 0)
			{
				return 0;
			}

			normalizedText.Append(afterText, previousEnd, afterText.Length - previousEnd);
			File.WriteAllText(tablePath, normalizedText.ToString(), new UTF8Encoding(false));
			AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
			return normalizedEntries;
		}

		private static Dictionary<string, string> GetTableEntries(string text)
		{
			Dictionary<string, string> entries = new Dictionary<string, string>();
			MatchCollection matches = _tableEntryRegex.Matches(text);

			for (int i = 0; i < matches.Count; i++)
			{
				Match match = matches[i];
				int entryEnd = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
				entries[match.Groups["id"].Value] = text.Substring(match.Index, entryEnd - match.Index);
			}

			return entries;
		}

		private static string NormalizeLocalizedValue(string entry)
		{
			return _localizedValueRegex.Replace(entry, match =>
			{
				string value = match.Groups["value"].Value;
				value = Regex.Replace(value, @"\r?\n[ \t]+", " ");

				value = _unicodeEscapeRegex.Replace(value, unicodeMatch =>
				{
					int code = Convert.ToInt32(unicodeMatch.Groups["code"].Value, 16);

					return code == '"' || code == '\\' || code < 0x20
						? unicodeMatch.Value
						: ((char)code).ToString();
				});

				return match.Groups["prefix"].Value + FormatYamlValue(value);
			});
		}

		private static string FormatYamlValue(string value)
		{
			bool canUsePlainValue = !string.IsNullOrEmpty(value)
			                        && value == value.Trim()
			                        && !value.Contains("\r")
			                        && !value.Contains("\n")
			                        && !value.Contains("\"")
			                        && !value.Contains("\\")
			                        && !value.Contains(": ")
			                        && !value.Contains(" #")
			                        && !"-?:,[]{}#&*!|>'\"%@`".Contains(value[0])
			                        && value != "null"
			                        && value != "Null"
			                        && value != "NULL"
			                        && value != "~"
			                        && value != "true"
			                        && value != "false"
			                        && value != "True"
			                        && value != "False"
			                        && value != "TRUE"
			                        && value != "FALSE";

			return canUsePlainValue ? value : $"\"{value}\"";
		}
	}
}
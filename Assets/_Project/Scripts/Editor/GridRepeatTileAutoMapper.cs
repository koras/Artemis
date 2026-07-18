using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _Project.Scripts.Presentation.Grid;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace _Project.Scripts.Editor.Grid
{
    /// <summary>
    /// Builds 8x8 repeat tile assets from sliced sprites and assigns them to GridTilemapRenderSettings.
    /// </summary>
    public static class GridRepeatTileAutoMapper
    {
        private const int RepeatGridSize = 8;
        private const int RepeatTileCount = RepeatGridSize * RepeatGridSize;
        private const int CellPixels = 256;

        [MenuItem("Tools/Artemis/Grid/Auto Map 8x8 Repeat Tiles")]
        private static void AutoMap()
        {
            GridTilemapRenderSettings settings = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<GridTilemapRenderSettings>()
                : null;

            if (settings == null)
            {
                settings = UnityEngine.Object.FindFirstObjectByType<GridTilemapRenderSettings>();
            }

            if (settings == null)
            {
            // Debug.LogError("[GridRepeatTileAutoMapper] GridTilemapRenderSettings not found. Select GameObject with this component.");
                return;
            }

            var serializedObject = new SerializedObject(settings);

            try
            {
                MapType(serializedObject, "_ironRepeatSourceSprite", "_ironTilesByRepeatIndex", "Iron");
                MapType(serializedObject, "_titanRepeatSourceSprite", "_titanTilesByRepeatIndex", "Titan");
                MapType(serializedObject, "_aluminiumRepeatSourceSprite", "_aluminiumTilesByRepeatIndex", "aluminium");
                MapType(serializedObject, "_rogaliteRepeatSourceSprite", "_rogaliteTilesByRepeatIndex", "Rogalite");
                MapType(serializedObject, "_atmosphereRepeatSourceSprite", "_atmosphereTilesByRepeatIndex", "Atmosphere");
                MapType(serializedObject, "_defaultRepeatSourceSprite", "_defaultTilesByRepeatIndex", "Default");

                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            // Debug.Log("[GridRepeatTileAutoMapper] Completed.");
            }
            catch (Exception)
            {
            // Debug.LogError($"[GridRepeatTileAutoMapper] Failed: {exception.Message}");
            }
        }

        private static void MapType(SerializedObject settingsSerializedObject, string sourceSpritePropertyName, string targetTilesPropertyName, string typeName)
        {
            SerializedProperty sourceSpriteProperty = settingsSerializedObject.FindProperty(sourceSpritePropertyName);
            SerializedProperty targetTilesProperty = settingsSerializedObject.FindProperty(targetTilesPropertyName);

            if (sourceSpriteProperty == null || targetTilesProperty == null)
            {
                throw new InvalidOperationException($"Missing serialized fields for {typeName} mapping.");
            }

            var sourceSprite = sourceSpriteProperty.objectReferenceValue as Sprite;
            if (sourceSprite == null)
            {
            // Debug.LogWarning($"[GridRepeatTileAutoMapper] {typeName}: source sprite is empty, skipped.");
                return;
            }

            string sourceAssetPath = AssetDatabase.GetAssetPath(sourceSprite);
            var sprites = LoadOrderedSpritesFromSheet(sourceAssetPath);
            if (sprites.Count == 1)
            {
                TryAutoSliceSheet(sourceAssetPath, typeName);
                sprites = LoadOrderedSpritesFromSheet(sourceAssetPath);
            }

            if (sprites.Count != RepeatTileCount)
            {
                throw new InvalidOperationException($"{typeName}: expected {RepeatTileCount} sliced sprites ({RepeatGridSize}x{RepeatGridSize}), got {sprites.Count} in '{sourceAssetPath}'.");
            }

            string tileFolder = EnsureTileFolder(sourceAssetPath, typeName);
            EnsureArraySize(targetTilesProperty, RepeatTileCount);

            for (int index = 0; index < RepeatTileCount; index++)
            {
                Tile tileAsset = GetOrCreateTileAsset(tileFolder, typeName, index);
                tileAsset.sprite = sprites[index];
                EditorUtility.SetDirty(tileAsset);

                SerializedProperty tileProperty = targetTilesProperty.GetArrayElementAtIndex(index);
                tileProperty.objectReferenceValue = tileAsset;
            }
        }

        private static List<Sprite> LoadOrderedSpritesFromSheet(string spriteSheetAssetPath)
        {
            var sprites = AssetDatabase
                .LoadAllAssetRepresentationsAtPath(spriteSheetAssetPath)
                .OfType<Sprite>()
                // Unity sprite rect origin is bottom-left, so this matches x%8 + y%8*8.
                .OrderBy(sprite => sprite.rect.y)
                .ThenBy(sprite => sprite.rect.x)
                .ToList();

            return sprites;
        }

        private static void TryAutoSliceSheet(string spriteSheetAssetPath, string typeName)
        {
            var importer = AssetImporter.GetAtPath(spriteSheetAssetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spriteSheetAssetPath);
            if (texture == null)
            {
                return;
            }

            int expectedSize = RepeatGridSize * CellPixels;
            if (texture.width != expectedSize || texture.height != expectedSize)
            {
                throw new InvalidOperationException(
                    $"{typeName}: cannot auto-slice '{spriteSheetAssetPath}'. Expected texture size {expectedSize}x{expectedSize}, got {texture.width}x{texture.height}.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;

            var spritesheet = new SpriteMetaData[RepeatTileCount];
            int index = 0;
            for (int y = 0; y < RepeatGridSize; y++)
            {
                for (int x = 0; x < RepeatGridSize; x++)
                {
                    spritesheet[index] = new SpriteMetaData
                    {
                        name = $"{Path.GetFileNameWithoutExtension(spriteSheetAssetPath)}_{index:D2}",
                        rect = new Rect(x * CellPixels, y * CellPixels, CellPixels, CellPixels),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                    index++;
                }
            }

            // Legacy spritesheet API is used here for one-click tooling compatibility.
            // TODO: migrate to ISpriteEditorDataProvider when this tool is expanded.
#pragma warning disable CS0618
            importer.spritesheet = spritesheet;
#pragma warning restore CS0618
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            AssetDatabase.ImportAsset(spriteSheetAssetPath, ImportAssetOptions.ForceUpdate);
        }

        private static string EnsureTileFolder(string spriteSheetAssetPath, string typeName)
        {
            string spriteDirectory = Path.GetDirectoryName(spriteSheetAssetPath)?.Replace("\\", "/") ?? "Assets";
            string generatedRoot = $"{spriteDirectory}/GeneratedRepeatTiles";
            string typeFolder = $"{generatedRoot}/{typeName}";

            CreateFolderIfMissing(spriteDirectory, "GeneratedRepeatTiles");
            CreateFolderIfMissing(generatedRoot, typeName);

            return typeFolder;
        }

        private static void CreateFolderIfMissing(string parentPath, string folderName)
        {
            string folderPath = $"{parentPath}/{folderName}";
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            AssetDatabase.CreateFolder(parentPath, folderName);
        }

        private static Tile GetOrCreateTileAsset(string folderPath, string typeName, int index)
        {
            string assetPath = $"{folderPath}/{typeName}_Repeat_{index:D2}.asset";
            Tile tileAsset = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (tileAsset != null)
            {
                return tileAsset;
            }

            tileAsset = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tileAsset, assetPath);
            return tileAsset;
        }

        private static void EnsureArraySize(SerializedProperty arrayProperty, int size)
        {
            if (arrayProperty.arraySize == size)
            {
                return;
            }

            arrayProperty.arraySize = size;
        }
    }
}

using System;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using OnlineMaps.Editors.Windows;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public partial class MapEditor
    {
        private const string ProviderHelpMessage = "Tile provider.\nImportant: all tile presets are for testing purpose only. Before using the tile provider, make sure that it suits you by the terms of use and price.";
        
        private bool hasGoogleTilesProvider;
        private MapType mapType;
        private GUIContent providerContent;
        private int providerIndex;
        private TileProvider[] providers;
        private string[] providersTitle;
        private bool showCustomProviderTokens;
        private bool showResourcesTokens;
        private GUIContent wizardIconContent;

        private void CheckMapTypeChanges()
        {
            if (!Application.isPlaying) return;
            if (mapType == map.activeType) return;
            
            string pid = map.activeType.provider.id;
            providerIndex = Array.FindIndex(providers, p => p.id == pid);
            mapType = map.activeType;
        }

        private void DrawAvailableLocalTokens()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            showResourcesTokens = EditorUtils.Foldout(showResourcesTokens, "Available Tokens");
            if (showResourcesTokens)
            {
                GUILayout.Label("{zoom} - Tile zoom");
                GUILayout.Label("{x} - Tile x");
                GUILayout.Label("{y} - Tile y");
                GUILayout.Label("{quad} - Tile quad key");
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawCustomProviderUrl()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            GUILayout.Space(5);
            EditorGUILayout.PropertyField(pCustomProviderURL);
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(wizardIconContent, GUILayout.ExpandWidth(false))) CustomURLWizard.OpenWindow();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            showCustomProviderTokens = EditorUtils.Foldout(showCustomProviderTokens, "Available tokens");
            if (showCustomProviderTokens)
            {
                GUILayout.Label("{zoom} or {z} - Tile zoom");
                GUILayout.Label("{x} - Tile X");
                GUILayout.Label("{y} - Tile Y");
                GUILayout.Label("{quad} - Tile Quad");
                GUILayout.Space(10);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLabelsGUI()
        {
            if (mapType.isCustom) return;

            bool showLanguage;
            if (mapType.hasLabels)
            {
                EditorUtils.PropertyField(pLabels, "Show labels?");
                showLanguage = pLabels.boolValue;
            }
            else
            {
                showLanguage = mapType.labelsEnabled;
                GUILayout.Label("Labels " + (showLanguage ? "enabled" : "disabled"));
            }
            if (showLanguage && mapType.hasLanguage)
            {
                EditorUtils.PropertyField(pLanguage, mapType.provider.twoLetterLanguage ? "Use two-letter code such as: en" : "Use three-letter code such as: eng");
            }
        }

        private void DrawMapType()
        {
            if (mapType.provider.types.Length < 2) return;
            
            GUIContent[] availableTypes = mapType.provider.types.Select(t => new GUIContent(t.title)).ToArray();
            int index = mapType.index;
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string tooltip = "Type (style) of the map";
            index = EditorGUILayout.Popup(new GUIContent("Type", tooltip), index, availableTypes);
            if (EditorGUI.EndChangeCheck())
            {
                mapType = mapType.provider.types[index];
                pMapType.stringValue = mapType.ToString();
            }
            EditorUtils.HelpButton(tooltip);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawProvider()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();

            CheckMapTypeChanges();
            providerIndex = EditorGUILayout.Popup(providerContent, providerIndex, providersTitle);
            if (EditorGUI.EndChangeCheck())
            {
                mapType = providers[providerIndex].types[0];
                pMapType.stringValue = mapType.ToString();
                pActiveTypeSettings.stringValue = "";
            }

            EditorUtils.HelpButton(ProviderHelpMessage);

            EditorGUILayout.EndHorizontal();

            DrawProviderAdditional();
        }

        private void DrawProviderAdditional()
        {
            if (mapType.useHTTP)
            {
                EditorGUILayout.HelpBox(mapType.provider.title + " - " + mapType.title + " uses HTTP, which can cause problems in iOS9+.", MessageType.Warning);
            }
            else if (mapType.isCustom)
            {
                DrawCustomProviderUrl();
            }
        }

        private void DrawGoogleTilesProviderMessage()
        {
            if (Application.isPlaying) return;
            if (hasGoogleTilesProvider) return;
            if (mapType.provider.id != "google") return;
            
            const string message = "The Google Tiles API allows you to use Google Maps tiles in more advanced ways.";
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("Add Google Tiles Provider Component"))
            {
                map.gameObject.AddComponent<GoogleTilesProvider>().map = map;
                hasGoogleTilesProvider = false;
            }
        }

        private void DrawProviderAndType()
        {
            if (control && !control.useRasterTiles) return;
            if (pSource.enumValueIndex == (int) MapSource.Resources) return;
            if (pSource.enumValueIndex == (int)MapSource.StreamingAssets) return;

            DrawProvider();
            DrawMapType();

            DrawProviderExtraFields(mapType.provider.extraFields);
            DrawProviderExtraFields(mapType.extraFields);
            if (mapType.fullID == "google.satellite")
            {
                if (GUILayout.Button("Detect the latest version of tiles")) DetectGoogleSatelliteVersion();
            }
            DrawLabelsGUI();
        }

        private void DetectGoogleSatelliteVersion()
        {
            WebClient client = new WebClient();
            string response = client.DownloadString("https://maps.googleapis.com/maps/api/js");
            Match match = Regex.Match(response, @"kh\?v=(\d+)");
            if (match.Success)
            {
                TileProvider.ExtraField version = mapType.extraFields.FirstOrDefault(f =>
                {
                    TileProvider.ExtraField ef = f as TileProvider.ExtraField;
                    if (ef == null) return false;
                    if (ef.token != "version") return false;
                    return true;
                }) as TileProvider.ExtraField;
                if (version != null)
                {
                    version.value = match.Groups[1].Value;
                    EditorUtility.SetDirty(target);
                }
            }
        }

        private void DrawProviderExtraFields(TileProvider.IExtraField[] extraFields)
        {
            if (extraFields == null) return;

            foreach (TileProvider.IExtraField field in extraFields)
            {
                if (field is TileProvider.ToggleExtraGroup) DrawProviderToggleExtraGroup(field as TileProvider.ToggleExtraGroup);
                else if (field is TileProvider.ExtraField) DrawProviderExtraField(field as TileProvider.ExtraField);
                else if (field is TileProvider.LabelField)
                {
                    EditorGUILayout.LabelField((field as TileProvider.LabelField).label);
                }
            }
        }

        private void DrawProviderExtraField(TileProvider.ExtraField field)
        {
            EditorGUI.BeginChangeCheck();
            field.value = EditorGUILayout.TextField(field.title, field.value);
            if (EditorGUI.EndChangeCheck()) isDirty = true;
        }

        private void DrawProviderToggleExtraGroup(TileProvider.ToggleExtraGroup group)
        {
            group.value = EditorGUILayout.Toggle(group.title, group.value);
            EditorGUI.BeginDisabledGroup(group.value);

            if (group.fields != null)
            {
                foreach (TileProvider.IExtraField field in group.fields)
                {
                    if (field is TileProvider.ToggleExtraGroup) DrawProviderToggleExtraGroup(field as TileProvider.ToggleExtraGroup);
                    else if (field is TileProvider.ExtraField) DrawProviderExtraField(field as TileProvider.ExtraField);
                }
            }

            EditorGUI.EndDisabledGroup();
        }

        private void DrawSourceGUI()
        {
            EditorGUI.BeginDisabledGroup(Utils.isPlaying);

            EditorUtils.PropertyField(pSource, "Source of tiles");
            if (pSource.enumValueIndex != (int)MapSource.Online) DrawSourceOffline();

            EditorGUI.EndDisabledGroup();

            DrawProviderAndType();
        }

        private void DrawSourceOffline()
        {
            if (GUILayout.Button("Fix Import Settings for Tiles"))
            {
                TileImporterUtils.FixImportSettings((MapSource)pSource.enumValueIndex, pResourcesPath.stringValue, pStreamingAssetsPath.stringValue);
            }

            if (GUILayout.Button("Import from GMapCatcher"))
            {
                GMapCatcherImporter.Import((MapSource)pSource.enumValueIndex);
            }

            if (pSource.enumValueIndex == (int) MapSource.Resources || pSource.enumValueIndex == (int) MapSource.ResourcesAndOnline)
            {
                EditorUtils.PropertyField(pResourcesPath, "The path pattern inside Resources folder");
            }
            else
            {
                EditorUtils.PropertyField(pStreamingAssetsPath, "The path pattern inside Streaming Assets folder");
#if UNITY_WEBGL
                EditorGUILayout.HelpBox("Streaming Assets folder is not available for WebGL!", MessageType.Warning);
#endif
            }

            DrawAvailableLocalTokens();
        }

        private void OnSourceEnable()
        {
            mapType = TileProvider.FindMapType(pMapType.stringValue);
            providerIndex = mapType.provider.index;
            hasGoogleTilesProvider = map.GetComponent<GoogleTilesProvider>();
            
            providerContent = new GUIContent("Provider", ProviderHelpMessage);
            wizardIconContent = new GUIContent(EditorUtils.LoadAsset<Texture2D>("Icons/WizardIcon.png"), "Wizard");
        }
    }
}
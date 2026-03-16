using System;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(GoogleTilesProvider))]
    public class GoogleTilesProviderEditor: Editor
    {
        private GoogleTilesProvider provider;
        private KeyManager keyManager;

        private void OnEnable()
        {
            provider = target as GoogleTilesProvider;
            keyManager = provider.GetComponent<KeyManager>();
            if (!keyManager) keyManager = Compatibility.FindObjectOfType<KeyManager>();
        }

        public override void OnInspectorGUI()
        {
            DrawKeyManagerWarnings();
            DrawCloudConsoleHint();

            base.OnInspectorGUI();
        }

        private void DrawCloudConsoleHint()
        {
            const string message = "Please ensure that you have enabled the Map Tiles API in the Google Cloud Console.";
            EditorGUILayout.HelpBox(message, MessageType.Info);
            if (GUILayout.Button("Open Google Cloud Console"))
            {
                Application.OpenURL("https://console.cloud.google.com/apis/library/tile.googleapis.com");
            }
        }

        private void DrawKeyManagerWarnings()
        {
            if (!keyManager)
            {
                const string message = "KeyManager component not found in the scene. Please add KeyManager to the scene to manage your API keys.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                if (GUILayout.Button("Add KeyManager to Scene"))
                {
                    keyManager = provider.gameObject.AddComponent<KeyManager>();
                }
            }
            else if (string.IsNullOrEmpty(keyManager.googleMaps))
            {
                const string message = "Google Maps API key is not set in KeyManager. Please set your API key to use Google Maps tiles.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);
            }
        }
    }
}
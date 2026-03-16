/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps.Editors.Windows;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(Map))]
    public partial class MapEditor : Editor
    {
        private Map map;
        private ControlBase control;
        
        private bool wrongInputModuleDetected;

        private SerializedProperty pSource;
        private SerializedProperty pMapType;

        private SerializedProperty pLabels;
        private SerializedProperty pCustomProviderURL;
        private SerializedProperty pResourcesPath;
        private SerializedProperty pStreamingAssetsPath;
        
        private SerializedProperty pLanguage;
        private SerializedProperty pActiveTypeSettings;
        
        private bool isDirty;

        private void CacheSerializedProperties()
        {
            pSource = serializedObject.FindProperty("source");
            pMapType = serializedObject.FindProperty("mapType");

            pCustomProviderURL = serializedObject.FindProperty("customProviderURL");
            pResourcesPath = serializedObject.FindProperty("resourcesPath");
            pStreamingAssetsPath = serializedObject.FindProperty("streamingAssetsPath");

            pLabels = serializedObject.FindProperty("labels");
            pLanguage = serializedObject.FindProperty("language");
            
            pActiveTypeSettings = serializedObject.FindProperty("_activeTypeSettings");
            
            CacheAdvancedProperties();
            CacheRuntimeProperties();
            CacheToolbarProperties();
            CacheTroubleshootingProperties();
        }

        private void DrawAddControlButton()
        {
            if (!GUILayout.Button("Add Control")) return;
            
            GenericMenu menu = new GenericMenu();

            TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ControlBase>();
            foreach (Type t in types)
            {
                if (t.IsAbstract) continue;
                string className = t.Name;
                int controlIndex = className.IndexOf("Control");
                className = className.Insert(controlIndex, " ");

                int textureIndex = className.IndexOf("Texture");
                if (textureIndex > 0) className = className.Insert(textureIndex, " ");

                menu.AddItem(new GUIContent(className), false, data =>
                {
                    Type ct = data as Type;
                    map.gameObject.AddComponent(ct);
                    Repaint();
                }, t);
            }

            menu.ShowAsContext();
        }

        private void DrawGeneralGUI()
        {
            DrawSourceGUI();
            DrawLocationGUI();
            DrawPlaymodeButtons();
        }

        private void DrawLocationGUI()
        {
            GeoPoint center = map.view.center;

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUI.BeginChangeCheck();
            center.y = EditorGUILayout.DoubleField(new GUIContent("Latitude", "Latitude of the center point of the map"), center.y);
            center.x = EditorGUILayout.DoubleField(new GUIContent("Longitude", "Longitude of the center point of the map"), center.x);

            if (EditorGUI.EndChangeCheck())
            {
                isDirty = true;
                map.view.center = center;
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical(GUILayout.Width(16));
            GUILayout.Space(10);
            EditorUtils.HelpButton("Coordinates of the center point of the map");
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.BeginHorizontal();
            string tooltip = "Current zoom of the map";
            float newZoom = EditorGUILayout.Slider(new GUIContent("Zoom", tooltip), map.view.zoom, Constants.MinZoom, Constants.MaxZoomExt);

            if (EditorGUI.EndChangeCheck())
            {
                map.view.zoom = newZoom;
                isDirty = true;
            }

            EditorUtils.HelpButton(tooltip);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNullControlWarning()
        {
            if (control) return;

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.HelpBox("Problem detected:\nCan not find OnlineMaps Control component.", MessageType.Error);
            DrawAddControlButton();

            EditorGUILayout.EndVertical();
        }

        private void DrawWrongInputModuleWarning()
        {
            if (!wrongInputModuleDetected) return;
            
            const string message = "Problem detected:\nYou are using StandaloneInputModule, which uses the old InputManager. You are using the new InputSystem, nad have old InputManager disabled. StandaloneInputManager will not work. Click the button below to replace StandaloneInputModule with a InputSystemUIInputModule, which uses the new InputSystem";
            EditorGUILayout.HelpBox(message, MessageType.Error);
            GUILayout.Space(5);
            if (GUILayout.Button("Replace with InputSystemUIInputModule"))
            {
                if (ComponentUtils.ReplaceInputModule())
                {
                    wrongInputModuleDetected = false;
                }
            }
        }

        private void OnEnable()
        {
            map = target as Map;
            control = map.GetComponent<ControlBase>();
            
            try
            {
                CacheSerializedProperties();

                providers = TileProvider.providers;
                providersTitle = TileProvider.GetProvidersTitle();

                Updater.CheckNewVersionAvailable();
                OnSourceEnable();

                serializedObject.ApplyModifiedProperties();
                
                wrongInputModuleDetected = ComponentUtils.HasWrongInputModule();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawToolbar();
            DrawWrongInputModuleWarning();

            serializedObject.Update();

            isDirty = false;

            try
            {
                DrawGeneralGUI();
                DrawAdvancedBlock();
                DrawTroubleshootingBlock();
                DrawNullControlWarning();
                DrawGoogleTilesProviderMessage();

                serializedObject.ApplyModifiedProperties();

                if (isDirty)
                {
                    EditorUtility.SetDirty(map);
                    if (!Utils.isPlaying) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    else map.Redraw();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OnlineMaps
{
    /// <summary>
    /// Custom inspector for RouteEmulator
    /// </summary>
    [CustomEditor(typeof(RouteEmulator))]
    public class RouteEmulatorEditor : Editor
    {
        private RouteEmulator routeEmulator;
        private SerializedProperty routeSourceTypeProp;
        private SerializedProperty speedProp;
        private SerializedProperty speedUnitProp;
        private SerializedProperty coordinatesProp;
        private SerializedProperty originTypeProp;
        private SerializedProperty originAddressProp;
        private SerializedProperty originLocationProp;
        private SerializedProperty destinationTypeProp;
        private SerializedProperty destinationAddressProp;
        private SerializedProperty destinationLocationProp;
        private SerializedProperty isPlayingProp;

        private bool showStatus = true;
        private ReorderableList coordinatesList;

        private void DrawOriginSection()
        {
            EditorGUILayout.PropertyField(originTypeProp, new GUIContent("Origin"));

            RouteEmulator.RoutingPointType originType = (RouteEmulator.RoutingPointType)originTypeProp.enumValueIndex;

            if (originType == RouteEmulator.RoutingPointType.Address)
            {
                EditorGUILayout.PropertyField(originAddressProp, new GUIContent("Address"));
            }
            else
            {
                EditorGUILayout.PropertyField(originLocationProp, new GUIContent("Location"));
            }
        }

        private void DrawDestinationSection()
        {

            EditorGUILayout.PropertyField(destinationTypeProp, new GUIContent("Destination"));

            RouteEmulator.RoutingPointType destinationType = (RouteEmulator.RoutingPointType)destinationTypeProp.enumValueIndex;

            if (destinationType == RouteEmulator.RoutingPointType.Address)
            {
                EditorGUILayout.PropertyField(destinationAddressProp, new GUIContent("Address"));
            }
            else
            {
                EditorGUILayout.PropertyField(destinationLocationProp, new GUIContent("Location"));
            }
        }

        private void DrawPlayControls()
        {
            bool isRouteActive = isPlayingProp.boolValue;
            bool isPaused = routeEmulator.IsPaused();

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !isRouteActive || isPaused;

            if (GUILayout.Button(isPaused ? "Resume" : "Start", GUILayout.Width(80)))
            {
                routeEmulator.Play();
            }

            GUI.enabled = isRouteActive && !isPaused;

            if (GUILayout.Button("Pause", GUILayout.Width(80)))
            {
                routeEmulator.Pause();
            }

            GUI.enabled = isRouteActive;

            if (GUILayout.Button("Reset", GUILayout.Width(80)))
            {
                routeEmulator.Reset();
            }

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpeedSection()
        {
            // Speed Settings (always editable)
            EditorGUILayout.LabelField("Speed Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(speedProp, new GUIContent("Speed"));
            EditorGUILayout.PropertyField(speedUnitProp, new GUIContent("Unit"));

            if (speedUnitProp.enumValueIndex == (int)RouteEmulator.SpeedUnit.MetersPerSecond)
            {
                double speedInKMH = routeEmulator.GetSpeedInKmPerHour();
                EditorGUILayout.LabelField($"Speed in km/h: {speedInKMH:F2}", EditorStyles.miniLabel);
            }
            else
            {
                double speedInMPS = routeEmulator.GetSpeedInMetersPerSecond();
                EditorGUILayout.LabelField($"Speed in m/s: {speedInMPS:F2}", EditorStyles.miniLabel);
            }
        }

        private void DrawStatusSection()
        {
            showStatus = EditorGUILayout.Foldout(showStatus, "Status", true);
            if (!showStatus) return;
            
            EditorGUI.indentLevel++;

            if (routeEmulator)
            {
                string latStr = routeEmulator.currentLocation.latitude.ToString("F6", Culture.numberFormat);
                string lngStr = routeEmulator.currentLocation.longitude.ToString("F6", Culture.numberFormat);
                EditorGUILayout.LabelField($"Current Location: (latitude: {latStr}, longitude: {lngStr})");
                EditorGUILayout.LabelField($"Progress: {( routeEmulator.currentProgress * 100).ToString("F1", Culture.numberFormat)}%");
                EditorGUILayout.LabelField($"Elapsed Time: {routeEmulator.elapsedTime.ToString("F1", Culture.numberFormat)}s");
                EditorGUILayout.LabelField($"Total Distance: {routeEmulator.totalDistance.ToString("F0", Culture.numberFormat)}m");

                string status = routeEmulator.currentProgress >= 1 ? "Completed" : (isPlayingProp.boolValue ? (routeEmulator.IsPaused() ? "Paused" : "Playing") : "Stopped");
                EditorGUILayout.LabelField($"Status: {status}");
            }
            else
            {
                EditorGUILayout.LabelField("Route Emulator not initialized");
            }

            EditorGUI.indentLevel--;
        }

        private void OnEnable()
        {
            routeEmulator = (RouteEmulator)target;

            routeSourceTypeProp = serializedObject.FindProperty("routeSourceType");
            speedProp = serializedObject.FindProperty("speed");
            speedUnitProp = serializedObject.FindProperty("speedUnit");
            coordinatesProp = serializedObject.FindProperty("coordinates");
            originTypeProp = serializedObject.FindProperty("originType");
            originAddressProp = serializedObject.FindProperty("originAddress");
            originLocationProp = serializedObject.FindProperty("originLocation");
            destinationTypeProp = serializedObject.FindProperty("destinationType");
            destinationAddressProp = serializedObject.FindProperty("destinationAddress");
            destinationLocationProp = serializedObject.FindProperty("destinationLocation");
            isPlayingProp = serializedObject.FindProperty("isPlaying");

            bool isEditable = !Application.isPlaying;

            coordinatesList = new ReorderableList(serializedObject, coordinatesProp, isEditable, true, isEditable, isEditable);
            coordinatesList.drawHeaderCallback = (Rect rect) => { EditorGUI.LabelField(rect, $"Coordinates ({coordinatesProp.arraySize})"); };
            coordinatesList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
            {
                SerializedProperty element = coordinatesProp.GetArrayElementAtIndex(index);
                EditorGUI.PropertyField(rect, element, new GUIContent("Point " + (index + 1)));
            };
            coordinatesList.elementHeightCallback = index => EditorGUI.GetPropertyHeight(coordinatesProp.GetArrayElementAtIndex(index), GUIContent.none);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool isRouteActive = isPlayingProp.boolValue;
            bool isRouteEditable = !isRouteActive && !Application.isPlaying;
            
            DrawSpeedSection();

            // Play Controls
            if (Application.isPlaying)
            {
                EditorGUILayout.Space(5);
                DrawPlayControls();
            }

            // Status Section
            if (Application.isPlaying || isRouteActive)
            {
                EditorGUILayout.Space(5);
                DrawStatusSection();
            }
            
            // Route Source Type
            GUI.enabled = isRouteEditable;
            EditorGUILayout.PropertyField(routeSourceTypeProp, new GUIContent("Route Source Type"));

            // Route Source Specific UI
            RouteEmulator.RouteSourceType sourceType = (RouteEmulator.RouteSourceType)routeSourceTypeProp.enumValueIndex;

            if (sourceType == RouteEmulator.RouteSourceType.Coordinates)
            {
                GUI.enabled = isRouteEditable;
                coordinatesList.DoLayoutList();
                GUI.enabled = true;
            }
            else if (sourceType == RouteEmulator.RouteSourceType.GoogleRoutingAPI)
            {
                DrawOriginSection();
                DrawDestinationSection();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}

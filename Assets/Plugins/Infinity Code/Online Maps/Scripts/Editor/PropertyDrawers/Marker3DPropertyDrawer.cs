/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(Marker3D))]
    public class Marker3DPropertyDrawer : MarkerBasePropertyDrawer
    {
        private static Dictionary<string, bool> altitudeVisibility = new Dictionary<string, bool>();
        public static float? isRotationChanged;
        protected override int countFields => Utils.isPlaying? 12: 11;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.FindPropertyRelative("expand").boolValue) return EditorGUIUtility.singleLineHeight;
            int fields = countFields;
            bool showAltitude;
            if (!altitudeVisibility.TryGetValue(property.propertyPath, out showAltitude))
            {
                SerializedProperty altitude = property.FindPropertyRelative("altitude");
                SerializedProperty altitudeType = property.FindPropertyRelative("altitudeType");
                showAltitude = altitude.floatValue != 0 || altitudeType.enumValueIndex != (int)AltitudeType.relative;
            }
            if (!showAltitude) fields -= 1;
            return fields * EditorGUIUtility.singleLineHeight + (fields - 1) * EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            try
            {
                Rect rect = new Rect(position.x, position.y, position.width, 16);

                if (!DrawHeader(label, rect, property))
                {
                    EditorGUI.EndProperty();
                    return;
                }
                
                rect.y += EditorGUIUtility.singleLineHeight;

                SerializedProperty location = DrawLocationProperty(property, "_location", ref rect);

                SerializedProperty altitude = property.FindPropertyRelative("altitude");
                SerializedProperty altitudeType = property.FindPropertyRelative("altitudeType");
                bool showAltitude;
                if (!altitudeVisibility.TryGetValue(property.propertyPath, out showAltitude))
                {
                    showAltitude = altitude.floatValue != 0 || altitudeType.enumValueIndex != (int)AltitudeType.relative;
                }

                if (showAltitude)
                {
                    DrawProperty(altitude, ref rect);
                    DrawProperty(altitudeType, ref rect);
                }
                else
                {
                    float labelWidth = EditorGUIUtility.labelWidth;
                    Rect r = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth, rect.height);
                    GUI.Label(r, "Altitude");
                    r.x += r.width;
                    r.xMax = rect.xMax;
                    if (GUI.Button(r, "Show"))
                    {
                        altitudeVisibility[property.propertyPath] = true;
                    }
                    rect.y += EditorGUIUtility.singleLineHeight;
                }
                DrawProperty(property, "range", ref rect, TempContent.Get("Zooms"));
                DrawProperty(property, "_scale", ref rect);
                DrawProperty(property, "sizeType", ref rect);

                EditorGUI.BeginChangeCheck();
                DrawProperty(property, "_rotation", ref rect);
                if (EditorGUI.EndChangeCheck() && Utils.isPlaying) isRotationChanged = property.FindPropertyRelative("_rotation").floatValue;

                DrawProperty(property, "label", ref rect);
                
                EditorGUI.BeginChangeCheck();
                DrawProperty(property, "_prefab", ref rect);
                if (EditorGUI.EndChangeCheck() && Utils.isPlaying)
                {
                    
                }

                DrawCenterButton(rect, location);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }


            EditorGUI.EndProperty();
        }
    }
}
/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace OnlineMaps.Editors
{
    [CustomPropertyDrawer(typeof(OverlayManager.OverlayLayer), true)]
    public class OverlayLayerPropertyDrawer : PropertyDrawer
    {
        private static Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
        public override void OnGUI(Rect area, SerializedProperty property, GUIContent label)
        {
            bool isPlaying = Application.isPlaying;
            
            EditorGUI.BeginProperty(area, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect labelRect = new Rect(area.x, area.y, area.width, lineHeight);
            EditorGUI.LabelField(labelRect, label);

            EditorGUI.BeginDisabledGroup(isPlaying);
            SerializedProperty enabledProp = property.FindPropertyRelative("_enabled");
            SerializedProperty tileLayerProp = property.FindPropertyRelative("tileLayer");
            SerializedProperty zoomRangeProp = property.FindPropertyRelative("zoomRange");
            SerializedProperty alphaProp = property.FindPropertyRelative("alpha");
            EditorGUI.EndDisabledGroup();

            Rect r = new Rect(area.x, area.y + lineHeight + spacing, area.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(r, enabledProp);
            if (EditorGUI.EndChangeCheck() && isPlaying)
            {
#if UNITY_2022_1_OR_NEWER
                OverlayManager.OverlayLayer layer = property.boxedValue as OverlayManager.OverlayLayer;
                layer?.SetEnabled(enabledProp.boolValue, property.serializedObject.targetObject as OverlayManager);
#endif
            }
            r.y += lineHeight + spacing;
            
            EditorGUI.BeginDisabledGroup(isPlaying);
            float zoomH = EditorGUI.GetPropertyHeight(zoomRangeProp, true);
            r.height = zoomH;
            EditorGUI.PropertyField(r, zoomRangeProp, true);
            r.y += zoomH + spacing;
                
            DrawSource(property, ref r);

            EditorGUI.PropertyField(r, tileLayerProp);
            r.y += lineHeight + spacing;
            
            EditorGUI.EndDisabledGroup();

            int tileLayerIndex = tileLayerProp.enumValueIndex;
            if (tileLayerIndex != 2)
            {
                r.height = lineHeight;
                EditorGUI.PropertyField(r, alphaProp);
                r.y += lineHeight + spacing;
            }

            EditorGUI.EndProperty();
        }

        private void DrawSource(SerializedProperty property, ref Rect r)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            // source
            SerializedProperty sourceProp = property.FindPropertyRelative("source");
            EditorGUI.PropertyField(r, sourceProp);
            r.y += lineHeight + spacing;
                
            // url or path based on source
            int sourceIndex = sourceProp.enumValueIndex; // 0: online, 1: resources, 2: streamingAssets
            if (sourceIndex == 0)
            {
                SerializedProperty urlProp = property.FindPropertyRelative("url");
                
                float h = EditorGUI.GetPropertyHeight(urlProp, true);
                r.height = h;
                EditorGUI.PropertyField(r, urlProp);
                r.y += h + spacing;
            }
            else // resources or streamingAssets
            {
                SerializedProperty pathProp = property.FindPropertyRelative("path");
                float h = EditorGUI.GetPropertyHeight(pathProp, true);
                r.height = h;
                EditorGUI.PropertyField(r, pathProp);
                r.y += h + spacing;
            }
            
            DrawAvailableTokens(property, ref r);
        }

        private static void DrawAvailableTokens(SerializedProperty property, ref Rect r)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            // Foldout for available tokens with unique key
            string foldoutKey = property.propertyPath;
            if (!foldoutStates.TryGetValue(foldoutKey, out bool showUrlTokens)) showUrlTokens = false;
                
            Rect foldoutRect = new Rect(r.x, r.y, r.width, lineHeight);
            EditorGUI.BeginChangeCheck();
            showUrlTokens = EditorGUI.Foldout(foldoutRect, showUrlTokens, "Available tokens", true);
            if (EditorGUI.EndChangeCheck())
            {
                foldoutStates[foldoutKey] = showUrlTokens;
            }
                
            r.y += lineHeight + spacing;
                
            if (showUrlTokens)
            {
                Rect boxRect = new Rect(r.x + 10, r.y, r.width - 10, 3 * lineHeight);
                    
                Rect labelRect = new Rect(boxRect.x + 5, boxRect.y, boxRect.width - 10, lineHeight);
                EditorGUI.LabelField(labelRect, "{zoom} or {z} - Tile zoom");
                labelRect.y += lineHeight;
                EditorGUI.LabelField(labelRect, "{x} - Tile X");
                labelRect.y += lineHeight;
                EditorGUI.LabelField(labelRect, "{y} - Tile Y");
                    
                r.y += 3 * lineHeight + spacing * 2;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            float height = lineHeight;
            
            height += lineHeight + spacing; // source
            height += lineHeight + spacing; // enabled
            height += lineHeight + spacing; // tileLayer

            SerializedProperty zoomRangeProp = property.FindPropertyRelative("zoomRange");
            height += EditorGUI.GetPropertyHeight(zoomRangeProp, true) + spacing;

            SerializedProperty sourceProp = property.FindPropertyRelative("source");
            int sourceIndex = sourceProp.enumValueIndex;
            if (sourceIndex == 0)
            {
                // Height for URL field
                SerializedProperty urlProp = property.FindPropertyRelative("url");
                height += EditorGUI.GetPropertyHeight(urlProp, true) + spacing;
            }
            else
            {
                SerializedProperty pathProp = property.FindPropertyRelative("path");
                height += EditorGUI.GetPropertyHeight(pathProp, true) + spacing;
            }
            
            // Height for foldout
            height += lineHeight + spacing;
                
            // Height for tokens box (estimated 3 lines + spacing)
            string foldoutKey = property.propertyPath;
            if (foldoutStates.TryGetValue(foldoutKey, out bool showUrlTokens) && showUrlTokens)
            {
                height += 3 * lineHeight + spacing;
            }

            SerializedProperty tileLayerProp = property.FindPropertyRelative("tileLayer");
            int tileLayerIndex = tileLayerProp.enumValueIndex;
            if (tileLayerIndex != 2)
            {
                height += lineHeight + spacing; // alpha
            }

            return height;
        }
    }
}
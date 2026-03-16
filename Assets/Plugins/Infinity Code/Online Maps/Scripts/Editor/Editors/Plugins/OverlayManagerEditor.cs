/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OnlineMaps.Editors
{
    [CustomEditor(typeof(OverlayManager))]
    public class OverlayManagerEditor : Editor
    {
        private OverlayManager manager;
        private SerializedProperty map;
        private SerializedProperty layers;
        private ReorderableList reorderableList;

        private void AddLayer()
        {
            Undo.RecordObject(manager, "Add Overlay Layer");
            List<OverlayManager.OverlayLayer> tmp = new List<OverlayManager.OverlayLayer>(manager.layers ?? Array.Empty<OverlayManager.OverlayLayer>())
            {
                new OverlayManager.OverlayLayer()
            };
            manager.layers = tmp.ToArray();
            EditorUtility.SetDirty(manager);
            serializedObject.Update();
        }

        private void InitReorderableList()
        {
            bool isPlaying = Application.isPlaying;
            reorderableList = new ReorderableList(
                serializedObject,
                layers,
                draggable: !isPlaying,
                displayHeader: true,
                displayAddButton: !isPlaying,
                displayRemoveButton: !isPlaying
            )
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Overlay Layers"),
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty element = layers.GetArrayElementAtIndex(index);
                    Rect elRect = new Rect(rect.x, rect.y, rect.width, EditorGUI.GetPropertyHeight(element, true));
                    EditorGUI.PropertyField(elRect, element, new GUIContent("Layer " + (index + 1)), true);
                },
                elementHeightCallback = index =>
                {
                    SerializedProperty element = layers.GetArrayElementAtIndex(index);
                    return EditorGUI.GetPropertyHeight(element, true) + 6;
                }
            };

            reorderableList.onAddCallback += _ => AddLayer();
            reorderableList.onRemoveCallback += RemoveLayer;
        }

        private void OnEnable()
        {
            manager = (OverlayManager)target;
            map = serializedObject.FindProperty("map");
            layers = serializedObject.FindProperty("layers");

            if (manager.layers == null) manager.layers = Array.Empty<OverlayManager.OverlayLayer>();

            InitReorderableList();

            serializedObject.ApplyModifiedProperties();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(map);
            reorderableList.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        private void RemoveLayer(ReorderableList list)
        {
            if (list.index < 0 || list.index >= manager.layers.Length) return;
            Undo.RecordObject(manager, "Remove Overlay Layer");
            List<OverlayManager.OverlayLayer> tmp = new List<OverlayManager.OverlayLayer>(manager.layers);
            tmp.RemoveAt(list.index);
            manager.layers = tmp.ToArray();
            EditorUtility.SetDirty(manager);
            serializedObject.Update();
        }
    }
}
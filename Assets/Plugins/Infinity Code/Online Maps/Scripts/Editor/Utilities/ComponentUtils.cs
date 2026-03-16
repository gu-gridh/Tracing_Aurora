/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace OnlineMaps.Editors
{
    public static class ComponentUtils
    {
        public static bool HasWrongInputModule()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            StandaloneInputModule[] inputModules = Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None);
            return inputModules.Length > 0;
#else
            return false;
#endif
        }
        
        public static bool ReplaceInputModule()
        {
            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.InputSystem");
            if (assembly == null)
            {
                EditorUtility.DisplayDialog("InputModule replacement failed", "Unity Input System package is not installed. Please install it from the Package Manager.", "OK");
                return false;
            }
                
            Type type = assembly.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (type == null)
            {
                EditorUtility.DisplayDialog("InputModule replacement failed", "InputSystemUIInputModule type not found. Please make sure you have the correct version of the Input System package.", "OK");
                return false;
            }
            
            StandaloneInputModule[] inputModules = Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None);
            
            Undo.SetCurrentGroupName("Replace StandaloneInputModule with InputSystemUIInputModule");
            int group = Undo.GetCurrentGroup();
                
            foreach (StandaloneInputModule inputModule in inputModules)
            {
                GameObject go = inputModule.gameObject;
                Undo.AddComponent(go, type);
                Undo.DestroyObjectImmediate(inputModule);
                EditorUtility.SetDirty(go);
            }
            
            Undo.CollapseUndoOperations(group);
            EditorUtility.DisplayDialog("InputModule replaced", "StandaloneInputModule has been replaced with InputSystemUIInputModule.", "OK");
            return true;
        }
    }
}
using UnityEditor;
using UnityEngine;

namespace OnlineMaps.Editors
{
    public static class EditorCompatibility
    {
        public static string GetAssetPath(int instanceId)
        {
#if UNITY_6000_3_OR_NEWER
            return AssetDatabase.GetAssetPath((EntityId)instanceId);
#else
            return AssetDatabase.GetAssetPath(instanceId);
#endif
        }

        public static Object InstanceIDToObject(int instanceId)
        {
#if UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(instanceId);
#else
            return EditorUtility.InstanceIDToObject(instanceId);
#endif
        }
    }
}
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SceneTransfer
{
    public static class Misc
    {
        /// <summary>
        /// Get all components under the root recursively.
        /// </summary>
        public static List<T> GetAllComponents<T>(GameObject root) where T : Component
        {
            var components = new List<T>();
            AddComponents<T>(root, ref components);
            return components;
        }

        static void AddComponents<T>(GameObject root, ref List<T> components) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component != null)
            {
                components.Add(component);
            }
            foreach (var c in root.GetComponentsInChildren<T>())
            {
                if (c.gameObject == root)
                {
                    continue;
                }
                AddComponents(c.gameObject, ref components);
            }
        }

        /// <summary>
        /// Get real image size.
        /// <see cref="https://forum.unity.com/threads/getting-original-size-of-texture-asset-in-pixels.165295/" />
        /// </summary>
        public static bool GetImageSize(Texture2D asset, out int width, out int height)
        {
            if (asset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null)
                {
                    object[] args = new object[2] { 0, 0 };
                    MethodInfo mi = typeof(TextureImporter).GetMethod("GetWidthAndHeight", BindingFlags.NonPublic | BindingFlags.Instance);
                    mi.Invoke(importer, args);
        
                    width = (int)args[0];
                    height = (int)args[1];
        
                    return true;
                }
            }
            height = width = 0;
            return false;
        }

        /// <summary>
        /// Get all objects in the scene.
        /// <see cref="https://docs.unity3d.com/ScriptReference/Resources.FindObjectsOfTypeAll.html" />
        /// </summary>
        public static List<GameObject> GetAllObjectsInScene()
        {
            List<GameObject> objectsInScene = new List<GameObject>();

            foreach (GameObject go in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
            {
                if (go.hideFlags == HideFlags.NotEditable || go.hideFlags == HideFlags.HideAndDontSave)
                    continue;

                if (!EditorUtility.IsPersistent(go.transform.root.gameObject))
                    continue;

                objectsInScene.Add(go);
            }

            return objectsInScene;
        }

        /// <summary>
        /// Load all assets of type T at asset path.
        /// <see cref="https://forum.unity.com/threads/loadallassetsatpath.21444/#post-1911318" />
        /// </summary>
        public static List<T> LoadAllAssetsOfType<T>(string optionalPath = "") where T : Object
        {
            string[] guids;
            if (optionalPath != "")
            {
                if (optionalPath.EndsWith("/"))
                {
                    optionalPath = optionalPath.TrimEnd('/');
                }
                else if (optionalPath.EndsWith("\\"))
                {
                    optionalPath = optionalPath.TrimEnd('\\');
                }
                guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new string[]{optionalPath});
            }
            else
            {
                guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            }

            List<T> objectList = new List<T>();
            for (int i = 0; i < guids.Length; ++i)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath(assetPath, typeof(T)) as T;
                if (asset != null)
                {
                    objectList.Add(asset);
                }
            }
    
            return objectList;
        }
    }
}
#endif
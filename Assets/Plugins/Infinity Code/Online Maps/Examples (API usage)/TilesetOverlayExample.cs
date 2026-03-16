/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using OnlineMaps;
using UnityEngine;

namespace OnlineMapsExamples
{
    /// <summary>
    /// Example of how to make the overlay for the tileset.
    /// </summary>
    [AddComponentMenu(Utils.ExampleMenuPath + "TilesetOverlayExample")]
    public class TilesetOverlayExample : MonoBehaviour
    {
        /// <summary>
        /// Reference to the tileset control. If not specified, the current instance will be used.
        /// </summary>
        public TileSetControl control;
        
        /// <summary>
        /// Overlay material.
        /// If missed, a new material with Transparent/Diffuse shader will be used.
        /// If present, the texture field will be ignored and you must specify the texture directly in the material.
        /// </summary>
        [Tooltip("If missed, a new material with Transparent/Diffuse shader will be used. If present, the texture field will be ignored and you must specify the texture directly in the material.")]
        public Material material;

        /// <summary>
        /// Shader for overlay material.
        /// If material is specified, this field will be ignored.
        /// If material is not specified and shader is missed, a default shader for the current render pipeline will be used.
        /// </summary>
        [Tooltip("If material is specified, this field will be ignored. If material is not specified and shader is missed, a default shader for the current render pipeline will be used.")]
        public Shader shader;
        
        /// <summary>
        /// Overlay texture in mercator projection
        /// </summary>
        [Tooltip("If material is specified, this field will be ignored and you must specify the texture directly in the material.")]
        public Texture texture;
        
        /// <summary>
        /// Border coordinates of the overlay
        /// </summary>
        public double leftLongitude = -180;
        public double topLatitude = 90;
        public double rightLongitude = 180;
        public double bottomLatitude = -90;
        
        /// <summary>
        /// Overlay transparency
        /// </summary>
        [Range(0, 1)] 
        public float alpha = 1;
        
        private Mesh overlayMesh;
        private Map map;

        private void Start()
        {
            // If the control is not specified, get the current instance.
            if (!control && !(control = GetComponent<TileSetControl>()))
            {
                Debug.LogError("TileSetControl not found");
                return;
            }
            
            // Create overlay container
            GameObject overlayContainer = new GameObject("OverlayContainer");
            overlayContainer.transform.parent = transform;

            // Init overlay material
            MeshRenderer meshRenderer = overlayContainer.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = overlayContainer.AddComponent<MeshFilter>();
            if (!material)
            {
                if (!shader) shader = RenderPipelineHelper.GetTransparentShader();
                material = new Material(shader);
                material.mainTexture = texture;
            }
            
            meshRenderer.sharedMaterial = material;

            overlayMesh = meshFilter.sharedMesh = new Mesh();
            overlayMesh.name = "Overlay Mesh";
            overlayMesh.MarkDynamic();
            overlayMesh.vertices = new Vector3[4];

            // Subscribe to events
            map = control.map;
            map.OnLocationChanged += UpdateMesh;
            map.OnZoomChanged += UpdateMesh;

            // Init mesh
            UpdateMesh();
        }

        private void UpdateMesh()
        {
            // Clear overlay mesh
            overlayMesh.Clear(true);
            
            Vector3 p1 = control.transform.worldToLocalMatrix * control.LocationToWorld(leftLongitude, topLatitude);
            Vector3 p2 = control.transform.worldToLocalMatrix * control.LocationToWorld(rightLongitude, bottomLatitude);

            // Init vertices and normals
            Vector2 size = control.sizeInScene;
            size.x *= -1;
            
            if (p1.x > 0 && p2.x > 0) return;
            if (p1.x < size.x && p2.x < size.x) return;
            if (p1.z < 0 && p2.z < 0) return;
            if (p1.z > size.y && p2.z > size.y) return;

            Vector2 uv0 = new Vector2(0, 1);
            Vector2 uv1 = new Vector2(1, 0);

            Vector3 v1 = p1;
            Vector3 v2 = p2;

            if (p1.x > 0)
            {
                float m = p1.x / (p1.x - p2.x);
                uv0.x = m;
                v1.x = 0;
            }

            if (p1.z < 0)
            {
                float m = p1.z / (p1.z - p2.z);
                uv0.y = 1 - m;
                v1.z = 0;
            }

            if (p2.x < size.x)
            {
                float m = (p1.x - size.x) / (p1.x - p2.x);
                uv1.x = m;
                v2.x = size.x;
            }

            if (p2.z > size.y)
            {
                float m = (size.y - p2.z) / (p1.z - p2.z);
                uv1.y = m;
                v2.z = size.y;
            }

            overlayMesh.vertices = new[]
            {
                new Vector3(v1.x, 0.1f, v1.z),
                new Vector3(v2.x, 0.1f, v1.z),
                new Vector3(v2.x, 0.1f, v2.z),
                new Vector3(v1.x, 0.1f, v2.z),
            };

            overlayMesh.normals = new[]
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            };

            overlayMesh.uv = new[]
            {
                uv0,
                new Vector2(uv1.x, uv0.y), 
                uv1, 
                new Vector2(uv0.x, uv1.y), 
            };

            // Init triangles
            overlayMesh.SetTriangles(new[]
            {
                0, 1, 2,
                0, 2, 3
            }, 0);

            overlayMesh.RecalculateBounds();
            overlayMesh.RecalculateNormals();
        }

        private void Update()
        {
            if (Math.Abs(material.color.a - alpha) > float.Epsilon)
            {
                Color color = material.color;
                color.a = alpha;
                material.color = color;
            }
        }
    }
}
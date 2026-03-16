using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace OnlineMaps
{
    public class SeparateTileDrawer : MapDrawer
    {
        private TileSetControl control;
        
        private Dictionary<ulong, SeparateTile> tiles = new Dictionary<ulong, SeparateTile>();
        private Transform container;
        private TileManager tileManager;

        public SeparateTileDrawer(TileSetControl control)
        {
            this.control = control;
            map = control.map;
            tileManager = map.tileManager;

            container = new GameObject("Tiles").transform;
            container.SetParent(control.transform, false);
        }
        
        public override void Dispose()
        {
            Object.Destroy(container.gameObject);
            
            foreach (KeyValuePair<ulong, SeparateTile> pair in tiles)
            {
                pair.Value.Dispose();
            }
            
            tiles.Clear();
            control = null;
            map = null;
        }

        public override void Draw()
        {
            TilePoint tlTile = map.view.topLeftTile;
            TilePoint brTile = map.view.bottomRightTile;

            int zoom = map.view.intZoom;
            int maxTiles = map.view.maxTiles;
            if (tlTile.x > brTile.x) brTile.x += maxTiles;

            DrawTiles( tlTile, brTile, maxTiles, zoom);
        }
        
        private void DrawTiles(TilePoint tlTile, TilePoint brTile, int maxTiles, int zoom)
        {
            foreach (KeyValuePair<ulong, SeparateTile> pair in tiles) pair.Value.used = false;
            
            bool hasElevation = control.hasElevation;

            for (int x = (int)Math.Floor(tlTile.x); x <= (int)Math.Ceiling(brTile.x); x++)
            {
                if (x >= maxTiles) x -= maxTiles;
                
                for (int y = (int)Math.Floor(tlTile.y); y <= (int)Math.Ceiling(brTile.y); y++)
                {
                    ulong key = TileManager.GetTileKey(zoom, x, y);
                    SeparateTile separateTile;
                    if (!tiles.TryGetValue(key, out separateTile))
                    {
                        separateTile = new SeparateTile(key, zoom, x, y);
                        tiles[key] = separateTile;
                    }

                    separateTile.Draw(this, hasElevation);
                }
            }

            SeparateTile[] unusedTiles = tiles.Where(t => !t.Value.used)
                .Select(t => t.Value)
                .ToArray();

            foreach (SeparateTile tile in unusedTiles)
            {
                tile.Dispose();
                tiles.Remove(tile.key);
            }
        }

        public override void DrawElements()
        {
            
        }

        public override void Initialize()
        {
            
        }

        public override bool Raycast(Ray ray, out RaycastHit hit, int maxRaycastDistance)
        {
            hit = new RaycastHit();
            if (tiles.Count == 0) return false;

            RaycastHit[] hits = Physics.RaycastAll(ray, maxRaycastDistance);
            if (hits.Length == 0) return false;

            foreach (RaycastHit h in hits)
            {
                if (tiles.All(t => t.Value.transform != h.transform)) continue;
                hit = h;
                return true;
            }

            return false;
        }

        private class SeparateTile
        {
            public readonly ulong key;
            public readonly int x;
            public readonly int y;
            public readonly int zoom;
            
            public GameObject gameObject;
            public Transform transform;
            public Tile tile;
            public bool used;
            private Material material;
            private Mesh mesh;
            private MeshCollider meshCollider;
            private MeshFilter meshFilter;
            private MeshRenderer meshRenderer;
            private float yScale;

            public SeparateTile(ulong key, int zoom, int x, int y)
            {
                this.key = key;
                this.zoom = zoom;
                this.x = x;
                this.y = y;
            }

            private void CreateMesh(SeparateTileDrawer drawer, bool hasElevation)
            {
                gameObject = new GameObject($"Tile {zoom}x{x}x{y}");
                transform = gameObject.transform;
                transform.SetParent(drawer.container, false);
                
                meshFilter = gameObject.AddComponent<MeshFilter>();
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshCollider = gameObject.AddComponent<MeshCollider>();
                material = GetMaterial(drawer.control);
                meshRenderer.sharedMaterial = material;

                mesh = new Mesh
                {
                    name = gameObject.name
                };
                mesh.MarkDynamic();
                meshFilter.mesh = mesh;
                
                UpdateMesh(drawer, hasElevation);
            }

            public void Dispose()
            {
                Object.Destroy(material);
                Object.Destroy(gameObject);
                
                gameObject = null;
                transform = null;
            }

            public void Draw(SeparateTileDrawer drawer, bool hasElevation)
            {
                used = true;
                
                if (tile == null)
                {
                    tile = drawer.tileManager.GetTile(zoom, x, y);
                    if (tile == null) return;
                }
                
                if (tile.status != TileStatus.loaded) return;

                if (hasElevation)
                {
                    yScale = drawer.control.elevationManager.GetElevationScale(drawer.map.view.rect);
                }
                
                if (!gameObject) CreateMesh(drawer, hasElevation);
                else if (hasElevation) UpdateMesh(drawer, hasElevation);
                
                material.mainTexture = tile.texture;

                Vector2 size = new Vector2(
                    drawer.control.sizeInScene.x / drawer.map.view.countTilesX,
                    drawer.control.sizeInScene.y / drawer.map.view.countTilesY);

                transform.localPosition = drawer.control.LocationToWorld(new TilePoint(x, y, zoom).ToLocation(drawer.map));
                transform.localScale = new Vector3(size.x, 1, size.y);
            }

            private Material GetMaterial(TileSetControl control)
            {
                if (control.tileMaterial) return Object.Instantiate(control.tileMaterial);
                return new Material(control.tilesetShader);
            }

            private void UpdateMesh(SeparateTileDrawer drawer, bool hasElevation)
            {
                if (!hasElevation)
                {
                    UpdateMeshNoElevation();
                }
                else
                {
                    UpdateMeshWithElevation(drawer);
                }
                
                mesh.RecalculateBounds();
                
                meshCollider.sharedMesh = Object.Instantiate(mesh);
            }

            private void UpdateMeshNoElevation()
            {
                mesh.vertices = new[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 0, 1),
                    new Vector3(0, 0, 1)
                };

                mesh.uv = new[]
                {
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                    new Vector2(0, 0),
                    new Vector2(1, 0),

                };
                
                mesh.normals = new[]
                {
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                    Vector3.up
                };
                
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            }

            private void UpdateMeshWithElevation(SeparateTileDrawer drawer)
            {
                GeoRect rect = drawer.map.view.rect;
                ElevationManagerBase elevationManager = drawer.control.elevationManager;
                Vector2 sizeInScene = drawer.control.sizeInScene;
                
                int res = drawer.control.elevationResolution;
                int countTilesX = drawer.map.view.countTilesX;
                int countTilesY = drawer.map.view.countTilesY;
                
                int resX = Mathf.CeilToInt(res / (float)countTilesX);
                int resY = Mathf.CeilToInt(res / (float)countTilesY);
                
                int resX1 = resX + 1;
                int resY1 = resY + 1;
                
                Vector3[] vertices = new Vector3[resX1 * resY1];
                Vector2[] uvs = new Vector2[vertices.Length];
                Vector3[] normals = new Vector3[vertices.Length];

                TilePoint tl = drawer.map.view.topLeftTile;
                double sx = tl.x - this.x;
                double sy = this.y - tl.y;

                for (int y = 0; y < resY1; y++)
                {
                    float ry = y / (float)resY;
                    
                    for (int x = 0; x < resX1; x++)
                    {
                        float rx = x / (float)resX;

                        double ex = (sx + rx) / countTilesX * sizeInScene.x;
                        double ey = (sy + ry) / countTilesY * sizeInScene.y;
                        float py = elevationManager.GetElevationValue(ex, ey, yScale, rect);
                        
                        vertices[y * resX1 + x] = new Vector3(rx, py, ry);
                        uvs[y * resX1 + x] = new Vector2(1 - rx, 1 - ry);
                        normals[y * resX1 + x] = Vector3.up;
                    }
                }
                
                int[] triangles = new int[resX * resY * 6];
                
                for (int y = 0; y < resY; y++)
                {
                    int cy = y * resX * 6;
                    int py1 = y * resX1;
                    int py2 = (y + 1) * resX1;
                    
                    for (int x = 0; x < resX; x++)
                    {
                        int ti = x * 6 + cy;
                        int p1 = py1 + x;
                        int p2 = p1 + 1;
                        int p3 = py2 + x;
                        int p4 = p3 + 1;

                        triangles[ti] = p1;
                        triangles[ti + 1] = p4;
                        triangles[ti + 2] = p2;
                        triangles[ti + 3] = p1;
                        triangles[ti + 4] = p3;
                        triangles[ti + 5] = p4;
                    }
                }
                
                mesh.vertices = vertices;
                mesh.uv = uvs;
                mesh.normals = normals;
                mesh.triangles = triangles;
            }
        }
    }
}
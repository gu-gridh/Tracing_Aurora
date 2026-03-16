/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Serialization;

namespace OnlineMaps
{
    /// <summary>
    /// Overlay Manager plugin.
    /// </summary>
    [Plugin("Overlay Manager")]
    public class OverlayManager : MonoBehaviour
    {
        private static Cache cache;

        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// Overlay layers.
        /// </summary>
        public OverlayLayer[] layers;
        
        [SerializeField] 
        private int maxParallelDownloads = 5;
        
        private readonly Queue<DownloadItem> downloadQueue = new Queue<DownloadItem>();
        private int currentDownloads = 0;

        private void CheckLayerConflicts()
        {
            if (layers == null) return;

            for (int i = 0; i < layers.Length; i++)
            {
                OverlayLayer layer = layers[i];
                if (!layer.enabled) continue;

                for (int j = i + 1; j < layers.Length; j++)
                {
                    OverlayLayer other = layers[j];
                    if (!other.enabled || layer.tileLayer != other.tileLayer) continue;

                    other.enabled = false;
                    object msg = $"OverlayManager: Conflict detected between layers using the same tile layer '{layer.tileLayer}'. " +
                                 $"The layer with URL '{other.url}' has been disabled.";
                    Debug.LogWarning(msg);
                }
            }
        }

        private void LoadTileOverlay(Tile tile)
        {
            foreach (OverlayLayer layer in layers)
            {
                downloadQueue.Enqueue(new DownloadItem()
                {
                    layer = layer,
                    tile = tile
                });
            }
        }

        private void OnDestroy()
        {
            if (cache) cache.OnLoadedFromCache -= LoadTileOverlay;
            TileManager.OnStartDownloadTile -= OnStartDownloadTile;
        }

        private void OnLoadComplete(Tile tile, OverlayLayer layer)
        {
            currentDownloads = Math.Max(0, currentDownloads - 1);
            map.Redraw();
        }

        private void OnStartDownloadTile(Tile tile)
        {
            LoadTileOverlay(tile);
            TileManager.StartDownloadTile(tile);
        }

        public void QueueDownloadTile(Tile tile, OverlayLayer layer)
        {
            downloadQueue.Enqueue(new DownloadItem
            {
                layer = layer,
                tile = tile
            });
        }

        private void Start()
        {
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return;
            }

            foreach (OverlayLayer layer in layers) layer.overlayManager = this;
            CheckLayerConflicts();

            cache = Cache.instance;
            if (cache) cache.OnLoadedFromCache += LoadTileOverlay;
            TileManager.OnStartDownloadTile += OnStartDownloadTile;
        }

        private void TryStartNextDownload()
        {
            while (currentDownloads < maxParallelDownloads && downloadQueue.Count > 0)
            {
                DownloadItem item = downloadQueue.Dequeue();
                if (item.layer.LoadTile(item.tile))
                {
                    currentDownloads++;
                }
            }
        }

        private void Update()
        {
            TryStartNextDownload();
            bool alphaChanged = false;
            foreach (OverlayLayer layer in layers)
            {
                if (layer.UpdateAlpha()) alphaChanged = true;
            }
            if (alphaChanged) map.Redraw();
        }

        /// <summary>
        /// Overlay layer settings.
        /// </summary>
        [Serializable]
        public class OverlayLayer
        {
            /// <summary>
            /// Source type for overlay tiles.
            /// </summary>
            public TileSource source = TileSource.online;
            
            /// <summary>
            /// URL template for online tiles. Supports tokens: {z}, {x}, {y}, {zoom}.
            /// </summary>
            public string url = "https://localhost/tiles/{z}/{x}/{y}.png";
            
            /// <summary>
            /// Local path template for overlay tiles (Resources or StreamingAssets). Supports tokens: {z}, {x}, {y}, {zoom}.
            /// </summary>
            public string path = "OnlineMaps/Overlay/{z}/{x}/{y}.png";
            
            /// <summary>
            /// Allowed zoom range for this layer.
            /// </summary>
            public LimitedRange zoomRange = new LimitedRange(Constants.MinZoom, Constants.MaxZoom, Constants.MinZoom, Constants.MaxZoom);
            
            /// <summary>
            /// Overlay opacity (0 = transparent, 1 = opaque).
            /// </summary>
            [Range(0, 1)]
            public float alpha = 1;
            
            /// <summary>
            /// Overlay layer ordering and purpose (front, back, traffic).
            /// </summary>
            public TileLayer tileLayer = TileLayer.back;

            private float _alpha = 1;

            /// <summary>
            /// Whether this overlay layer is enabled.
            /// </summary>
            [SerializeField]
            [FormerlySerializedAs("enabled")]
            private bool _enabled = true;

            /// <summary>
            /// Reference to the OverlayManager.
            /// </summary>
            public OverlayManager overlayManager { get; set; }

            public bool enabled
            {
                get => _enabled;
                set => SetEnabled(value);
            }
            
            /// <summary>
            /// Load overlay tile.
            /// </summary>
            /// <param name="tile">Tile.</param>
            public bool LoadTile(Tile tile)
            {
                if (!_enabled) return false;
                if (!zoomRange.Contains(tile.zoom)) return false;

                UpdateTileAlpha(tile);

                if (source == TileSource.online) TryLoadOnline(tile);
                else if (source == TileSource.resources) overlayManager.StartCoroutine(TryLoadResources(tile));
                else if (source == TileSource.streamingAssets) TryLoadStreamingAssets(tile);
                return true;
            }

            private string GetLocalPath(Tile tile)
            {
                return Regex.Replace(path, @"{\w+}", match =>
                {
                    string v = match.Value.ToLower().Trim('{', '}');
                    if (v == "zoom") return tile.zoom.ToString();
                    if (v == "z") return tile.zoom.ToString();
                    if (v == "x") return tile.x.ToString();
                    if (v == "y") return tile.y.ToString();
                    return match.Value;
                });
            }

            private string GetUrl(Tile tile)
            {
                return Regex.Replace(url, @"{\w+}", match =>
                {
                    string v = match.Value.ToLower().Trim('{', '}');
                    if (v == "zoom" || v == "z") return tile.zoom.ToString();
                    if (v == "x") return tile.x.ToString();
                    if (v == "y") return tile.y.ToString();
                    return match.Value;
                });
            }

            private void LoadOverlayTexture(Tile tile, byte[] bytes)
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (tile.status != TileStatus.disposed && texture.LoadImage(bytes)) SetOverlayTexture(tile, texture);
                else Destroy(texture);
            }

            public void SetEnabled(bool value)
            {
                _enabled = value;
                if (!Application.isPlaying) return;

                List<Tile> tiles = overlayManager.map.tileManager.tiles;
                if (!value)
                {
                    tiles.ForEach(t =>
                    {
                        if (t != null) SetOverlayTexture(t, null);
                    });
                }
                else
                {
                    tiles.ForEach(t =>
                    {
                        overlayManager.QueueDownloadTile(t, this);
                    });
                }
                
                overlayManager.map.Redraw();
            }

            public void SetEnabled(bool value, OverlayManager manager)
            {
                overlayManager = manager;
                SetEnabled(value);
            }

            private void SetOverlayTexture(Tile tile, Texture2D texture)
            {
                if (texture) texture.wrapMode = TextureWrapMode.Clamp;
                if (tileLayer == TileLayer.back) tile.overlayBackTexture = texture;
                else if (tileLayer == TileLayer.front) tile.overlayFrontTexture = texture;
                else if (tileLayer == TileLayer.traffic) (tile as RasterTile).trafficTexture = texture;
            }

            private bool TryLoadFromCache(Tile tile, string tileUrl)
            {
                if (!cache) return false;
                
                byte[] bytes = cache.GetItem(tileUrl);
                if (bytes == null || bytes.Length == 0) return false;
                
                LoadOverlayTexture(tile, bytes);
                overlayManager.OnLoadComplete(tile, this);
                return true;

            }

            private void TryLoadOnline(Tile tile)
            {
                string tileUrl = GetUrl(tile);
                if (TryLoadFromCache(tile, tileUrl)) return;

                WebRequest request = new WebRequest(tileUrl);
                request.OnComplete += www =>
                {
                    overlayManager.OnLoadComplete(tile, this);
                    byte[] bytes = www.bytes;
                    if (bytes == null || bytes.Length == 0) return;
                    if (cache) cache.AddItem(tileUrl, bytes);
                    LoadOverlayTexture(tile, bytes);
                };
            }

            private IEnumerator TryLoadResources(Tile tile)
            {
                string resourcePath = GetLocalPath(tile);
                ResourceRequest request = Resources.LoadAsync(resourcePath);
                yield return request;

                Texture2D texture = request.asset as Texture2D;
                if (tile.status != TileStatus.disposed && texture) SetOverlayTexture(tile, texture);
                else Resources.UnloadAsset(request.asset);
            }

            private void TryLoadStreamingAssets(Tile tile)
            {
                string localPath = Application.streamingAssetsPath + "/" + GetLocalPath(tile);
                WebRequest request = new WebRequest(localPath);
                request.OnComplete += www =>
                {
                    byte[] bytes = www.bytes;
                    if (bytes == null || bytes.Length == 0)
                    {
                        overlayManager.OnLoadComplete(tile, this);
                        return;
                    }
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                    if (tile.status != TileStatus.disposed && texture.LoadImage(bytes)) SetOverlayTexture(tile, texture);
                    else Destroy(texture);
                    overlayManager.OnLoadComplete(tile, this);
                };
            }

            /// <summary>
            /// Update overlay alpha for all tiles.
            /// </summary>
            public bool UpdateAlpha()
            {
                if (Math.Abs(_alpha - alpha) < float.Epsilon) return false;
                _alpha = alpha;
                foreach (Tile tile in overlayManager.map.tileManager.tiles)
                {
                    UpdateTileAlpha(tile);
                }

                return true;
            }

            private void UpdateTileAlpha(Tile tile)
            {
                if (tileLayer == TileLayer.back) tile.overlayBackAlpha = alpha;
                else if (tileLayer == TileLayer.front) tile.overlayFrontAlpha = alpha;
            }
        }

        /// <summary>
        /// Specifies the source type for overlay tiles.
        /// </summary>
        public enum TileSource
        {
            /// <summary>
            /// Load tiles from the network (HTTP).
            /// </summary>
            online,

            /// <summary>
            /// Load tiles from Unity Resources folder.
            /// </summary>
            resources,

            /// <summary>
            /// Load tiles from the StreamingAssets folder.
            /// </summary>
            streamingAssets,
        }

        /// <summary>
        /// Specifies the overlay layer ordering and purpose.
        /// </summary>
        public enum TileLayer
        {
            /// <summary>
            /// Overlay rendered in front of the base map.
            /// </summary>
            front,

            /// <summary>
            /// Overlay rendered behind the base map.
            /// </summary>
            back,

            /// <summary>
            /// Overlay used for traffic data.
            /// </summary>
            traffic
        }
        
        public struct DownloadItem
        {
            public OverlayLayer layer;
            public Tile tile;
        }
    }
}
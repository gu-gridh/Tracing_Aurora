/*         INFINITY CODE         */
/*   https://infinity-code.com   */

using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace OnlineMaps
{
    /// <summary>
    /// Google Tiles Provider
    /// </summary>
    public class GoogleTilesProvider : MonoBehaviour
    {
        /// <summary>
        /// Reference to the map. If not specified, the current instance will be used.
        /// </summary>
        public Map map;

        /// <summary>
        /// The type of base map.
        /// </summary>
        [Header("Required")]
        public MapType mapType = MapType.roadmap;

        /// <summary>
        /// An IETF language tag that specifies the language used to display information on the tiles. For example, en-US specifies the English language as spoken in the United States.
        /// </summary>
        public string language = "en-us";

        /// <summary>
        /// A Common Locale Data Repository region identifier (two uppercase letters) that represents the physical location of the user. For example, US.
        /// </summary>
        public string region = "US";

        /// <summary>
        /// Scales-up the size of map elements (such as road labels), while retaining the tile size and coverage area of the default tile. Increasing the scale also reduces the number of labels on the map, which reduces clutter. 
        /// </summary>
        [Header("Optional")]
        public ScaleFactor scale = ScaleFactor.scaleFactor1x;

        /// <summary>
        /// Specifies whether to return high-resolution tiles. 
        /// </summary>
        public bool highDpi = false;

        /// <summary>
        /// Layer types added to the map
        /// </summary>
        public LayerTypes layerTypes = new LayerTypes();

        /// <summary>
        /// Specifies whether layerTypes should be rendered as a separate overlay, or combined with the base imagery.
        /// </summary>
        public bool overlay = false;

        /// <summary>
        /// An array of JSON style objects that specify the appearance and detail level of map features such as roads, parks, and built-up areas. Styling is used to customize the standard Google base map.
        /// </summary>
        [Multiline]
        public string stylesJsonArray;

        private string sessiontoken;
        private int tokenExpire;
        private string url = "https://www.googleapis.com/tile/v1/tiles/{zoom}/{x}/{y}?session={sessiontoken}&key={apikey}";
        private Coroutine getSessionRoutine;

        private JSONObject GetSessionJson()
        {
            JSONObject jReq = new JSONObject(
                ("mapType", mapType.ToString()),
                ("language", language),
                ("region", region.ToUpper())
            );

            if (scale != ScaleFactor.scaleFactor1x) jReq.Add("scaleFactor", scale.ToString());
            if (highDpi) jReq.Add("highDpi", true);
            if (overlay) jReq.Add("overlay", true);
            
            if (layerTypes == null) layerTypes = new LayerTypes();
            if (mapType == MapType.terrain) layerTypes.roadmap = true;
            jReq.Add("layerTypes", layerTypes.ToJson());

            TryAppendStyles(jReq);

            return jReq;
        }

        private IEnumerator GetSessionToken()
        {
            JSONObject jReq = GetSessionJson();

            byte[] requestBytes = Encoding.UTF8.GetBytes(jReq.ToString());

            string createSessionUrl = "https://tile.googleapis.com/tile/v1/createSession?key=" + KeyManager.GoogleMaps();
            UnityWebRequest www = new UnityWebRequest(createSessionUrl, "POST");
            www.SetRequestHeader("Content-Type", "application/json");
            www.uploadHandler = new UploadHandlerRaw(requestBytes);
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.Log(www.error + "\n" + Encoding.UTF8.GetString(www.downloadHandler.data));
                yield break;
            }

            string response = Encoding.UTF8.GetString(www.downloadHandler.data);
            Debug.Log(response);
            JSONItem json = JSON.Parse(response);
            sessiontoken = json.V<string>("session");
            tokenExpire = json.V<int>("expiry");
            
            getSessionRoutine = null;
            map.tileManager.canLoadTiles = true;

            map.tileManager.tiles.ForEach(t =>
            {
                if (t.status == TileStatus.error) t.status = TileStatus.idle;
            });

            map.Redraw();
        }

        private string OnReplaceUrlToken(Tile tile, string token)
        {
            if (token == "sessiontoken") return sessiontoken;
            if (token == "apikey") return KeyManager.GoogleMaps();
            return null;
        }

        private void OnStartDownloadTile(Tile tile)
        {
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= tokenExpire)
            {
                if (getSessionRoutine == null)
                {
                    map.tileManager.canLoadTiles = false;
                    getSessionRoutine = StartCoroutine(GetSessionToken());
                }
                return;
            }

            TileManager.StartDownloadTile(tile);
        }

        private void Start()
        {
            if (!ValidateFields()) return;

            TileProvider.CreateMapType("google.tilesapi", url);
            Tile.OnReplaceURLToken += OnReplaceUrlToken;
            map.tileManager.canLoadTiles = false;
            TileManager.OnStartDownloadTile += OnStartDownloadTile;

            map.mapType = "google.tilesapi";
            getSessionRoutine = StartCoroutine(GetSessionToken());
        }

        private void TryAppendStyles(JSONObject jReq)
        {
            if (string.IsNullOrEmpty(stylesJsonArray)) return;
            
            try
            {
                JSONArray stylesJson = JSONArray.ParseArray(stylesJsonArray);
                if (stylesJson != null) jReq.Add("styles", stylesJson);
                else Debug.LogWarning("Styles JSON must be JSON array");
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to parse styles JSON: " + e.Message);
            }
        }

        private bool ValidateFields()
        {
            if (!map && !(map = Map.instance))
            {
                Debug.LogError("Map not found");
                return false;
            }

            if (!KeyManager.hasGoogleMaps)
            {
                Debug.LogError("Google Maps API key not found");
                return false;
            }

            if (string.IsNullOrEmpty(language))
            {
                Debug.LogError("Language is not specified");
                return false;
            }
            
            if (string.IsNullOrEmpty(region))
            {
                Debug.LogError("Region is not specified");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Scales-up the size of map elements (such as road labels), while retaining the tile size and coverage area of the default tile. Increasing the scale also reduces the number of labels on the map, which reduces clutter. 
        /// </summary>
        public enum ScaleFactor
        {
            /// <summary>
            /// The default.
            /// </summary>
            scaleFactor1x,
            
            /// <summary>
            /// Doubles label size and removes minor feature labels.
            /// </summary>
            scaleFactor2x,
            
            /// <summary>
            /// Quadruples label size and removes minor feature labels.
            /// </summary>
            scaleFactor4x
        }

        /// <summary>
        /// Map type
        /// </summary>
        public enum MapType
        {
            /// <summary>
            /// Roads, buildings, points of interest, and political boundaries
            /// </summary>
            roadmap,
            
            /// <summary>
            /// Photographic imagery taken from space
            /// </summary>
            satellite,
            
            /// <summary>
            /// A contour map that shows natural features such as vegetation
            /// </summary>
            terrain
        }

        /// <summary>
        /// Layer types added to the map
        /// </summary>
        [Serializable]
        public class LayerTypes
        {
            /// <summary>
            /// Roadmap layer
            /// </summary>
            public bool roadmap;
            
            /// <summary>
            /// Shows Street View-enabled streets and locations using blue outlines on the map
            /// </summary>
            public bool streetview;
            
            /// <summary>
            /// Displays current traffic conditions
            /// </summary>
            public bool traffic;

            public JSONArray ToJson()
            {
                JSONArray layers = new JSONArray();
                if (roadmap) layers.Add("roadmap");
                if (streetview) layers.Add("streetview");
                if (traffic) layers.Add("traffic");

                return layers;
            }
        }
    }
}
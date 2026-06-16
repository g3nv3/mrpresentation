using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YandexMiniMapTileLayer : MonoBehaviour
{
    private const string DefaultTileUrlTemplate =
        "https://tiles.api-maps.yandex.ru/v1/tiles/?apikey={apikey}&x={x}&y={y}&z={z}&lang={lang}&l={layer}&projection=web_mercator";

    private sealed class TileView
    {
        public RectTransform RectTransform;
        public RawImage Image;
        public int DisplayX;
        public int Y;
    }

    [Header("Yandex Tiles")]
    [SerializeField] private string apiKey;
    [SerializeField] private string tileUrlTemplate = DefaultTileUrlTemplate;
    [SerializeField] private string language = "ru_RU";
    [SerializeField] private string layer = "map";
    [SerializeField, Min(1f)] private float timeoutSeconds = 8f;

    [Header("View")]
    [SerializeField] private RectTransform tileRoot;
    [SerializeField, Range(1, 20)] private int zoom = 17;
    [SerializeField, Min(0.01f)] private float uiScale = 1f;
    [SerializeField, Range(1, 4)] private int tileRadius = 2;

    [Header("Cache")]
    [SerializeField, Min(9)] private int maxCachedTextures = 128;
    [SerializeField] private Color loadingColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private Color errorColor = new Color(1f, 0.2f, 0.2f, 0.25f);

    private readonly Dictionary<string, TileView> _activeTiles = new Dictionary<string, TileView>();
    private readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
    private readonly HashSet<string> _loadingTiles = new HashSet<string>();
    private readonly List<string> _scratchKeys = new List<string>();
    private GeoCoordinate _center;
    private bool _hasCenter;

    public int Zoom => zoom;
    public float UiScale => uiScale;

    private RectTransform TileRoot
    {
        get
        {
            if (tileRoot == null)
            {
                tileRoot = transform as RectTransform;
            }

            return tileRoot;
        }
    }

    private void Awake()
    {
        if (tileRoot == null)
        {
            tileRoot = transform as RectTransform;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _loadingTiles.Clear();
    }

    public void SetApiKey(string value)
    {
        apiKey = value;
    }

    public void SetView(GeoCoordinate center, int viewZoom, float viewUiScale)
    {
        if (!center.IsValid)
        {
            ClearActiveTiles();
            _hasCenter = false;
            return;
        }

        _center = center;
        _hasCenter = true;
        zoom = Mathf.Clamp(viewZoom, 1, 20);
        uiScale = Mathf.Max(0.01f, viewUiScale);
        RebuildVisibleTiles();
    }

    public void Refresh()
    {
        if (_hasCenter)
        {
            RebuildVisibleTiles();
        }
    }

    public void Clear()
    {
        _hasCenter = false;
        ClearActiveTiles();
    }

    private void RebuildVisibleTiles()
    {
        var root = TileRoot;
        if (root == null)
        {
            return;
        }

        var centerPixel = YandexMiniMapProjection.GeoToWorldPixel(_center, zoom);
        var centerTileX = Mathf.FloorToInt(centerPixel.x / YandexMiniMapProjection.TileSize);
        var centerTileY = Mathf.FloorToInt(centerPixel.y / YandexMiniMapProjection.TileSize);

        _scratchKeys.Clear();
        foreach (var key in _activeTiles.Keys)
        {
            _scratchKeys.Add(key);
        }

        for (var dy = -tileRadius; dy <= tileRadius; dy++)
        {
            var tileY = centerTileY + dy;
            var clampedY = YandexMiniMapProjection.ClampTileY(tileY, zoom);
            if (tileY != clampedY)
            {
                continue;
            }

            for (var dx = -tileRadius; dx <= tileRadius; dx++)
            {
                var displayX = centerTileX + dx;
                var tileX = YandexMiniMapProjection.WrapTileX(displayX, zoom);
                var key = BuildTileKey(zoom, tileX, tileY);

                _scratchKeys.Remove(key);
                var tile = GetOrCreateTile(key, displayX, tileY);
                PositionTile(tile, centerPixel);
                LoadTileTexture(key, tileX, tileY, tile);
            }
        }

        for (var i = 0; i < _scratchKeys.Count; i++)
        {
            ReleaseTile(_scratchKeys[i]);
        }
    }

    private TileView GetOrCreateTile(string key, int displayX, int tileY)
    {
        if (_activeTiles.TryGetValue(key, out var tile))
        {
            tile.DisplayX = displayX;
            tile.Y = tileY;
            tile.RectTransform.SetAsFirstSibling();
            return tile;
        }

        var tileObject = new GameObject("Yandex Tile " + key);
        tileObject.transform.SetParent(TileRoot, false);

        var rectTransform = tileObject.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var image = tileObject.AddComponent<RawImage>();
        image.color = loadingColor;
        image.raycastTarget = false;

        tile = new TileView
        {
            RectTransform = rectTransform,
            Image = image,
            DisplayX = displayX,
            Y = tileY
        };

        _activeTiles[key] = tile;
        return tile;
    }

    private void PositionTile(TileView tile, Vector2 centerPixel)
    {
        var size = YandexMiniMapProjection.TileSize * uiScale;
        tile.RectTransform.sizeDelta = new Vector2(size, size);

        var tileCenterPixel = new Vector2(
            (tile.DisplayX + 0.5f) * YandexMiniMapProjection.TileSize,
            (tile.Y + 0.5f) * YandexMiniMapProjection.TileSize);

        tile.RectTransform.anchoredPosition =
            YandexMiniMapProjection.WorldPixelToUiOffset(tileCenterPixel, centerPixel, uiScale);
    }

    private void LoadTileTexture(string key, int tileX, int tileY, TileView tile)
    {
        if (_textureCache.TryGetValue(key, out var texture))
        {
            tile.Image.texture = texture;
            tile.Image.color = Color.white;
            return;
        }

        tile.Image.texture = null;
        tile.Image.color = loadingColor;

        if (!string.IsNullOrWhiteSpace(apiKey) && !_loadingTiles.Contains(key))
        {
            StartCoroutine(LoadTileRoutine(key, tileX, tileY));
        }
    }

    private IEnumerator LoadTileRoutine(string key, int tileX, int tileY)
    {
        _loadingTiles.Add(key);

        var url = BuildTileUrl(tileX, tileY);
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = Mathf.CeilToInt(timeoutSeconds);
            yield return request.SendWebRequest();

            _loadingTiles.Remove(key);

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (_activeTiles.TryGetValue(key, out var failedTile))
                {
                    failedTile.Image.color = errorColor;
                }

                Debug.LogWarning("Failed to load Yandex mini map tile: " + request.error, this);
                yield break;
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            _textureCache[key] = texture;
            TrimTextureCache();

            if (_activeTiles.TryGetValue(key, out var tile))
            {
                tile.Image.texture = texture;
                tile.Image.color = Color.white;
            }
        }
    }

    private string BuildTileUrl(int tileX, int tileY)
    {
        var template = string.IsNullOrWhiteSpace(tileUrlTemplate) ? DefaultTileUrlTemplate : tileUrlTemplate;
        return template
            .Replace("{apikey}", UnityWebRequest.EscapeURL(apiKey))
            .Replace("{x}", tileX.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{y}", tileY.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{z}", zoom.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{lang}", UnityWebRequest.EscapeURL(language))
            .Replace("{layer}", UnityWebRequest.EscapeURL(layer));
    }

    private void TrimTextureCache()
    {
        if (_textureCache.Count <= maxCachedTextures)
        {
            return;
        }

        _scratchKeys.Clear();
        foreach (var key in _textureCache.Keys)
        {
            if (!_activeTiles.ContainsKey(key))
            {
                _scratchKeys.Add(key);
            }
        }

        for (var i = 0; i < _scratchKeys.Count && _textureCache.Count > maxCachedTextures; i++)
        {
            var key = _scratchKeys[i];
            var texture = _textureCache[key];
            _textureCache.Remove(key);
            if (texture != null)
            {
                Destroy(texture);
            }
        }
    }

    private void ReleaseTile(string key)
    {
        if (!_activeTiles.TryGetValue(key, out var tile))
        {
            return;
        }

        _activeTiles.Remove(key);
        if (tile.RectTransform != null)
        {
            Destroy(tile.RectTransform.gameObject);
        }
    }

    private void ClearActiveTiles()
    {
        _scratchKeys.Clear();
        foreach (var key in _activeTiles.Keys)
        {
            _scratchKeys.Add(key);
        }

        for (var i = 0; i < _scratchKeys.Count; i++)
        {
            ReleaseTile(_scratchKeys[i]);
        }
    }

    private static string BuildTileKey(int tileZoom, int tileX, int tileY)
    {
        return tileZoom + "/" + tileX + "/" + tileY;
    }
}

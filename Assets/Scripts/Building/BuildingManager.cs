using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class TileOption
{
    public GameObject tilePrefab; // Prefab for the tile
    public int buildCost;         // Cost to build this tile
}

public class BuildingManager : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PlayerInventory playerInventory;
    public PlayerController playerController;

    [Header("Tiles & Prefabs")]
    public List<TileOption> tileOptions; // List of tile options
    public GameObject indicatorPrefab;   // Single indicator prefab
    public List<Transform> foundationTiles; // At least one placed tile to seed the grid
    public float snapTolerance = 0.15f; // how close is “close enough” to auto-snap
    public float snapStrength = 1f;     // 1 = full snap, 0.5 = half pull

    [Header("Rules")]
    public bool restrictFirstBuildAbove = true; // First placement cannot go Down
    public float contactEpsilon = 0.01f;

    [Header("Input")]
    public KeyCode toggleBuildKey = KeyCode.T;
    public KeyCode nextTileKey = KeyCode.E; // Key to cycle to the next tile
    public KeyCode previousTileKey = KeyCode.Q; // Key to cycle to the previous tile

    [Header("Indicator Colors")]
    public Color availableColor = Color.green;
    public Color unavailableColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);

    [Header("Debug")]
    public bool showGizmos = false;

    // --- Internal ---
    private HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>(); // Grid cells
    private GameObject _indicator; // Single indicator
    private Vector2 _gridOrigin;
    private bool _firstExpansionDone = false;
    private bool _buildMode = false;
    private Camera _cam;
    private int _currentTileIndex = 0; // Index of the currently selected tile
    private Vector2 _cellSize;
    private Dictionary<Vector2Int, Vector2> _tileSizes = new Dictionary<Vector2Int, Vector2>();
    private Dictionary<Vector2Int, Vector2> _tileCenters = new Dictionary<Vector2Int, Vector2>();
    private Dictionary<(Vector2, Vector2), int> _perimeterEdges = new Dictionary<(Vector2, Vector2), int>();
    private float _minYAllowed;
    private Vector2 _lastGoodPos;
    private bool _hasLastGood = false;


    void Start()
    {
        _cam = Camera.main;
        if (!playerInventory && player) playerInventory = player.GetComponent<PlayerInventory>();

        // Ensure tile options are set up
        if (tileOptions == null || tileOptions.Count == 0)
        {
            Debug.LogError("[Building] No tile options assigned.");
            return;
        }

        // Origin from first seed tile
        if (foundationTiles == null || foundationTiles.Count == 0 || foundationTiles[0] == null)
        {
            Debug.LogError("[Building] No foundationTiles assigned.");
            return;
        }
        _gridOrigin = foundationTiles[0].position;
        _minYAllowed = _gridOrigin.y;

        // Initialize cell size based on the first foundation tile's prefab
        GameObject firstFoundationTile = foundationTiles[0].gameObject;
        _cellSize = GetPrefabWorldSize(firstFoundationTile);

        // Seed occupied and outer borders
        _occupied.Clear();
        foreach (var t in foundationTiles)
        {
            if (t)
            {
                Vector2Int cell = WorldToCellCenter(t.position);
                Vector2 tileSize = GetPrefabWorldSize(t.gameObject);

                _occupied.Add(cell);
                _tileSizes[cell] = tileSize;
                _tileCenters[cell] = t.position;
            }
        }

        RebuildPerimeter();

        // Create the single indicator
        CreateIndicator();
    }

    void Update()
    {
        if (playerInventory == null || tileOptions.Count == 0) return;

        // Toggle build mode
        if (Input.GetKeyDown(toggleBuildKey))
        {
            _buildMode = !_buildMode;
            _indicator.SetActive(_buildMode);
        }

        if (!_buildMode) return;

        // Cycle through tile options
        if (Input.GetKeyDown(nextTileKey))
        {
            _currentTileIndex = (_currentTileIndex + 1) % tileOptions.Count;
            UpdateIndicator();
        }
        else if (Input.GetKeyDown(previousTileKey))
        {
            _currentTileIndex = (_currentTileIndex - 1 + tileOptions.Count) % tileOptions.Count;
            UpdateIndicator();
        }

        // Update indicator position and state
        UpdateIndicatorPosition();

        // Place tile on right-click
        if (Input.GetMouseButtonDown(1))
        {
            TryBuild(_indicator.transform.position);
        }
    }

    void CreateIndicator()
    {
        Debug.Log("[BuildingManager] Creating indicator...");
        _indicator = Instantiate(indicatorPrefab);
        if (_indicator == null)
        {
            Debug.LogError("[BuildingManager] Failed to create indicator. Check if indicatorPrefab is assigned.");
            return;
        }
        _indicator.SetActive(false); // Initially hidden
        Debug.Log("[BuildingManager] Indicator created successfully.");
    }

    void UpdateIndicatorPosition()
    {
        if (_perimeterEdges.Count == 0)
        {
            _indicator.SetActive(false);
            return;
        }

        Vector2 mouseWorld = _cam.ScreenToWorldPoint(Input.mousePosition);

        Vector2 tileSize = GetPrefabWorldSize(tileOptions[_currentTileIndex].tilePrefab);
        Vector2 half = tileSize * 0.5f;

        float bestDist = float.MaxValue;
        Vector2 bestPos = Vector2.zero;
        bool foundValid = false;

        foreach (var kvp in _perimeterEdges)
        {
            if (kvp.Value != 1) continue;

            var (A, B) = kvp.Key;

            Vector2 closest = ClosestPointOnSegment(A, B, mouseWorld);

            bool horizontal = Mathf.Abs(A.y - B.y) < 0.0001f;
            bool vertical   = Mathf.Abs(A.x - B.x) < 0.0001f;

            Vector2 snapped = closest;

            // Correct side positioning
            if (horizontal)
                snapped.y = (mouseWorld.y > A.y) ? A.y + half.y : A.y - half.y;
            else if (vertical)
                snapped.x = (mouseWorld.x > A.x) ? A.x + half.x : A.x - half.x;

            snapped = ApplyGentleSnap(snapped, tileSize);

            // Forbidden below-min-Y placement
            if (snapped.y < _minYAllowed - 0.001f)
                continue;

            // Skip if overlapping existing
            if (OverlapsExistingRect(snapped, tileSize))
                continue;

            // VALID candidate
            float d = Vector2.Distance(mouseWorld, snapped);
            if (d < bestDist)
            {
                bestDist = d;
                bestPos = snapped;
                foundValid = true;
            }
        }

        // ----- FINAL VALIDATION HANDLING -----
        if (foundValid)
        {
            // Save this as the new last known valid position
            _lastGoodPos = bestPos;
            _hasLastGood = true;

            _indicator.SetActive(true);
            _indicator.transform.position = bestPos;

            var tile = tileOptions[_currentTileIndex];
            bool canAfford = playerInventory.parts >= tile.buildCost;

            var bi = _indicator.GetComponent<BuildIndicator>();
            if (bi) bi.ConfigureState(canAfford, availableColor, unavailableColor);

            return;
        }

        // No valid position this frame
        if (_hasLastGood)
        {
            _indicator.SetActive(true);
            _indicator.transform.position = _lastGoodPos;

            // The indicator STILL updates color based on affordability
            var tile = tileOptions[_currentTileIndex];
            bool canAfford = playerInventory.parts >= tile.buildCost;

            var bi = _indicator.GetComponent<BuildIndicator>();
            if (bi) bi.ConfigureState(canAfford, availableColor, unavailableColor);

            return;
        }
        else
        {
            // No valid ever found → hide indicator
            _indicator.SetActive(false);
            return;
        }
    }


    Vector2 ClosestPointOnSegment(Vector2 A, Vector2 B, Vector2 P)
    {
        Vector2 AP = P - A;
        Vector2 AB = B - A;

        float magnitudeAB = AB.sqrMagnitude;
        float ABAPproduct = Vector2.Dot(AP, AB);
        float distance = ABAPproduct / magnitudeAB;

        if (distance < 0) return A;
        else if (distance > 1) return B;
        else return A + AB * distance;
    }


    void UpdateIndicator()
    {
        var selectedTile = tileOptions[_currentTileIndex];
        Vector2 size = GetPrefabWorldSize(selectedTile.tilePrefab);

        var bi = _indicator.GetComponent<BuildIndicator>();
        if (bi != null)
            bi.SetVisualSize(size);
    }

    void TryBuild(Vector3 worldCenter)
    {
        var selectedTile = tileOptions[_currentTileIndex];

        // Get intended size & center
        Vector2 tileSize = GetPrefabWorldSize(selectedTile.tilePrefab);
        Vector2 center2D = worldCenter;

        // Do NOT allow overlapping builds (rectangle-based)
        if (OverlapsExistingRect(center2D, tileSize))
            return;

        if (!playerInventory.SpendParts(selectedTile.buildCost))
            return;

        // WorldCenter is ALREADY snapped against the perimeter edge
        Vector3 snapped = worldCenter;

        // Instantiate the tile at its actual snapped position
        var tile = Instantiate(selectedTile.tilePrefab, snapped, Quaternion.identity);
        if (!tile.GetComponent<FloorTile>()) tile.AddComponent<FloorTile>();

        // Register in grid-space and world-space
        Vector2Int cell = WorldToCellCenter(snapped);
        _tileSizes[cell]   = tileSize;
        _tileCenters[cell] = snapped;
        _occupied.Add(cell);

        // Recalculate actual perimeter based on real positions
        RebuildPerimeter();

        // Update indicator after placement
        UpdateIndicatorPosition();
    }


    // ---------------- Grid helpers ----------------
    Vector2Int WorldToCellCenter(Vector3 world)
    {
        Vector2 rel = (Vector2)world - _gridOrigin;
        int gx = Mathf.RoundToInt(rel.x / _cellSize.x);
        int gy = Mathf.RoundToInt(rel.y / _cellSize.y);
        return new Vector2Int(gx, gy);
    }

    Vector2 CellCenterToWorld(Vector2Int cell)
    {
        return _gridOrigin + new Vector2(cell.x * _cellSize.x, cell.y * _cellSize.y);
    }

    static Vector2 GetPrefabWorldSize(GameObject prefab)
    {
        if (prefab == null) return Vector2.one;

        if (prefab.TryGetComponent<BoxCollider2D>(out var box))
        {
            Vector3 s = prefab.transform.lossyScale;
            return new Vector2(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y));
        }
        if (prefab.TryGetComponent<SpriteRenderer>(out var sr))
        {
            Vector3 s = prefab.transform.lossyScale;
            var sz = sr.sprite ? sr.sprite.bounds.size : Vector3.one;
            return new Vector2(Mathf.Abs(sz.x * s.x), Mathf.Abs(sz.y * s.y));
        }
        return Vector2.one;
    }

    void OnDrawGizmos()
    {
        if (!showGizmos || _perimeterEdges.Count == 0)
            return;

        Gizmos.color = Color.magenta;

        foreach (var kvp in _perimeterEdges)
        {
            if (kvp.Value == 1) // outer edges
            {
                var (a, b) = kvp.Key;
                Gizmos.DrawLine(a, b);
            }
        }
    }

    // Helper: treats edge AB and BA as the same edge
    void AddEdge(Dictionary<(Vector2, Vector2), int> dict, Vector2 p1, Vector2 p2)
    {
        // Canonical order so (A,B) == (B,A)
        bool useAsIs = p1.x < p2.x || 
                    (Mathf.Approximately(p1.x, p2.x) && p1.y <= p2.y);

        var key = useAsIs ? (p1, p2) : (p2, p1);

        if (dict.TryGetValue(key, out int count))
            dict[key] = count + 1;
        else
            dict[key] = 1;
    }

    void RebuildPerimeter()
    {
        _perimeterEdges.Clear();

        // Count how many times each edge appears
        var edgeCount = new Dictionary<(Vector2, Vector2), int>();

        foreach (var kvp in _tileSizes)
        {
            Vector2Int cell = kvp.Key;
            Vector2 size = kvp.Value;
            Vector2 center = _tileCenters[cell];
            Vector2 half = size * 0.5f;

            Vector2 topLeft     = center + new Vector2(-half.x,  half.y);
            Vector2 topRight    = center + new Vector2( half.x,  half.y);
            Vector2 bottomLeft  = center + new Vector2(-half.x, -half.y);
            Vector2 bottomRight = center + new Vector2( half.x, -half.y);

            // Use AddEdge as a counter on edgeCount
            AddEdge(edgeCount, topLeft,     topRight);     // top
            AddEdge(edgeCount, topRight,    bottomRight);  // right
            AddEdge(edgeCount, bottomRight, bottomLeft);   // bottom
            AddEdge(edgeCount, bottomLeft,  topLeft);      // left
        }

        // Only edges seen exactly once are on the outside
        foreach (var kvp in edgeCount)
        {
            if (kvp.Value == 1)
            {
                _perimeterEdges[kvp.Key] = 1;
            }
        }
    }


    // Normalize edge so (A,B) == (B,A)
    (Vector2, Vector2) NormalizeEdge(Vector2 a, Vector2 b)
    {
        if (a.x < b.x || (Mathf.Approximately(a.x, b.x) && a.y <= b.y))
            return (a, b);
        return (b, a);
    }

    List<(Vector2, Vector2)> MergeColinearEdges(List<(Vector2 a, Vector2 b)> edges)
    {
        List<(Vector2, Vector2)> result = new List<(Vector2, Vector2)>();

        // Horizontal merges
        var horizontal = edges.FindAll(e => Mathf.Abs(e.a.y - e.b.y) < 0.0001f);
        horizontal.Sort((e1, e2) => e1.a.x.CompareTo(e2.a.x));

        for (int i = 0; i < horizontal.Count; i++)
        {
            Vector2 start = horizontal[i].a;
            Vector2 end = horizontal[i].b;

            while (i + 1 < horizontal.Count &&
                Mathf.Abs(horizontal[i + 1].a.y - start.y) < 0.001f &&
                Mathf.Abs(horizontal[i + 1].a.x - end.x) < 0.001f)
            {
                end = horizontal[++i].b;
            }

            result.Add((start, end));
        }

        // Vertical merges
        var vertical = edges.FindAll(e => Mathf.Abs(e.a.x - e.b.x) < 0.0001f);
        vertical.Sort((e1, e2) => e1.a.y.CompareTo(e2.a.y));

        for (int i = 0; i < vertical.Count; i++)
        {
            Vector2 start = vertical[i].a;
            Vector2 end = vertical[i].b;

            while (i + 1 < vertical.Count &&
                Mathf.Abs(vertical[i + 1].a.x - start.x) < 0.001f &&
                Mathf.Abs(vertical[i + 1].a.y - end.y) < 0.001f)
            {
                end = vertical[++i].b;
            }

            result.Add((start, end));
        }

        return result;
    }

    // AABB overlap using our stored tile centers/sizes
    bool RectsOverlap(Vector2 c1, Vector2 s1, Vector2 c2, Vector2 s2)
    {
        Vector2 half1 = s1 * 0.5f;
        Vector2 half2 = s2 * 0.5f;

        float dx = Mathf.Abs(c1.x - c2.x);
        float dy = Mathf.Abs(c1.y - c2.y);

        float limitX = half1.x + half2.x - contactEpsilon;
        float limitY = half1.y + half2.y - contactEpsilon;

        // overlap only if they actually intrude into each other
        return (dx < limitX) && (dy < limitY);
    }

    // Does a candidate tile (center/size) overlap ANY existing tile?
    bool OverlapsExistingRect(Vector2 center, Vector2 size)
    {
        foreach (var kvp in _tileSizes)
        {
            Vector2Int cell = kvp.Key;
            Vector2 existingSize   = kvp.Value;
            Vector2 existingCenter = _tileCenters[cell];

            if (RectsOverlap(center, size, existingCenter, existingSize))
                return true;
        }

        return false;
    }

    Vector2 ApplyGentleSnap(Vector2 candidateCenter, Vector2 candidateSize)
    {
        Vector2 best = candidateCenter;
        float bestDist = snapTolerance;

        foreach (var kv in _tileSizes)
        {
            Vector2 otherCenter = _tileCenters[kv.Key];
            Vector2 otherSize   = kv.Value;
            Vector2 half        = otherSize * 0.5f;

            // All 4 corners of the existing tile
            Vector2[] corners = new Vector2[]
            {
                otherCenter + new Vector2(-half.x,  half.y), // TL
                otherCenter + new Vector2( half.x,  half.y), // TR
                otherCenter + new Vector2(-half.x, -half.y), // BL
                otherCenter + new Vector2( half.x, -half.y)  // BR
            };

            foreach (var c in corners)
            {
                float d = Vector2.Distance(candidateCenter, c);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = c;
                }
            }
        }

        // If no corner was close, return original position
        if (bestDist >= snapTolerance)
            return candidateCenter;

        // Snap gently (lerp) for smooth motion
        return Vector2.Lerp(candidateCenter, best, snapStrength);
    }

}
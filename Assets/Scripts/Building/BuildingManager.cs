using UnityEngine;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PlayerInventory playerInventory;
    public PlayerController playerController;

    [Header("Tiles & Prefabs")]
    public GameObject tilePrefab;           // tile prefab (SpriteRenderer + non-trigger Collider2D + FloorTile)
    public GameObject indicatorPrefab;      // green/gray indicator (BuildIndicator + trigger BoxCollider2D)
    public List<Transform> foundationTiles; // at least one placed tile to seed the grid

    [Header("Rules")]
    public int  buildCost = 10;
    public bool restrictFirstBuildAbove = true; // first placement cannot go Down
    public float contactEpsilon = 0.01f;

    [Header("Input")]
    public KeyCode toggleBuildKey = KeyCode.T;

    [Header("Indicator Colors")]
    public Color availableColor   = Color.green;
    public Color unavailableColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);

    [Header("Debug")]
    public bool showGizmos = false;

    // --- internal ---
    HashSet<Vector2Int> _occupied = new HashSet<Vector2Int>(); // grid cells
    readonly List<GameObject> _indicators = new List<GameObject>();
    Vector2 _cellSize;
    Vector2 _gridOrigin;
    bool _firstExpansionDone = false;
    bool _buildMode = false;
    Camera _cam;
    BuildIndicator _hovered; // current hovered indicator (for yellow highlight)

    void Start()
    {
        _cam = Camera.main;
        if (!playerInventory && player) playerInventory = player.GetComponent<PlayerInventory>();

        // grid cell from prefab only
        _cellSize = GetPrefabWorldSize(tilePrefab);
        if (_cellSize.x <= 0f || _cellSize.y <= 0f) _cellSize = Vector2.one;

        // origin from first seed tile
        if (foundationTiles == null || foundationTiles.Count == 0 || foundationTiles[0] == null)
        {
            Debug.LogError("[Building] No foundationTiles assigned.");
            return;
        }
        _gridOrigin = foundationTiles[0].position;

        // seed occupied
        _occupied.Clear();
        foreach (var t in foundationTiles)
            if (t) _occupied.Add(WorldToCellCenter(t.position));
    }

    void Update()
    {
        if (playerInventory == null || tilePrefab == null) return;

        // Toggle build mode
        if (Input.GetKeyDown(toggleBuildKey))
        {
            _buildMode = !_buildMode;
            if (_buildMode) RefreshIndicators();
            else { ClearIndicators(); _hovered = null; }
        }

        if (!_buildMode) return;

        // Update states each frame so color/clickability reflect current parts
        bool canAfford = playerInventory.parts >= buildCost;
        UpdateIndicatorStates(canAfford);

        // Hover highlight (yellow) over the one under the mouse (only if buildable)
        UpdateHover(canAfford);

        // Place on Right Click (change to 0 for left click if you prefer)
        if (canAfford && Input.GetMouseButtonDown(1) && _hovered != null && _hovered.canBuild)
        {
            TryBuild(_hovered.targetWorld);
        }
    }

    // ---------------- Build ----------------
    void TryBuild(Vector3 worldCenter)
    {
        if (!playerInventory.SpendParts(buildCost)) return;

        // snap to grid
        Vector2Int cell = WorldToCellCenter(worldCenter);
        Vector3 snapped = (Vector3)CellCenterToWorld(cell);

        var tile = Instantiate(tilePrefab, snapped, Quaternion.identity);
        if (!tile.GetComponent<FloorTile>()) tile.AddComponent<FloorTile>();

        _occupied.Add(cell);
        if (!_firstExpansionDone) _firstExpansionDone = true;

        // Placeholder just upgrade wizard upon build
        PlayerHelper.damage += 1;
        PlayerHelper.attackspeed -= 0.1f;
        Debug.Log(PlayerHelper.damage);
        Debug.Log(PlayerHelper.attackspeed);
        playerController.fireRate = PlayerHelper.attackspeed;

        RefreshIndicators(); // rebuild perimeter after placement
    }

    // ---------------- Indicators ----------------
    void RefreshIndicators()
    {
        ClearIndicators();

        // perimeter candidates
        HashSet<Vector2Int> candidates = new HashSet<Vector2Int>();
        foreach (var c in _occupied)
            foreach (var n in EligibleNeighbors(c))
                if (!_occupied.Contains(n))
                    candidates.Add(n);

        bool canAfford = playerInventory && playerInventory.parts >= buildCost;

        foreach (var c in candidates)
        {
            Vector2 center = CellCenterToWorld(c);
            if (!IsSpaceFree(center, _cellSize)) continue;

            var go = Instantiate(indicatorPrefab, center, Quaternion.identity);
            var bi = go.GetComponent<BuildIndicator>();
            bi.targetWorld = center;
            bi.SetVisualSize(_cellSize);
            bi.ConfigureState(canAfford, availableColor, unavailableColor);
            _indicators.Add(go);
        }
    }

    void UpdateIndicatorStates(bool canAfford)
    {
        foreach (var go in _indicators)
        {
            if (!go) continue;
            var bi = go.GetComponent<BuildIndicator>();
            if (!bi) continue;
            bi.ConfigureState(canAfford, availableColor, unavailableColor);
        }
    }

    void UpdateHover(bool canAfford)
    {
        // clear previous hover
        if (_hovered != null) { _hovered.SetHovered(false); _hovered = null; }

        if (!canAfford) return;

        Vector2 mouse = _cam ? (Vector2)_cam.ScreenToWorldPoint(Input.mousePosition)
                             : (Vector2)Input.mousePosition;

        var hit = Physics2D.OverlapPoint(mouse);
        var bi = hit ? hit.GetComponent<BuildIndicator>() : null;
        if (bi != null && bi.canBuild)
        {
            bi.SetHovered(true);
            _hovered = bi;
        }
    }

    void ClearIndicators()
    {
        foreach (var go in _indicators) if (go) Destroy(go);
        _indicators.Clear();
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

    IEnumerable<Vector2Int> EligibleNeighbors(Vector2Int c)
    {
        if (!_firstExpansionDone && restrictFirstBuildAbove)
        {
            yield return new Vector2Int(c.x, c.y + 1); // up
            yield return new Vector2Int(c.x - 1, c.y); // left
            yield return new Vector2Int(c.x + 1, c.y); // right
            yield break;
        }
        yield return new Vector2Int(c.x, c.y + 1);
        yield return new Vector2Int(c.x, c.y - 1);
        yield return new Vector2Int(c.x - 1, c.y);
        yield return new Vector2Int(c.x + 1, c.y);
    }

    // ---------------- Size & space ----------------
    static Vector2 GetPrefabWorldSize(GameObject prefab)
    {
        if (prefab == null) return Vector2.one;

        if (prefab.TryGetComponent<BoxCollider2D>(out var box))
        {
            Vector3 s = prefab.transform.lossyScale;
            return new Vector2(Mathf.Abs(box.size.x * s.x), Mathf.Abs(box.size.y * s.y));
        }
        if (prefab.TryGetComponent<CircleCollider2D>(out var cir))
        {
            Vector3 s = prefab.transform.lossyScale;
            float d = cir.radius * 2f;
            return new Vector2(Mathf.Abs(d * s.x), Mathf.Abs(d * s.y));
        }
        if (prefab.TryGetComponent<PolygonCollider2D>(out var poly))
        {
            Vector3 s = prefab.transform.lossyScale;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var p in poly.points)
            {
                Vector2 wp = new Vector2(p.x * s.x, p.y * s.y);
                min = Vector2.Min(min, wp);
                max = Vector2.Max(max, wp);
            }
            return max - min;
        }
        if (prefab.TryGetComponent<SpriteRenderer>(out var sr))
        {
            Vector3 s = prefab.transform.lossyScale;
            var sz = sr.sprite ? sr.sprite.bounds.size : Vector3.one;
            return new Vector2(Mathf.Abs(sz.x * s.x), Mathf.Abs(sz.y * s.y));
        }
        return Vector2.one;
    }

    bool IsSpaceFree(Vector2 center, Vector2 size)
    {
        Vector2 test = new Vector2(Mathf.Max(0.01f, size.x - contactEpsilon),
                                   Mathf.Max(0.01f, size.y - contactEpsilon));

        var hits = Physics2D.OverlapBoxAll(center, test, 0f);
        foreach (var h in hits)
        {
            if (!h || h.isTrigger) continue;

            // Only block on placed tiles or static world; ignore dynamic entities
            if (h.GetComponent<FloorTile>() != null) return false;
            if (h.attachedRigidbody == null) return false; // static collider (e.g., foundation/walls)
        }
        return true;
    }

    void OnDrawGizmosSelected()
    {
        if (!showGizmos || tilePrefab == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
        foreach (var go in _indicators)
        {
            if (!go) continue;
            var bi = go.GetComponent<BuildIndicator>();
            if (bi == null) continue;
            Gizmos.DrawWireCube(bi.targetWorld, _cellSize);
        }
    }
}

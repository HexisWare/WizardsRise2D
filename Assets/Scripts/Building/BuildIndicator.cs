using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
public class BuildIndicator : MonoBehaviour
{
    [HideInInspector] public Vector3 targetWorld;
    [HideInInspector] public bool canBuild;

    public Color availableColor   = Color.green;
    // [SerializeField] Color unavailableColor = new Color(0.6f, 0.6f, 0.6f, 0.9f);
    public Color unavailableColor = Color.red;
    public Color hoverColor       = Color.yellow;

    SpriteRenderer _sr;
    BoxCollider2D _col;
    bool _hovered;

    void Awake()
    {
        _sr  = GetComponent<SpriteRenderer>();
        _col = GetComponent<BoxCollider2D>();
    }

    public void SetVisualSize(Vector2 desiredWorldSize)
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();
        var current = _sr.bounds.size;
        if (current.x > 1e-4f && current.y > 1e-4f)
        {
            var s = transform.localScale;
            s.x *= desiredWorldSize.x / current.x;
            s.y *= desiredWorldSize.y / current.y;
            transform.localScale = s;
        }
    }

    public void ConfigureState(bool canBuildNow, Color avail, Color unavail)
    {
        canBuild        = canBuildNow;
        availableColor  = avail;
        unavailableColor = unavail;

        if (_col != null) _col.enabled = canBuild;   // only clickable if buildable
        ApplyColor();
    }

    public void SetHovered(bool hovered)
    {
        _hovered = hovered;
        ApplyColor();
    }

    void ApplyColor()
    {
        if (_sr == null) return;
        if (!canBuild) { _sr.color = unavailableColor; return; }
        _sr.color = _hovered ? hoverColor : availableColor;
    }
}

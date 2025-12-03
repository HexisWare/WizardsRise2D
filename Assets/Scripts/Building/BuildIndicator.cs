using UnityEngine;

public class BuildIndicator : MonoBehaviour
{
    [Header("Child Sprite Renderers")]
    public SpriteRenderer previewSR;   // shows tile preview
    public SpriteRenderer overlaySR;   // shows red/green/yellow tint

    private bool _hovered = false;

    //------------------------------------------
    // PREVIEW SPRITE
    //------------------------------------------
    public void SetPreviewSprite(Sprite s, Vector2 worldSize)
    {
        if (s == null)
        {
            previewSR.enabled = false;
            return;
        }

        previewSR.enabled = true;
        previewSR.sprite = s;

        // VERY IMPORTANT — reset scale EVERY TIME
        previewSR.transform.localScale = Vector3.one;

        // Scale to world size
        Vector2 spriteSize = previewSR.sprite.bounds.size;
        previewSR.transform.localScale = new Vector3(
            worldSize.x / spriteSize.x,
            worldSize.y / spriteSize.y,
            1f
        );

        // Reset alpha properly
        previewSR.color = new Color(1f, 1f, 1f, 0.8f);

        // Ensure preview is above overlay
        previewSR.sortingOrder = overlaySR.sortingOrder + 1;
    }

    //------------------------------------------
    // OVERLAY COLOR + SCALE
    //------------------------------------------
    public void SetOverlayColor(Color c)
    {
        overlaySR.enabled = true;
        overlaySR.color = c;
    }

    public void SetIndicatorSize(Vector2 worldSize)
    {
        if (overlaySR.sprite == null) return;

        overlaySR.transform.localScale = Vector3.one;

        Vector2 spriteSize = overlaySR.sprite.bounds.size;
        overlaySR.transform.localScale = new Vector3(
            worldSize.x / spriteSize.x,
            worldSize.y / spriteSize.y,
            1f
        );
    }

    //------------------------------------------
    // HOVERING (optional)
    //------------------------------------------
    public void SetHovered(bool hovered)
    {
        _hovered = hovered;
    }
}

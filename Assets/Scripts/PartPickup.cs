using UnityEngine;

public class PartPickup : MonoBehaviour
{
    public int value = 20;
    public float lifetime = 20f;

    void Awake()
    {
        var rb = GetComponent<Rigidbody2D>();
        if (rb)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.gravityScale = Mathf.Max(1.5f, rb.gravityScale);
        }
    }

    void Start() => Destroy(gameObject, lifetime);

    void OnCollisionEnter2D(Collision2D col)
    {
        // Collides with ground and doesn't fall through
        if (col.collider.CompareTag("Player"))
        {
            if (col.collider.TryGetComponent<PlayerInventory>(out var inv))
                inv.AddParts(value);

            Destroy(gameObject);
        }
    }
}

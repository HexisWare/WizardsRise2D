using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(EnemyHealth))]
public class GroundEnemyAI : MonoBehaviour
{
    public float moveSpeed = 2.2f;

    private Rigidbody2D _rb;
    private BoxCollider2D _box;
    private Transform _player;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _box = GetComponent<BoxCollider2D>();

        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.gravityScale = Mathf.Max(2f, _rb.gravityScale);

        // Kill friction so floor physics don't “glue” the enemy
        var mat = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
        _box.sharedMaterial = mat;
    }

    public void Init(Transform player) => _player = player;

    void Update()
    {
        if (_player == null) return;

        float dir = Mathf.Sign(_player.position.x - transform.position.x);
        _rb.linearVelocity = new Vector2(dir * moveSpeed, _rb.linearVelocity.y);

        // Visual flip
        transform.localScale = new Vector3(dir >= 0 ? 0.25f : -0.25f, 0.25f, 1f);
    }
}

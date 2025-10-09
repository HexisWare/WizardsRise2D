using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class FlyingEnemyAI : MonoBehaviour
{
    public float moveSpeed = 3.3f;

    private Rigidbody2D _rb;
    private Transform _player;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    public void Init(Transform player) => _player = player;

    void Update()
    {
        if (_player == null) return;

        Vector2 dir = (_player.position - transform.position).normalized;
        _rb.linearVelocity = dir * moveSpeed;

        // orient sprite toward movement (optional)
        if (dir.sqrMagnitude > 0.001f)
            transform.right = dir;
    }
}

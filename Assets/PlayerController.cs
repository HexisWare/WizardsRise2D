using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = PlayerHelper.speed;

    [Tooltip("Impulse force applied on jump.")]
    public float jumpForce = 10f;

    [Tooltip("Allowed time after leaving ground where a jump still works.")]
    public float coyoteTime = 0.12f;

    [Tooltip("Allowed time a jump input is buffered before landing.")]
    public float jumpBuffer = 0.12f;

    [Header("Shooting")]
    public GameObject projectilePrefab;     // assign prefab (Rigidbody2D + collider)
    public Transform muzzle;                // set to a child fire point (optional)
    public float projectileSpeed = 14f;
    public float fireRate = PlayerHelper.attackspeed;          // seconds between shots
    public Camera cam;                      // optional; if null uses Camera.main

    private Rigidbody2D _rb;
    private BoxCollider2D _box;
    private Collider2D[] _myCols;
    private float _nextFireTime;
    private float _lastGroundedTime = -999f;
    private float _lastJumpPressedTime = -999f;

    // For robust ground detection via collider cast
    private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];
    private ContactFilter2D _groundFilter;
    private const float GroundCastDistance = 0.06f; // just below feet

    void Awake()
    {
        PlayerSetup();
        moveSpeed = PlayerHelper.speed;
        Debug.Log(moveSpeed);
        fireRate = PlayerHelper.attackspeed;
        Debug.Log(fireRate);
        _rb = GetComponent<Rigidbody2D>();
        _box = GetComponent<BoxCollider2D>();
        _myCols = GetComponentsInChildren<Collider2D>(false);

        _rb.gravityScale = Mathf.Max(2.5f, _rb.gravityScale);
        _rb.freezeRotation = true;

        if (cam == null) cam = Camera.main;
        if (muzzle == null) muzzle = transform;

        // Ignore triggers when checking ground; no layer setup required
        _groundFilter = new ContactFilter2D { useTriggers = false };
    }

    void Update()
    {
        HandleHorizontal();
        HandleJumpInput();
        HandleShootInput();
    }

    // ---------------- Movement ----------------
    void HandleHorizontal()
    {
        float h = Input.GetAxisRaw("Horizontal");
        _rb.linearVelocity = new Vector2(h * moveSpeed, _rb.linearVelocity.y);

        // Flip visually by scale (purely cosmetic)
        if (h > 0.01f) transform.localScale = new Vector3(0.25f, 0.25f, 1f);
        else if (h < -0.01f) transform.localScale = new Vector3(-0.25f, 0.25f, 1f);

        // Update grounded time each frame
        if (IsGrounded()) _lastGroundedTime = Time.time;
    }

    // ---------------- Jump (instant on landing with buffer/coyote) ----------------
    void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _lastJumpPressedTime = Time.time;

        bool canUseBufferedJump = (Time.time - _lastJumpPressedTime) <= jumpBuffer;
        bool withinCoyote = (Time.time - _lastGroundedTime) <= coyoteTime;

        if (canUseBufferedJump && withinCoyote)
        {
            // Consume buffer
            _lastJumpPressedTime = -999f;

            // Reset vertical speed for consistent jump pop
            Vector2 v = _rb.linearVelocity;
            v.y = 0f;
            _rb.linearVelocity = v;

            _rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        }
    }

    bool IsGrounded()
    {
        // Cast the collider a tiny distance down to see if we would hit anything solid
        int hitCount = _box.Cast(Vector2.down, _groundFilter, _groundHits, GroundCastDistance);
        if (hitCount == 0) return false;

        // Ignore hits on ourselves
        for (int i = 0; i < hitCount; i++)
        {
            var col = _groundHits[i].collider;
            if (!col) continue;
            if (System.Array.IndexOf(_myCols, col) >= 0) continue; // it's us
            return true;
        }
        return false;
    }

    // ---------------- Shooting (aim at mouse) ----------------
    void HandleShootInput()
    {
        if (Input.GetMouseButton(0) && Time.time >= _nextFireTime)
        {
            _nextFireTime = Time.time + fireRate;
            ShootTowardMouse();
        }
    }

    void ShootTowardMouse()
    {
        if (!projectilePrefab)
        {
            Debug.LogWarning("[PlayerController] No projectile prefab assigned.");
            return;
        }
        if (cam == null) cam = Camera.main;

        // Spawn slightly outside player to avoid instant collision
        float pad = 0.08f;
        float halfWidth = _box ? _box.bounds.extents.x : 0.5f;
        Vector3 baseSpawn = muzzle ? muzzle.position : transform.position;

        // World position of mouse
        Vector3 mouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = baseSpawn.z;

        Vector2 dir = (mouseWorld - baseSpawn).normalized;
        Vector3 spawn = baseSpawn + (Vector3)(dir * (halfWidth + pad));

        GameObject proj = Instantiate(projectilePrefab, spawn, Quaternion.identity);

        // Orient projectile to its travel direction (optional but nice)
        proj.transform.right = dir;

        // Ignore collisions with our own colliders
        if (proj.TryGetComponent<Collider2D>(out var pcol))
        {
            foreach (var myCol in _myCols)
            {
                if (myCol) Physics2D.IgnoreCollision(pcol, myCol, true);
            }
        }

        // Give it velocity
        if (proj.TryGetComponent<Rigidbody2D>(out var prb))
        {
            prb.gravityScale = 0f;
            prb.linearVelocity = dir * projectileSpeed;
            prb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void PlayerSetup()
    {
        PlayerHelper.attackspeed = 0.8f;
        PlayerHelper.speed = 5f;
        PlayerHelper.damage = 1f;
        PlayerHelper.maxHp = 10f;
        PlayerHelper.currentHp = PlayerHelper.maxHp;
    }

    // ---------------- Gizmos ----------------
    void OnDrawGizmosSelected()
    {
        if (_box == null) return;
        Gizmos.color = Color.green;
        Bounds b = _box.bounds;
        Vector3 from = new Vector3(b.center.x, b.min.y, 0f);
        Gizmos.DrawLine(from, from + Vector3.down * GroundCastDistance);
    }
}

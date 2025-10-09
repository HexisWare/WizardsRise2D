using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveSpawner : MonoBehaviour
{
    [Header("References")]
    public Camera cam;                  // Main Camera
    public Transform player;            // Player transform
    public Collider2D groundCollider;   // The big floor's BoxCollider2D

    [Header("Prefabs")]
    public GameObject groundEnemyPrefab;
    public GameObject flyingEnemyPrefab;

    [Header("Timing")]
    public float startDelay = 1.0f;
    public float interWaveDelay = 1.5f;      // short breather
    public float waveTimeoutSeconds = 60f;   // next wave even if enemies remain

    [Header("Spawn Offscreen")]
    public float offscreenMargin = 2.0f;     // how far outside camera bounds

    [Header("Debug")]
    public bool debugLogs = false;
    public Color gizmoColor = new Color(1f, 0.6f, 0.1f, 0.75f);

    [Header("Wave Number")]
    public int wave = 1;

    private readonly HashSet<EnemyHealth> _alive = new HashSet<EnemyHealth>();

    void Start()
    {
        if (!ValidateRefs()) return;
        StartCoroutine(SpawnLoop());
    }

    bool ValidateRefs()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) { Debug.LogError("[WaveSpawner] Missing Camera."); return false; }
        if (player == null) { Debug.LogError("[WaveSpawner] Missing Player transform."); return false; }
        if (groundCollider == null) { Debug.LogError("[WaveSpawner] Missing Ground collider."); return false; }
        if (groundEnemyPrefab == null) { Debug.LogError("[WaveSpawner] Missing Ground Enemy prefab."); return false; }
        if (flyingEnemyPrefab == null) { Debug.LogError("[WaveSpawner] Missing Flying Enemy prefab."); return false; }
        return true;
    }

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(startDelay);

        while (true)
        {
            StartWave();  // 1 ground + 1 flying

            float start = Time.time;
            while (true)
            {
                CleanupNulls();
                bool allDead = _alive.Count == 0;
                bool timedOut = (Time.time - start) >= waveTimeoutSeconds;
                if (allDead || timedOut) break;
                yield return null;
            }

            yield return new WaitForSeconds(interWaveDelay);
        }
    }

    void StartWave()
    {
        for (int i = 1; i <= wave; ++i)
        {
            var g = SpawnGroundEnemy();
            var f = SpawnFlyingEnemy();
            Track(g);
            Track(f);
        }
        if (debugLogs)
            Debug.Log($"[WaveSpawner] Wave started. Alive now: {_alive.Count}");
        wave += 1;
    }

    void Track(GameObject go)
    {
        if (!go) return;
        if (go.TryGetComponent<EnemyHealth>(out var hp))
        {
            _alive.Add(hp);
            hp.OnDeath += () => _alive.Remove(hp);
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[WaveSpawner] Spawned enemy without EnemyHealth component.");
        }
    }

    void CleanupNulls() => _alive.RemoveWhere(e => e == null);

    // ---------------- Spawning helpers ----------------

    GameObject SpawnGroundEnemy()
    {
        // Camera bounds
        float vert = cam.orthographicSize;
        float horiz = vert * cam.aspect;
        float camLeft = cam.transform.position.x - horiz;
        float camRight = cam.transform.position.x + horiz;

        bool fromLeft = Random.value < 0.5f;
        float x = fromLeft ? (camLeft - offscreenMargin) : (camRight + offscreenMargin);

        // Spawn roughly near floor, adjust after instantiation based on enemy collider
        Vector3 spawn = new Vector3(x, groundCollider.bounds.max.y + 0.25f, 0f);
        var go = Instantiate(groundEnemyPrefab, spawn, Quaternion.identity);

        // Snap to sit on top of floor using collider height
        if (go.TryGetComponent<Collider2D>(out var ec))
        {
            float halfH = ec.bounds.extents.y;
            float floorTopY = groundCollider.bounds.max.y;
            go.transform.position = new Vector3(x, floorTopY + halfH + 0.02f, 0f);
        }

        if (go.TryGetComponent<GroundEnemyAI>(out var ai))
            ai.Init(player);

        if (debugLogs)
            Debug.Log($"[WaveSpawner] Ground enemy @ {go.transform.position}");

        return go;
    }

    GameObject SpawnFlyingEnemy()
    {
        float vert = cam.orthographicSize;
        float horiz = vert * cam.aspect;
        float left = cam.transform.position.x - horiz;
        float right = cam.transform.position.x + horiz;
        float top = cam.transform.position.y + vert;
        float bottom = cam.transform.position.y - vert;

        int line = Random.Range(0, 3); // 0=left, 1=top, 2=right
        Vector3 spawn;

        switch (line)
        {
            default:
            case 0: spawn = new Vector3(left  - offscreenMargin, Random.Range(bottom, top), 0f); break;
            case 1: spawn = new Vector3(Random.Range(left, right), top + offscreenMargin,   0f); break;
            case 2: spawn = new Vector3(right + offscreenMargin, Random.Range(bottom, top), 0f); break;
        }

        var go = Instantiate(flyingEnemyPrefab, spawn, Quaternion.identity);

        if (go.TryGetComponent<FlyingEnemyAI>(out var ai))
            ai.Init(player);

        if (debugLogs)
            Debug.Log($"[WaveSpawner] Flying enemy @ {spawn} (line {line})");

        return go;
    }

    // Visualize spawn lines in Scene view
    void OnDrawGizmos()
    {
        if (cam == null) return;
        float vert = cam.orthographicSize;
        float horiz = vert * cam.aspect;

        float left = cam.transform.position.x - horiz - offscreenMargin;
        float right = cam.transform.position.x + horiz + offscreenMargin;
        float top = cam.transform.position.y + vert + offscreenMargin;
        float bottom = cam.transform.position.y - vert;

        Gizmos.color = gizmoColor;
        // left vertical
        Gizmos.DrawLine(new Vector3(left, bottom, 0f), new Vector3(left, top, 0f));
        // top horizontal
        Gizmos.DrawLine(new Vector3(left + offscreenMargin, top, 0f), new Vector3(right - offscreenMargin, top, 0f));
        // right vertical
        Gizmos.DrawLine(new Vector3(right, bottom, 0f), new Vector3(right, top, 0f));
    }
}

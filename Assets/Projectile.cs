using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float lifetime = 2.5f;
    private float damage = PlayerHelper.damage;

    void Start() => Destroy(gameObject, lifetime);

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.isTrigger) return; // ignore triggers

        if (other.TryGetComponent<IDamageable>(out var d))
            d.Damage(damage);

        Destroy(gameObject);
    }
}

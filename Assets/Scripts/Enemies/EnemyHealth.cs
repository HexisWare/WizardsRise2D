using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public float maxHP = 3f;
    float _hp;

    [Header("Drop")]
    public GameObject partPickupPrefab;   // assign your circle pickup prefab
    public int dropValue = 20;            // redundancy; prefab also has value

    public System.Action OnDeath;

    void Awake() => _hp = maxHP;

    public void Damage(float amount)
    {
        _hp -= amount;
        if (_hp <= 0f)
        {
            OnDeath?.Invoke();
            DropParts();
            Destroy(gameObject);
        }
    }

    void DropParts()
    {
        if (!partPickupPrefab) return;
        var go = Instantiate(partPickupPrefab, transform.position, Quaternion.identity);
        if (go.TryGetComponent<PartPickup>(out var pp))
            pp.value = dropValue;
    }
}

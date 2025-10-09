using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public int parts = 0;

    public void AddParts(int amount)
    {
        parts += Mathf.Max(0, amount);
        Debug.Log($"[Inventory] Parts = {parts}");
    }

    public bool SpendParts(int cost)
    {
        if (parts < cost) return false;
        parts -= cost;
        Debug.Log($"[Inventory] Spent {cost}. Parts = {parts}");
        return true;
    }
}

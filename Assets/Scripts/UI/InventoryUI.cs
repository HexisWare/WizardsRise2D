using UnityEngine;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory playerInventory; // Reference to the PlayerInventory script
    public TextMeshProUGUI inventoryText; // Reference to the TextMeshProUGUI component

    void Update()
    {
        if (playerInventory != null && inventoryText != null)
        {
            inventoryText.text = $"Parts: {playerInventory.parts}";
        }
    }
}
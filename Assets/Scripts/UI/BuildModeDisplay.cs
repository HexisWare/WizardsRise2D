// 12/2/2025 AI-Tag
// This was created with the help of Assistant, a Unity Artificial Intelligence product.

using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BuildModeDisplay : MonoBehaviour
{
    public TextMeshProUGUI buildModeText; // Reference to the TMPPro text object
    public Image miniPicture; // Reference to the mini picture image
    public Sprite[] tileSprites; // Array of tile sprites
    private string[] tileNames = { "Tile Small", "Tile Medium", "Tile Large", "Tile Huge" }; // Example tile names
    private int currentTileIndex = 0; // Index of the currently selected tile

    private bool isBuildModeActive = false; // Tracks if build mode is active

    void Start()
    {
        // Initially hide the build mode text and mini picture
        buildModeText.gameObject.SetActive(false);
        miniPicture.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            ToggleBuildModeDisplay();
        }

        if (isBuildModeActive)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                ChangeTile(1); // Move to the next tile
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                ChangeTile(-1); // Move to the previous tile
            }
        }
    }

    void ToggleBuildModeDisplay()
    {
        isBuildModeActive = !isBuildModeActive; // Toggle the build mode state
        buildModeText.gameObject.SetActive(isBuildModeActive);
        miniPicture.gameObject.SetActive(isBuildModeActive);

        if (isBuildModeActive)
        {
            UpdateBuildModeDisplay();
        }
    }

    void UpdateBuildModeDisplay()
    {
        buildModeText.text = $"Selected: {tileNames[currentTileIndex]}";
        miniPicture.sprite = tileSprites[currentTileIndex];
    }

    void ChangeTile(int direction)
    {
        currentTileIndex += direction;

        // Wrap around the index if it goes out of bounds
        if (currentTileIndex < 0)
        {
            currentTileIndex = tileSprites.Length - 1;
        }
        else if (currentTileIndex >= tileSprites.Length)
        {
            currentTileIndex = 0;
        }

        UpdateBuildModeDisplay();
    }

    public void SetSelectedTile(int index)
    {
        currentTileIndex = index;
        if (isBuildModeActive)
        {
            UpdateBuildModeDisplay();
        }
    }
}
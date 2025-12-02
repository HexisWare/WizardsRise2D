using UnityEngine;

public class CameraZoomController : MonoBehaviour
{
    public Camera mainCamera; // Reference to the Main Camera
    public float zoomSpeed = 2f; // Speed of zooming
    public float minZoom = 2f; // Minimum zoom level
    public float maxZoom = 10f; // Maximum zoom level

    void Update()
    {
        // Get the scroll wheel input
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        // Adjust the orthographic size based on the scroll input
        if (mainCamera.orthographic)
        {
            mainCamera.orthographicSize -= scrollInput * zoomSpeed;
            mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize, minZoom, maxZoom);
        }
    }
}
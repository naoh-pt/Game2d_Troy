using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Optional click-to-destroy harness compiled only for Editor and Development input paths.</summary>
[DisallowMultipleComponent]
public sealed class TerrainDestructionDebugTester : MonoBehaviour
{
    [SerializeField] private DestructibleTerrain2D terrain;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool testingEnabled = true;
    [SerializeField, Min(0.01f)] private float testRadius = 1.5f;

    public void Configure(DestructibleTerrain2D terrainReference, Camera cameraReference)
    {
        terrain = terrainReference;
        worldCamera = cameraReference;
    }

    private void OnValidate()
    {
        testRadius = Mathf.Max(0.01f, testRadius);
    }

    private void Update()
    {
#if !UNITY_EDITOR
        if (!Debug.isDebugBuild)
            return;
#endif
        if (!testingEnabled || terrain == null || worldCamera == null)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.rightButton.wasPressedThisFrame || PointerIsOverUi())
            return;

        Vector2 screenPosition = mouse.position.ReadValue();
        float terrainDepth = worldCamera.WorldToScreenPoint(terrain.transform.position).z;
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, terrainDepth));
        terrain.DestroyCircle(worldPosition, testRadius);
    }

    private static bool PointerIsOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }
}

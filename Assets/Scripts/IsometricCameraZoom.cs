using UnityEngine;
using UnityEngine.InputSystem;

public class IsometricCameraZoom : MonoBehaviour {
    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float zoomSmoothness = 10f;

    public float maxZoom = 20f;
    public float minZoom = 5f;

    public bool _manualOverride = false;

    private float _currentZoom;
    private float _scrollInput;

    private Camera _camera;

    void Awake() {
        _camera = GetComponentInChildren<Camera>();
        _currentZoom = _camera.orthographicSize;
    }

    void Update() {
        if (_scrollInput != 0f && !_manualOverride) {
            _currentZoom = Mathf.Clamp(_currentZoom - _scrollInput * zoomSpeed, minZoom, maxZoom );
        }

        _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, _currentZoom, zoomSmoothness * Time.deltaTime );
    }

    // Input System callback
    public void OnScroll(InputAction.CallbackContext ctx) {
        if (ctx.performed) {
            Vector2 scroll = ctx.ReadValue<Vector2>();
            _scrollInput = scroll.y;
        } else if (ctx.canceled) {
            _scrollInput = 0f;
        }
    }
}

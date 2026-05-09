using UnityEngine;
using UnityEngine.InputSystem;

public class IsometricCameraRotate : MonoBehaviour {
    public float rotationSpeed = 200f;

    private Camera _mainCamera;
    private bool _isDragging;
    private Vector2 _lastMousePos;

    private void Awake() {
        _mainCamera = Camera.main;
    }

    // Hook this to your RMB action (e.g. "Rotate")
    public void OnRotate(InputAction.CallbackContext ctx) {
        if (ctx.started) {
            _lastMousePos = Mouse.current.position.ReadValue();
            _isDragging = true;
        } else if (ctx.canceled) {
            _isDragging = false;
        }
    }

    private void LateUpdate() {
        if (!_isDragging) return;

        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        float deltaX = currentMousePos.x - _lastMousePos.x;

        // Rotate around world Y axis
        transform.Rotate(Vector3.up, deltaX * rotationSpeed * Time.deltaTime, Space.World);

        _lastMousePos = currentMousePos;
    }
}
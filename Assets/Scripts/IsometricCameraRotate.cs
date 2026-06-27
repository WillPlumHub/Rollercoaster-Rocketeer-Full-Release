using UnityEngine;
using UnityEngine.InputSystem;

public class IsometricCameraRotate : MonoBehaviour {
    [Header("Drag")]
    public float rotationSpeed = 200f;
    public float maxDragSpeed = 360f;

    [Header("Momentum")]
    public float momentumEndThreshold = 20f;
    public float deceleration = 180f;
    public float maxMomentumSpeed = 720f;
    [Range(0.01f, 1f)]
    public float dragSmoothingTime = 0.25f;

    public bool _manualOverride = false;

    private bool _isDragging;
    private Vector2 _lastMousePos;

    private float angularVelocity;   // Only X movement matters for yaw

    public void OnRotate(InputAction.CallbackContext ctx) {
        if (ctx.started) {
            _lastMousePos = Mouse.current.position.ReadValue();
            _isDragging = true;
        } else if (ctx.canceled) {
            if (Mathf.Abs(angularVelocity) < momentumEndThreshold)
                angularVelocity = 0f;

            _isDragging = false;
        }
    }

    private void LateUpdate() {
        if (_manualOverride) return;

        if (_isDragging) ApplyDrag();
        else ApplyMomentum();
    }

    private void ApplyDrag() {
        Vector2 current = Mouse.current.position.ReadValue();
        float deltaX = current.x - _lastMousePos.x;
        _lastMousePos = current;

        // Smooth velocity
        angularVelocity = Mathf.Lerp(angularVelocity, deltaX * rotationSpeed, dragSmoothingTime);
        angularVelocity = Mathf.Clamp(angularVelocity, -maxMomentumSpeed, maxMomentumSpeed);

        float applied = Mathf.Clamp(angularVelocity, -maxDragSpeed, maxDragSpeed);
        Rotate(applied * Time.deltaTime);
    }

    private void ApplyMomentum() {
        if (Mathf.Abs(angularVelocity) < 0.01f)
            return;

        Rotate(angularVelocity * Time.deltaTime);

        angularVelocity = Mathf.MoveTowards(
            angularVelocity,
            0f,
            deceleration * Time.deltaTime
        );
    }

    private void Rotate(float deltaYaw) {
        transform.Rotate(Vector3.up, deltaYaw, Space.World);
    }
}

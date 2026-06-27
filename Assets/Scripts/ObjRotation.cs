// Fix rotation when facing away from camera

using UnityEngine;
using UnityEngine.InputSystem;

public class ObjRotation : MonoBehaviour {

    [Header("Drag")]
    public float rotationSpeed = 200f;
    public float maxDragSpeed = 360f;

    [Header("Momentum")]
    public float momentumEndThreshold = 20f;
    public float deceleration = 180f;
    public float maxMomentumSpeed = 720f;
    [Range(0.01f, 1f)]
    public float dragSmoothinTime = 0.25f;

    [Header("Yaw Limits (Y axis)")]
    public bool useYawLimits = false;
    public float minYaw = -90f;
    public float maxYaw = 90f;

    [Header("Roll Limits (Z axis)")]
    public bool useRollLimits = false;
    public float minRoll = -45f;
    public float maxRoll = 45f;

    private Camera _camera;
    private bool dragging = false;

    private Vector2 lastMouse;
    private Vector2 angularVelocity;   // x = yaw, y = roll
    private float yawAngle = 0f;
    private float rollAngle = 0f;

    private void Awake() {
        _camera = Camera.main;
    }

    private void Update() {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            if (HitCheck()) {
                dragging = true;
                lastMouse = Mouse.current.position.ReadValue();

                // Only THIS object overrides camera
                _camera.GetComponent<IsometricCameraPanner>()._manualOverride = true;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {

            if (Mathf.Abs(angularVelocity.x) < momentumEndThreshold)
                angularVelocity.x = 0f;

            if (Mathf.Abs(angularVelocity.y) < momentumEndThreshold)
                angularVelocity.y = 0f;

            dragging = false;

            // Only THIS object stops override
            _camera.GetComponent<IsometricCameraPanner>()._manualOverride = false;
        }
    }

    private void LateUpdate() {
        if (dragging)
            ApplyDrag();
        else
            ApplyMomentum();
    }

    private void ApplyDrag() {
        Vector2 delta = Mouse.current.position.ReadValue() - lastMouse;
        lastMouse = Mouse.current.position.ReadValue();

        angularVelocity = Vector2.Lerp(angularVelocity, delta * rotationSpeed, dragSmoothinTime);
        angularVelocity = Vector2.ClampMagnitude(angularVelocity, maxMomentumSpeed);

        Vector2 applied = Vector2.ClampMagnitude(angularVelocity, maxDragSpeed);

        Rotate(applied * Time.deltaTime);
    }

    private void ApplyMomentum() {
        if (angularVelocity.sqrMagnitude < 0.01f) return;

        Rotate(angularVelocity * Time.deltaTime);
        angularVelocity = Vector2.MoveTowards(angularVelocity, Vector2.zero, deceleration * Time.deltaTime);
    }

    private void Rotate(Vector2 delta) {
        float yawDelta = -delta.x;
        float rollDelta = -delta.y;

        // --- YAW LIMITS ---
        if (useYawLimits) {
            float nextYaw = yawAngle + yawDelta;
            if (nextYaw < minYaw || nextYaw > maxYaw)
                yawDelta = 0f;
            else
                yawAngle = nextYaw;
        } else {
            yawAngle += yawDelta;
        }

        // --- ROLL LIMITS ---
        if (useRollLimits) {
            float nextRoll = rollAngle + rollDelta;
            if (nextRoll < minRoll || nextRoll > maxRoll)
                rollDelta = 0f;
            else
                rollAngle = nextRoll;
        } else {
            rollAngle += rollDelta;
        }

        Vector3 pivot = transform.position;

        // USE PIVOT AXES IF PIVOT EXISTS
        Vector3 yawAxis = transform.up;
        Vector3 rollAxis = transform.forward;

        transform.RotateAround(pivot, yawAxis, yawDelta);
        transform.RotateAround(pivot, rollAxis, rollDelta);

        // Lock X rotation
        Vector3 e = transform.eulerAngles;
        e.x = 0f;
        transform.eulerAngles = e;
    }



    public bool HitCheck() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            return hit.collider.GetComponentInParent<ObjRotation>() == this;

        return false;
    }
}

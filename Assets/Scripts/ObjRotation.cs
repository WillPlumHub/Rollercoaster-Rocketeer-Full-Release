using UnityEngine;
using UnityEngine.InputSystem;

public class ObjRotation : MonoBehaviour {
    public bool _isClicked = false;

    [Header("Drag")]
    public float rotationSpeed = 200f;
    public float maxDragSpeed = 360f;

    [Header("Momentum")]
    public float momentumThreshold = 30f;
    public float deceleration = 180f;
    public float maxMomentumSpeed = 720f;
    [Range(0.01f, 1f)]
    private float velocitySmoothing = 0.25f;

    [Header("Rotation Rules")]
    public Vector3 rotationAxis = Vector3.up;
    public Transform rotationPoint;

    [Header("Limits")]
    public bool useLimits = false;
    public float minAngle = -45f;
    public float maxAngle = 45f;

    private Vector2 _lastMousePos;
    private Camera _camera;

    private float currentAngularVelocity = 0f;

    // reference direction for measuring angle
    private Vector3 _referenceDir;

    private void Awake() {
        _camera = Camera.main;
        if (rotationAxis != Vector3.zero) rotationAxis.Normalize();

        // store a reference direction to measure angles against
        if (rotationPoint != null) {
            _referenceDir = (transform.position - rotationPoint.position).normalized;
        } else {
            // choose a stable local axis as reference (forward works well)
            _referenceDir = transform.forward;
        }
    }

    private void Update() {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            _isClicked = HitCheck();

            if (_isClicked) {
                _lastMousePos = Mouse.current.position.ReadValue();
                _camera.GetComponent<IsometricCameraPanner>()._manualOverride = true;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            if (_isClicked) {
                if (GetComponent<FlagSetter>() as FlagSetter != null) {
                    GetComponent<FlagSetter>().TryTriggerFlag();
                }
                if (Mathf.Abs(currentAngularVelocity) < momentumThreshold)
                    currentAngularVelocity = 0f;
            }            

            _isClicked = false;
            _camera.GetComponent<IsometricCameraPanner>()._manualOverride = false;
        }
    }

    private void LateUpdate() {
        if (_isClicked)
            HandleDragRotation();
        else
            HandleMomentumRotation();
    }

    private void HandleDragRotation() {
        Vector2 currentMousePos = Mouse.current.position.ReadValue();
        float deltaX = currentMousePos.x - _lastMousePos.x;

        float angle = deltaX * -rotationSpeed * Time.deltaTime;
        float rawVelocity = angle / Time.deltaTime;

        currentAngularVelocity = Mathf.Lerp(currentAngularVelocity, rawVelocity, velocitySmoothing);
        currentAngularVelocity = Mathf.Clamp(currentAngularVelocity, -maxMomentumSpeed, maxMomentumSpeed);

        float appliedVelocity = Mathf.Clamp(currentAngularVelocity, -maxDragSpeed, maxDragSpeed);

        TryApplyRotation(appliedVelocity * Time.deltaTime);

        _lastMousePos = currentMousePos;
    }

    private void HandleMomentumRotation() {
        if (Mathf.Abs(currentAngularVelocity) <= 0.01f)
            return;

        float angle = currentAngularVelocity * Time.deltaTime;

        if (TryApplyRotation(angle)) {
            float decel = deceleration * Time.deltaTime;
            currentAngularVelocity = Mathf.MoveTowards(currentAngularVelocity, 0f, decel);
        } else {
            currentAngularVelocity = 0f;
        }
    }

    private bool TryApplyRotation(float angle) {
        if (!useLimits) {
            ApplyRotation(angle);
            return true;
        }

        float before = GetCurrentAngle();

        ApplyRotation(angle);

        float after = GetCurrentAngle();

        if (after < minAngle - 0.001f || after > maxAngle + 0.001f) {
            // undo rotation if out of bounds
            ApplyRotation(-angle);
            return false;
        }

        return true;
    }

    private float GetCurrentAngle() {
        Vector3 currentDir;

        if (rotationPoint != null) {
            currentDir = (transform.position - rotationPoint.position).normalized;
        } else {
            currentDir = transform.forward;
        }

        // signed angle around rotationAxis
        return Vector3.SignedAngle(_referenceDir, currentDir, rotationAxis);
    }

    private void ApplyRotation(float angle) {
        if (rotationPoint != null)
            transform.RotateAround(rotationPoint.position, rotationAxis, angle);
        else
            transform.Rotate(rotationAxis, angle, Space.World);
    }

    public bool HitCheck() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
            return hit.collider.GetComponentInParent<ObjRotation>() == this;

        return false;
    }
}

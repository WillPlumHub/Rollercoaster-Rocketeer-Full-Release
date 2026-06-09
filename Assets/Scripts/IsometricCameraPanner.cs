using UnityEngine;
using UnityEngine.InputSystem;

public class IsometricCameraPanner : MonoBehaviour {

    public float minX;
    public float maxX;
    public float minZ;
    public float maxZ;

    public Vector3 _origin;
    public Vector3 _difference;

    public Camera _mainCamera;

    public bool _isDragging;
    [SerializeField]
    public bool _manualOverride;


    private void Awake() {
        _mainCamera = Camera.main;
    }

    public void OnDrag(InputAction.CallbackContext ctx) {
        if (ctx.started) _origin = _mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        _isDragging = ctx.started || ctx.performed;

    }

    private void LateUpdate() {
        //Debug.Log("Manual override = " + _manualOverride);

        if (!_isDragging || _manualOverride) return;

        _difference = GetMousePosition - transform.position;
        transform.position = _origin - _difference;

        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX), transform.position.y, Mathf.Clamp(transform.position.z, minZ, maxZ));
        
        
    }

    private Vector3 GetMousePosition => _mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
}
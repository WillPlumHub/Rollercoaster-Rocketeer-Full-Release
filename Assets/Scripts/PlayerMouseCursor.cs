using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMouseCursor : MonoBehaviour {

    private Vector3 _mouseHitPoint = Vector3.zero;

    private Camera _camera;
        
    void Start() {
        _camera = Camera.main;
    }

    void Update() {
        if (Mouse.current == null) return;
        UpdateMouseHitPoint();
    }

    private void UpdateMouseHitPoint() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) {
            _mouseHitPoint = hit.point;
            ShowDebugSphere(_mouseHitPoint);
            return;
        }
    }

    private GameObject debugSphere;
    // Draws temp debug Mouse Cursor
    //      Param 1: Mouse Position
    private void ShowDebugSphere(Vector3 pos) {
        if (debugSphere == null) {
            debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.transform.localScale = Vector3.one * 0.2f;
            Destroy(debugSphere.GetComponent<Collider>()); // Destroy Collider to Avoid Raycast Interference
        }
        debugSphere.transform.position = pos;
    }
}
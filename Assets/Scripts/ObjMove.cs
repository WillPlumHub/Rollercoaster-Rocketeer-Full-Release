using UnityEngine;
using UnityEngine.InputSystem;

public class ObjMove : MonoBehaviour {

    public static ObjMove activeObj;

    public float gridSize = 1f;

    public float floatHeight = 1.5f;
    public float moveSmoothness = 10f;

    public LayerMask placementLayerMask = ~0;

    public Vector2 xLim;
    public Vector2 yLim;
    public Vector2 zLim;

    public bool verticallyDraggable = false;

    public Texture2D validCursorTexture;
    public Texture2D invalidCursorTexture;

    private Camera _camera;
    private bool _isHeld = false;
    private bool _canPlace = false;
    private float _halfHeight;

    private Vector3 _mouseHitPoint = Vector3.zero;
    private Vector3 _mouseHitNormal = Vector3.up;
    private bool _mouseOverValidSurface = false;

    private int _defaultLayer;
    private int _heldLayer;

    private void Awake() {
        _camera = Camera.main;
        _halfHeight = transform.localScale.y / 2f;

        _defaultLayer = LayerMask.NameToLayer("UnHeldObject");
        _heldLayer = LayerMask.NameToLayer("HeldObject");
    }

    private void Update() {
        if (Mouse.current == null) return;

        UpdateMouseHitPoint();

        if (Mouse.current.leftButton.wasPressedThisFrame) {
            if (_isHeld && _canPlace) PlaceObject();
            else if (activeObj == null) TryPickUpObject();
        }

        if (_isHeld) MoveToMouse();

        posLims();

        // --- CURSOR FEEDBACK ---
        if (_mouseOverValidSurface)
            Cursor.SetCursor(validCursorTexture, Vector2.zero, CursorMode.Auto);
        else
            Cursor.SetCursor(invalidCursorTexture, Vector2.zero, CursorMode.Auto);
    }

    private void posLims() {
        Vector3 pos = transform.position;

        if (xLim != Vector2.zero)
            pos.x = Mathf.Clamp(pos.x, xLim.x, xLim.y);

        if (yLim != Vector2.zero)
            pos.y = Mathf.Clamp(pos.y, yLim.x, yLim.y);

        if (zLim != Vector2.zero)
            pos.z = Mathf.Clamp(pos.z, zLim.x, zLim.y);

        transform.position = pos;
    }

    private void TryPickUpObject() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayerMask & ~(1 << _heldLayer))) {
            if (hit.collider.GetComponentInParent<ObjMove>() == this) {
                activeObj = this;
                _isHeld = true;
                _canPlace = true;

                SetLayerRecursive(gameObject, _heldLayer);
            }
        }
    }

    private void PlaceObject() {
        _isHeld = false;
        _canPlace = true;
        activeObj = null;

        SetLayerRecursive(gameObject, _defaultLayer);

        Vector3 pos = transform.position;

        if (gridSize > 0) {
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        }
        pos.y -= floatHeight;
        transform.position = pos;
    }

    private void MoveToMouse() {
        Vector3 mousePos = _mouseHitPoint;

        // Safety check
        if (float.IsInfinity(mousePos.x) ||
            float.IsInfinity(mousePos.y) ||
            float.IsInfinity(mousePos.z))
            return;

        // Snap to grid first
        Vector3 target = (gridSize > 0) ? SnapToGrid(mousePos) : mousePos;

        bool mouseOnGround = _mouseHitNormal.y > 0.7f;

        if (!verticallyDraggable) {
            _canPlace = mouseOnGround;

            if (mouseOnGround) {
                target.y = mousePos.y + _halfHeight + floatHeight;
            } else {
                // Keep current height when over void
                target.y = transform.position.y;
            }
        } else {
            _canPlace = true;
            target.y = mousePos.y + _halfHeight + floatHeight;
            target.y = Mathf.Round(target.y / gridSize) * gridSize;
        }

        // ⭐ CRITICAL FIX: Project BEFORE moving
        target = ProjectToBounds(target);

        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * moveSmoothness);
    }

    private Vector3 SnapToGrid(Vector3 pos) {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        return pos;
    }

    private void SetLayerRecursive(GameObject obj, int layer) {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    // --- EDGE PROJECTION FUNCTION ---
    private Vector3 ProjectToBounds(Vector3 pos) {
        Vector3 p = pos;

        if (xLim != Vector2.zero)
            p.x = Mathf.Clamp(p.x, xLim.x, xLim.y);

        if (yLim != Vector2.zero)
            p.y = Mathf.Clamp(p.y, yLim.x, yLim.y);

        if (zLim != Vector2.zero)
            p.z = Mathf.Clamp(p.z, zLim.x, zLim.y);

        return p;
    }

    private void UpdateMouseHitPoint() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
        int mask = placementLayerMask & ~(1 << _heldLayer);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask)) {
            _mouseHitPoint = hit.point;
            _mouseHitNormal = hit.normal;
            _mouseOverValidSurface = true;
            return;
        }

        // --- FALLBACK: infinite ground plane ---
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float dist)) {
            _mouseHitPoint = ray.GetPoint(dist);
            _mouseHitNormal = Vector3.up;
            _mouseOverValidSurface = false;
            return;
        }

        _mouseHitPoint = transform.position;
        _mouseHitNormal = Vector3.up;
        _mouseOverValidSurface = false;
    }

    private void OnDrawGizmos() {
        if (_mouseHitPoint != Vector3.zero) {
            Vector3 mousePos = _mouseHitPoint;

            if (float.IsInfinity(mousePos.x) ||
                float.IsInfinity(mousePos.y) ||
                float.IsInfinity(mousePos.z))
                Gizmos.color = Color.red;
            else
                Gizmos.color = Color.cyan;

            Gizmos.DrawSphere(_mouseHitPoint, 0.25f);
        }

        if (!_isHeld) return;

        Gizmos.color = Color.yellow;
        Vector3 origin = transform.position;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 50f, placementLayerMask)) {
            Gizmos.DrawLine(origin, hit.point);
            Gizmos.DrawSphere(hit.point, 0.2f);
        }

        Gizmos.DrawSphere(origin, 0.15f);
    }

    private void OnDrawGizmosSelected() {
        if (xLim == Vector2.zero && yLim == Vector2.zero && zLim == Vector2.zero) return;

        Gizmos.color = Color.green;

        float cx = (xLim.x + xLim.y) * 0.5f;
        float cy = (yLim.x + yLim.y) * 0.5f;
        float cz = (zLim.x + zLim.y) * 0.5f;

        float sx = Mathf.Abs(xLim.y - xLim.x);
        float sy = Mathf.Abs(yLim.y - yLim.x);
        float sz = Mathf.Abs(zLim.y - zLim.x);

        Vector3 center = new Vector3(cx, cy, cz);
        Vector3 size = new Vector3(sx, sy, sz);

        Gizmos.DrawWireCube(center, size);
    }
}

// Held object height is triggering

using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class ObjMove : MonoBehaviour {

    public static ObjMove activeObj;

    [Header("Basic Object Settings")]
    public float gridSize = 1f;
    public float floatHeight = 1.5f;
    public bool verticallyDraggable = false;
    public float maxRaiseHeight = 100f;
    public LayerMask layerMask = 6;

    [Header("Position Limits (Min, Max)")]
    public Vector2 xLim, yLim, zLim;

    [Header("Render on Top Settings")]
    public Material renderOnTopMaterial;
    public Material defaultMaterial;
    public RenderQueue queue = RenderQueue.Background; // Base queue
    [Range(-20, 20)] public int queueOffset = 0; // Small offset to control order of objects on the same queue
    private Material[][] originalMaterials; // per renderer

    [Header("Private Variables")]
    private float clickThreshold = 10f;
    private float moveSmoothness = 10f;
    private Vector2 _mouseDownPos;
    private bool _mouseDragging = false;
        
    private bool _isObjectHeld = false;
    private bool _canPlaceObject = false;
    private float _halfHeldObjectHeight;

    private Vector3 _mouseHitPoint = Vector3.zero;
    private Vector3 _mouseHitNormal = Vector3.up;

    private Camera _camera;
    private LineRenderer lr; 
    private Renderer[] renderers;

    private void Awake() {
        _camera = Camera.main;
        _halfHeldObjectHeight = transform.localScale.y / 2f;
        layerMask = ~layerMask;

        renderers = GetComponentsInChildren<Renderer>();

        // Cache original materials for each renderer
        originalMaterials = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) {
            var mats = renderers[i].materials;
            originalMaterials[i] = new Material[mats.Length];
            for (int j = 0; j < mats.Length; j++) {
                originalMaterials[i][j] = mats[j];
            }
        }

        lr = gameObject.AddComponent<LineRenderer>();
        if (gridSize < 0) gridSize = 0;
    }


    private void Update() {
        if (Mouse.current == null) return;

        if (activeObj == this) {
            Vector3 start = transform.position;
            Vector3 end = start + Vector3.down * 10f; // ray length
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        //Debug.Log("FloatHeight: " + floatHeight + "");

        RenderOnTop();
        UpdateMouseHitPoint();
        ClickControl();
    }

    //
    void ClickControl() {
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            _mouseDownPos = Mouse.current.position.ReadValue();
            _mouseDragging = false;
            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) {
                if (hit.collider.GetComponentInParent<ObjMove>() == this) PlayerClickMove.manualOverride = true;
            }
        }

        if (Mouse.current.leftButton.isPressed) {
            float dist = Vector2.Distance(Mouse.current.position.ReadValue(), _mouseDownPos);
            if (dist > clickThreshold) _mouseDragging = true;
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            if (_mouseDragging) return;

            if (_isObjectHeld && _canPlaceObject) PlaceObject();
            else if (activeObj == null) TryPickUpObject();
        }

        if (_isObjectHeld) MoveToMouse();
        transform.position = ClampToBounds(transform.position);
    }

    // Clamp Held Object to it's Position Bounds
    private Vector3 ClampToBounds(Vector3 pos) {
        if (xLim != Vector2.zero) {
            pos.x = Mathf.Clamp(pos.x, xLim.x, xLim.y);
        }
        if (yLim != Vector2.zero) {
            pos.y = Mathf.Clamp(pos.y, yLim.x, yLim.y);
        }
        if (zLim != Vector2.zero) {
            pos.z = Mathf.Clamp(pos.z, zLim.x, zLim.y);
        }
        return pos;
    }

    // 
    private void TryPickUpObject() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue()); // REUSE FROM OTHER INSTANCES???

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask)) {
            if (hit.collider.GetComponentInParent<ObjMove>() == this) { // Check Parent to account for clicking Child of intended object
                activeObj = this;
                PlayerClickMove.manualOverride = true;
                _isObjectHeld = true;
                _canPlaceObject = true;
                DropLine();

                gameObject.layer = 6;
                // Change collision layer
            }
        }
    }

    // 
    private void PlaceObject() {
        _isObjectHeld = false; _canPlaceObject = true; activeObj = null;

        StartCoroutine(releaseOverrideNextFrame());

        if (gridSize > 0) transform.position = ClampToBounds(transform.position);

        Vector3 pos = transform.position;

        /*if (!verticallyDraggable) { // Lower object back down held amount when placed
            pos.y -= floatHeight;
        }*/
        
        transform.position = pos;
        gameObject.layer = 7;

        // Manage Object's Click to Activate Flag(s)
        if (GetComponent<FlagSetter>() as FlagSetter != null) {
            GetComponent<FlagSetter>().TryTriggerFlag();
        }
    }

    // Account for Click to Move Player. Wait a frame to reactivate Player's movement so they can still click to place an object
    private IEnumerator releaseOverrideNextFrame() {
        yield return null;
        PlayerClickMove.manualOverride = false;
    }

    // Updates Held Object's position to the Mouse's Position
    private void MoveToMouse() {
        Vector3 mousePos = _mouseHitPoint;
        if (float.IsInfinity(mousePos.x) || float.IsInfinity(mousePos.y) || float.IsInfinity(mousePos.z)) return;

        Vector3 target = (gridSize > 0) ? SnapToGrid(mousePos) : mousePos; // Check for if gridSize <= 0. If it is, just stay at Mouse Position

        bool mouseInsideXZ = // Check if Mouse's Position is still inside of Held Object's Bounds
            (xLim == Vector2.zero || (mousePos.x >= xLim.x && mousePos.x <= xLim.y)) &&
            (zLim == Vector2.zero || (mousePos.z >= zLim.x && mousePos.z <= zLim.y));


        if (Keyboard.current.wKey.wasPressedThisFrame && (floatHeight + 2f) < maxRaiseHeight) {
            floatHeight += 2f;
            Debug.Log("Added 2f height " + floatHeight);
        }
        if (Keyboard.current.sKey.wasPressedThisFrame && (floatHeight - 2f) > 0) {
            floatHeight -= 2f;
            Debug.Log("Reduced by 2f height " + floatHeight);
        }

        if (!verticallyDraggable) {
            if (mouseInsideXZ) {
                _canPlaceObject = _mouseHitNormal.y > 0.7f;
                target.y = Mathf.Round((mousePos.y + _halfHeldObjectHeight + floatHeight) / gridSize) * gridSize;
            } else {
                _canPlaceObject = true;
                target.y = transform.position.y;
            }
        } else {
            _canPlaceObject = true;
            if (mouseInsideXZ) {
                target.y = Mathf.Round((mousePos.y + _halfHeldObjectHeight + floatHeight) / gridSize) * gridSize;
            } else {
                target.y = transform.position.y;
            }
        }

        target = ClampToBounds(target);
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * moveSmoothness);
    }

    // Round Held Object's X & Z positions to match grid size
    private Vector3 SnapToGrid(Vector3 pos) {
        pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
        pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        return pos;
    }

    // 
    private void UpdateMouseHitPoint() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, layerMask)) {
            _mouseHitPoint = hit.point;
            _mouseHitNormal = hit.normal;
            return;
        }

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float dist)) {
            _mouseHitPoint = ray.GetPoint(dist);
            _mouseHitNormal = Vector3.up;
            return;
        }

        _mouseHitPoint = transform.position;
        _mouseHitNormal = Vector3.up;
    }

    //
    void RenderOnTop() {
        if (renderers == null || originalMaterials == null) return;

        if (_isObjectHeld && !_canPlaceObject) {
            // Use render-on-top material
            for (int i = 0; i < renderers.Length; i++) {
                var r = renderers[i];

                // Build an array of the same length as original, but all using renderOnTopMaterial
                Material[] mats = r.materials;
                for (int j = 0; j < mats.Length; j++) {
                    mats[j] = renderOnTopMaterial;
                    mats[j].renderQueue = (int)queue + queueOffset;
                }
                r.materials = mats;
            }
        } else if (_isObjectHeld && _canPlaceObject) {
            // Restore original materials
            for (int i = 0; i < renderers.Length; i++) {
                var r = renderers[i];
                Material[] mats = r.materials;

                // Ensure length matches; if not, recreate from cached originals
                if (mats.Length != originalMaterials[i].Length) {
                    mats = new Material[originalMaterials[i].Length];
                }

                for (int j = 0; j < originalMaterials[i].Length; j++) {
                    mats[j] = originalMaterials[i][j];
                    mats[j].renderQueue = (int)queue + queueOffset;
                }

                r.materials = mats;
            }
        }
    }

    // Render Line under object to ground to help judge its position
    void DropLine() {
        lr.startWidth = 0.03f;
        lr.endWidth = 0.03f;

        lr.positionCount = 2;

        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = Color.yellow;

        lr.useWorldSpace = true;
    }
}
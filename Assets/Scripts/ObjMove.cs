using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Unity.Mathematics; 

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

    public float clickThreshold = 10f;

    private Vector2 _mouseDownPos;
    private bool _mouseDragging = false;

    private Camera _camera;
    private bool _isHeld = false;
    private bool _canPlace = false;
    private float _halfHeight;

    private Vector3 _mouseHitPoint = Vector3.zero;
    private Vector3 _mouseHitNormal = Vector3.up;

    private int _defaultLayer;
    private int _heldLayer;

    public Material silhouetteMaterial;

    private Renderer[] renderers;
    private Material[] originalMaterials;
    private int[] originalQueues;

    private int overlapCount = 0;   // trigger‑based overlap count


    private void Awake() {
        _camera = Camera.main;
        _halfHeight = transform.localScale.y / 2f;

        _defaultLayer = LayerMask.NameToLayer("UnHeldObject");
        _heldLayer = LayerMask.NameToLayer("HeldObject");

        if (gridSize < 0) gridSize = 0;

        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        originalQueues = new int[renderers.Length];

        for (int i = 0; i < renderers.Length; i++) {
            originalMaterials[i] = renderers[i].material;
            originalQueues[i] = renderers[i].material.renderQueue;
        }
    }


    private void Update() {
        if (Mouse.current == null) return;

        UpdateMouseHitPoint();

        // click vs drag
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            _mouseDownPos = Mouse.current.position.ReadValue();
            _mouseDragging = false;
        
            Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, placementLayerMask)) {
                if (hit.collider.GetComponentInParent<ObjMove>() == this) {
                    PlayerClickMove.manualOverride = true;
                }
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            if (!_mouseDragging) {
                if (_isHeld && _canPlace) PlaceObject();
                else if (activeObj == null) TryPickUpObject();
            }
        }

        if (_isHeld) MoveToMouse();

        // --- silhouette + render-on-top condition (trigger-based) ---
        bool shouldHighlight = _isHeld && !verticallyDraggable && overlapCount > 0;

        if (shouldHighlight) {
            ApplySilhouetteAndOnTop();
        } else {
            RestoreMaterialsAndQueue();
        }

        posLims();
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
                PlayerClickMove.manualOverride = true;
                _isHeld = true;
                _canPlace = true;

                SetLayerRecursive(gameObject, _heldLayer);
                if (gameObject.transform.childCount == 1) {
                    Destroy(gameObject.transform.GetChild(0).gameObject);
                }
            }
        }
    }


    private void PlaceObject() {
        _isHeld = false;
        _canPlace = true;
        activeObj = null;

        StartCoroutine(releaseOverrideNextFrame());
        SetLayerRecursive(gameObject, _defaultLayer);

        Vector3 pos = transform.position;

        if (gridSize > 0) {
            pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
            pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
        }
        if (!verticallyDraggable)
        {
            pos.y -= floatHeight;
        }
        else
        {
            GameObject SplineSupport = new GameObject("SplineSupport");
            SplineSupport.transform.parent = this.gameObject.transform;
            SplineContainer RollerSupport = SplineSupport.AddComponent<SplineContainer>();
            RollerSupport.Spline = new Spline();
            RollerSupport.Spline.AddRange(new float3[] { new(pos.x, pos.y, pos.z), new(pos.x, pos.y-floatHeight - 1.4f, pos.z) });
            SplineExtrude SupportExtrude = SplineSupport.AddComponent<SplineExtrude>();
            SupportExtrude.Container = RollerSupport;
            bool SupportMesh = SupportExtrude.TryGetComponent<MeshFilter>(out var meshFilter);
            if (SupportMesh)
            {
                if(meshFilter.sharedMesh == null)
                {
                    var extrudeMesh = new Mesh();
                    extrudeMesh.name = "support mesh";
                    meshFilter.sharedMesh = extrudeMesh;
                }
                SupportExtrude.Radius = 0.25f;
                SupportExtrude.SegmentsPerUnit = 20;
                SupportExtrude.RebuildOnSplineChange = true;
                SupportExtrude.Sides = 8;
                SupportExtrude.Range = new float2(0, 100);

                bool hasMeshRenderer = SupportExtrude.TryGetComponent<MeshRenderer>(out var meshRenderer);
                if (hasMeshRenderer)
                {
                    meshRenderer.material = new Material(Shader.Find("Standard"));
                }
                SupportExtrude.Rebuild();
            }
        }
            transform.position = pos;

        if (GetComponent<FlagSetter>() as FlagSetter != null) {
            GetComponent<FlagSetter>().TryTriggerFlag();
        }

        RestoreMaterialsAndQueue();
    }

    private IEnumerator releaseOverrideNextFrame() {
        yield return null;
        PlayerClickMove.manualOverride = false;
    }

    private void MoveToMouse() {
        Vector3 mousePos = _mouseHitPoint;

        if (float.IsInfinity(mousePos.x) || float.IsInfinity(mousePos.y) || float.IsInfinity(mousePos.z)) return;

        Vector3 target = (gridSize > 0) ? SnapToGrid(mousePos) : mousePos;

        bool mouseOnGround = _mouseHitNormal.y > 0.7f;

        if (!verticallyDraggable) {
            _canPlace = mouseOnGround;

            if (mouseOnGround)
                target.y = mousePos.y + _halfHeight + floatHeight;
            else
                target.y = transform.position.y;
        } else {
            _canPlace = true;
            if (Keyboard.current.wKey.wasPressedThisFrame)
            {
                floatHeight += 2f;
            }
            else if (Keyboard.current.sKey.wasPressedThisFrame)
            {
                floatHeight -= 2f;
            }
            target.y = Mathf.Round((mousePos.y + _halfHeight + floatHeight) / gridSize) * gridSize;
        }

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


    private void ApplySilhouetteAndOnTop() {
        for (int i = 0; i < renderers.Length; i++) {
            renderers[i].material = silhouetteMaterial;
            renderers[i].material.renderQueue = 4000;
        }
    }

    private void RestoreMaterialsAndQueue() {
        for (int i = 0; i < renderers.Length; i++) {
            renderers[i].material = originalMaterials[i];
            renderers[i].material.renderQueue = originalQueues[i];
        }
    }


    private void OnTriggerEnter(Collider other) {
        if (!_isHeld) return;

        if (other.GetComponentInParent<ObjMove>() == this) return;

        overlapCount++;
    }

    private void OnTriggerExit(Collider other) {
        if (!_isHeld) return;

        if (other.GetComponentInParent<ObjMove>() == this) return;

        overlapCount--;
    }
}

using UnityEngine;

public class ObjRotation : MonoBehaviour {

    private Camera _camera;
    private Vector3 _mouseHitPoint = Vector3.zero;
    private Vector3 _mouseHitNormal = Vector3.up;

    private int _defaultLayer;
    private int _heldLayer;

    private void Awake() {
        _camera = Camera.main;

        _defaultLayer = LayerMask.NameToLayer("UnHeldObject");
        _heldLayer = LayerMask.NameToLayer("HeldObject");
    }

    void Start() {
        
    }

    void Update() {
        
    }
}
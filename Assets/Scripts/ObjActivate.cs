using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class ObjActivate : MonoBehaviour {

    public GameObject targetObject;

    public bool _isActivated = false;
    public float fallBackTimer = 10f;

    public bool _isExtendable = false;
    public bool _isRepeatable = false;

    private IActivatable _activatable;
    private Camera _camera;
    private Coroutine fallbackRoutine;

    private void Awake() {
        _camera = Camera.main;

        if (targetObject == null) {
            targetObject = gameObject;
        }

        _activatable = targetObject.GetComponent<IActivatable>();

        if (_activatable == null) {
            Debug.LogError($"{name} has ObjActivate but no IActivatable on target!");
        }
    }

    private void Update() {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame) {

            if (!_isActivated) {
                if (CheckHit()) {
                    Activate();
                }
            } else if (_isRepeatable) {
                if (CheckHit()) {
                    Activate();
                }
            } else if (_isExtendable) {
                if (CheckHit()) {
                    RestartFallbackTimer();
                }
            }
        }
    }

    private void Activate() {
        _isActivated = true;
        _activatable.Activate();
        RestartFallbackTimer();
    }

    private void RestartFallbackTimer() {
        if (fallbackRoutine != null) {
            StopCoroutine(fallbackRoutine);
        }

        fallbackRoutine = StartCoroutine(FallbackDeactivate());
    }

    private IEnumerator FallbackDeactivate() {
        yield return new WaitForSeconds(fallBackTimer);
        _isActivated = false;
        _activatable.Deactivate();
    }

    public bool CheckHit() {
        Ray ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) {
            return hit.collider.GetComponentInParent<ObjActivate>() == this;
        }

        return false;
    }
}
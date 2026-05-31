using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
public class PlayerClickMove : MonoBehaviour {
    private NavMeshAgent _agent;
    private RaycastHit _hitInfo;

    [Header("Settings")]
    public float dragThreshold = 10f;

    private Vector2 _mouseDownPos;
    private bool _isMouseDown;

    void Start() {
        _agent = GetComponent<NavMeshAgent>();
    }

    void Update() {
        if (Mouse.current.leftButton.wasPressedThisFrame) {
            _isMouseDown = true;
            _mouseDownPos = Mouse.current.position.ReadValue();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame) {
            if (!_isMouseDown) return;
            _isMouseDown = false;

            Vector2 releasePos = Mouse.current.position.ReadValue();
            float distance = Vector2.Distance(_mouseDownPos, releasePos);

            if (distance > dragThreshold)
                return;

            TryMoveToCursor();
        }

        // Stop agent when reaching destination
        if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance) {
            if (!_agent.hasPath || _agent.velocity.sqrMagnitude == 0f)
                _agent.isStopped = true;
        }
    }

    private void TryMoveToCursor() {
        var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out _hitInfo)) {
            _agent.isStopped = false;
            _agent.destination = _hitInfo.point;
        }
    }
}

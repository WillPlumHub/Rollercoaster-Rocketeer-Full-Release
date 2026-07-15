using UnityEngine;
using UnityEngine.Splines;

public class FollowKart : MonoBehaviour {

    [Header("Stupid Garbage")]
    private Vector3 carriedForward = Vector3.forward; // Tangent direction used last frame, for parallel transport
    private bool carriedUpInitialized = false;

    [Header("Physics Detach")]
    [Tooltip("Once this kart leaves the rail, its Rigidbody is jointed to the leader with this much breaking force. Leave at Infinity so the train never snaps apart.")]
    public float jointBreakForce = Mathf.Infinity;

    private bool hasLeftRail = false;
    private float virtualOdometer;           // This follower's own progress once it's gliding on its own past the leader's rail-end point
    private bool virtualOdometerInitialized = false;
    private Rigidbody rb;
    private FixedJoint joint;

    public void Follow(CoasterMove leader, float offset) {
        if (hasLeftRail) return; // Physics + the FixedJoint are driving this kart so quit prematurely

        float targetOdometer;

        if (leader.OnRail) { // Track the leader's position on the rail, offset behind it.
            targetOdometer = leader.Odometer - offset;
            virtualOdometerInitialized = false; // Keep this synced to avoid jump if the leader leaves the rail later
        } else { // Leader detached. Move this kart forward at the leader's last speed, so it smoothly continues toward the same point the leader left from
            if (!virtualOdometerInitialized) {
                virtualOdometer = leader.RailEndOdometer - offset; // Exactly where targetOdometer was the instant before the leader left, to avoid snap
                virtualOdometerInitialized = true;
            }
            virtualOdometer += leader.speed * Time.deltaTime;
            targetOdometer = virtualOdometer;
        }

        // Follower has reached the point where the rail ended
        if (targetOdometer >= leader.RailEndOdometer) {
            OnSplineEnd();
            return;
        }

        // Walks Main Kart's recorded segment history instead of its currSpline
        leader.TryGetPointAtOdometer(targetOdometer, out Vector3 pos, out Vector3 tangent);
        transform.position = pos;

        if (tangent.sqrMagnitude > 1e-6f) {    // Check if spline tangent is too short to be a real direction. 1e-6f = 0.001, but avoids sqroot
            Vector3 newForward = tangent.normalized;
            // Shared parallel-transport rotation logic, to stay consistent with leader's rptations
            transform.rotation = CoasterMove.ComputeTransportedRotation(newForward, ref carriedForward, ref carriedUpInitialized);
        }
    }

    public void OnSplineEnd() {
        if (rb != null) {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward;
        }
    }
}
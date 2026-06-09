using UnityEngine;
using UnityEngine.Splines;

public class CoasterMove : MonoBehaviour {

    public SplineAnimate animator;

    public SplineContainer currentSpline = null;
    private Rigidbody rb;

    void Start() {
        if (animator == null) {
            animator = GetComponent<SplineAnimate>();
        }

        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;
    }

    private void Update() {
        if (IsFinished(animator) && animator.isActiveAndEnabled) {
            //Debug.Log("Finished current spline");
            OnSplineEnd(animator);
        }
    }

    bool IsFinished(SplineAnimate anim) {
        return anim.NormalizedTime >= 1f;
    }

    private void OnTriggerEnter(Collider other) {
        //Debug.Log("Collided with Trigger");
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null) {
            //Debug.Log("Trigger was spline entrance");
            currentSpline = placer.container;
        }
    }

    private void OnTriggerExit(Collider other) {
        //Debug.Log("Left Trigger");
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null && placer.container == currentSpline) {
            //Debug.Log("Left spline Trigger");
            currentSpline = null;
        }
    }

    private void OnSplineEnd(SplineAnimate anim) {
        if (currentSpline != null && currentSpline != animator.Container) {
            //Debug.Log("Reached end. Switching to next spline");

            animator.Container = currentSpline;
            animator.NormalizedTime = 0f;
            animator.Play();
            return;
        }

        //Debug.Log("Spline animation finished. Detaching from splines now");

        animator.enabled = false;

        Vector3 vel = -transform.forward * anim.MaxSpeed;

        if (rb != null) {
            rb.isKinematic = false;
            rb.linearVelocity = vel;
        }
    }
}

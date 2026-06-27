using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class CoasterMove : MonoBehaviour {

    [Header("Spline")]
    public float angle; 
    public SplineAnimate animator;
    public SplineContainer currentSpline;

    public float acceleration;
    public float decceleration;
    
    public float defaultAcceleration;
    public float defaultDecceleration;

    private Rigidbody rb;

    private void Awake() {
        defaultAcceleration = acceleration;
        defaultDecceleration = decceleration;
    }

    void Start() {
        if (animator == null) animator = GetComponent<SplineAnimate>();        

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        animator.AnimationMethod = SplineAnimate.Method.Speed;
    }

    private void Update() {

        //ApplySlopeSpeed();
                
        //Vector3 targetDir = target.position - transform.position;
        angle = Vector3.Angle(Vector3.up, transform.forward);
        if (angle < 80f) {
            //animator.MaxSpeed -= decceleration * Time.deltaTime * Vector3.Dot(Vector3.down, transform.forward);
            animator.MaxSpeed -= decceleration * Time.deltaTime;
            //Debug.Log("Angle Up: " + angle + ", MaxSpeed: " + animator.MaxSpeed);
        } else if (angle >= 120f && angle < 180f) {
            //Debug.Log("Angle Down: " + angle + ", MaxSpeed: " + animator.MaxSpeed);
            animator.MaxSpeed += acceleration * Time.deltaTime;
        }

        if (animator.MaxSpeed < 2) {
            animator.MaxSpeed = 2;
        }
        if (animator.MaxSpeed > 5) {
            animator.MaxSpeed = 5;
        }

        if (animator.NormalizedTime >= 1f && animator.isActiveAndEnabled) {
            //Debug.Log("Finished current spline");
            OnSplineEnd();
        }
    }

    private void OnTriggerEnter(Collider other) {

        //Debug.Log("Collided with Trigger");
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null) {
            if (placer.action != null && placer.action.GetType().Name != "TSpeedKart") {
                acceleration = defaultAcceleration;
            }
            if (placer.action != null && placer.action.GetType().Name != "TSlowKart") {
                decceleration = defaultDecceleration;
            }
            //Debug.Log("Trigger " + placer.gameObject.name + " was spline entrance: " + placer.container);
            currentSpline = placer.container;
            if (rb != null) {
                if (rb.isKinematic == false && animator.enabled == false) {
                    rb.isKinematic = true;
                    animator.enabled = true;
                    placer.curKart = gameObject;
                    placer.TryTriggerMod();
                }
            }
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

    private void OnSplineEnd() {
        if (currentSpline != null && currentSpline != animator.Container) {
            //Debug.Log("Reached end. Switching to next spline");
            animator.Container = currentSpline;
            animator.NormalizedTime = 0f;
            animator.Play();
            return;
        }

        //Debug.Log("Spline animation finished. Detaching from splines now");
        animator.enabled = false;
        if (acceleration != defaultAcceleration) acceleration = defaultAcceleration;
        if (decceleration != defaultDecceleration) decceleration = defaultDecceleration;
        //transform.Rotate(new Vector3(0,180,0));
        Vector3 vel = transform.forward * animator.MaxSpeed;

        if (rb != null) {
            rb.isKinematic = false;
            rb.linearVelocity = vel;
        }
    }
}

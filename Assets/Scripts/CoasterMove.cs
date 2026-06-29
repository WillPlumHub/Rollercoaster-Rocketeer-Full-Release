using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Splines;

public class CoasterMove : MonoBehaviour {

    [Header("Spline")]
    public SplineContainer currentSpline;

    [Header("Motion")]
    public float speed = 0f;
    public float gravityScale = 1f;
    public float friction = 0.2f;
    public float minSpeed = 0f;
    public float maxSpeed = 20f;

    [Header("State")]
    public float distance = 0f;

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb != null) rb.isKinematic = true;
    }

    void Update()
    {
        if (currentSpline == null) return;

        var spline = currentSpline.Spline;

        float splineLength = spline.GetLength();

        // Convert distance -> normalized t
        float t = distance / splineLength;

        // Clamp or loop safety
        t = Mathf.Clamp01(t);

        // Evaluate spline direction (tangent)
        Vector3 position = spline.EvaluatePosition(t);
        Vector3 tangent = spline.EvaluateTangent(t);

        // Gravity projected onto track
        float gravityAccel = Vector3.Dot(Physics.gravity * gravityScale, tangent);

        // Integrate velocity
        speed += gravityAccel * Time.deltaTime;

        // Friction (simple damping)
        speed = (1f - friction * Time.deltaTime);

        // Clamp speed
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);

        // Advance along spline
        distance += speed * Time.deltaTime;

        // Convert back to t for placement
        float newT = distance / splineLength;
        //Debug.Log("newT = " + newT);
        if (newT >= 1f)
        {
            OnSplineEnd();
            return;
        }

        // Apply position
        transform.position = spline.EvaluatePosition(newT);
        transform.position += currentSpline.gameObject.transform.position;
        Debug.Log("moving to " + spline.EvaluatePosition(newT));

        // rotation alignment
        Vector3 forward = spline.EvaluateTangent(newT);
        transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    private void OnTriggerEnter(Collider other)
    {
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null)
        {
            currentSpline = placer.container;
            distance = 0f;
            speed = 0f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null && placer.container == currentSpline)
        {
            currentSpline = null;
        }
    }

    private void OnSplineEnd()
    {
        if (currentSpline == null) return;

        rb.isKinematic = false;
        rb.linearVelocity = transform.forward * speed;

        currentSpline = null;
    }
}
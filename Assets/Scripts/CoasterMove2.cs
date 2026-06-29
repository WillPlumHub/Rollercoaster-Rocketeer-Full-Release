using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using Interpolators = UnityEngine.Splines.Interpolators;

public class CoasterMove2 : MonoBehaviour {
    [Header("Spline")]
    public SplineContainer currentSpline;     // spline we are currently on
    private Spline spline;                    // cached spline reference
    private float splineLength;               // cached spline length

    [Header("Movement")]
    public float currentOffset = 0f;          // normalized 0–1 position along spline
    public float currentSpeed = 3f;           // current speed (m/s)
    public float minSpeed = 2f;
    public float maxSpeed = 5f;

    [Header("Slope-Based Speed Control")]
    public float angle;
    public float acceleration = 1f;
    public float deceleration = 1f;
    private float defaultAcceleration;
    private float defaultDeceleration;

    [Header("Advanced Spline Data")]
    public SplineData<float> speedData = new SplineData<float>();
    public SplineData<float3> tiltData = new SplineData<float3>();
    public SplineData<float> driftData = new SplineData<float>();

    private Rigidbody rb;

    private void Awake() {
        defaultAcceleration = acceleration;
        defaultDeceleration = deceleration;
    }

    private void Start() {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        if (currentSpline == null) {
            Debug.LogError("CoasterMove: No starting spline assigned.");
            enabled = false;
            return;
        }

        InitializeSpline(currentSpline);
    }

    private void InitializeSpline(SplineContainer container) {
        currentSpline = container;
        spline = container.Spline;
        splineLength = spline.GetLength();

        currentOffset = 0f;   // <-- REQUIRED
        currentSpeed = minSpeed; // optional but recommended
    }


    private void Update() {
        if (currentOffset == 0f && Time.frameCount < 3)
            return;

        if (currentSpline == null || spline == null)
            return;
        currentOffset = Mathf.Clamp01(currentOffset);


        ApplySlopeSpeed();
        ApplySplineDataSpeed();
        MoveAlongSpline();
        ApplyRotationAndTilt();
        ApplyDriftOffset();
        CheckSplineEnd();
    }

    // ---------------------------------------------------------
    // 1. SLOPE-BASED SPEED CONTROL
    // ---------------------------------------------------------
    private void ApplySlopeSpeed() {
        angle = Vector3.Angle(Vector3.up, transform.forward);

        if (angle < 80f)
            currentSpeed -= deceleration * Time.deltaTime;
        else if (angle >= 120f)
            currentSpeed += acceleration * Time.deltaTime;

        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
    }

    // ---------------------------------------------------------
    // 2. SPEED FROM SPLINEDATA
    // ---------------------------------------------------------
    private void ApplySplineDataSpeed() {
        if (speedData.Count == 0)
            return;

        float splineSpeed = speedData.Evaluate(
            spline, currentOffset, PathIndexUnit.Normalized, new Interpolators.LerpFloat()
        );

        currentSpeed = splineSpeed;
    }

    // ---------------------------------------------------------
    // 3. MOVE ALONG SPLINE
    // ---------------------------------------------------------
    private void MoveAlongSpline() {
        currentOffset += (currentSpeed * Time.deltaTime) / splineLength;

        if (currentOffset > 1f)
            currentOffset = 1f; // stop at end (we handle switching separately)
    }

    // ---------------------------------------------------------
    // 4. ROTATION + TILT
    // ---------------------------------------------------------
    private void ApplyRotationAndTilt() {
        if (currentOffset <= 0.0001f && Time.frameCount < 3)
            return;

        if (currentSpline == null || spline == null)
            return;

        float3 tangent = SplineUtility.EvaluateTangent(spline, currentOffset);
        float3 upSpline = SplineUtility.EvaluateUpVector(spline, currentOffset);

        // Validate vectors
        if (math.lengthsq(tangent) < 0.0001f || math.lengthsq(upSpline) < 0.0001f)
            return;

        Quaternion baseRot = Quaternion.LookRotation(tangent, upSpline);

        // -----------------------------
        // SAFE CURVATURE-BASED BANKING
        // -----------------------------
        float sampleOffset = Mathf.Clamp01(currentOffset + 0.02f);

        float3 tangentAhead = SplineUtility.EvaluateTangent(spline, sampleOffset);

        if (math.lengthsq(tangentAhead) < 0.0001f)
            tangentAhead = tangent;

        // Curvature magnitude
        float curvature = math.length(tangentAhead - tangent);

        // Clamp curvature to avoid spikes
        curvature = Mathf.Clamp(curvature * 50f, 0f, 1f);

        // Determine bank direction
        float3 right = math.normalize(math.cross(upSpline, tangent));
        float signed = math.dot(right, tangentAhead - tangent);
        float bankSign = Mathf.Sign(signed);

        float maxBankAngle = 45f;
        float bankAngle = curvature * maxBankAngle * bankSign;

        Quaternion bankRot = Quaternion.AngleAxis(bankAngle, tangent);

        // -----------------------------
        // OPTIONAL: ADD TILT DATA
        // -----------------------------
        float3 tilt = tiltData.Count > 0
            ? tiltData.Evaluate(spline, currentOffset, PathIndexUnit.Normalized, new Interpolators.LerpFloat3())
            : tiltData.DefaultValue;

        Quaternion tiltRot = Quaternion.Euler(tilt.x, tilt.y, tilt.z);

        // Final rotation
        transform.rotation = bankRot * baseRot * tiltRot;
    }



    // ---------------------------------------------------------
    // 5. DRIFT OFFSET
    // ---------------------------------------------------------
    private void ApplyDriftOffset() {
        if (currentOffset <= 0.0001f && Time.frameCount < 3)
            return;

        // Stop if we are no longer on a spline
        if (currentSpline == null || spline == null)
            return;

        // Stop if offset is invalid
        if (float.IsNaN(currentOffset) || currentOffset < 0f || currentOffset > 1f)
            return;

        float3 pos = SplineUtility.EvaluatePosition(spline, currentOffset);
        float3 tangent = SplineUtility.EvaluateTangent(spline, currentOffset);
        float3 upSpline = SplineUtility.EvaluateUpVector(spline, currentOffset);

        // Validate vectors
        if (!IsValidVector(pos) || !IsValidVector(tangent) || !IsValidVector(upSpline))
            return;

        // Prevent zero-length tangent/up vectors
        if (math.lengthsq(tangent) < 0.0001f || math.lengthsq(upSpline) < 0.0001f)
            return;

        float3 right = math.cross(upSpline, tangent);

        // Prevent zero-length right vector
        if (math.lengthsq(right) < 0.0001f)
            return;

        right = math.normalize(right);

        float drift = driftData.Count > 0
            ? driftData.Evaluate(spline, currentOffset, PathIndexUnit.Normalized, new Interpolators.LerpFloat())
            : driftData.DefaultValue;

        // Prevent drift from being NaN
        if (float.IsNaN(drift))
            drift = 0f;

        float3 finalLocal = pos + right * drift;

        // Validate final position
        if (!IsValidVector(finalLocal))
            return;

        transform.position = currentSpline.transform.TransformPoint(finalLocal);
    }

    private bool IsValidVector(float3 v) {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
    }


    // ---------------------------------------------------------
    // 6. SPLINE END HANDLING
    // ---------------------------------------------------------
    private void CheckSplineEnd() {
        if (currentOffset < 1f)
            return;

        if (currentSpline != null) {
            // If another spline is queued, switch to it
            if (nextSpline != null) {
                InitializeSpline(nextSpline);
                nextSpline = null;
                return;
            }
        }

        // No next spline > exit to physics
        ExitToPhysics();
    }

    private SplineContainer nextSpline;

    private void ExitToPhysics() {
        if (rb == null)
            return;

        rb.isKinematic = false;
        rb.linearVelocity = transform.forward * currentSpeed;

        acceleration = defaultAcceleration;
        deceleration = defaultDeceleration;

        enabled = false;
    }

    // ---------------------------------------------------------
    // 7. TRIGGER HANDLING (same as before)
    // ---------------------------------------------------------
    private void OnTriggerEnter(Collider other) {
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer == null)
            return;

        nextSpline = placer.container;

        if (placer.action != null && placer.action.GetType().Name != "TSpeedKart")
            acceleration = defaultAcceleration;

        if (placer.action != null && placer.action.GetType().Name != "TSlowKart")
            deceleration = defaultDeceleration;
    }

    private void OnTriggerExit(Collider other) {
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null && placer.container == nextSpline)
            nextSpline = null;
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class CoasterMove : MonoBehaviour {

    [Header("Spline")]
    public SplineContainer currentSpline;

    [Header("Movement")]
    public float speed = 0f;
    public float accelerationMult = 1f;
    public float gravityScale = 1f;
    [Tooltip("0 = no friction, 1 = stops pretty much immediately")]
    public float friction = 0.2f;
    [Tooltip("Hard floor for speed. Leave at 0 unless the cart should never go below this even briefly.")]
    public float minSpeed = 0f;
    public float maxSpeed = 20f;

    [Header("Low-Speed Assist")]
    [Tooltip("If speed drops below this, it snaps up to minMovementSpeed instead of stalling.")]
    public float lowSpeedThreshold = 0.5f;
    [Tooltip("The speed the cart is bumped up to once it falls below lowSpeedThreshold.")]
    public float minMovementSpeed = 2f;

    [Header("Spline State")]
    public float distance = 0f; // Distance travelled along currentSpline, in world units
    public int curKnotIndex;

    [Header("Follow Kart Data")]
    public List<FollowKart> followers = new List<FollowKart>();
    public float spacing = 5f;

    [Header("Stupid Garbage")]
    public float currentSplineLength = -1f; // Cached, world-space length of currentSpline. -1 is signal this needs to be recached or spline aborted
    public Rigidbody rb;
    private Vector3 carriedForward = Vector3.forward; // Tangent direction used last frame, for parallel transport
    private bool carriedUpInitialized = false;

    // Path history, so followers can walk the exact route the leader took
    [System.Serializable]
    public struct SplineSegmentRecord {
        public SplineContainer spline;
        public float length;
        public float startOdometer; // Leader's total distance value when this segment began
    }

    private List<SplineSegmentRecord> history = new List<SplineSegmentRecord>();
    
    // Total distance travelled since the very start, never resets on track transitions
    public float Odometer => (history.Count > 0 ? history[history.Count - 1].startOdometer : 0f) + distance;
    public bool OnRail => currentSpline != null;

    // Odometer value when kart left the rail. Stays at +infinity if it never has, so followers never misunderstand there's a rail-end to catch up to
    private float railEndOdometer = float.PositiveInfinity;
    public float RailEndOdometer => railEndOdometer;

    void Start() {
        currentSpline = GameObject.Find("StarterSpline").GetComponent<SplineContainer>();

        rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        CacheSplineLength();

        history.Clear();
        history.Add(new SplineSegmentRecord {
            spline = currentSpline,
            length = currentSplineLength,
            startOdometer = 0f
        });
    }

    void Update() {
        if (currentSpline != null) {
            HandleRailMovement();
        }

        // Update Follow Karts' movement
        for (int i = 0; i < followers.Count; i++) {
            float offset = (i + 1) * spacing;
            followers[i].Follow(this, offset);
        }
    }

    private void HandleRailMovement() {
        var spline = currentSpline.Spline;

        if (currentSplineLength <= 0f) {
            CacheSplineLength();
            if (currentSplineLength <= 0f) return;  // Spline has no length, abort
        }

        // GetPointAtLinearDistance walks the curve by actual arc length rather than assuming t is proportional to distance (which is only true for a straight line)
        float currentT = Mathf.Clamp01(distance / currentSplineLength);
        spline.GetPointAtLinearDistance(currentT, 0f, out currentT);    // Convert current arc length distance to a normalized t

        Vector3 worldTangentDir = ((Vector3)currentSpline.EvaluateTangent(currentT)).normalized;    // Evaluate phys tangent in WORLD space, based on distance along curSplne
        if (worldTangentDir.sqrMagnitude < 1e-6f) worldTangentDir = transform.forward;  // 

        // Gravity projected onto the track direction
        // Positive when gravity pulls "forward" along the track (downhill), negative uphill.
        float gravityAccel = Vector3.Dot(Physics.gravity * gravityScale, worldTangentDir);

        speed += gravityAccel * Time.deltaTime * accelerationMult;  // Integrate speed
        speed *= Mathf.Clamp01(1f - friction * Time.deltaTime); // Exponential friction damping speed * (1 (base speed) - friction built up over time) Clamped between 1 & 0.
                                                                // Ex. 10 * (1 - 0.5 * 2 sec)
                                                                //     10 * (1 - 1)
                                                                //     10 * 0   Therefore Kart stops
                                                                // Ex. 10 * (1 - 0.1 * 2 sec)
                                                                //     10 * (1 - 0.2)
                                                                //     10 * 0.8
                                                                //     8    Still slower than 10
        speed = Mathf.Clamp(speed, minSpeed, maxSpeed);
        if (speed < lowSpeedThreshold) speed = minMovementSpeed;    // Low-speed assist


        distance += speed * Time.deltaTime;

        if (distance >= currentSplineLength) {  // If reached end of spline
            OnSplineEnd();
            return;
        }

        if (distance < 0f) {    // Edge case
            distance = 0f;
            speed = 0f;
        }

        float newT = Mathf.Clamp01(distance / currentSplineLength); // Ratio along the spline
        spline.GetPointAtLinearDistance(newT, 0f, out newT);    // Converts that ratio to the normalized t based on arc length

        Vector3 worldPos = currentSpline.EvaluatePosition(newT);    // Evaluates, and later sets the kart's position to, the point newT along the spline
        transform.position = worldPos;

        Vector3 worldFwd = currentSpline.EvaluateTangent(newT); // Gets the direction the spline is heading at that point 
        if (worldFwd.sqrMagnitude > 1e-6f) {    // Check if spline tangent is too short to be a real direction. 1e-6f = 0.001, but avoids sqroot
            Vector3 newForward = worldFwd.normalized;

            transform.rotation = ComputeTransportedRotation(newForward, ref carriedForward, ref carriedUpInitialized);

            Vector3 nextKnotWorldPos = currentSpline.transform.TransformPoint(currentSpline.Spline[curKnotIndex + 1].Position); // Transform local knot position into world space
            if (curKnotIndex + 1 < currentSpline.Spline.Count - 1 && Vector3.Distance(nextKnotWorldPos, transform.position) < 0.1f) {
                curKnotIndex++;
            }
        }
    }

    // Shared rotation logic for Main Kart & Follow Karts
    public static Quaternion ComputeTransportedRotation(Vector3 newForward, ref Vector3 carriedForward, ref bool carriedUpInitialized) {
        // The auto generated tangents aren't guarenteed to be consistent, so rotate the previous up vector by the min rotation needed to take previous tangent to the new (parallel) tangent
        if (!carriedUpInitialized) { // On the first call, carriedForward gets a default value
            carriedForward = newForward;
            carriedUpInitialized = true;
        }

        Quaternion minimalRotation = Quaternion.FromToRotation(carriedForward, newForward); // Determines the min rotation from cur value to default value
        Vector3 transportedUp = minimalRotation * Vector3.up;   // Calculate the up tangent along the spline

        Vector3 projectedUp = Vector3.ProjectOnPlane(transportedUp, newForward);    // Re-project transportedUp to be perpendicular to newForward

        // Degenerate case only: transportedUp itself near-parallel to newForward (steep/near-vertical track), where projection collapses toward zero length.
        const float parallelThreshold = 0.999f; // About 2.5 degrees away from fully parallel
        if (Mathf.Abs(Vector3.Dot(transportedUp.normalized, newForward)) > parallelThreshold) { // Would the projection above collapse to near-zero length?
            Vector3 reference = Vector3.up; // Try world up as a stand-in instead
            if (Mathf.Abs(Vector3.Dot(reference, newForward)) > parallelThreshold) reference = Vector3.right; // World up is ALSO parallel (near-vertical loop), so use world right instead
            projectedUp = Vector3.ProjectOnPlane(reference, newForward); // Project the stand-in instead of the collapsed vector
        }

        Vector3 finalUp = projectedUp.normalized;   // Normalize the final up vector
        if (finalUp.sqrMagnitude < 1e-6f) finalUp = Vector3.up; // Last resort if it's still bad somehow
        carriedForward = newForward; // Save for next frame's transport calculation
        return Quaternion.LookRotation(newForward, finalUp);
    }

    private void CacheSplineLength() {
        if (currentSpline != null) {
            currentSplineLength = currentSpline.CalculateLength(); // World-space length, accounts for container transform
        } else {
            currentSplineLength = -1f;
        }
    }

    private void OnTriggerEnter(Collider other) {
        var placer = other.GetComponentInParent<SplinePlacer>();
        if (placer != null && placer.container != currentSpline) {  // The Spline Entrance is for a new Spline
            if (accelerationMult != 1f) accelerationMult = 1f;  // Reset accelerationMult to account for leaving an acceleration modifying track mod

            //  Reset references for Kart movement / New Track & activate any Track Mods
            if (currentSpline != null && currentSpline.gameObject.GetComponent<SplinePlacer>() != null && currentSpline.gameObject.GetComponent<SplinePlacer>().curKart != null) {
                currentSpline.GetComponent<SplinePlacer>().curKart = null;
            }

            placer.curKart = gameObject;

            // Push completed segment's odometer span before swapping to the new spline.
            // This uses the *history's* record of the last segment's length, not the live currentSplineLength field directly — if we just flew off the end of the track (gap jump), OnSplineEnd() already reset currentSplineLength to -1, which would corrupt this math.
            float newSegmentStart = history.Count > 0 ? history[history.Count - 1].startOdometer + history[history.Count - 1].length : 0f;

            currentSpline = placer.container;
            distance = 0f;
            curKnotIndex = 0;
            CacheSplineLength();
            
            if (rb != null) rb.isKinematic = true;

            railEndOdometer = float.PositiveInfinity;

            history.Add(new SplineSegmentRecord {
                spline = currentSpline,
                length = currentSplineLength,
                startOdometer = newSegmentStart
            });
            TrimHistory();

            placer.GetComponent<SplinePlacer>().TryTriggerMod(gameObject);
        }
    }

    // Drops old history entries
    private void TrimHistory() {
        float maxNeeded = followers.Count * spacing + currentSplineLength;
        while (history.Count > 1 &&
               Odometer - (history[0].startOdometer + history[0].length) > maxNeeded) {
            history.RemoveAt(0);
        }
    }

    // For followers to call to follow Main Kart's recorded segments
    public bool TryGetPointAtOdometer(float targetOdometer, out Vector3 pos, out Vector3 tangent) {
        for (int i = history.Count - 1; i >= 0; i--) {
            var seg = history[i];
            if (targetOdometer >= seg.startOdometer) {
                float local = targetOdometer - seg.startOdometer;
                float t = Mathf.Clamp01(local / seg.length);
                seg.spline.Spline.GetPointAtLinearDistance(t, 0f, out t);
                pos = seg.spline.EvaluatePosition(t);
                tangent = seg.spline.EvaluateTangent(t);
                return true;
            }
        }

        // Not enough history, so clamp to earliest known point
        var first = history[0];
        first.spline.Spline.GetPointAtLinearDistance(0f, 0f, out float t0);
        pos = first.spline.EvaluatePosition(t0);
        tangent = first.spline.EvaluateTangent(t0);
        return false;
    }

    // Disable attachment to pre. Spline and bring on physics for CRASHING!!! <3
    public void OnSplineEnd() {
        if (currentSpline == null) return;
        
        if (currentSpline != null && currentSpline.gameObject.GetComponent<SplinePlacer>() != null && currentSpline.gameObject.GetComponent<SplinePlacer>().curKart != null) {
            currentSpline.GetComponent<SplinePlacer>().curKart = null;
        }

        railEndOdometer = Odometer; // Record exactly where spline ended, so followers know where to reach before leaving the rail.

        if (rb != null) {
            rb.isKinematic = false;
            rb.linearVelocity = transform.forward * speed;
        }

        currentSpline = null;
        currentSplineLength = -1f;
    }
}
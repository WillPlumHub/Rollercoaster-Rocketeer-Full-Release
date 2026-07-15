using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class SplinePlacer : MonoBehaviour {

    [Header("References")]
    public GameObject curKart;
    public TrackModHeader action;                // assign ScriptableObject mod
    public SplineContainer container;

    [Header("Limit(s)")]
    public float speedLim = 500f;

    [Header("Knot Data")]
    public List<GameObject> knotPositions = new List<GameObject>();
    public List<quaternion> knotRotations = new List<quaternion>();

    public List<float3> knotTangentsIn = new List<float3>();
    public List<float3> knotTangentsOut = new List<float3>();

    [Header("Base Knot Reference (for breaking/repairing)")]
    public BezierKnot[] originalKnots;

    void Awake() {
        SyncListSizes();
        componentFinder();

        var spline = container.Spline;

        // Cache knot positions in spline local space.
        var localPositions = new float3[knotPositions.Count];
        for (int i = 0; i < knotPositions.Count; i++) {
            localPositions[i] = (float3)container.transform.InverseTransformPoint(knotPositions[i].transform.position);
        }

        // Estimate tangent direction and handle length for each knot.
        const float tangentScale = 0.35f;
        var tangentDirs = new float3[localPositions.Length]; // feeds the rotation frame below
        var rawTangentVectors = new float3[localPositions.Length]; // spline-local, NOT yet Rotation-relative

        for (int i = 0; i < localPositions.Length; i++) {
            float3 dirVec;
            if (localPositions.Length == 1) dirVec = new float3(0, 0, 1);
            else if (i == 0) dirVec = localPositions[1] - localPositions[0];
            else if (i == localPositions.Length - 1) dirVec = localPositions[i] - localPositions[i - 1];
            else dirVec = localPositions[i + 1] - localPositions[i - 1];

            float3 dirNorm = math.normalizesafe(dirVec, new float3(0, 0, 1));
            tangentDirs[i] = dirNorm;

            float neighborDist = i == 0
                ? math.distance(localPositions[math.min(1, localPositions.Length - 1)], localPositions[0])
                : math.distance(localPositions[i], localPositions[i - 1]);

            rawTangentVectors[i] = dirNorm * neighborDist * tangentScale;
        }

        // Build knot rotations from a transported frame, then apply any roll authored on the editor markers. This keeps the spline twist-free while preserving intentional banking.
        knotRotations = ComputeKnotRotations(localPositions, tangentDirs, container.transform);

        // Tangents are stored relative to the knot rotation, so convert them into the knot's local frame before assigning them.
        for (int i = 0; i < localPositions.Length; i++) {
            quaternion invRot = math.conjugate(knotRotations[i]);
            float3 rotationRelativeTangentOut = math.rotate(invRot, rawTangentVectors[i]);
            knotTangentsOut[i] = rotationRelativeTangentOut;
            knotTangentsIn[i] = -rotationRelativeTangentOut;
        }

        // Knot Position / Instantiation Management
        var knots = new BezierKnot[knotPositions.Count];
        for (int i = 0; i < knotPositions.Count; i++) {
            knots[i] = new BezierKnot(
                position: localPositions[i],
                tangentIn: knotTangentsIn[i],
                tangentOut: knotTangentsOut[i],
                rotation: knotRotations[i]
            );
        }

        spline.Knots = knots;
        originalKnots = knots.ToArray();

        Debug.Log($"[Awake, immediately after assignment] EvalUp={container.EvaluateUpVector(0f)} EvalTangent={container.EvaluateTangent(0f)}");
    }

    void Update() {
        Debug.Log($"[Update] rawRotation={container.Spline[0].Rotation} rawTangentOut={container.Spline[0].TangentOut} evalUp={container.EvaluateUpVector(0f)}");
        if (curKart != null) {
            if (curKart.GetComponent<CoasterMove>().speed > speedLim) {
                BreakSpline(curKart.GetComponent<CoasterMove>().curKnotIndex);
            }
        }
    }

    void componentFinder() {
        if (GetComponent<SplineContainer>() == null) {
            Debug.LogError("SplineContainer missing on CoasterSpline!");
            return;
        } else {
            container = GetComponent<SplineContainer>();
        }
    }

    // Sync every list related to knots
    void SyncListSizes() {
        // Sync Tangents IN
        while (knotTangentsIn.Count < knotPositions.Count) knotTangentsIn.Add(float3.zero);
        while (knotTangentsIn.Count > knotPositions.Count) knotTangentsIn.RemoveAt(knotTangentsIn.Count - 1);

        // Sync Tangents OUT
        while (knotTangentsOut.Count < knotPositions.Count) knotTangentsOut.Add(float3.zero);
        while (knotTangentsOut.Count > knotPositions.Count) knotTangentsOut.RemoveAt(knotTangentsOut.Count - 1);

        // Sync the stupid Knot Rotations
        while (knotRotations.Count < knotPositions.Count) knotRotations.Add(quaternion.identity);
        while (knotRotations.Count > knotPositions.Count) knotRotations.RemoveAt(knotRotations.Count - 1);
    }

    
    // Computes knot rotations using parallel transport and applies any roll authored on the knot markers.
    List<quaternion> ComputeKnotRotations(float3[] positions, float3[] tangents, Transform containerTransform) {
        int n = positions.Length;
        var result = new List<quaternion>(new quaternion[n]);
        if (n == 0) return result;

        // Use the same tangent directions as the spline handles.
        var baseUp = new float3[n]; {
            float3 t0 = tangents[0];
            float3 seedRef = math.abs(math.dot(t0, new float3(0, 1, 0))) < 0.99f
                ? new float3(0, 1, 0)
                : new float3(1, 0, 0);

            float3 r0 = math.normalizesafe(math.cross(seedRef, t0));
            if (math.lengthsq(r0) < 1e-8f) r0 = math.normalizesafe(math.cross(new float3(1, 0, 0), t0));
            baseUp[0] = math.normalizesafe(math.cross(t0, r0));

            float3 rPrev = r0;
            float3 tPrev = t0;

            for (int i = 0; i < n - 1; i++) {
                float3 v1 = positions[i + 1] - positions[i];
                float c1 = math.dot(v1, v1);

                float3 rL, tL;
                if (c1 < 1e-12f) {
                    rL = rPrev; tL = tPrev;
                } else {
                    rL = rPrev - (2f / c1) * math.dot(v1, rPrev) * v1;
                    tL = tPrev - (2f / c1) * math.dot(v1, tPrev) * v1;
                }

                float3 tNext = tangents[i + 1];
                float3 v2 = tNext - tL;
                float c2 = math.dot(v2, v2);

                float3 rNext = c2 < 1e-12f ? rL : rL - (2f / c2) * math.dot(v2, rL) * v2;
                rNext = math.normalizesafe(rNext);

                baseUp[i + 1] = math.normalizesafe(math.cross(tNext, rNext));

                rPrev = rNext;
                tPrev = tNext;
            }
        }

        // Apply each marker's roll to the transported frame
        Quaternion containerRotUnity = containerTransform.rotation;

        for (int i = 0; i < n; i++) {
            float3 fwd = tangents[i];

            // Marker rotation in spline local space
            Quaternion markerLocalUnity = Quaternion.Inverse(containerRotUnity) * knotPositions[i].transform.rotation;
            float3 markerUp = RotateVec(markerLocalUnity, new float3(0, 1, 0));

            // Reference up vector for an unrotated marker
            float3 neutralRef = math.abs(math.dot(fwd, new float3(0, 1, 0))) < 0.999f
                ? new float3(0, 1, 0)
                : new float3(1, 0, 0);

            float3 neutralUp = ProjectAndNormalize(neutralRef, fwd, baseUp[i]);
            float3 projectedMarkerUp = ProjectAndNormalize(markerUp, fwd, neutralUp);

            float rollAngle = SignedAngleAroundAxis(neutralUp, projectedMarkerUp, fwd);

            float3 finalUp = RotateAroundAxis(baseUp[i], fwd, rollAngle);

            result[i] = quaternion.LookRotationSafe(fwd, finalUp);
        }

        return result;
    }

    static float3 RotateVec(Quaternion q, float3 v) {
        Vector3 r = q * new Vector3(v.x, v.y, v.z);
        return new float3(r.x, r.y, r.z);
    }

    // Project onto the plane perpendicular to axis, with a fallback if needed
    static float3 ProjectAndNormalize(float3 v, float3 axis, float3 fallback) {
        float3 projected = v - axis * math.dot(v, axis);
        if (math.lengthsq(projected) < 1e-8f) return fallback;
        return math.normalize(projected);
    }

    // Returns the signed angle around the given axis
    static float SignedAngleAroundAxis(float3 a, float3 b, float3 axis) {
        float3 cross = math.cross(a, b);
        float sin = math.dot(cross, axis);
        float cos = math.dot(a, b);
        return math.atan2(sin, cos);
    }

    static float3 RotateAroundAxis(float3 v, float3 axis, float angleRad) {
        quaternion q = quaternion.AxisAngle(axis, angleRad);
        return math.rotate(q, v);
    }

    // REMEMBER: From Index knot onwards INCLUSIVE
    public void BreakSpline(int index) {

        if (curKart != null && curKart.GetComponent<CoasterMove>().curKnotIndex >= index) curKart.GetComponent<CoasterMove>().OnSplineEnd();  // To trigger coaster's off spline phys

        var spline = container.Spline;
        var knots = spline.Knots.ToArray(); // Have to get array of knots to better utilize it
        index = Mathf.Clamp(index, 0, knots.Length);
        var newKnots = knots.Take(index).ToArray(); // Make new array of all knots FROM INDEX KNOT ONWARDS
        spline.Knots = newKnots;

        if (index <= 1) knotPositions[0].SetActive(false);  // Account for entrance knot's collider
    }

    public void RepairSpline(int index) {
        //Debug.Log("STARTED");

        if (index <= 1) knotPositions[0].SetActive(true);
        var spline = container.Spline;
        //Debug.Log("SPLINE" + spline);
        index = Mathf.Clamp(index, 0, originalKnots.Length);

        var restored = originalKnots.Take(originalKnots.Length).ToArray();  // Restore the the OG knots to replace old, broken spline
        spline.Knots = restored;
    }

    public void TryTriggerMod(GameObject kart) {
        if (action == null) {
            Debug.LogWarning("No TrackMod assigned.");
            return;
        }

        action.Activate(this, kart);
    }
}
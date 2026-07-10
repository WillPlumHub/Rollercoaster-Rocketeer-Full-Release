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

        // (BezierKnot.Rotation is the basis the stored tangents are interpreted relative to)
        
        // Knot Rotation Management
        for (int i = 0; i < knotPositions.Count; i++) { // For each dummy knot (empty GameObject)'s position...
            knotRotations[i] = Quaternion.Inverse(container.transform.rotation) * knotPositions[i].transform.rotation;  // Convert it's rotation into container's local space w/ Inverse
        }

        // Knot Position / Instantiation Management
        var knots = new BezierKnot[knotPositions.Count];    // Create new array of knots. Idk wtf type it SHOULD be, but this seems to work, god forbid
        for (int i = 0; i < knotPositions.Count; i++) { // For every dummy knot position...
            float3 local = (float3)container.transform.InverseTransformPoint(knotPositions[i].transform.position);  // The ACTUAL Knot's position converted to the container's local space w/ InverseTransformPoint

            knots[i] = new BezierKnot(position: local, tangentIn: knotTangentsIn[i], tangentOut: knotTangentsOut[i], rotation: knotRotations[i]);
        }

        spline.Knots = knots;
        originalKnots = knots.ToArray();
    }

    void Update() {
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

    public void BreakSpline(int index) {

        curKart.GetComponent<CoasterMove>().OnSplineEnd();  // To trigger coaster's off spline phys

        var spline = container.Spline;
        var knots = spline.Knots.ToArray(); // Have to get array of knots to better utilize it
        index = Mathf.Clamp(index, 0, knots.Length);
        var newKnots = knots.Take(index).ToArray(); // Make new array of all knots BEFORE break point
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
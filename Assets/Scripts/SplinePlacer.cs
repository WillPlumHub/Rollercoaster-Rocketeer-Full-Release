using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class SplinePlacer : MonoBehaviour {

    public GameObject curKart;
    public TrackModHeader action;                // assign ScriptableObject mod

    public SplineContainer container;

    public List<GameObject> knotPositions = new List<GameObject>();
    
    public List<float3> knotTangentsIn = new List<float3>();
    public List<float3> knotTangentsOut = new List<float3>();
    
    public BezierKnot[] originalKnots;

    void Awake() {
        SyncListSizes();
        componentFinder();

        container = GetComponent<SplineContainer>();

        var spline = container.Spline;
        var knots = new BezierKnot[knotPositions.Count];

        for (int i = 0; i < knotPositions.Count; i++) {
            Vector3 worldPos = knotPositions[i].transform.position;
            float3 local = (float3)container.transform.InverseTransformPoint(worldPos);

            knots[i] = new BezierKnot(
                position: local,
                tangentIn: knotTangentsIn[i],
                tangentOut: knotTangentsOut[i]
            );
        }

        spline.Knots = knots;
        originalKnots = knots.ToArray();
    }

    void componentFinder() {
        var go = GameObject.Find("CoasterSpline");
        if (go == null) {
            Debug.LogError("CoasterSpline not found in scene!");
            return;
        }

        container = go.GetComponent<SplineContainer>();
        if (container == null) {
            Debug.LogError("SplineContainer missing on CoasterSpline!");
            return;
        }
    }

    void SyncListSizes() {
        while (knotTangentsIn.Count < knotPositions.Count)
            knotTangentsIn.Add(float3.zero);

        while (knotTangentsIn.Count > knotPositions.Count)
            knotTangentsIn.RemoveAt(knotTangentsIn.Count - 1);

        while (knotTangentsOut.Count < knotPositions.Count)
            knotTangentsOut.Add(float3.zero);

        while (knotTangentsOut.Count > knotPositions.Count)
            knotTangentsOut.RemoveAt(knotTangentsOut.Count - 1);
    }

    public void BreakSpline(int index) {
        var spline = container.Spline;
        // Convert to array so we can slice it
        var knots = spline.Knots.ToArray();
        // Clamp index
        index = Mathf.Clamp(index, 0, knots.Length);
        // Create a new array with only the knots BEFORE the break point
        var newKnots = knots.Take(index).ToArray();
        // Assign back to the spline
        spline.Knots = newKnots;

        if (index <= 1) knotPositions[0].SetActive(false);
    }
    public void RepairSpline(int index) {
        Debug.Log("STARTED");

        if (index <= 1) knotPositions[0].SetActive(true);

        var spline = container.Spline;
        Debug.Log("SPLINE" + spline);
        // Clamp index
        index = Mathf.Clamp(index, 0, originalKnots.Length);
        
        // Restore the first `index` knots from the original
        var restored = originalKnots.Take(originalKnots.Length).ToArray();

        spline.Knots = restored;
    }

    public void TryTriggerMod() {
        if (action == null) {
            Debug.LogWarning("No TrackMod assigned.");
            return;
        }

        action.Activate(this, curKart);
    }
}
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class SplinePlacer : MonoBehaviour {

    public SplineContainer container;

    public List<Vector3> knotPositions = new List<Vector3>();
    public List<float3> knotTangentsIn = new List<float3>();
    public List<float3> knotTangentsOut = new List<float3>();

    void Awake() {
        SyncListSizes();
        AutoGenerateTangents();
        componentFinder();

        container = GetComponent<SplineContainer>();

        var spline = container.AddSpline();
        var knots = new BezierKnot[knotPositions.Count];

        for (int i = 0; i < knotPositions.Count; i++) {
            Vector3 worldPos = transform.position + knotPositions[i];
            float3 local = (float3)container.transform.InverseTransformPoint(worldPos);

            knots[i] = new BezierKnot(
                position: local,
                tangentIn: knotTangentsIn[i],
                tangentOut: knotTangentsOut[i]
            );
        }

        spline.Knots = knots;
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

    void AutoGenerateTangents() {
        float tension = 1f / 3f;
        int count = knotPositions.Count;
        if (count < 2) return;

        List<float3> pos = new List<float3>(count);
        for (int i = 0; i < count; i++) {
            pos.Add((float3)(transform.position + knotPositions[i]));
        }

        for (int i = 0; i < count; i++) {
            float3 prev = (i == 0) ? pos[i] : pos[i - 1];
            float3 next = (i == count - 1) ? pos[i] : pos[i + 1];
            float3 current = pos[i];

            float3 dirPrev = current - prev;
            float3 dirNext = next - current;

            if (math.lengthsq(knotTangentsIn[i]) == 0)
                knotTangentsIn[i] = dirPrev * tension;

            if (math.lengthsq(knotTangentsOut[i]) == 0)
                knotTangentsOut[i] = dirNext * tension;
        }
    }
}

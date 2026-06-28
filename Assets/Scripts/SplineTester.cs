using UnityEngine;
using UnityEngine.Splines;

public class SplineTester : MonoBehaviour {
    public SplinePlacer placer;

    [Range(0, 50)]
    public int testIndex = 0;

    void Awake() {
        if (placer == null)
            placer = GetComponent<SplinePlacer>();
    }

    void Update() {
        // Press B to break the spline at testIndex
        if (Input.GetKeyDown(KeyCode.B)) {
            placer.BreakSpline(testIndex);
            Debug.Log($"[SplineTester] Broke spline at index {testIndex}");
        }

        // Press R to repair the spline up to testIndex
        if (Input.GetKeyDown(KeyCode.R)) {
            placer.RepairSpline(testIndex);
            Debug.Log($"[SplineTester] Repaired spline to index {testIndex}");
        }
    }

    void OnGUI() {
        GUI.Label(new Rect(20, 20, 500, 30),
            $"Test Index: {testIndex}   (B = Break, R = Repair, ↑↓ = Change Index)");
    }
}

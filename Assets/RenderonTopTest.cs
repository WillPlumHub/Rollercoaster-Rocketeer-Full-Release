using UnityEngine;
using UnityEngine.Rendering;

public class RenderonTopTest : MonoBehaviour {

    public RenderQueue queue = RenderQueue.Geometry; // Base queue
    [Range(-20, 20)] public int queueOffset = 0; // Small offset to control order of objects on the same queue

    private Material material;

    private void Start() {
        material = GetComponent<Renderer>().material;
    }

    void Update() {
        material.renderQueue = (int)queue + queueOffset;
    }
}

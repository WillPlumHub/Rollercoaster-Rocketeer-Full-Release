using UnityEngine;

public class HoldJump : MonoBehaviour {
    public float upwardForce = 10f;
    public float initialVelocity = 20f;
    public Vector3 direction;

    private Rigidbody rb;

    void Start() {
        rb = GetComponent<Rigidbody>();
    }

    void Update() {
        // Instant upward velocity when the key is first pressed
        if (Input.GetKeyDown(KeyCode.Space)) {
            rb.linearVelocity = new Vector3(direction.x * rb.linearVelocity.x, direction.y * initialVelocity, direction.z * rb.linearVelocity.z);
        }

        // Continuous upward force while held
        if (Input.GetKey(KeyCode.Space)) {
            rb.AddForce(direction * upwardForce, ForceMode.Acceleration);
        }
    }
}

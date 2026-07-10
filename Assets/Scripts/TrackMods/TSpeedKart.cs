using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(menuName = "TrackMods/Speed Mod")]
public class TSpeedKart : TrackModHeader {

    public float speedMultiplier = 2f;

    public override void Activate(SplinePlacer placer, GameObject kart) {
        
        if (kart.GetComponent<CoasterMove>() == null) {
            Debug.LogWarning("No SplineAnimate assigned to mod.");
            return;
        }

        kart.GetComponent<CoasterMove>().accelerationMult = speedMultiplier;
        kart.GetComponent<CoasterMove>().speed = speedMultiplier * 2f;
        kart.GetComponent<CoasterMove>().minMovementSpeed = speedMultiplier * 2f;
        //Debug.Log($"Speed mod applied to {kart.GetComponent<SplineAnimate>().gameObject.name}, new speed: {kart.GetComponent<SplineAnimate>().MaxSpeed}");
    }
}
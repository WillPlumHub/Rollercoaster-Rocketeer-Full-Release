// TSpeedKart.cs
using UnityEngine;
using UnityEngine.Splines;

[CreateAssetMenu(menuName = "TrackMods/Speed Mod")]
public class TSpeedKart : TrackModHeader {
    public float speedMultiplier = 2f;

    public override void Activate(SplinePlacer placer, GameObject kart) {
        
        if (kart.GetComponent<SplineAnimate>() == null) {
            Debug.LogWarning("No SplineAnimate assigned to mod.");
            return;
        }

        kart.GetComponent<SplineAnimate>().MaxSpeed += speedMultiplier;
        kart.GetComponent<CoasterMove>().acceleration = speedMultiplier;
        Debug.Log($"Speed mod applied to {kart.GetComponent<SplineAnimate>().gameObject.name}, new speed: {kart.GetComponent<SplineAnimate>().MaxSpeed}");
    }
}
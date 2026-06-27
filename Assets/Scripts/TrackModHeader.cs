// TrackModHeader.cs
using UnityEngine;
using UnityEngine.Splines;

public abstract class TrackModHeader : ScriptableObject {
    public abstract void Activate(SplinePlacer placer, GameObject kart);
}

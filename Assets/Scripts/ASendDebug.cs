using UnityEngine;

public class ASendDebug : MonoBehaviour, IFlagAction {

    public void RunAction() {
        Debug.Log($"{gameObject.name} is triggered");
    }
}
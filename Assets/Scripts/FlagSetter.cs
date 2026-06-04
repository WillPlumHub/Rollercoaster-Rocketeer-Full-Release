using UnityEngine;

public class FlagSetter : MonoBehaviour {

    public bool _manualOverride = false;

    public Vector2 _xPosRange = Vector2.zero;
    public Vector2 _yPosRange = Vector2.zero;
    public Vector2 _zPosRange = Vector2.zero;

    public Vector2 _xRotRange = Vector2.zero;
    public Vector2 _yRotRange = Vector2.zero;
    public Vector2 _zRotRange = Vector2.zero;

    public GameObject _triggerObj;

    public MonoBehaviour _targetScript;
    private IFlagAction _action;

    public void Awake() {
        _action = _targetScript as IFlagAction;
    }

    public void TryTriggerFlag() {
        if (_manualOverride) return;

        if (IsWithinRange(transform.position.x, _xPosRange) &&
            IsWithinRange(transform.position.y, _yPosRange) &&
            IsWithinRange(transform.position.z, _zPosRange) &&
            IsWithinRange(NormalizeAngle(transform.localEulerAngles.x), _xRotRange) &&
            IsWithinRange(NormalizeAngle(transform.localEulerAngles.y), _yRotRange) &&
            IsWithinRange(NormalizeAngle(transform.localEulerAngles.z), _zRotRange)) {

            if (_triggerObj != null /*&& it's being used on this*/) {

            }

            Debug.Log($"{gameObject.name} is triggering a flag");

            _action?.RunAction();
            
        }
    }

    bool IsWithinRange(float value, Vector2 range) {
        if (range == Vector2.zero)
            return true;
        return value >= range.x && value <= range.y;
    }

    float NormalizeAngle(float angle) {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}

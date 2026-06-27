using System.Collections.Generic;
using UnityEngine;

public class CoasterPassengers : MonoBehaviour {

    public float passengers = 0f;
    public int bonusKartNumber = 0;
    public List<GameObject> bonusKarts = new List<GameObject>();

    public float speed;
    public float dis;
    public GameObject target;

    void Start() {
        
    }

    void Update() {
        dis = Vector3.Distance(transform.position, target.transform.position);
        if (dis >= 1) transform.position = Vector3.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);
    }
}
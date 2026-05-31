using System;
using System.Collections;
using UnityEngine;

public class ColorChange : MonoBehaviour, IActivatable {

    public Material newColor;
    public float colorChangeTimer = 1f;

    private Material defaultMaterial; 

    private void Start() {
        defaultMaterial = GetComponent<Renderer>().material;
    }


    public void Activate() {
        GetComponent<Renderer>().material = newColor;
        StopAllCoroutines();
        StartCoroutine(ColorReset());
    }

    public void Deactivate() {
        GetComponent<Renderer>().material = defaultMaterial;
    }

    public IEnumerator ColorReset() {
        yield return new WaitForSeconds(colorChangeTimer);
        Deactivate();
    }
}
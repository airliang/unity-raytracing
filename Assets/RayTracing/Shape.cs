using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shape : MonoBehaviour
{
    public enum ShapeType
    {
        sphere,
        disk,
        triangleMesh,
        rectangle,   //in fact is a triangleMesh with 2 triangles
    }

    public ShapeType shapeType;
    public bool isAreaLight = false;
    public Color lightSpectrum = Color.white;
    public Vector3 spectrumScale = Vector3.one;
    public float lightIntensity = 1.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

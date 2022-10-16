using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTLight : MonoBehaviour
{
    public enum LightType
    {
        delta_distant,
        delta_point,
        area,
    }

    public LightType lightType = LightType.area;
    public Color color = Color.white;
    public float intensity = 1.0f;
    public float pointRadius;
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

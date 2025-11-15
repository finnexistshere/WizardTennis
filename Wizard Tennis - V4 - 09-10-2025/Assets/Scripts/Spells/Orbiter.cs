using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Orbiter : MonoBehaviour
{
    public float speed;
    
    void Update()
    {
        this.transform.rotation *= Quaternion.AngleAxis((speed * Time.deltaTime), Vector3.up);
    }
}

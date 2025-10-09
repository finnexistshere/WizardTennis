using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class shadowFollow : MonoBehaviour
{
    public GameObject follow;
    // Update is called once per frame
    void Update()
    {
        transform.position = new Vector3(follow.transform.position.x, 0.941f, follow.transform.position.z);
        transform.rotation = Quaternion.Euler(-90, 0, 0);
    }
}

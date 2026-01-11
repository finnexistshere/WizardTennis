using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Tether : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public Transform Player;

    private void Awake()
    {
        lineRenderer = this.GetComponent<LineRenderer>();
    }

    void Update()
    {
        if (lineRenderer != null && this.transform != null && Player != null)
        {
            lineRenderer.SetPosition(0, this.transform.position);
            lineRenderer.SetPosition(1, Player.position);
        }
    }
}

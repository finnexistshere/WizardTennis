using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OppHitting : MonoBehaviour
{
    public Transform ball;
    public Transform aimTarget;
    public float strength = 13;
    public float upForce = 5;
    private Vector3 targetPosition;
    public float speed;

    void Start()
    {
        targetPosition = transform.position; // make the 'targetPosition' equal to the opponent's current position
    }

    void Update()
    {
        targetPosition.z = ball.position.z; // update the targetPosition to the ball's z position so the bot only moves on the z axis
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball")) // if the opponent collide with the ball 
        {
            Vector3 dir = aimTarget.position - transform.position;
            other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);
        }
    }
}

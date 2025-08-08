using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OppHitting : MonoBehaviour
{
    public Transform ball;
    public GameObject aimTarget;
    public float strength = 25;
    public float upForce = 14;
    private Vector3 targetPosition;
    public float speed;

    public float[] xCourtAim = {27.5f, 40.5f};
    public float[] zCourtAim = { -12.5f, 0f, 12.5f };

    void Start()
    {
        targetPosition = transform.position; // make the 'targetPosition' equal to the opponent's current position
        aimTarget = GameObject.Find("OppAim");
    }

    void Update()
    {
        targetPosition.z = ball.position.z; // update the targetPosition to the ball's z position so the bot only moves on the z axis
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball")) // if the opponent collides with the ball 
        {
            // Change the position of the aimTarget to a random position on the Player court. This position is divided into sections (upcourt, downcourt, and centre, left, right)
            float aimTargety = aimTarget.transform.position.y;
            int xRand = Random.Range(0, xCourtAim.Length);
            int zRand = Random.Range(0, zCourtAim.Length);
            if (xRand == 0)
            {
                upForce = 17;
            } else
            {
                upForce = 15;
            }
            if (zRand == 1)
            {
                upForce += 2;
            }

                aimTarget.transform.position = new Vector3(xCourtAim[xRand], aimTargety, zCourtAim[zRand]);

            // If you want more detailed comments regarding how the ball hitting works, check the PlayerHitting code
            Vector3 dir = aimTarget.transform.position - transform.position;
            other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);
        }
    }
}

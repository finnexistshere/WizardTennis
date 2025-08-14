using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : MonoBehaviour
{
    public Transform aimTarget; // point on the opp side that the ball will aim towards
    public float strength = 25; // strength of hit
    public float ogUpForce = 11;
    private float upForce = 11; // upwards force of hit
    public float ballSpeed = 5;

    private bool hitting; // is the player currently hitting the ball
    private bool serving; // is the player's next hit a serve

    void Start()
    {
        serving = true; // Making the player's first hit a serve
        upForce = ogUpForce;
    }

    void Update()
    {
        // While the player holds down the 'H' key, they can hit the ball, but once they let go, they cannot
        if (Input.GetKeyDown(KeyCode.H)) // If the player is holding down the 'H' key
        {
            hitting = true;
        }
        else if (Input.GetKeyUp(KeyCode.H)) // If the player lets go of the 'H' key
        {
            hitting = false;
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log(other);
        if (other.CompareTag("Ball")) // if we collide with the ball 
        {
            if (hitting)
            {
                if (35.5 < transform.position.x)
                {
                    upForce = ogUpForce + 2;
                } else
                {
                    upForce = ogUpForce;
                }
                if (-6.25 < transform.position.z || transform.position.z < 6.25)
                {
                    upForce += 2;
                }


                if (serving)
                {
                    other.GetComponent<Rigidbody>().useGravity = true; // Make the ball stop floating midair (will change this once mechanics are properly fleshed out)
                    other.GetComponent<Rigidbody>().velocity = new Vector3(0, upForce, 0).normalized * strength/2; // Send the ball straight upwards
                    serving = false; // Player is no longer serving
                }
                else
                {
                    //Vector3 dir = aimTarget.position - transform.position; // Use the aimTarget to get a new direction vector we can use to aim
                    Vector3 dir = aimTarget.position - transform.position;
                    other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0); // Apply a force to the ball in the direction made above with the strength modifier + some upwards force so it can get over the net
                }
            }
        }
    }
}

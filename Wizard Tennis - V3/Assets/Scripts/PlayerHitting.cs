using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class Ball : MonoBehaviour
{
    public Transform aimTarget; // point on the opp side that the ball will aim towards
    public float strength = 25; // strength of hit
    public float ogUpForce = 11;
    private float upForce = 11; // upwards force of hit
    public float ballSpeed = 5;

    private bool hitting = true; // is the player currently hitting the ball
    public bool serving; // is the player's next hit a serve

    private bool nearBall = false; // only used when serving; detect if the player is near the ball when they press e
    private GameObject ball;

    void Start()
    {
        serving = true; // Making the player's first hit a serve
        upForce = ogUpForce;
        ball = GameObject.FindWithTag("Ball");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if(nearBall && serving)
            {
                ball.GetComponent<Rigidbody>().useGravity = true; // Make the ball stop floating midair (will change this once mechanics are properly fleshed out)
                ball.GetComponent<Rigidbody>().velocity = new Vector3(0, upForce, 0).normalized * strength / 2; // Send the ball straight upwards
                serving = false; // Player is no longer serving
            }
        }
    }

    /*public void OnHitBall(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            hitting = true;
        } else if (context.canceled)
        {
            hitting = false;
        }
    }*/

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball")) // if we collide with the ball 
        {
            nearBall = true;
            if (hitting)
            {
                if (35.5 < transform.position.x)
                {
                    upForce = ogUpForce + 2;
                }
                else
                {
                    upForce = ogUpForce;
                }
                if (-6.25 < transform.position.z || transform.position.z < 6.25)
                {
                    upForce += 2;
                }


                if (!serving)
                {
                    //Vector3 dir = aimTarget.position - transform.position; // Use the aimTarget to get a new direction vector we can use to aim
                    Vector3 dir = aimTarget.position - transform.position;
                    ball.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0); // Apply a force to the ball in the direction made above with the strength modifier + some upwards force so it can get over the net
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball")) // if we collide with the ball 
        {
            nearBall = false;
        }
    }
}

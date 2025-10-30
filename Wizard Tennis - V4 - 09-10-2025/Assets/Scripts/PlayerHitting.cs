using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Ball : MonoBehaviour
{
    public Transform aimTarget; // point on the opposite side
    public float strength = 25;
    public float ogUpForce = 11;
    private float upForce = 11;
    public float ballSpeed = 5;

    public TwoHandIKController_Opponent OppIKRig;
    public TwoHandIKController PlayerIKRig;

    private bool hitting = true;
    public bool serving;

    private bool nearBall = false;

    public SpellEffects SpellEffects;

    [Header("Spawn Settings")]
    public Transform ballSpawnPoint;  // Where the ball will spawn
    public GameObject ballPrefab;      // Prefab for the ball
    private static GameObject currentBall;  // Ensures only one ball exists

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] hitsounds;
    [Range(0f, 0.5f)] public float pitchJitter = 0.07f;
    [Range(0f, 0.5f)] public float volumeJitter = 0.12f;
    public float minInterval = 0.08f;     // Prevents rapid double-fires on multi-collider entries

    private float _lastHitSfxTime = -999f;

    public GameObject Opponent;
    public float xPos;

    public GameObject servingBarriers;

    private void PlayHitsound(Vector3 contactPoint)
    {
        if (audioSource == null) return;
        if (hitsounds == null || hitsounds.Length == 0) return;

        // Anti-spam: ignore if we just played a hit very recently
        if (Time.time - _lastHitSfxTime < minInterval) return;
        _lastHitSfxTime = Time.time;

        // Pick a random clip
        int index = (hitsounds.Length == 1) ? 0 : Random.Range(0, hitsounds.Length);

        // Optional: move the source to the contact point for better spatialization
        audioSource.transform.position = contactPoint;

        // Subtle variation for natural feel
        float basePitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        float baseVol = 1f + Random.Range(-volumeJitter, volumeJitter);
        baseVol = Mathf.Clamp01(baseVol); // keep in [0,1]

        audioSource.pitch = basePitch;
        audioSource.PlayOneShot(hitsounds[index], baseVol);
    }


    public Spellcasting spellcasting;

    public CollisionTrackerBall CollisionTracker;

    public int rallyCount = 0;
    public int greenRallyCount = 0;
    public bool green = false;
    public int greenPoints = 0;

    public ScoreManager scoreManager;

    void Start()
    {
        serving = true;
        upForce = ogUpForce;

        // Only try to find the ball if one exists
        if (currentBall == null)
            currentBall = GameObject.FindWithTag("Ball");
    }

    private void Awake()
    {
        Opponent = GameObject.Find("Opponent");
        servingBarriers = GameObject.Find("ServingBarriers");
    }

    void Update()
    {
        // Press E: spawn ball if none exists
        if (Input.GetKeyDown(KeyCode.E) && currentBall == null && ballPrefab != null && ballSpawnPoint != null)
        {
            currentBall = Instantiate(ballPrefab, ballSpawnPoint.position, ballSpawnPoint.rotation);
            CollisionTracker = currentBall.GetComponent<CollisionTrackerBall>(); // get tracker
            nearBall = true; // immediately allow serving
            GameManager.Instance.UnlockPickupSpawning();

            if (PlayerIKRig != null)
            {
                PlayerIKRig.AssignBall(currentBall.transform);
            }
            if (OppIKRig != null)
            {
                OppIKRig.AssignBall(currentBall.transform);
            }
        }

        // Press E: serve if near ball
        if (Input.GetKeyDown(KeyCode.E) && nearBall && serving)
        {
            if (currentBall != null)
            {
                Rigidbody rb = currentBall.GetComponent<Rigidbody>();
                rb.useGravity = true;
                rb.velocity = new Vector3(0, upForce, 0).normalized * strength / 2;
                serving = false;
                if (servingBarriers != null)
                { 
                    servingBarriers.SetActive(false);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            nearBall = true;
            if (hitting)
            {

                if (35.5 < transform.position.x) upForce = ogUpForce + 2;
                else upForce = ogUpForce;

                if (-6.25 < transform.position.z || transform.position.z < 6.25) upForce += 2;

                float aimTargety = aimTarget.transform.position.y;
                float aimTargetz = aimTarget.transform.position.z;
                Vector3 oppPos = Opponent.transform.position;
                if (oppPos.x > 0)
                {
                    xPos = -2f;
                }
                else
                {
                    xPos = 2f;
                }

                if (transform.position.z < 5 || transform.position.x < -5 || transform.position.x > 5) xPos = 0f;

                if (SpellEffects.plrHitSpell) SpellEffects.castSpell();

                aimTarget.transform.position = new Vector3(xPos, aimTargety, aimTargetz);

                ParticleSystem particle = GameObject.FindGameObjectWithTag("Player Hit Particle").GetComponent<ParticleSystem>();
                particle.transform.position = other.transform.position;
                particle.Play();

                //Contact point for audio Spatialization
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                PlayHitsound(contactPoint);

                if (!serving)
                {
                    Vector3 dir = aimTarget.position - transform.position;
                    other.GetComponent<Rigidbody>().velocity = dir.normalized * strength + new Vector3(0, upForce, 0);
                    rallyCount++;
                    if (green)
                    {
                        greenRallyCount++;
                        if (greenRallyCount % 4 == 0)
                        {
                            greenPoints++;
                            this.GetComponent<UIManager>().UpdateGreenPoints(greenPoints);
                            scoreManager.greenPoints = greenPoints;
                        }
                    }
                    this.GetComponent<UIManager>().UpdateRallyCount(rallyCount);
                }

                if (SpellEffects.resetOnPlrHit)
                {
                    SpellEffects.resetSpellEffect();
                }

                if (CollisionTracker != null)
                {
                    CollisionTracker.LastHitWizard = "Player";
                    CollisionTracker.hasBounced = false;
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ball"))
            nearBall = false;
    }

    // Optional: helper to let other scripts safely get the current ball
    public static GameObject GetCurrentBall() => currentBall;
}

using UnityEngine;

public abstract class PickupBase : MonoBehaviour
{
    private GameManager manager;

    private void Awake()
    {
        manager = FindObjectOfType<GameManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            MainCharacterMovement playerMovement = other.GetComponent<MainCharacterMovement>();
            if (playerMovement != null)
            {
                ApplyEffect(playerMovement);
                manager.spellSpawned = false;
            }

            Destroy(gameObject);
        }
    }

    public abstract void ApplyEffect(MainCharacterMovement player);
}


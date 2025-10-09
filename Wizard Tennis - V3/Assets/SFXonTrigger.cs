using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SFXonTrigger : MonoBehaviour
{
    AudioSource source;

    Collider2D soundTrigger;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        soundTrigger = GetComponent<Collider2D>();
    }


    void OnTriggerEnter2D(Collider2D collider)
    {
        source.Play();
    }


    private void OnTriggerExit2D(Collider2D collider)
    {
        source.Play();
    }
}
  
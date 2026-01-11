using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public class TimedDisabler : MonoBehaviour
{
    public GameObject objectToDisable;
    public float disableDelay = 7.0f;

    void Start()
    {
        StartCoroutine(DisableAfterTime(disableDelay));
    }

    private IEnumerator DisableAfterTime(float seconds)
    {
        yield return new WaitForSeconds(seconds);

        if (objectToDisable != null )
        {
            objectToDisable.SetActive(false);
        }
    }
}

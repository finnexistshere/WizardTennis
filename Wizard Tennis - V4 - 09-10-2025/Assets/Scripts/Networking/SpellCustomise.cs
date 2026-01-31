using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEngine;

public class SpellCustomise : MonoBehaviour
{
    private GameManager gameManager;
    public List<GameObject> playerPrefabs;

    private void Awake()
    {
        gameManager = FindAnyObjectByType<GameManager>();

        if (OptionsManager.Instance != null)
        {
            if (OptionsManager.Instance.FireballBool) playerPrefabs.Add(gameManager.pickupPrefabs[0]);
            if (OptionsManager.Instance.IceBool) playerPrefabs.Add(gameManager.pickupPrefabs[1]);
            if (OptionsManager.Instance.LightningBool) playerPrefabs.Add(gameManager.pickupPrefabs[2]);
            if (OptionsManager.Instance.ShadowBool) playerPrefabs.Add(gameManager.pickupPrefabs[3]);
            if (OptionsManager.Instance.GreenBool) playerPrefabs.Add(gameManager.pickupPrefabs[4]);
            if (OptionsManager.Instance.StoneBool) playerPrefabs.Add(gameManager.pickupPrefabs[5]);
            if (OptionsManager.Instance.ChronosBool) playerPrefabs.Add(gameManager.pickupPrefabs[6]);
            if (OptionsManager.Instance.GeminiBool) playerPrefabs.Add(gameManager.pickupPrefabs[7]);
            if (OptionsManager.Instance.PiscesBool) playerPrefabs.Add(gameManager.pickupPrefabs[8]);
            if (OptionsManager.Instance.JollyBool) playerPrefabs.Add(gameManager.pickupPrefabs[9]);
            if (OptionsManager.Instance.BlinkBool) playerPrefabs.Add(gameManager.pickupPrefabs[10]);
            if (OptionsManager.Instance.WarpBool) playerPrefabs.Add(gameManager.pickupPrefabs[11]);
            if (OptionsManager.Instance.TetherBool) playerPrefabs.Add(gameManager.pickupPrefabs[12]);
            if (OptionsManager.Instance.MudBool) playerPrefabs.Add(gameManager.pickupPrefabs[13]);
            if (OptionsManager.Instance.GambitBool) playerPrefabs.Add(gameManager.pickupPrefabs[14]);
            if (OptionsManager.Instance.GorbinoBool) playerPrefabs.Add(gameManager.pickupPrefabs[15]);
        }
        else
        {
            playerPrefabs = gameManager.pickupPrefabs;
        }
    }
}

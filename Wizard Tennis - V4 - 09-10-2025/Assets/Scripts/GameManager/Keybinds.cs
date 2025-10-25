using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class Keybinds
{
    public KeyCode moveForward = KeyCode.W;
    public KeyCode moveBackward = KeyCode.S;
    public KeyCode moveLeft = KeyCode.A;
    public KeyCode moveRight = KeyCode.D;
    public KeyCode jump = KeyCode.Space;

    public void ApplyLeftHandedLayout(bool enabled)
    {
        if (enabled)
        {
            moveForward = KeyCode.UpArrow;
            moveBackward = KeyCode.DownArrow;
            moveLeft = KeyCode.LeftArrow;
            moveRight = KeyCode.RightArrow;
        }
        else
        {
            moveForward = KeyCode.W;
            moveBackward = KeyCode.S;
            moveLeft = KeyCode.A;
            moveRight = KeyCode.D;
        }
    }

    public Dictionary<string, int> ToDict()
    {
        return new Dictionary<string, int>
        {
            { "Forward", (int)moveForward },
            { "Backward", (int)moveBackward },
            { "Left", (int)moveLeft },
            { "Right", (int)moveRight },
            { "Jump", (int)jump }
        };
    }

    public void FromDict(Dictionary<string, int> dict)
    {
        if (dict == null) return;
        if (dict.TryGetValue("Forward", out var f)) moveForward = (KeyCode)f;
        if (dict.TryGetValue("Backward", out var b)) moveBackward = (KeyCode)b;
        if (dict.TryGetValue("Left", out var l)) moveLeft = (KeyCode)l;
        if (dict.TryGetValue("Right", out var r)) moveRight = (KeyCode)r;
        if (dict.TryGetValue("Jump", out var j)) jump = (KeyCode)j;
    }
}

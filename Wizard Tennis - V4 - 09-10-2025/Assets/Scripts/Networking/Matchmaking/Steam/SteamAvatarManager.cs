using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System.Threading.Tasks;

public class SteamAvatarManager : MonoBehaviour
{
    public static SteamAvatarManager Instance { get; private set; }

    private Dictionary<ulong, Sprite> avatarCache = new();
    private HashSet<ulong> currentlyFetching = new();

    [Header("Settings")]
    public AvatarSize avatarSize = AvatarSize.Medium;

    [Header("Fallback")]
    public Sprite fallbackAvatar;

    public enum AvatarSize
    {
        Small,
        Medium,
        Large
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ----------------------------------------------------
    // PUBLIC API (UNCHANGED)
    // ----------------------------------------------------

    public void GetAvatar(ulong steamId, Action<Sprite> callback)
    {
        if (avatarCache.TryGetValue(steamId, out var cached))
        {
            callback?.Invoke(cached);
            return;
        }

        if (currentlyFetching.Contains(steamId))
        {
            StartCoroutine(WaitForFetch(steamId, callback));
            return;
        }

        currentlyFetching.Add(steamId);
        _ = FetchAvatarAsync(steamId, callback);
    }

    public void GetAvatar(Friend friend, Action<Sprite> callback)
    {
        GetAvatar(friend.Id, callback);
    }

    public void PreloadLobbyAvatars(Lobby lobby)
    {
        foreach (var member in lobby.Members)
            GetAvatar(member.Id, null);
    }

    public void ClearCache()
    {
        foreach (var sprite in avatarCache.Values)
        {
            if (sprite != null && sprite.texture != null)
                Destroy(sprite.texture);
        }

        avatarCache.Clear();
        currentlyFetching.Clear();
    }

    // ----------------------------------------------------
    // ASYNC FETCH (FACEPUNCH-CORRECT)
    // ----------------------------------------------------

    private async Task FetchAvatarAsync(ulong steamId, Action<Sprite> callback)
    {
        Sprite result = fallbackAvatar;

        try
        {
            var friend = new Friend(steamId);
            Image? image = avatarSize switch
            {
                AvatarSize.Small => await friend.GetSmallAvatarAsync(),
                AvatarSize.Medium => await friend.GetMediumAvatarAsync(),
                AvatarSize.Large => await friend.GetLargeAvatarAsync(),
                _ => null
            };

            if (image.HasValue)
                result = ConvertImageToSprite(image.Value);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SteamAvatar] Failed to fetch avatar for {steamId}: {e.Message}");
        }

        // Return to Unity main thread
        await SwitchToUnityThread();

        avatarCache[steamId] = result;
        currentlyFetching.Remove(steamId);
        callback?.Invoke(result);
    }

    // ----------------------------------------------------
    // UTILITIES
    // ----------------------------------------------------

    private Sprite ConvertImageToSprite(Image img)
    {
        var texture = new Texture2D((int)img.Width, (int)img.Height, TextureFormat.RGBA32, false);

        var pixels = new UnityEngine.Color[img.Width * img.Height];

        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                int i = (int)(y * img.Width + x);
                var p = img.GetPixel(x, y);

                pixels[i] = new UnityEngine.Color(
                    p.r / 255f,
                    p.g / 255f,
                    p.b / 255f,
                    p.a / 255f
                );
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Bilinear;

        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
    }

    private IEnumerator WaitForFetch(ulong steamId, Action<Sprite> callback)
    {
        while (currentlyFetching.Contains(steamId))
            yield return null;

        if (avatarCache.TryGetValue(steamId, out var sprite))
            callback?.Invoke(sprite);
        else
            callback?.Invoke(fallbackAvatar);
    }

    /// <summary>
    /// Ensures continuation runs on Unity main thread
    /// </summary>
    private static Task SwitchToUnityThread()
    {
        var tcs = new TaskCompletionSource<bool>();
        Instance.StartCoroutine(SwitchCoroutine(tcs));
        return tcs.Task;
    }

    private static IEnumerator SwitchCoroutine(TaskCompletionSource<bool> tcs)
    {
        yield return null;
        tcs.SetResult(true);
    }
}

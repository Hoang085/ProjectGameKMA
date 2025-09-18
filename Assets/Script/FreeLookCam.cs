using UnityEngine;
using Cinemachine;
using System.Collections;

public class FreeLookCam : MonoBehaviour
{
    public CinemachineFreeLook freeLookCam;     // có thể để trống, script sẽ tự tìm
    public PlayerSaveManager playerSaveManager; // có thể để trống, script sẽ tự tìm

    public bool saveOnPause = true;
    public bool saveOnQuit = true;

    void Awake()
    {
        if (!playerSaveManager) playerSaveManager = FindObjectOfType<PlayerSaveManager>();
        if (!freeLookCam) freeLookCam = FindObjectOfType<CinemachineFreeLook>();
    }

    void Start()
    {
        // Fallback: nếu PlayerSaveManager chưa kịp BindTo(), đợi instance sẵn sàng rồi tự bind
        StartCoroutine(BindWhenReady());
    }

    IEnumerator BindWhenReady()
    {
        // đợi đến khi có instance
        while (!playerSaveManager || !playerSaveManager.PlayerInstance)
            yield return null;

        BindTo(playerSaveManager.PlayerInstance);
    }

    // === API được PlayerSaveManager gọi trực tiếp sau khi Instantiate ===
    public void BindTo(GameObject playerInstance)
    {
        if (!freeLookCam || !playerInstance) return;

        var t = playerInstance.transform;
        var lookAt = t.Find("LookAt") ?? t;

        freeLookCam.Follow = t;      // bám theo thân Player
        freeLookCam.LookAt = lookAt; // nhìn vào child "LookAt" nếu có, nếu không thì gốc
    }

    void OnApplicationPause(bool paused)
    {
        if (saveOnPause && paused)
            playerSaveManager?.SaveNow();
    }

    void OnApplicationQuit()
    {
        if (saveOnQuit)
            playerSaveManager?.SaveNow();
    }
}

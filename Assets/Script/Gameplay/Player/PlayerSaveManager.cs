using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerSaveManager : MonoBehaviour
{
    private FreeLookCam _freeLookCam;

    [Header("Spawn")]
    public GameObject playerPrefab;
    public Transform defaultSpawnPoint;

    [Header("Options")]
    public bool restoreOnStart = true;
    public bool onlyYawRotation = true; // chỉ nhớ góc Y

    // Keys
    const string KX = "player_pos_x", KY = "player_pos_y", KZ = "player_pos_z";
    const string KRX = "player_rot_x", KRY = "player_rot_y", KRZ = "player_rot_z";
    const string KSCENE = "player_scene";

    // Expose cho script khác dùng (read-only)
    public GameObject PlayerInstance { get; private set; }

    private void Awake()
    {
        _freeLookCam = FindAnyObjectByType<FreeLookCam>();
    }

    void Start()
    {
        if (!playerPrefab)
        {
            Debug.LogError("[PlayerSaveManager] Missing playerPrefab.");
            return;
        }
        if (restoreOnStart) SpawnFromSave();
        else SpawnAtDefault();
    }

    void SpawnFromSave()
    {
        if (HasSavedPosition())
        {
            Vector3 pos = new Vector3(
                PlayerPrefs.GetFloat(KX),
                PlayerPrefs.GetFloat(KY),
                PlayerPrefs.GetFloat(KZ)
            );

            Vector3 eul = onlyYawRotation
                ? new Vector3(0f, PlayerPrefs.GetFloat(KRY), 0f)
                : new Vector3(PlayerPrefs.GetFloat(KRX), PlayerPrefs.GetFloat(KRY), PlayerPrefs.GetFloat(KRZ));

            PlayerInstance = Instantiate(playerPrefab, pos, Quaternion.Euler(eul));
        }
        else
        {
            SpawnAtDefault();
            return; // SpawnAtDefault sẽ tự bind camera
        }

        BindCamera();
    }

    void SpawnAtDefault()
    {
        Vector3 pos = defaultSpawnPoint ? defaultSpawnPoint.position : Vector3.zero;
        Quaternion rot = defaultSpawnPoint ? defaultSpawnPoint.rotation : Quaternion.identity;

        PlayerInstance = Instantiate(playerPrefab, pos, rot);
        BindCamera();
    }

    void BindCamera()
    {
        if (!_freeLookCam || !PlayerInstance) return;
        _freeLookCam.BindTo(PlayerInstance);   // <<< gọi sang FreeLookCam
    }

    bool HasSavedPosition() => PlayerPrefs.HasKey(KX);

    public void SaveNow()
    {
        if (!PlayerInstance) return;

        var t = PlayerInstance.transform;
        var p = t.position;
        var r = t.eulerAngles;

        PlayerPrefs.SetFloat(KX, p.x);
        PlayerPrefs.SetFloat(KY, p.y);
        PlayerPrefs.SetFloat(KZ, p.z);
        PlayerPrefs.SetFloat(KRX, r.x);
        PlayerPrefs.SetFloat(KRY, r.y);
        PlayerPrefs.SetFloat(KRZ, r.z);
        PlayerPrefs.SetString(KSCENE, SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
    }
}

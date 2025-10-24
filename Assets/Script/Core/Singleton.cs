using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected static T m_ins;
    protected static bool s_isQuitting;

    // Reset các biến static mỗi lần load domain/subsystem (kể cả khi Disable Domain Reload)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        m_ins = null;
        s_isQuitting = false;
    }

    public static bool HasInstance => m_ins != null;

    public static T Ins
    {
        get
        {
            // Khi đang play, nếu lỡ có cờ quitting từ lần trước thì vẫn nên cho tìm lại
            // (cờ này chỉ hữu ích trong đúng khoảnh khắc app sắp thoát)
            if (!Application.isPlaying && s_isQuitting) return null;

            if (m_ins == null)
                m_ins = GameObject.FindFirstObjectByType<T>(FindObjectsInactive.Exclude);

            return m_ins;
        }
    }

    public virtual void Awake()
    {
        MakeSingleton(dontDestroyOnLoad: true);
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    protected void MakeSingleton(bool dontDestroyOnLoad)
    {
        if (m_ins == null)
        {
            m_ins = this as T;

            if (dontDestroyOnLoad)
            {
                var root = transform.root;
                if (root != transform) DontDestroyOnLoad(root.gameObject);
                else DontDestroyOnLoad(gameObject);
            }
        }
        else if (m_ins != this)
        {
            // tránh trùng instance khi load lại scene
            Destroy(gameObject);
        }
    }

    protected virtual void OnDestroy()
    {
        // Nếu object hiện tại là instance và app chưa thoát → nhả tham chiếu
        if (!s_isQuitting && ReferenceEquals(m_ins, this))
            m_ins = null;
    }
}

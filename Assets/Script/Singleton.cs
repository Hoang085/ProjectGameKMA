using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    static T m_ins;
    static bool s_isQuitting;

    public static bool HasInstance => m_ins != null;

    public static T Ins
    {
        get
        {
            // Đừng khởi tạo khi đang quit
            if (s_isQuitting) return null;

            // Chỉ tìm trong scene; KHÔNG tạo GameObject mới ở giai đoạn teardown
            if (m_ins == null)
                m_ins = GameObject.FindObjectOfType<T>();

            return m_ins;
        }
    }

    public virtual void Awake()
    {
        MakeSingleton(true);
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public void MakeSingleton(bool destroyOnload)
    {
        if (m_ins == null)
        {
            m_ins = this as T;
            if (destroyOnload)
            {
                var root = transform.root;
                if (root != transform) DontDestroyOnLoad(root);
                else DontDestroyOnLoad(gameObject);
            }
        }
        else if (m_ins != this)
        {
            Destroy(gameObject);
        }
    }
}

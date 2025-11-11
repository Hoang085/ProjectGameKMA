using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(menuName = "TTCS3D/Video Profile", fileName = "VideoProfile")]
public class VideoProfile : ScriptableObject
{
    public VideoClip clip;
    [Range(0, 1)] public float dimBackground = 1f; // nền mờ phía sau
}

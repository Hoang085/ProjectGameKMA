using System.Collections;
using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    [Header("Refs")]
    public GameClock clock; // để trống sẽ tự tìm

    [Header("Skyboxes per slot")]
    public Material skyMorningA;
    public Material skyMorningB;
    public Material skyAfternoonA;
    public Material skyAfternoonB;
    public Material skyEvening; // dùng cho tối/đêm (nếu slotsPerDay >= 5)

    [Header("Transition")]
    [Tooltip("Thời gian chuyển mượt exposure khi đổi skybox")]
    public float transitionSeconds = 0.5f;

    void Awake()
    {
        if (!clock) clock = FindFirstObjectByType<GameClock>();
    }

    void OnEnable()
    {
        if (clock != null) clock.OnSlotChanged += HandleSlotChanged;
        ApplyForCurrentSlotInstant();
    }

    void OnDisable()
    {
        if (clock != null) clock.OnSlotChanged -= HandleSlotChanged;
    }

    void ApplyForCurrentSlotInstant()
    {
        var target = GetSkyFor(clock ? clock.Slot : DaySlot.MorningA);
        if (target)
        {
            RenderSettings.skybox = target;
            DynamicGI.UpdateEnvironment();
        }
    }

    void HandleSlotChanged()
    {
        var target = GetSkyFor(clock.Slot); // đọc từ thuộc tính Slot
        if (target) StartCoroutine(FadeSwapSkybox(target, transitionSeconds));
    }

    Material GetSkyFor(DaySlot slot)
    {
        switch (slot)
        {
            case DaySlot.MorningA: return skyMorningA ? skyMorningA : skyMorningB;
            case DaySlot.MorningB: return skyMorningB ? skyMorningB : skyMorningA;
            case DaySlot.AfternoonA: return skyAfternoonA ? skyAfternoonA : skyAfternoonB;
            case DaySlot.AfternoonB: return skyAfternoonB ? skyAfternoonB : skyAfternoonA;
            case DaySlot.Evening:
            default: return skyEvening ? skyEvening : skyAfternoonB;
        }
    }

    IEnumerator FadeSwapSkybox(Material target, float seconds)
    {
        // Swap ngay lập tức
        RenderSettings.skybox = target;
        DynamicGI.UpdateEnvironment();

        // Nếu material có tham số _Exposure thì ta fade-in từ thấp lên
        if (target && target.HasProperty("_Exposure"))
        {
            float targetExp = 1f; // hoặc để inspector chỉnh
            target.SetFloat("_Exposure", 0.01f);

            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                target.SetFloat("_Exposure", Mathf.Lerp(0.01f, targetExp, k));
                DynamicGI.UpdateEnvironment();
                yield return null;
            }

            target.SetFloat("_Exposure", targetExp);
        }
    }
}

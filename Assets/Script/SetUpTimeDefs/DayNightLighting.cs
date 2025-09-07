using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // để chỉnh AmbientMode

[DisallowMultipleComponent]
public class DayNightLighting : MonoBehaviour
{
    [System.Serializable]
    public struct SlotLighting
    {
        public Vector3 sunEuler;     // góc quay Directional Light
        public float sunIntensity;   // cường độ light
        public Color sunColor;       // màu light
        [Range(0f, 2f)] public float ambientIntensity; // độ sáng môi trường
        public Color ambientColor;   // màu môi trường (Flat)
        [Range(0f, 2f)] public float skyboxExposure;   // nếu dùng Skybox procedural
    }

    [Header("Refs")]
    public Light sun;                          // Directional Light
    public Material skyboxMat;                 // Skybox (optional, để đổi exposure)

    [Header("Transition")]
    [Range(0f, 5f)] public float lerpSeconds = 1.0f;   // thời gian chuyển cảnh
    public AnimationCurve lerpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Lighting Preset theo Slot")]
    public SlotLighting morningA = new SlotLighting
    {
        sunEuler = new Vector3(25, 30, 0),
        sunIntensity = 1.0f,
        sunColor = new Color(1.0f, 0.95f, 0.85f),
        ambientIntensity = 1.0f,
        ambientColor = new Color(0.75f, 0.78f, 0.9f),
        skyboxExposure = 1.1f
    };
    public SlotLighting morningB = new SlotLighting
    {
        sunEuler = new Vector3(50, 60, 0),
        sunIntensity = 1.2f,
        sunColor = new Color(1.0f, 0.98f, 0.92f),
        ambientIntensity = 1.1f,
        ambientColor = new Color(0.8f, 0.82f, 0.92f),
        skyboxExposure = 1.2f
    };
    public SlotLighting afternoonA = new SlotLighting
    {
        sunEuler = new Vector3(35, 150, 0),
        sunIntensity = 1.1f,
        sunColor = new Color(1.0f, 0.97f, 0.9f),
        ambientIntensity = 1.0f,
        ambientColor = new Color(0.78f, 0.8f, 0.9f),
        skyboxExposure = 1.1f
    };
    public SlotLighting afternoonB = new SlotLighting
    {
        sunEuler = new Vector3(10, 200, 0),
        sunIntensity = 0.7f,
        sunColor = new Color(1.0f, 0.9f, 0.8f),
        ambientIntensity = 0.8f,
        ambientColor = new Color(0.65f, 0.7f, 0.85f),
        skyboxExposure = 0.9f
    };
    public SlotLighting evening = new SlotLighting
    {     // dùng khi bạn có slot Evening
        sunEuler = new Vector3(-5, 230, 0),
        sunIntensity = 0.15f,
        sunColor = new Color(0.7f, 0.8f, 1.0f),
        ambientIntensity = 0.5f,
        ambientColor = new Color(0.35f, 0.4f, 0.55f),
        skyboxExposure = 0.6f
    };

    Coroutine _routine;

    void Reset()
    {
        // auto-find Directional Light
        if (!sun)
        {
            foreach (var l in FindObjectsOfType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
        }
        if (!skyboxMat && RenderSettings.skybox) skyboxMat = RenderSettings.skybox;
    }

    void OnEnable()
    {
        if (GameClock.I)
        {
            Hook(true);
            ApplyNow(true);
        }
        else
        {
            StartCoroutine(WaitAndHook());
        }
    }
    void OnDisable() => Hook(false);

    IEnumerator WaitAndHook()
    {
        while (!GameClock.I) yield return null;
        Hook(true);
        ApplyNow(true);
    }

    void Hook(bool sub)
    {
        if (!GameClock.I) return;
        if (sub)
        {
            GameClock.I.OnSlotChanged += ApplyNow;
            GameClock.I.OnDayChanged += ApplyNow;
        }
        else
        {
            GameClock.I.OnSlotChanged -= ApplyNow;
            GameClock.I.OnDayChanged -= ApplyNow;
        }
    }

    void ApplyNow() => ApplyNow(false);

    void ApplyNow(bool instant)
    {
        if (!sun || !GameClock.I) return;

        var target = GetPreset(GameClock.I.Slot, GameClock.I);
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(LerpLighting(target, instant ? 0f : lerpSeconds));
    }

    SlotLighting GetPreset(DaySlot slot, GameClock clock)
    {
        // nếu CalendarConfig chỉ có 4 ca/ngày → coi như không dùng evening
        int sPerD = clock && clock.config ? Mathf.Max(1, clock.config.slotsPerDay) : 4;
        if (sPerD <= 4 && slot == DaySlot.Evening) slot = DaySlot.AfternoonB;

        return slot switch
        {
            DaySlot.MorningA => morningA,
            DaySlot.MorningB => morningB,
            DaySlot.AfternoonA => afternoonA,
            DaySlot.AfternoonB => afternoonB,
            _ => evening
        };
    }

    IEnumerator LerpLighting(SlotLighting target, float seconds)
    {
        // capture current
        var startRot = sun.transform.eulerAngles;
        var startIntensity = sun.intensity;
        var startColor = sun.color;
        var startAmbient = RenderSettings.ambientLight;
        var startAmbientInt = RenderSettings.ambientIntensity;
        float startExposure = skyboxMat ? skyboxMat.GetFloat("_Exposure") : 1f;

        if (seconds <= 0f)
        {
            Apply(target);
            yield break;
        }

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            float k = lerpCurve.Evaluate(Mathf.Clamp01(t));

            sun.transform.eulerAngles = Vector3.Lerp(startRot, target.sunEuler, k);
            sun.intensity = Mathf.Lerp(startIntensity, target.sunIntensity, k);
            sun.color = Color.Lerp(startColor, target.sunColor, k);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(startAmbient, target.ambientColor, k);
            RenderSettings.ambientIntensity = Mathf.Lerp(startAmbientInt, target.ambientIntensity, k);

            if (skyboxMat) skyboxMat.SetFloat("_Exposure", Mathf.Lerp(startExposure, target.skyboxExposure, k));

            yield return null;
        }
        Apply(target);
    }

    void Apply(SlotLighting p)
    {
        sun.transform.eulerAngles = p.sunEuler;
        sun.intensity = p.sunIntensity;
        sun.color = p.sunColor;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = p.ambientColor;
        RenderSettings.ambientIntensity = p.ambientIntensity;

        if (skyboxMat) skyboxMat.SetFloat("_Exposure", p.skyboxExposure);
    }
}

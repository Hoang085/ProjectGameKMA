using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] CanvasGroup canvasGroup;
    [SerializeField] Image barFill;

    [Header("Text (UGUI)")]
    [SerializeField] TextMeshProUGUI percentText;

    [Header("Fade")]
    [SerializeField] float fadeDuration = 0.25f;

    [TextArea] public string[] tips;

    private float _lastDisplayedPercent = -1f;
    private const float MIN_UPDATE_DELTA = 0.01f;
    private System.Text.StringBuilder _percentStringBuilder;

    void Awake()
    {
        if (canvasGroup) canvasGroup.alpha = 0f;

        _percentStringBuilder = new System.Text.StringBuilder(4);
        
        SetBar(0f);
        gameObject.SetActive(false);
    }

    public void SetBar(float v)
    {
        v = Mathf.Clamp01(v);

        if (Mathf.Abs(v - _lastDisplayedPercent) < MIN_UPDATE_DELTA && v < 1f)
            return;
            
        _lastDisplayedPercent = v;
        
        if (barFill) 
        {
            barFill.fillAmount = v;
        }
        
        if (percentText) 
        {
            _percentStringBuilder.Clear();
            _percentStringBuilder.Append(Mathf.RoundToInt(v * 100f));
            _percentStringBuilder.Append('%');
            percentText.text = _percentStringBuilder.ToString();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        _lastDisplayedPercent = -1f; 
        StopAllCoroutines();
        StartCoroutine(FadeTo(1f, false));
    }

    public void Hide()
    {
        StopAllCoroutines();
        StartCoroutine(FadeTo(0f, true));
    }

    System.Collections.IEnumerator FadeTo(float target, bool disableAfter)
    {
        if (!canvasGroup) yield break;
        
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        
        float invDuration = 1f / fadeDuration;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed * invDuration;
            canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }
        
        canvasGroup.alpha = target;
        if (disableAfter) gameObject.SetActive(false);
    }
}

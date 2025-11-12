using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TutorialPlayer : MonoBehaviour
{
    [Header("Cấu hình trang")]
    [SerializeField] private CanvasGroup[] pages;
    [SerializeField] private float fadeDuration = 0.4f;

    [Header("Nút điều hướng")]
    [SerializeField] private Button btnPrev;
    [SerializeField] private Button btnNext;
    [SerializeField] private Button btnClose;

    private int currentIndex = 0;
    private Coroutine transitionRoutine;

    private void Start()
    {
        // Chỉ bật trang đầu tiên
        for (int i = 0; i < pages.Length; i++)
            pages[i].gameObject.SetActive(i == 0);

        UpdateButtonVisibility();
    }

    public void NextPage()
    {
        if (currentIndex < pages.Length - 1)
            ShowPage(currentIndex + 1);
    }

    public void PrevPage()
    {
        if (currentIndex > 0)
            ShowPage(currentIndex - 1);
    }

    private void ShowPage(int newIndex)
    {
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        transitionRoutine = StartCoroutine(TransitionPage(currentIndex, newIndex));
        currentIndex = newIndex;
        UpdateButtonVisibility();
    }

    private IEnumerator TransitionPage(int from, int to)
    {
        CanvasGroup oldPage = pages[from];
        CanvasGroup newPage = pages[to];
        newPage.gameObject.SetActive(true);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = t / fadeDuration;
            oldPage.alpha = Mathf.Lerp(1, 0, a);
            newPage.alpha = Mathf.Lerp(0, 1, a);
            yield return null;
        }

        oldPage.alpha = 0;
        newPage.alpha = 1;
        oldPage.gameObject.SetActive(false);
    }

    private void UpdateButtonVisibility()
    {
        // Ẩn nút Prev ở trang đầu
        if (btnPrev) btnPrev.gameObject.SetActive(currentIndex > 0);

        // Ẩn nút Next ở trang cuối
        if (btnNext) btnNext.gameObject.SetActive(currentIndex < pages.Length - 1);

        // Chỉ hiện BtnClose ở trang cuối cùng (ví dụ TutorialPage6)
        if (btnClose) btnClose.gameObject.SetActive(currentIndex == pages.Length - 1);
    }
}

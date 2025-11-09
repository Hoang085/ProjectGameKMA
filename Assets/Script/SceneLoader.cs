using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader I { get; private set; }

    [Header("UI Prefab")]
    [SerializeField] LoadingScreen loadingUIPrefab;

    [Header("Behaviour")]
    [SerializeField] float minShowSeconds = 1.0f;
    [SerializeField] float progressSmooth = 0.45f;
    [SerializeField] bool autoActivate = true;
    [SerializeField] bool preventDoubleLoads = true;

    LoadingScreen ui;
    float displayProgress;
    bool isLoading;

    private WaitForEndOfFrame _waitEndOfFrame;
    private const float PROGRESS_UPDATE_THRESHOLD = 0.01f;

    void Awake()
    {
        if (I != null && I != this) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        _waitEndOfFrame = new WaitForEndOfFrame();

        if (!loadingUIPrefab) { Debug.LogError("[SceneLoader] Missing LoadingScreen prefab"); return; }
        ui = Instantiate(loadingUIPrefab, transform);
        ui.gameObject.SetActive(false);
    }

    public static void Load(string sceneName, Action onDone = null)
    {
        if (!I) { Debug.LogError("[SceneLoader] Not in bootstrap scene"); return; }
        if (I.preventDoubleLoads && I.isLoading) return;
        I.StartCoroutine(I.LoadRoutine(sceneName, onDone));
    }

    IEnumerator LoadRoutine(string sceneName, Action onDone)
    {
        isLoading = true;
        Time.timeScale = 1f;

        displayProgress = 0f;
        ui.SetBar(0f);
        ui.Show();
        yield return null; 

        yield return Resources.UnloadUnusedAssets();
        System.GC.Collect();

        var oldPrio = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.BelowNormal;

        float start = Time.realtimeSinceStartup;
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        float lastShown = 0f;
        float lastUpdateTime = Time.realtimeSinceStartup;
        
        while (!op.isDone)
        {
            float target = Mathf.Clamp01(op.progress / 0.9f);
            displayProgress = Mathf.MoveTowards(displayProgress, target, progressSmooth * Time.unscaledDeltaTime);

            float currentTime = Time.realtimeSinceStartup;
            bool shouldUpdate = (displayProgress - lastShown >= PROGRESS_UPDATE_THRESHOLD) || 
                               (currentTime - lastUpdateTime >= 0.1f); 
            
            if (shouldUpdate)
            {
                ui.SetBar(displayProgress);
                lastShown = displayProgress;
                lastUpdateTime = currentTime;
            }

            bool ready = op.progress >= 0.9f;
            bool timeOk = (Time.realtimeSinceStartup - start) >= minShowSeconds;
            
            if (ready && timeOk && autoActivate) 
            {
                op.allowSceneActivation = true;
            }

            yield return null;
        }

        ui.SetBar(1f);
        ui.Hide();
        Application.backgroundLoadingPriority = oldPrio;

        yield return null;
        onDone?.Invoke();
        isLoading = false;
    }

}

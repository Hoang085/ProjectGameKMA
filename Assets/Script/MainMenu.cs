using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    private const string KEY_SAVE_EXISTS = "SaveExists";
    private const string KEY_SHOW_TUTORIAL = "ShowTutorial";
    
    [SerializeField] private GameObject continueButton;
    [SerializeField] private float delayBeforeShow = 2f;

    private void Awake()
    {
        if (continueButton) continueButton.SetActive(false);
    }

    private void Start()
    {
        int state = PlayerPrefs.GetInt(KEY_SAVE_EXISTS, 0);
        if (state == 1)
        {
            if (continueButton) continueButton.SetActive(true);
        }
        else if (state == 2)
        {
            StartCoroutine(ShowContinueAfterDelayThenStabilize());
        }
    }

    private IEnumerator ShowContinueAfterDelayThenStabilize()
    {
        yield return new WaitForSeconds(delayBeforeShow);
        if (continueButton) continueButton.SetActive(true);
        PlayerPrefs.SetInt(KEY_SAVE_EXISTS, 1);
        PlayerPrefs.Save();
    }

    public void OnContinue()
    {
        int state = PlayerPrefs.GetInt(KEY_SAVE_EXISTS, 0);
        if (state == 0) return;
        SceneLoader.Load("GameScene");
    }

    public void OnNewGame()
    {
        // **BƯỚC 1: Xóa file điểm thi trước**
        ExamResultStorageFile.ClearAll();
        
        // **BƯỚC 2: Xóa toàn bộ PlayerPrefs (bao gồm cache điểm)**
        PlayerPrefs.DeleteAll();
        
        // **BƯỚC 3: Khôi phục các key cần thiết**
        PlayerPrefs.SetInt(KEY_SAVE_EXISTS, 2);
        PlayerPrefs.SetInt(KEY_SHOW_TUTORIAL, 1); 
        PlayerPrefs.Save();
        
        // **BƯỚC 4: Load game mới**
        SceneLoader.Load("GameScene");
    }

    public void OnSettings()
    {
        Debug.Log("Settings button clicked");
    }

    public void OnExit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

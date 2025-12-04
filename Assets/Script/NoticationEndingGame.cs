using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NoticationEndingGame : MonoBehaviour
{
    public TextMeshProUGUI textMessage;
    [SerializeField] private TextMeshProUGUI textGPA;
    [SerializeField] private Button onclickMenu;

    private void Start()
    {
        onclickMenu.onClick.AddListener(OnClickMenu);
        
        // Ẩn textGPA ban đầu
        if (textGPA != null)
        {
            textGPA.gameObject.SetActive(false);
        }
    }

    private string message1 = "Bạn đã trở nên cực kỳ thân thiết với mọi người trong trường. Có lẽ đây là lúc để khám phá kết thúc đặc biệt dành cho những tình bạn đẹp…!!!";
    private string message2 = "Mệt mỏi, áp lực và những ngày tháng lạc lối… Cuối cùng, bạn đã quyết định rời khỏi con đường học tập. Không ai có thể trách bạn — nhưng cũng không ai thể thay bạn viết tiếp trang sách dang dở này.";
    private string message3 = "Chúc mừng bạn đã hoàn thành chặng đường của mình trong suốt 5 năm qua! Sau tất cả những nỗ lực, thử thách và những đêm dài mệt mỏi… Hôm nay, bạn đã bước sang một chương mới của cuộc đời.";

    public void GetMes1()
    {
        // Đảm bảo notification đang active**
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Ẩn GPA khi không phải graduation ending
        if (textGPA != null)
        {
            textGPA.gameObject.SetActive(false);
        }
        
        textMessage.text = message1;
        Debug.Log($"[NoticationEndingGame] Message 1 displayed. GameObject active: {gameObject.activeSelf}");
    }

    public void GetMes2()
    {
        // Đảm bảo notification đang active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Ẩn GPA khi không phải graduation ending
        if (textGPA != null)
        {
            textGPA.gameObject.SetActive(false);
        }    
        textMessage.text = message2;
        Debug.Log($"[NoticationEndingGame] Message 2 displayed. GameObject active: {gameObject.activeSelf}");
    }

    public void GetMes3()
    {
        // Đảm bảo notification đang active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Ẩn GPA vì GetMes3 không có GPA parameter
        if (textGPA != null)
        {
            textGPA.gameObject.SetActive(false);
        }
        
        textMessage.text = message3;
        
        // Debug log để kiểm tra**
        Debug.Log($"[NoticationEndingGame] Message 3 displayed. GameObject active: {gameObject.activeSelf}");
    }

    /// <summary>
    /// Hiển thị message 3 (graduation) với GPA
    /// </summary>
    public void GetMes3WithGPA(float gpa)
    {
        // Đảm bảo notification đang active
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        // Hiển thị message graduation
        textMessage.text = message3;
        if (textGPA != null)
        {
            // Bật GameObject nếu đang tắt
            if (!textGPA.gameObject.activeSelf)
            {
                textGPA.gameObject.SetActive(true);
            }
            
            textGPA.text = $"GPA bạn đạt được là: {gpa:F2} / 4.0";
        }
        else
        {
            Debug.LogWarning("[NoticationEndingGame] textGPA is NULL! Cannot display GPA.");
        }
    }

    public void OnClickMenu()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneLoader.Load("MainMenu");
    }
}

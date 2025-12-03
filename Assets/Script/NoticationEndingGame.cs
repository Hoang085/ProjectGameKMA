using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NoticationEndingGame : MonoBehaviour
{
    public TextMeshProUGUI textMessage;
    [SerializeField] private Button onclickMenu;

    private void Start()
    {
        onclickMenu.onClick.AddListener(OnClickMenu);
    }

    private string message1 = "Bạn đã trở nên cực kỳ thân thiết với mọi người trong trường. Có lẽ đây là lúc để khám phá kết thúc đặc biệt dành cho những tình bạn đẹp…!!!";
    private string message2 = "Mệt mỏi, áp lực và những ngày tháng lạc lối… Cuối cùng, bạn đã quyết định rời khỏi con đường học tập. Không ai có thể trách bạn — nhưng cũng không ai thể thay bạn viết tiếp trang sách dang dở này.";
    private string message3 = "Sau tất cả những nỗ lực, thử thách và những đêm dài mệt mỏi… Hôm nay, bạn đã bước sang một chương mới của cuộc đời. Chúc mừng! Bạn đã tốt nghiệp.";

    public void GetMes1()
    {
        // **MỚI: Đảm bảo notification đang active**
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        textMessage.text = message1;
        
        // **MỚI: Debug log để kiểm tra**
        Debug.Log($"[NoticationEndingGame] Message 1 displayed. GameObject active: {gameObject.activeSelf}");
    }

    public void GetMes2()
    {
        // **MỚI: Đảm bảo notification đang active**
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        textMessage.text = message2;
        
        // **MỚI: Debug log để kiểm tra**
        Debug.Log($"[NoticationEndingGame] Message 2 displayed. GameObject active: {gameObject.activeSelf}");
    }

    public void GetMes3()
    {
        // **MỚI: Đảm bảo notification đang active**
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
        
        textMessage.text = message3;
        
        // **MỚI: Debug log để kiểm tra**
        Debug.Log($"[NoticationEndingGame] Message 3 displayed. GameObject active: {gameObject.activeSelf}");
    }

    public void OnClickMenu()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneLoader.Load("MainMenu");
    }
}

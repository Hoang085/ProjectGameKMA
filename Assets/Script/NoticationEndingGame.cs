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
        textMessage.text = message1;
    }

    public void GetMes2()
    {
        textMessage.text = message2;
    }

    public void GetMes3()
    {
        textMessage.text = message3;
    }

    public void OnClickMenu()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SceneLoader.Load("MainMenu");
    }
}

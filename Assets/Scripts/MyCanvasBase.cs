using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MyCanvasBaseBase : MonoBehaviour
{
    //自动生成的控件声明
    public Button testBtn;
    public Button testBtn1;
    public Button testBtn2;
    public Button testBtn3;
    public Button testBtn4;
    public Button testBtn4_1;
    public Button testBtn4_2;
    public Button testBtn4_3;
    public ScrollRect scroll_View;
    public Button testSSS;
    public Button asdf;
    public TextMeshProUGUI qqqq;
    public Button button;


    protected virtual void Start()
    {
        //控件引用已在编辑器模式下自动绑定，直接使用即可

        //自动生成的进行对应控件的事件监听
        testBtn.onClick.AddListener(OntestBtnClick);
        testBtn1.onClick.AddListener(OntestBtn1Click);
        testBtn2.onClick.AddListener(OntestBtn2Click);
        testBtn3.onClick.AddListener(OntestBtn3Click);
        testBtn4.onClick.AddListener(OntestBtn4Click);
        testBtn4_1.onClick.AddListener(OntestBtn4_1Click);
        testBtn4_2.onClick.AddListener(OntestBtn4_2Click);
        testBtn4_3.onClick.AddListener(OntestBtn4_3Click);
        testSSS.onClick.AddListener(OntestSSSClick);
        asdf.onClick.AddListener(OnasdfClick);
        button.onClick.AddListener(OnbuttonClick);

    }

    //自动生成的对应进行监听事件的响应函数
    protected virtual void OntestBtnClick() { }
    protected virtual void OntestBtn1Click() { }
    protected virtual void OntestBtn2Click() { }
    protected virtual void OntestBtn3Click() { }
    protected virtual void OntestBtn4Click() { }
    protected virtual void OntestBtn4_1Click() { }
    protected virtual void OntestBtn4_2Click() { }
    protected virtual void OntestBtn4_3Click() { }
    protected virtual void OntestSSSClick() { }
    protected virtual void OnasdfClick() { }
    protected virtual void OnbuttonClick() { }
}

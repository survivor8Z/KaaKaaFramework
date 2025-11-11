using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 层级枚举
/// </summary>
public enum E_UILayer
{
    /// <summary>
    /// 最底层
    /// </summary>
    Bottom,
    /// <summary>
    /// 中层
    /// </summary>
    Middle,
    /// <summary>
    /// 高层
    /// </summary>
    Top,
    /// <summary>
    /// 系统层 最高层
    /// </summary>
    System,
}

/// <summary>
/// 管理所有UI面板的管理器
/// 注意：面板预设体名要和面板类名一致！！！！！
/// </summary>
public class UIMgr : BaseManager<UIMgr>
{
    private abstract class BasePanelInfo { }

    private class PanelInfo<T> : BasePanelInfo where T : BasePanel
    {
        public T panel;
        public UnityAction<T> callBack;
        public bool isHide;

        public PanelInfo(UnityAction<T> callBack)
        {
            this.callBack += callBack;
        }
    }

    private Camera uiCamera;
    private Canvas uiCanvas;
    private EventSystem uiEventSystem;

    private Transform bottomLayer;
    private Transform middleLayer;
    private Transform topLayer;
    private Transform systemLayer;

    private Dictionary<string, BasePanelInfo> panelDic = new Dictionary<string, BasePanelInfo>();

    private UIMgr()
    {

        GameObject cameraPrefab = AddressablesMgr.Instance.LoadAssetSync<GameObject>("UICamera");
        if (cameraPrefab != null)
        {
            uiCamera = GameObject.Instantiate(cameraPrefab).GetComponent<Camera>();
            GameObject.DontDestroyOnLoad(uiCamera.gameObject);
        }

        GameObject canvasPrefab = AddressablesMgr.Instance.LoadAssetSync<GameObject>("Canvas");
        if (canvasPrefab != null)
        {
            uiCanvas = GameObject.Instantiate(canvasPrefab).GetComponent<Canvas>();
            uiCanvas.worldCamera = uiCamera;
            GameObject.DontDestroyOnLoad(uiCanvas.gameObject);

            bottomLayer = uiCanvas.transform.Find("Bottom");
            middleLayer = uiCanvas.transform.Find("Middle");
            topLayer = uiCanvas.transform.Find("Top");
            systemLayer = uiCanvas.transform.Find("System");
        }

        GameObject eventPrefab = AddressablesMgr.Instance.LoadAssetSync<GameObject>("EventSystem");
        if (eventPrefab != null)
        {
            uiEventSystem = GameObject.Instantiate(eventPrefab).GetComponent<EventSystem>();
            GameObject.DontDestroyOnLoad(uiEventSystem.gameObject);
        }
    }

    public Transform GetLayerFather(E_UILayer layer)
    {
        switch (layer)
        {
            case E_UILayer.Bottom: return bottomLayer;
            case E_UILayer.Middle: return middleLayer;
            case E_UILayer.Top: return topLayer;
            case E_UILayer.System: return systemLayer;
            default: return middleLayer; // 提供一个默认值
        }
    }

    /// <summary>
    /// 显示面板 (Addressables 异步驱动)
    /// </summary>
    public void ShowPanel<T>(E_UILayer layer = E_UILayer.Middle, UnityAction<T> callBack = null) where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        {
            PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
            if (panelInfo.panel == null)
            {
                panelInfo.isHide = false;
                if (callBack != null)
                    panelInfo.callBack += callBack;
            }
            else
            {
                if (!panelInfo.panel.gameObject.activeSelf)
                    panelInfo.panel.gameObject.SetActive(true);

                panelInfo.panel.ShowMe();
                callBack?.Invoke(panelInfo.panel);
            }
            return;
        }

        panelDic.Add(panelName, new PanelInfo<T>(callBack));
        AddressablesMgr.Instance.LoadAssetAsync<GameObject>(panelName, (handle) =>
        {
            // 检查加载完成时，该面板是否已经被标记为隐藏
            if (!panelDic.ContainsKey(panelName)) return; // 可能在加载过程中被销毁了
            PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
            if (panelInfo.isHide)
            {
                // 如果在加载过程中被标记为隐藏，直接释放资源并移除记录
                panelDic.Remove(panelName);
                AddressablesMgr.Instance.Release<GameObject>(panelName);
                return;
            }

            Transform father = GetLayerFather(layer);

            // 使用 handle.Result 获取加载到的 GameObject 资源
            GameObject panelObj = GameObject.Instantiate(handle.Result, father, false);

            T panel = panelObj.GetComponent<T>();
            panel.ShowMe();

            panelInfo.callBack?.Invoke(panel);
            panelInfo.callBack = null;
            panelInfo.panel = panel;
        });
    }

    /// <summary>
    /// 隐藏面板
    /// </summary>
    public void HidePanel<T>(bool isDestroy = false) where T : BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        {
            PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
            if (panelInfo.panel == null)
            {
                panelInfo.isHide = true;
                panelInfo.callBack = null;
            }
            else
            {
                panelInfo.panel.HideMe();
                if (isDestroy)
                {
                    GameObject.Destroy(panelInfo.panel.gameObject);
                    panelDic.Remove(panelName);
                    AddressablesMgr.Instance.Release<GameObject>(panelName);
                }
                else
                {
                    panelInfo.panel.gameObject.SetActive(false);
                }
            }
        }
    }




    /// <summary>
    /// 获取面板
    /// </summary>
    /// <typeparam name="T">面板的类型</typeparam>
    public void GetPanel<T>( UnityAction<T> callBack ) where T:BasePanel
    {
        string panelName = typeof(T).Name;
        if (panelDic.ContainsKey(panelName))
        {
            //取出字典中已经占好位置的数据
            PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
            //正在加载中
            if(panelInfo.panel == null)
            {
                //加载中 应该等待加载结束 再通过回调传递给外部去使用
                panelInfo.callBack += callBack;
            }
            else if(!panelInfo.isHide)//加载结束 并且没有隐藏
            {
                callBack?.Invoke(panelInfo.panel);
            }
        }
    }


    /// <summary>
    /// 为控件添加自定义事件
    /// </summary>
    /// <param name="control">对应的控件</param>
    /// <param name="type">事件的类型</param>
    /// <param name="callBack">响应的函数</param>
    public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
    {
        //这种逻辑主要是用于保证 控件上只会挂载一个EventTrigger
        EventTrigger trigger = control.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = control.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry entry = new EventTrigger.Entry();
        entry.eventID = type;
        entry.callback.AddListener(callBack);

        trigger.triggers.Add(entry);
    }
}

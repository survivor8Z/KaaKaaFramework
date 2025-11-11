using UnityEngine;

/// <summary>
/// Item设置基类 - 提供更高层级的差异化功能
/// 例如：ItemSetting_Gun、ItemSetting_Weapon等
/// </summary>
public abstract class ItemSettingBase : MonoBehaviour
{
    protected Item Item
    {
        get
        {
            if (_item == null)
            {
                _item = GetComponent<Item>();
            }
            return _item;
        }
    }

    private Item _item;

    protected virtual void Awake()
    {
        if (Item != null)
        {
            SetMarkerParam(Item);
            OnInit();
        }
    }

    protected virtual void OnInit() { }
    protected virtual void Start() { }

    /// <summary>
    /// 设置标记参数 - 在Item的Variables中设置标记
    /// </summary>
    protected abstract void SetMarkerParam(Item item);
}


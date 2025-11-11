using UnityEngine;

/// <summary>
/// Item组件基类
/// </summary>
public abstract class ItemComponent : MonoBehaviour
{
    protected Item Master
    {
        get
        {
            if (_master == null)
            {
                _master = GetComponent<Item>();
            }
            return _master;
        }
    }

    private Item _master;

    protected virtual void Awake()
    {
        if (Master == null)
        {
            Debug.LogError($"{GetType().Name} 需要挂载在Item上！");
        }
    }

    protected virtual void OnInitialize() { }
}


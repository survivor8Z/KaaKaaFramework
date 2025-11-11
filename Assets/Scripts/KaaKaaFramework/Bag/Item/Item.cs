using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Item基类 - 所有物品的基础
/// 组合优于继承：通过组件系统实现功能
/// </summary>
public class Item : MonoBehaviour
{
    [Header("基础属性")]
    [SerializeField] private int typeID;
    [SerializeField] private string displayName;
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int maxStackCount = 1;
    [SerializeField] private int value;
    [SerializeField] private float weight;
    [SerializeField] private TagCollection tags = new TagCollection();

    [Header("组件系统")]
    [SerializeField] private UsageUtilities usageUtilities;
    [SerializeField] private SlotCollection slots;
    [SerializeField] private StatCollection stats;

    [Header("动态数据")]
    [SerializeField] private CustomDataCollection variables = new CustomDataCollection(); // 运行时数据（需要存档）
    [SerializeField] private CustomDataCollection constants = new CustomDataCollection(); // 常量数据（不需要存档）

    [Header("内部数据")]
    private int stackCount = 1;
    private Inventory inInventory;
    private Slot pluggedIntoSlot;

    // 属性访问器
    public int TypeID => typeID;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int MaxStackCount => maxStackCount;
    public bool Stackable => maxStackCount > 1;
    public int StackCount
    {
        get => stackCount;
        set => stackCount = Mathf.Clamp(value, 0, maxStackCount);
    }
    public int Value => value;
    public float Weight => weight;
    public TagCollection Tags => tags;
    public CustomDataCollection Variables => variables;
    public CustomDataCollection Constants => constants;
    public Inventory InInventory => inInventory;
    public Slot PluggedIntoSlot => pluggedIntoSlot;

    // 组件访问器
    public UsageUtilities UsageUtilities => usageUtilities ??= GetComponent<UsageUtilities>();
    public SlotCollection Slots => slots ??= GetComponent<SlotCollection>();
    public StatCollection Stats => stats ??= GetComponent<StatCollection>();

    // 便捷方法
    public bool IsUsable(object user = null)
    {
        return UsageUtilities != null && UsageUtilities.IsUsable(this, user);
    }

    public void Use(object user = null)
    {
        if (UsageUtilities != null)
        {
            UsageUtilities.Use(this, user);
        }
    }

    public float GetStatValue(string statKey)
    {
        return Stats != null ? Stats.GetStatValue(statKey) : 0f;
    }

    // 数据访问便捷方法
    public float GetFloat(string key, float defaultValue = 0f)
    {
        return Variables.GetFloat(key, defaultValue);
    }

    public void SetFloat(string key, float value)
    {
        Variables.SetFloat(key, value);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        return Variables.GetInt(key, defaultValue);
    }

    public void SetInt(string key, int value)
    {
        Variables.SetInt(key, value);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        return Variables.GetBool(key, defaultValue);
    }

    public void SetBool(string key, bool value, bool createIfNotExist = false)
    {
        Variables.SetBool(key, value, createIfNotExist);
    }

    public string GetString(string key, string defaultValue = null)
    {
        return Variables.GetString(key, defaultValue);
    }

    public void SetString(string key, string value)
    {
        Variables.SetString(key, value);
    }

    // 内部方法
    internal void NotifyPluggedTo(Slot slot)
    {
        pluggedIntoSlot = slot;
    }

    internal void NotifyUnpluggedFrom(Slot slot)
    {
        if (pluggedIntoSlot == slot)
        {
            pluggedIntoSlot = null;
        }
    }

    internal void SetInventory(Inventory inventory)
    {
        inInventory = inventory;
    }

    public bool Combine(Item other)
    {
        if (other == null || other.TypeID != TypeID || !Stackable)
            return false;

        int spaceLeft = MaxStackCount - StackCount;
        if (spaceLeft <= 0) return false;

        int transferAmount = Mathf.Min(spaceLeft, other.StackCount);
        StackCount += transferAmount;
        other.StackCount -= transferAmount;

        if (other.StackCount <= 0)
        {
            Destroy(other.gameObject);
        }

        return true;
    }
}


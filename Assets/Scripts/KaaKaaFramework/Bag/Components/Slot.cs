using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slot - 配件槽位
/// </summary>
[Serializable]
public class Slot
{
    [SerializeField] private string key;
    [SerializeField] private Sprite slotIcon;
    [SerializeField] private List<Tag> requireTags = new List<Tag>();
    [SerializeField] private List<Tag> excludeTags = new List<Tag>();
    [SerializeField] private bool forbidItemsWithSameID;

    private SlotCollection collection;
    private Item content;

    public string Key => key;
    public Sprite SlotIcon => slotIcon;
    public Item Content => content;
    public Item Master => collection?.MasterItem;

    public event Action<Slot> OnSlotContentChanged;

    public void Initialize(SlotCollection collection)
    {
        this.collection = collection;
    }

    public bool CanPlug(Item item)
    {
        if (item == null) return false;
        if (item == content) return false;

        // 检查Tag匹配
        if (!item.Tags.Check(requireTags, excludeTags))
        {
            return false;
        }

        // 检查是否禁止相同ID
        if (forbidItemsWithSameID && content != null && content.TypeID == item.TypeID)
        {
            return false;
        }

        return true;
    }

    public bool Plug(Item item, out Item unpluggedItem)
    {
        unpluggedItem = null;

        if (!CanPlug(item))
        {
            return false;
        }

        // 如果槽位已有物品，先取出
        if (content != null)
        {
            unpluggedItem = Unplug();
        }

        // 如果新物品在其他地方，先分离
        if (item.PluggedIntoSlot != null)
        {
            item.PluggedIntoSlot.Unplug();
        }

        if (item.InInventory != null)
        {
            item.InInventory.Remove(item);
        }

        // 安装新物品
        content = item;
        item.transform.SetParent(collection.transform);
        item.NotifyPluggedTo(this);

        OnSlotContentChanged?.Invoke(this);
        return true;
    }

    public Item Unplug()
    {
        Item item = content;
        content = null;

        if (item != null)
        {
            item.transform.SetParent(null);
            item.NotifyUnpluggedFrom(this);
            OnSlotContentChanged?.Invoke(this);
        }

        return item;
    }
}


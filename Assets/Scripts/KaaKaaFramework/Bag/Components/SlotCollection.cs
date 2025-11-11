using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Slot集合组件
/// </summary>
public class SlotCollection : ItemComponent
{
    [SerializeField] private List<Slot> slots = new List<Slot>();
    private Dictionary<int, Slot> slotsDictionary;
    private bool dirty = true;

    private Dictionary<int, Slot> SlotsDictionary
    {
        get
        {
            if (slotsDictionary == null || dirty)
            {
                BuildDictionary();
            }
            return slotsDictionary;
        }
    }

    private void BuildDictionary()
    {
        if (slotsDictionary == null)
        {
            slotsDictionary = new Dictionary<int, Slot>();
        }
        slotsDictionary.Clear();

        foreach (var slot in slots)
        {
            if (slot != null && !string.IsNullOrEmpty(slot.Key))
            {
                int hashCode = slot.Key.GetHashCode();
                slotsDictionary[hashCode] = slot;
            }
        }
        dirty = false;
    }

    public Slot GetSlot(string key)
    {
        int hashCode = key.GetHashCode();
        if (SlotsDictionary.TryGetValue(hashCode, out var slot))
        {
            return slot;
        }
        return slots.Find(s => s.Key == key);
    }

    public Slot GetSlot(int index)
    {
        if (index >= 0 && index < slots.Count)
        {
            return slots[index];
        }
        return null;
    }

    public int Count => slots.Count;
    
    /// <summary>
    /// 获取Master Item（供Slot使用）
    /// </summary>
    public Item MasterItem => Master;

    protected override void OnInitialize()
    {
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.Initialize(this);
            }
        }
    }
}


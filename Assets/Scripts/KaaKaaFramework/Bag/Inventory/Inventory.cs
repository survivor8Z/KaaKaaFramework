using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包系统
/// </summary>
public class Inventory : MonoBehaviour, IEnumerable<Item>
{
    [SerializeField] private int maxCapacity = 20;
    [SerializeField] private float maxWeight = 100f;
    [SerializeField] private List<Item> items = new List<Item>();

    public int MaxCapacity => maxCapacity;
    public float MaxWeight => maxWeight;
    public int Count => items.Count;
    public float CurrentWeight
    {
        get
        {
            float weight = 0f;
            foreach (var item in items)
            {
                if (item != null)
                {
                    weight += item.Weight * item.StackCount;
                }
            }
            return weight;
        }
    }

    public event Action<Item> OnItemAdded;
    public event Action<Item> OnItemRemoved;

    public bool Add(Item item)
    {
        if (item == null) return false;

        // 检查容量
        if (items.Count >= maxCapacity && !TryMerge(item))
        {
            return false;
        }

        // 检查重量
        if (CurrentWeight + item.Weight * item.StackCount > maxWeight)
        {
            return false;
        }

        // 尝试合并相同物品
        if (item.Stackable)
        {
            foreach (var existingItem in items)
            {
                if (existingItem != null && existingItem.TypeID == item.TypeID)
                {
                    if (existingItem.Combine(item))
                    {
                        if (item.StackCount <= 0)
                        {
                            Destroy(item.gameObject);
                        }
                        return true;
                    }
                }
            }
        }

        // 添加新物品
        items.Add(item);
        item.transform.SetParent(transform);
        item.SetInventory(this);
        OnItemAdded?.Invoke(item);
        return true;
    }

    public bool AddAndMerge(Item item, int index = 0)
    {
        if (item == null) return false;

        // 尝试合并
        if (item.Stackable)
        {
            foreach (var existingItem in items)
            {
                if (existingItem != null && existingItem.TypeID == item.TypeID)
                {
                    if (existingItem.Combine(item))
                    {
                        if (item.StackCount <= 0)
                        {
                            Destroy(item.gameObject);
                        }
                        return true;
                    }
                }
            }
        }

        // 如果无法合并，尝试添加
        return Add(item);
    }

    public bool Remove(Item item)
    {
        if (item == null) return false;
        bool removed = items.Remove(item);
        if (removed)
        {
            item.SetInventory(null);
            OnItemRemoved?.Invoke(item);
        }
        return removed;
    }

    public Item FindItem(int typeID)
    {
        return items.Find(item => item != null && item.TypeID == typeID);
    }

    public List<Item> FindItemsByTag(Tag tag)
    {
        return items.FindAll(item => item != null && item.Tags.Contains(tag));
    }

    private bool TryMerge(Item item)
    {
        if (!item.Stackable) return false;

        foreach (var existingItem in items)
        {
            if (existingItem != null && existingItem.TypeID == item.TypeID)
            {
                if (existingItem.StackCount < existingItem.MaxStackCount)
                {
                    return true; // 可以合并，不需要新槽位
                }
            }
        }
        return false;
    }

    public IEnumerator<Item> GetEnumerator()
    {
        return items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可使用组件 - 处理物品的使用逻辑
/// </summary>
public class UsageUtilities : ItemComponent
{
    [SerializeField] private float useTime = 1f;
    [SerializeField] private List<UsageBehavior> behaviors = new List<UsageBehavior>();
    [SerializeField] private bool useDurability;
    [SerializeField] private int durabilityUsage = 1;

    public float UseTime => useTime;
    public static event Action<Item> OnItemUsedStaticEvent;

    public bool IsUsable(Item item, object user)
    {
        if (item == null) return false;

        // 检查耐久度
        if (useDurability)
        {
            float durability = item.GetFloat("Durability", 100f);
            if (durability < durabilityUsage)
            {
                return false;
            }
        }

        // 检查是否有可用的行为
        foreach (var behavior in behaviors)
        {
            if (behavior != null && behavior.CanBeUsed(item, user))
            {
                return true;
            }
        }

        return false;
    }

    public void Use(Item item, object user)
    {
        foreach (var behavior in behaviors)
        {
            if (behavior != null && behavior.CanBeUsed(item, user))
            {
                behavior.Use(item, user);
            }
        }

        // 消耗耐久度
        if (useDurability)
        {
            float durability = item.GetFloat("Durability", 100f);
            durability -= durabilityUsage;
            item.SetFloat("Durability", Mathf.Max(0, durability));
        }

        OnItemUsedStaticEvent?.Invoke(item);
    }
}


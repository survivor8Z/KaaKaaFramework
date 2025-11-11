using UnityEngine;

/// <summary>
/// 治疗行为
/// </summary>
public class HealBehavior : UsageBehavior
{
    [SerializeField] private int healValue = 20;
    [SerializeField] private bool useDurability;
    [SerializeField] private float durabilityUsage = 1f;
    [SerializeField] private bool canUsePart; // 是否可以部分使用

    public override bool CanBeUsed(Item item, object user)
    {
        // 可以添加额外的检查逻辑
        return true;
    }

    public override void Use(Item item, object user)
    {
        // 尝试从user获取IHealth接口
        IHealth health = null;
        
        if (user is MonoBehaviour mono)
        {
            health = mono.GetComponent<IHealth>();
        }
        else if (user is IHealth healthInterface)
        {
            health = healthInterface;
        }

        if (health != null)
        {
            float healAmount = healValue;
            
            if (useDurability && item.GetBool("UseDurability", false))
            {
                float durability = item.GetFloat("Durability", 100f);
                if (canUsePart)
                {
                    float maxHeal = health.MaxHealth - health.CurrentHealth;
                    healAmount = Mathf.Min(healAmount, maxHeal);
                    float actualDurabilityUsage = healAmount / healValue * durabilityUsage;
                    if (actualDurabilityUsage > durability)
                    {
                        actualDurabilityUsage = durability;
                        healAmount = healValue * durability / durabilityUsage;
                    }
                    item.SetFloat("Durability", durability - actualDurabilityUsage);
                }
            }

            health.Heal(healAmount);
            Debug.Log($"治疗了 {healAmount} 点生命值");
        }
        else
        {
            Debug.LogWarning($"HealBehavior: user对象没有实现IHealth接口");
        }
    }
}


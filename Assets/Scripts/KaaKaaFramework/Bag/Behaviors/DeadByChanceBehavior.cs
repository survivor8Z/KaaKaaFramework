using UnityEngine;

/// <summary>
/// 概率死亡行为
/// </summary>
public class DeadByChanceBehavior : UsageBehavior
{
    [SerializeField] private float deathChance = 0.05f; // 5%概率

    public override bool CanBeUsed(Item item, object user)
    {
        return true;
    }

    public override void Use(Item item, object user)
    {
        if (Random.Range(0f, 1f) < deathChance)
        {
            Debug.Log("你死了！");
            // 触发死亡逻辑
        }
    }
}


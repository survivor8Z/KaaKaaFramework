using UnityEngine;

/// <summary>
/// 恢复饱食度行为
/// </summary>
public class RestoreHungerBehavior : UsageBehavior
{
    [SerializeField] private int hungerValue = 20;

    public override bool CanBeUsed(Item item, object user)
    {
        return true;
    }

    public override void Use(Item item, object user)
    {
        // 恢复饱食度逻辑
        Debug.Log($"恢复了 {hungerValue} 点饱食度");
    }
}


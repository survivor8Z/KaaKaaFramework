using UnityEngine;

/// <summary>
/// 使用行为基类
/// </summary>
public abstract class UsageBehavior : MonoBehaviour
{
    public abstract bool CanBeUsed(Item item, object user);
    public abstract void Use(Item item, object user);
}


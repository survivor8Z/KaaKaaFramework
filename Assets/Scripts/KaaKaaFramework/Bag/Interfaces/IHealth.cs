/// <summary>
/// 生命值接口 - 用于HealBehavior等组件
/// 需要在使用HealBehavior的GameObject上实现此接口
/// </summary>
public interface IHealth
{
    float CurrentHealth { get; }
    float MaxHealth { get; }
    void Heal(float amount);
}


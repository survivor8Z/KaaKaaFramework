using System;

/// <summary>
/// 按键绑定特性，用于标记数据结构中的字段对应的Action信息
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class KeyBindingAttribute : Attribute
{
    /// <summary>
    /// ActionMap名称
    /// </summary>
    public string ActionMapName { get; }
    
    /// <summary>
    /// Action名称（如果为空则使用字段名）
    /// </summary>
    public string ActionName { get; }
    
    /// <summary>
    /// 绑定索引（用于有多个绑定的Action，默认0）
    /// </summary>
    public int BindingIndex { get; }
    
    /// <summary>
    /// 显示名称（用于UI显示）
    /// </summary>
    public string DisplayName { get; }
    
    public KeyBindingAttribute(string actionMapName, string actionName = null, int bindingIndex = 0, string displayName = null)
    {
        ActionMapName = actionMapName;
        ActionName = actionName;
        BindingIndex = bindingIndex;
        DisplayName = displayName;
    }
}


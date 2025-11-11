using System;
using UnityEngine;

/// <summary>
/// Tag类 - 用于物品分类和匹配
/// </summary>
[Serializable]
public class Tag
{
    [SerializeField] private string key;
    [SerializeField] private string displayName;

    public string Key => key;
    public string DisplayName => displayName;

    public Tag(string key, string displayName = null)
    {
        this.key = key;
        this.displayName = displayName ?? key;
    }

    public override int GetHashCode()
    {
        return key?.GetHashCode() ?? 0;
    }

    public override bool Equals(object obj)
    {
        if (obj is Tag other)
        {
            return key == other.key;
        }
        return false;
    }
}


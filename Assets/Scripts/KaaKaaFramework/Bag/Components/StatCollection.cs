using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stat集合组件 - 用于存储物品的属性值
/// </summary>
public class StatCollection : ItemComponent
{
    [SerializeField] private List<Stat> stats = new List<Stat>();
    private Dictionary<int, Stat> statsDictionary;
    private bool dirty = true;

    private Dictionary<int, Stat> StatsDictionary
    {
        get
        {
            if (statsDictionary == null || dirty)
            {
                BuildDictionary();
            }
            return statsDictionary;
        }
    }

    private void BuildDictionary()
    {
        if (statsDictionary == null)
        {
            statsDictionary = new Dictionary<int, Stat>();
        }
        statsDictionary.Clear();

        foreach (var stat in stats)
        {
            if (stat != null && !string.IsNullOrEmpty(stat.Key))
            {
                int hashCode = stat.Key.GetHashCode();
                statsDictionary[hashCode] = stat;
            }
        }
        dirty = false;
    }

    public float GetStatValue(string key)
    {
        int hashCode = key.GetHashCode();
        if (StatsDictionary.TryGetValue(hashCode, out var stat))
        {
            return stat.Value;
        }
        return 0f;
    }

    public void SetStatValue(string key, float value)
    {
        int hashCode = key.GetHashCode();
        if (StatsDictionary.TryGetValue(hashCode, out var stat))
        {
            stat.Value = value;
        }
        else
        {
            var newStat = new Stat(key, value);
            stats.Add(newStat);
            dirty = true;
        }
    }
}

/// <summary>
/// Stat数据类
/// </summary>
[Serializable]
public class Stat
{
    [SerializeField] private string key;
    [SerializeField] private float value;

    public string Key => key;
    public float Value
    {
        get => value;
        set => this.value = value;
    }

    public Stat(string key, float value)
    {
        this.key = key;
        this.value = value;
    }
}


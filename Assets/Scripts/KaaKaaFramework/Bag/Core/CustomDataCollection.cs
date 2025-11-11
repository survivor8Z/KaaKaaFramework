using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 自定义数据集合 - 用于存储Item的动态数据
/// </summary>
[Serializable]
public class CustomDataCollection : ICollection<CustomData>, IEnumerable<CustomData>
{
    [SerializeField] private List<CustomData> dataList = new List<CustomData>();
    private Dictionary<int, CustomData> _dictionary;
    private bool dirty = true;

    private Dictionary<int, CustomData> Dictionary
    {
        get
        {
            if (_dictionary == null || dirty)
            {
                RebuildDictionary();
            }
            return _dictionary;
        }
    }

    private void RebuildDictionary()
    {
        if (_dictionary == null)
        {
            _dictionary = new Dictionary<int, CustomData>();
        }
        _dictionary.Clear();

        foreach (var customData in dataList)
        {
            if (customData != null && !string.IsNullOrEmpty(customData.Key))
            {
                int hashCode = customData.Key.GetHashCode();
                _dictionary[hashCode] = customData;
            }
        }
        dirty = false;
    }

    public CustomData GetData(string key)
    {
        int hashCode = key.GetHashCode();
        if (Dictionary.TryGetValue(hashCode, out var data))
        {
            return data;
        }
        return null;
    }

    public float GetFloat(string key, float defaultValue = 0f)
    {
        var data = GetData(key);
        return data != null ? data.GetFloat() : defaultValue;
    }

    public void SetFloat(string key, float value)
    {
        var data = GetData(key);
        if (data == null)
        {
            data = new CustomData(key, CustomDataType.Float);
            Add(data);
        }
        data.SetFloat(value);
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        var data = GetData(key);
        return data != null ? data.GetInt() : defaultValue;
    }

    public void SetInt(string key, int value)
    {
        var data = GetData(key);
        if (data == null)
        {
            data = new CustomData(key, CustomDataType.Int);
            Add(data);
        }
        data.SetInt(value);
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var data = GetData(key);
        return data != null ? data.GetBool() : defaultValue;
    }

    public void SetBool(string key, bool value, bool createIfNotExist = false)
    {
        var data = GetData(key);
        if (data == null)
        {
            if (createIfNotExist)
            {
                data = new CustomData(key, CustomDataType.Bool);
                Add(data);
            }
            else
            {
                return;
            }
        }
        data.SetBool(value);
    }

    public string GetString(string key, string defaultValue = null)
    {
        var data = GetData(key);
        return data != null ? data.GetString() : defaultValue ?? string.Empty;
    }

    public void SetString(string key, string value)
    {
        var data = GetData(key);
        if (data == null)
        {
            data = new CustomData(key, CustomDataType.String);
            Add(data);
        }
        data.SetString(value);
    }

    // ICollection实现
    public int Count => dataList.Count;
    public bool IsReadOnly => false;

    public void Add(CustomData item)
    {
        if (item == null) return;
        dataList.Add(item);
        dirty = true;
    }

    public void Clear()
    {
        dataList.Clear();
        if (_dictionary != null) _dictionary.Clear();
        dirty = true;
    }

    public bool Contains(CustomData item)
    {
        return dataList.Contains(item);
    }

    public void CopyTo(CustomData[] array, int arrayIndex)
    {
        dataList.CopyTo(array, arrayIndex);
    }

    public bool Remove(CustomData item)
    {
        bool result = dataList.Remove(item);
        if (result) dirty = true;
        return result;
    }

    public IEnumerator<CustomData> GetEnumerator()
    {
        return dataList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


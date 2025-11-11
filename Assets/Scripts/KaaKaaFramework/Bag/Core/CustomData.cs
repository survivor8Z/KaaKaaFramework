using System;
using UnityEngine;

/// <summary>
/// 自定义数据项
/// </summary>
[Serializable]
public class CustomData
{
    [SerializeField] private string key;
    [SerializeField] private CustomDataType dataType;
    [SerializeField] private byte[] data = new byte[0];
    [SerializeField] private bool display; // 是否在Inspector中显示

    public string Key => key;
    public CustomDataType DataType => dataType;

    private byte[] Data
    {
        get => data;
        set => data = value ?? new byte[0];
    }

    public float GetFloat()
    {
        if (dataType != CustomDataType.Float)
        {
            Debug.LogWarning($"尝试获取Float，但CustomData {key} 的类型是 {dataType}");
            return 0f;
        }
        try
        {
            return BitConverter.ToSingle(Data, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取Float失败: {ex.Message}");
            return 0f;
        }
    }

    public void SetFloat(float value)
    {
        if (dataType != CustomDataType.Float)
        {
            Debug.LogWarning($"设置Float值，但类型是 {dataType}！");
            return;
        }
        Data = BitConverter.GetBytes(value);
    }

    public int GetInt()
    {
        if (dataType != CustomDataType.Int)
        {
            Debug.LogWarning($"尝试获取Int，但CustomData {key} 的类型是 {dataType}");
            return 0;
        }
        try
        {
            return BitConverter.ToInt32(Data, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取Int失败: {ex.Message}");
            return 0;
        }
    }

    public void SetInt(int value)
    {
        if (dataType != CustomDataType.Int)
        {
            Debug.LogWarning($"设置Int值，但类型是 {dataType}！");
            return;
        }
        Data = BitConverter.GetBytes(value);
    }

    public bool GetBool()
    {
        if (dataType != CustomDataType.Bool)
        {
            Debug.LogWarning($"尝试获取Bool，但CustomData {key} 的类型是 {dataType}");
            return false;
        }
        try
        {
            return BitConverter.ToBoolean(Data, 0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取Bool失败: {ex.Message}");
            return false;
        }
    }

    public void SetBool(bool value)
    {
        if (dataType != CustomDataType.Bool)
        {
            Debug.LogWarning($"设置Bool值，但类型是 {dataType}！");
            return;
        }
        Data = BitConverter.GetBytes(value);
    }

    public string GetString()
    {
        if (dataType != CustomDataType.String)
        {
            Debug.LogWarning($"尝试获取String，但CustomData {key} 的类型是 {dataType}");
            return string.Empty;
        }
        try
        {
            return System.Text.Encoding.UTF8.GetString(Data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"获取String失败: {ex.Message}");
            return string.Empty;
        }
    }

    public void SetString(string value)
    {
        if (dataType != CustomDataType.String)
        {
            Debug.LogWarning($"设置String值，但类型是 {dataType}！");
            return;
        }
        Data = string.IsNullOrEmpty(value) ? new byte[0] : System.Text.Encoding.UTF8.GetBytes(value);
    }

    public CustomData(string key, CustomDataType dataType)
    {
        this.key = key;
        this.dataType = dataType;
        this.data = new byte[0];
    }
}


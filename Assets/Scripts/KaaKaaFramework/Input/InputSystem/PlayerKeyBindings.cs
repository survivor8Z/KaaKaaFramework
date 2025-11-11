using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PlayerKeyBindings
/// 自动生成的按键绑定数据类
/// 根据 InputActionAsset 自动生成，请勿手动修改
/// </summary>
[Serializable]
public class PlayerKeyBindings : IKeyBindingData
{
    [KeyBinding("Player", "Move", 0, "Move-W")]
    public string MoveW = "";

    [KeyBinding("Player", "Move", 1, "Move-UpArrow")]
    public string MoveUpArrow = "";

    [KeyBinding("Player", "Move", 2, "Move-S")]
    public string MoveS = "";

    [KeyBinding("Player", "Move", 3, "Move-DownArrow")]
    public string MoveDownArrow = "";

    [KeyBinding("Player", "Move", 4, "Move-A")]
    public string MoveA = "";

    [KeyBinding("Player", "Move", 5, "Move-LeftArrow")]
    public string MoveLeftArrow = "";

    [KeyBinding("Player", "Move", 6, "Move-D")]
    public string MoveD = "";

    [KeyBinding("Player", "Move", 7, "Move-RightArrow")]
    public string MoveRightArrow = "";

    [KeyBinding("Player", "Look", 0, "Look")]
    public string Look = "";

    [KeyBinding("Player", "Fire", 0, "Fire")]
    public string Fire = "";

    [KeyBinding("UI", "Navigate", 0, "Navigate-W")]
    public string NavigateW = "";

    [KeyBinding("UI", "Navigate", 1, "Navigate-UpArrow")]
    public string NavigateUpArrow = "";

    [KeyBinding("UI", "Navigate", 2, "Navigate-S")]
    public string NavigateS = "";

    [KeyBinding("UI", "Navigate", 3, "Navigate-DownArrow")]
    public string NavigateDownArrow = "";

    [KeyBinding("UI", "Navigate", 4, "Navigate-A")]
    public string NavigateA = "";

    [KeyBinding("UI", "Navigate", 5, "Navigate-LeftArrow")]
    public string NavigateLeftArrow = "";

    [KeyBinding("UI", "Navigate", 6, "Navigate-D")]
    public string NavigateD = "";

    [KeyBinding("UI", "Navigate", 7, "Navigate-RightArrow")]
    public string NavigateRightArrow = "";

    [KeyBinding("UI", "Submit", 0, "Submit")]
    public string Submit = "";

    [KeyBinding("UI", "Cancel", 0, "Cancel")]
    public string Cancel = "";

    [KeyBinding("UI", "Point", 0, "Point-Position")]
    public string PointPosition = "";

    [KeyBinding("UI", "Point", 1, "Point-Position")]
    public string PointPosition1 = "";

    [KeyBinding("UI", "Click", 0, "Click-LeftButton")]
    public string ClickLeftButton = "";

    [KeyBinding("UI", "Click", 1, "Click-Tip")]
    public string ClickTip = "";

    [KeyBinding("UI", "ScrollWheel", 0, "ScrollWheel")]
    public string ScrollWheel = "";

    [KeyBinding("UI", "MiddleClick", 0, "MiddleClick")]
    public string MiddleClick = "";

    [KeyBinding("UI", "RightClick", 0, "RightClick")]
    public string RightClick = "";

    // 实现接口方法
    private Dictionary<string, string> bindings = new Dictionary<string, string>();

    public string GetBindingPath(string fieldName)
    {
        if (bindings.ContainsKey(fieldName))
            return bindings[fieldName];

        // 使用反射获取字段值（作为默认值）
        var field = GetType().GetField(fieldName);
        if (field != null && field.FieldType == typeof(string))
        {
            string value = (string)field.GetValue(this);
            if (!string.IsNullOrEmpty(value))
            {
                bindings[fieldName] = value;
                return value;
            }
        }

        return "";
    }

    public void SetBindingPath(string fieldName, string bindingPath)
    {
        bindings[fieldName] = bindingPath;

        // 同时更新字段值
        var field = GetType().GetField(fieldName);
        if (field != null && field.FieldType == typeof(string))
        {
            field.SetValue(this, bindingPath);
        }
    }
}

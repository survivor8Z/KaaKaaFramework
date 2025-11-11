/// <summary>
/// 按键绑定数据接口
/// 所有按键配置数据结构都应实现此接口
/// </summary>
public interface IKeyBindingData
{
    /// <summary>
    /// 获取字段名对应的按键绑定路径
    /// </summary>
    string GetBindingPath(string fieldName);
    
    /// <summary>
    /// 设置字段名对应的按键绑定路径
    /// </summary>
    void SetBindingPath(string fieldName, string bindingPath);
}


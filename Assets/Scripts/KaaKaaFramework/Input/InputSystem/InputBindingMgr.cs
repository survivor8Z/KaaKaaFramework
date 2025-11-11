using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// 通用InputSystem管理器，支持数据驱动的改键系统
/// 只需定义一个数据结构类，系统自动处理所有改键逻辑
/// </summary>
public class InputBindingMgr : BaseManager<InputBindingMgr>
{
    private InputActionAsset inputActionAsset;
    
    private struct RebindingInfo
    {
        public string fieldName;
        public string actionMapName;
        public string actionName;
        public int bindingIndex;
        public Action<bool, string> callback;
        public IKeyBindingData bindingData;
    }
    
    private RebindingInfo? currentRebinding;
    private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
    
    private Dictionary<string, (string actionMapName, string actionName, int bindingIndex)> fieldActionMap = new Dictionary<string, (string, string, int)>();
    private Dictionary<string, string> defaultBindings = new Dictionary<string, string>();
    
    private InputBindingMgr() { }
    
    /// <summary>
    /// 初始化InputActionAsset
    /// </summary>
    public void Init(InputActionAsset asset)
    {
        if (asset == null)
        {
            Debug.LogError("InputActionAsset不能为空！");
            return;
        }
        
        inputActionAsset = asset;
        
        foreach (var actionMap in inputActionAsset.actionMaps)
        {
            actionMap.Enable();
        }
    }
    
    /// <summary>
    /// 获取当前管理的 InputActionAsset（用于其他脚本直接使用）
    /// </summary>
    public InputActionAsset GetInputActionAsset()
    {
        return inputActionAsset;
    }
    
    /// <summary>
    /// 获取指定的 Action（用于直接使用 Action）
    /// </summary>
    public InputAction GetActionDirect(string actionMapName, string actionName)
    {
        return GetAction(actionMapName, actionName);
    }
    
    /// <summary>
    /// 初始化按键绑定数据（解析特性，建立映射）
    /// 这个方法会扫描数据结构中所有带 [KeyBinding] 特性的字段，建立字段名到Action的映射关系
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="bindingData">绑定数据实例</param>
    /// <returns>是否初始化成功</returns>
    public bool InitBindingData<T>(T bindingData) where T : class, IKeyBindingData
    {
        // 步骤1：参数验证 - 检查绑定数据是否为空
        if (bindingData == null)
        {
            Debug.LogError("绑定数据不能为空！");
            return false;
        }

        // 步骤2：状态验证 - 检查InputActionAsset是否已初始化
        // 必须先调用 Init() 方法初始化 InputActionAsset，才能建立映射关系
        if (inputActionAsset == null)
        {
            Debug.LogWarning("请先初始化InputActionAsset！");
            return false;
        }

        // 步骤3：清空之前的映射表和默认绑定缓存
        // 如果多次调用此方法，需要先清空之前的数据
        fieldActionMap.Clear();  // 清空字段名到Action信息的映射表
        defaultBindings.Clear();  // 清空默认绑定路径缓存

        // 步骤4：获取数据类型的反射信息
        // 使用反射获取类型信息，用于扫描字段
        Type type = typeof(T);
        // 获取所有公共实例字段（public字段）
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        
        // 步骤5：遍历所有字段，查找带 [KeyBinding] 特性的字段
        int validCount = 0;  // 记录有效字段数量
        foreach (var field in fields)
        {
            // 步骤5.1：获取字段上的 KeyBindingAttribute 特性
            // 如果没有特性，说明这个字段不需要绑定，跳过
            var attribute = field.GetCustomAttribute<KeyBindingAttribute>();
            if (attribute == null)
                continue;  // 跳过没有特性的字段
            
            // 步骤5.2：提取字段信息和特性信息
            string fieldName = field.Name;  // 字段名（如 "Move", "Fire"）
            string actionMapName = attribute.ActionMapName;  // ActionMap名称（如 "Player"）
            // Action名称：如果特性中指定了就用指定的，否则使用字段名
            string actionName = attribute.ActionName ?? fieldName;
            int bindingIndex = attribute.BindingIndex;  // 绑定索引（过滤后列表的索引，从0开始）
            
            // 步骤5.3：验证Action是否存在
            // 根据ActionMap名称和Action名称查找对应的Action
            var action = GetAction(actionMapName, actionName);
            if (action == null)
            {
                // Action不存在，可能是InputActionAsset中未配置，记录警告并跳过
                Debug.LogWarning($"字段 {fieldName} 对应的Action不存在: {actionMapName}/{actionName}");
                continue;  // 跳过无效字段
            }

            // 步骤5.4：获取过滤后的键鼠绑定列表
            // GetKeyboardMouseBindings 会排除复合绑定本身和非键鼠绑定
            // 返回的列表只包含可以改键的键鼠绑定
            var bindings = GetKeyboardMouseBindings(actionMapName, actionName);
            // 步骤5.5：验证绑定索引是否在有效范围内
            // bindingIndex 必须小于过滤后列表的长度
            if (bindingIndex >= bindings.Count)
            {
                // 索引超出范围，可能是特性中配置的索引错误
                Debug.LogWarning($"字段 {fieldName} 的绑定索引 {bindingIndex} 超出范围 (总数: {bindings.Count})");
                continue;  // 跳过无效字段
            }
            
            // 步骤5.6：建立映射关系并缓存
            // 将字段名映射到 (ActionMap名称, Action名称, 绑定索引) 的元组
            // 这个映射表用于后续改键时快速查找对应的Action信息
            fieldActionMap[fieldName] = (actionMapName, actionName, bindingIndex);
            
            // 步骤5.7：缓存默认绑定路径
            // 从过滤后的绑定列表中获取默认绑定路径（InputActionAsset中配置的原始路径）
            // 这个默认值用于重置功能
            string defaultPath = bindings[bindingIndex].path;
            if (!string.IsNullOrEmpty(defaultPath))
            {
                // 将默认路径缓存到字典中，键为字段名
                defaultBindings[fieldName] = defaultPath;
            }
            
            validCount++;  // 增加有效字段计数
        }
        
        // 步骤6：验证是否至少有一个有效字段
        // 如果没有找到任何有效字段，说明配置有问题
        if (validCount == 0)
        {
            Debug.LogError("没有找到有效的按键绑定配置！请检查特性标记和InputActionAsset配置");
            return false;  // 初始化失败
        }

        // 步骤7：初始化成功
        return true;
    }
    
    /// <summary>
    /// 加载并应用按键绑定
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="bindingData">绑定数据实例</param>
    /// <returns>是否加载成功</returns>
    public bool LoadBindings<T>(T bindingData) where T : class, IKeyBindingData, new()
    {
        if (bindingData == null)
        {
            Debug.LogError("绑定数据不能为空！");
            return false;
        }

        if (fieldActionMap.Count == 0)
        {
            Debug.LogWarning("请先调用InitBindingData初始化映射表！");
            return false;
        }

        try
        {
            // 从SaveMgr加载数据
            T savedData = SaveMgr.Instance.LoadData<T>("KeyBindings");
            if (savedData != null)
            {
                // 复制保存的数据到当前实例
                CopyBindingData(savedData, bindingData);
            }
            
            // 应用绑定
            ApplyBindings(bindingData);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"加载按键绑定失败: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 保存按键绑定
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="bindingData">绑定数据实例</param>
    /// <returns>是否保存成功</returns>
    public bool SaveBindings<T>(T bindingData) where T : class, IKeyBindingData
    {
        if (bindingData == null)
        {
            Debug.LogError("绑定数据不能为空！");
            return false;
        }

        try
        {
            SaveMgr.Instance.SaveData(bindingData, "KeyBindings");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"保存按键绑定失败: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 应用所有绑定到InputActionAsset
    /// 遍历映射表中的所有字段，从绑定数据中读取键位信息，然后应用到对应的Action上
    /// </summary>
    public void ApplyBindings<T>(T bindingData) where T : class, IKeyBindingData
    {
        // 步骤1：验证InputActionAsset是否已初始化
        if (inputActionAsset == null)
        {
            Debug.LogWarning("InputActionAsset未初始化！");
            return;  // 未初始化，无法应用绑定
        }
        
        // 步骤2：遍历映射表中的所有字段
        // fieldActionMap 存储了所有字段名到Action信息的映射关系
        foreach (var kvp in fieldActionMap)
        {
            // 步骤2.1：提取字段名和映射信息
            string fieldName = kvp.Key;  // 字段名（如 "Move"）
            // 解构元组，获取ActionMap名称、Action名称和绑定索引
            var (actionMapName, actionName, bindingIndex) = kvp.Value;
            
            // 步骤2.2：从绑定数据中读取该字段对应的绑定路径
            // 绑定路径格式如 "<Keyboard>/w" 或 "<Mouse>/leftButton"
            string bindingPath = bindingData.GetBindingPath(fieldName);
            // 如果绑定路径为空，说明该字段没有配置绑定，跳过
            if (string.IsNullOrEmpty(bindingPath))
                continue;  // 跳过空绑定
            
            // 步骤2.3：根据ActionMap名称和Action名称查找对应的Action
            var action = GetAction(actionMapName, actionName);
            if (action == null)
                continue;  // Action不存在，跳过
            
            // 步骤2.4：获取过滤后的键鼠绑定列表
            // 这个列表用于通过 bindingIndex 找到对应的绑定
            var bindings = GetKeyboardMouseBindings(actionMapName, actionName);
            // 验证绑定索引是否在有效范围内
            if (bindingIndex >= bindings.Count)
                continue;  // 索引超出范围，跳过
            
            // 步骤2.5：索引转换 - 从 bindingIndex 转换为 originalIndex
            // bindingIndex 是过滤后列表的索引，originalIndex 是原始 action.bindings 的索引
            // 需要通过 id 匹配来找到正确的 originalIndex
            
            // 从过滤后的列表中获取目标绑定的唯一标识符
            Guid targetBindingId = bindings[bindingIndex].id;
            
            // 在原始 action.bindings 列表中查找具有相同 id 的绑定
            int originalIndex = -1;  // 初始化为-1，表示未找到
            for (int i = 0; i < action.bindings.Count; i++)
            {
                // 通过 id 匹配找到对应的绑定
                if (action.bindings[i].id == targetBindingId)
                {
                    originalIndex = i;  // 找到原始索引
                    break;  // 找到后立即退出循环
                }
            }
            
            // 步骤2.6：应用绑定覆盖
            // 如果找到了有效的 originalIndex，则应用绑定覆盖
            if (originalIndex >= 0)
            {
                try
                {
                    // 使用原始索引和新的绑定路径应用覆盖
                    // 这会修改 InputActionAsset 中的绑定，使其使用新的键位
                    action.ApplyBindingOverride(originalIndex, bindingPath);
                }
                catch (Exception e)
                {
                    // 如果应用失败（如绑定路径格式错误），记录错误但不中断流程
                    Debug.LogError($"[应用绑定] 应用 {fieldName} 失败: {e.Message}");
                }
            }
            // 注意：如果 originalIndex < 0，说明未找到对应的绑定，静默跳过
        }
    }
    
    /// <summary>
    /// 开始改键（简化版，推荐使用）
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="fieldName">数据结构中的字段名</param>
    /// <param name="bindingData">绑定数据实例</param>
    /// <param name="onComplete">完成回调，返回详细结果信息</param>
    public void StartRebinding<T>(string fieldName, T bindingData, Action<RebindingResult> onComplete) where T : class, IKeyBindingData
    {
        StartRebinding(fieldName, bindingData, (success, path) =>
        {
            var result = new RebindingResult
            {
                Success = success,
                NewPath = path
            };
            
            if (success)
            {
                result.DisplayName = GetKeyDisplayName(path);
            }
            else
            {
                result.ErrorMessage = "改键被取消";
            }
            
            onComplete?.Invoke(result);
        });
    }

    /// <summary>
    /// 开始改键（使用字段名）
    /// 这是改键操作的核心方法，会启动交互式改键流程
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="fieldName">数据结构中的字段名（如 "Move", "Fire"）</param>
    /// <param name="bindingData">绑定数据实例（如 PlayerKeyBindings）</param>
    /// <param name="onComplete">完成回调（成功标志，新按键路径）</param>
    public void StartRebinding<T>(string fieldName, T bindingData, Action<bool, string> onComplete) where T : class, IKeyBindingData
    {
        // 步骤1：参数验证 - 检查绑定数据是否为空
        // 绑定数据不能为空，否则无法保存改键结果
        if (bindingData == null)
        {
            Debug.LogError("绑定数据不能为空！");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 参数无效，退出
        }

        // 步骤2：参数验证 - 检查字段名是否为空
        // 字段名用于查找映射关系，不能为空
        if (string.IsNullOrEmpty(fieldName))
        {
            Debug.LogError("字段名不能为空！");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 参数无效，退出
        }

        // 步骤3：验证字段名是否在映射表中
        // fieldActionMap 存储了字段名到Action信息的映射关系
        // 如果字段名不在映射表中，说明未调用 InitBindingData 或字段配置错误
        if (!fieldActionMap.ContainsKey(fieldName))
        {
            Debug.LogError($"字段 {fieldName} 没有对应的Action配置！请先调用InitBindingData");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 映射不存在，退出
        }
        
        // 步骤3.1：从映射表中获取Action信息
        // 解构元组，获取ActionMap名称、Action名称和绑定索引
        // bindingIndex 是过滤后列表的索引，不是原始列表的索引
        var (actionMapName, actionName, bindingIndex) = fieldActionMap[fieldName];
        
        // 步骤3.2：根据ActionMap名称和Action名称查找对应的Action
        // Action 是 Unity Input System 中的输入动作对象
        var action = GetAction(actionMapName, actionName);
        // 如果Action不存在，可能是InputActionAsset配置错误
        if (action == null)
        {
            Debug.LogError($"找不到Action: {actionMapName}/{actionName}");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // Action不存在，退出
        }
        
        // 步骤4：获取过滤后的键鼠绑定列表
        // GetKeyboardMouseBindings 会排除复合绑定本身和非键鼠绑定，只返回可改键的绑定
        var bindings = GetKeyboardMouseBindings(actionMapName, actionName);
        // 步骤4.1：验证绑定索引是否在有效范围内
        // bindingIndex 必须小于过滤后列表的长度
        if (bindingIndex >= bindings.Count)
        {
            Debug.LogError($"绑定索引超出范围: {bindingIndex} (总数: {bindings.Count})");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 索引无效，退出
        }

        // 步骤5：从过滤后的列表中获取目标绑定
        // 使用 bindingIndex 从过滤后的列表中获取对应的绑定对象
        InputBinding targetBinding = bindings[bindingIndex];
        // 步骤5.1：获取目标绑定的唯一标识符（Guid）
        // 这个 id 用于在原始 action.bindings 列表中查找对应的绑定
        Guid targetBindingId = targetBinding.id;

        // 步骤6：防御性检查 - 验证目标绑定不是复合绑定
        // 虽然 GetKeyboardMouseBindings 已经排除了复合绑定，但为了安全起见再次检查
        // 如果目标绑定是复合绑定，无法直接改键（复合绑定本身不能改键，只能改其子绑定）
        if (targetBinding.isComposite)
        {
            Debug.LogError($"无法改键：字段 {fieldName} 对应的是复合绑定，不能直接改键");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 是复合绑定，无法改键，退出
        }

        // 步骤7：索引转换 - 从 bindingIndex 转换为 originalIndex
        // bindingIndex 是过滤后列表的索引，但 Unity 的 ApplyBindingOverride 需要原始列表的索引
        // 因此需要通过 id 匹配来找到在 action.bindings 原始列表中的索引
        
        // 步骤7.1：在原始 action.bindings 列表中查找具有相同 id 的绑定
        int originalIndex = -1;  // 初始化为-1，表示未找到
        // 遍历原始列表中的所有绑定
        for (int i = 0; i < action.bindings.Count; i++)
        {
            // 通过 id 匹配找到对应的绑定
            // id 是绑定的唯一标识符，不受过滤影响
            if (action.bindings[i].id == targetBindingId)
            {
                originalIndex = i;  // 找到原始索引，赋值并退出循环
                break;  // 找到后立即退出循环
            }
        }

        // 步骤7.2：验证是否找到了有效的 originalIndex
        // 如果 originalIndex < 0，说明未找到对应的绑定（不应该发生，但防御性检查）
        // 如果找到的绑定是复合绑定，也不能改键（双重检查）
        if (originalIndex < 0 || action.bindings[originalIndex].isComposite)
        {
            Debug.LogError($"找不到对应的绑定索引或绑定是复合绑定: {fieldName}");
            onComplete?.Invoke(false, null);  // 调用失败回调
            return;  // 未找到或无效，退出
        }
        
        // 步骤8：取消之前的改键操作（如果存在）
        // 如果之前有未完成的改键操作，需要先取消并释放资源
        // 这样可以确保同时只有一个改键操作在进行
        if (rebindingOperation != null)
        {
            try
            {
                // 取消之前的改键操作
                rebindingOperation.Cancel();
                // 释放改键操作资源
                rebindingOperation.Dispose();
            }
            catch (Exception e)
            {
                // 如果取消时发生错误，记录警告但不中断流程
                Debug.LogWarning($"取消之前的改键操作时发生错误: {e.Message}");
            }
            rebindingOperation = null;  // 清空引用
        }
        
        // 步骤9：改键前必须禁用 Action
        // 在改键过程中，Action 必须处于禁用状态，避免输入冲突
        // 记录 Action 的原始启用状态，以便改键完成后恢复
        bool wasEnabled = action.enabled;  // 记录是否原本是启用的
        if (wasEnabled)
        {
            action.Disable();  // 禁用 Action，避免改键时触发输入事件
        }
        
        // 步骤10：保存当前改键操作的信息
        // 将改键相关信息保存到结构体中，用于回调时使用
        currentRebinding = new RebindingInfo
        {
            fieldName = fieldName,           // 字段名
            actionMapName = actionMapName,   // ActionMap名称
            actionName = actionName,         // Action名称
            bindingIndex = bindingIndex,    // 绑定索引（过滤后列表的索引）
            callback = onComplete,           // 完成回调
            bindingData = bindingData        // 绑定数据实例
        };
        
        // 步骤11：开始交互式改键操作
        // 使用 Unity Input System 的 PerformInteractiveRebinding 方法启动改键
        try
        {
            // 创建改键操作，使用 originalIndex（原始列表的索引）
            rebindingOperation = action.PerformInteractiveRebinding(originalIndex)
                // 步骤11.1：设置取消键为 ESC
                // 用户按 ESC 键可以取消改键操作
                .WithCancelingThrough("<Keyboard>/escape")
                // 步骤11.2：设置等待组合键的时间
                // 如果用户按下组合键（如 Ctrl+W），等待 0.1 秒确认是否还有更多按键
                .OnMatchWaitForAnother(0f)
                // 步骤11.3：设置取消回调
                // 当用户按 ESC 或改键操作被取消时调用
                .OnCancel(operation =>
                {
                    try
                    {
                        // 步骤11.3.1：恢复 Action 的启用状态
                        // 如果 Action 原本是启用的，改键取消后需要重新启用
                        var actionToEnable = GetAction(actionMapName, actionName);
                        if (wasEnabled && actionToEnable != null)
                        {
                            actionToEnable.Enable();  // 重新启用 Action
                        }
                        // 步骤11.3.2：释放改键操作资源
                        if (operation != null)
                        {
                            operation.Dispose();  // 释放资源
                        }
                        rebindingOperation = null;  // 清空引用
                        // 步骤11.3.3：调用失败回调，通知调用者改键已取消
                        currentRebinding?.callback?.Invoke(false, null);
                        currentRebinding = null;  // 清空当前改键信息
                    }
                    catch (Exception ex)
                    {
                        // 如果取消回调中发生错误，记录错误并清理资源
                        Debug.LogError($"改键取消回调中发生错误: {ex.Message}");
                        rebindingOperation = null;
                        currentRebinding = null;
                    }
                })
                // 步骤11.4：设置完成回调
                // 当用户按下新键完成改键操作时调用
                .OnComplete(operation =>
                {
                    try
                    {
                        // 步骤11.4.1：验证改键操作对象是否有效
                        // 检查 operation 和 operation.action 是否为 null
                        if (operation == null || operation.action == null)
                        {
                            // 如果无效，恢复 Action 状态并调用失败回调
                            var actionToEnable = GetAction(actionMapName, actionName);
                            if (wasEnabled && actionToEnable != null)
                            {
                                actionToEnable.Enable();  // 重新启用 Action
                            }
                            currentRebinding?.callback?.Invoke(false, null);  // 调用失败回调
                            currentRebinding = null;  // 清空当前改键信息
                            return;  // 退出回调
                        }
                        
                        // 步骤11.4.2：验证 originalIndex 是否在有效范围内
                        // originalIndex 必须 >= 0 且 < action.bindings.Count
                        if (originalIndex < 0 || originalIndex >= operation.action.bindings.Count)
                        {
                            // 如果索引无效，恢复 Action 状态并调用失败回调
                            var actionToEnable = GetAction(actionMapName, actionName);
                            if (wasEnabled && actionToEnable != null)
                            {
                                actionToEnable.Enable();  // 重新启用 Action
                            }
                            if (operation != null)
                            {
                                operation.Dispose();  // 释放资源
                            }
                            rebindingOperation = null;  // 清空引用
                            currentRebinding?.callback?.Invoke(false, null);  // 调用失败回调
                            currentRebinding = null;  // 清空当前改键信息
                            return;  // 退出回调
                        }
                        
                        // 步骤11.4.3：获取新的绑定路径
                        // effectivePath 是实际生效的绑定路径（包括改键后的新路径）
                        // 优先使用 effectivePath，因为它包含用户改键后的新路径
                        string newPath = operation.action.bindings[originalIndex].effectivePath;
                        
                        // 步骤11.4.4：验证新路径是否有效
                        // 如果新路径为空，说明改键失败或无效
                        if (string.IsNullOrEmpty(newPath))
                        {
                            // 如果路径为空，恢复 Action 状态并调用失败回调
                            var actionToEnable = GetAction(actionMapName, actionName);
                            if (wasEnabled && actionToEnable != null)
                            {
                                actionToEnable.Enable();  // 重新启用 Action
                            }
                            if (operation != null)
                            {
                                operation.Dispose();  // 释放资源
                            }
                            rebindingOperation = null;  // 清空引用
                            currentRebinding?.callback?.Invoke(false, null);  // 调用失败回调
                            currentRebinding = null;  // 清空当前改键信息
                            return;  // 退出回调
                        }
                        
                        // 步骤11.4.5：更新绑定数据
                        // 如果绑定数据不为空，更新数据并应用绑定
                        if (bindingData != null)
                        {
                            // 步骤11.4.5.1：将新路径保存到绑定数据中
                            // 这会更新 PlayerKeyBindings 中的对应字段
                            bindingData.SetBindingPath(fieldName, newPath);
                            
                            // 步骤11.4.5.2：恢复 Action 的启用状态
                            // 改键完成后，如果 Action 原本是启用的，需要重新启用
                            var actionToEnable = GetAction(actionMapName, actionName);
                            if (wasEnabled && actionToEnable != null)
                            {
                                actionToEnable.Enable();  // 重新启用 Action
                            }
                            
                            // 步骤11.4.5.3：应用绑定覆盖到 InputActionAsset
                            // 使用 originalIndex 和 newPath 应用覆盖
                            // 这会修改 InputActionAsset 中的绑定，使其使用新的键位
                            var actionToApply = GetAction(actionMapName, actionName);
                            if (actionToApply != null)
                            {
                                try
                                {
                                    // 应用绑定覆盖，使用原始索引和新路径
                                    actionToApply.ApplyBindingOverride(originalIndex, newPath);
                                }
                                catch (Exception ex)
                                {
                                    // 如果应用失败（如路径格式错误），记录错误但不中断流程
                                    Debug.LogError($"应用绑定覆盖失败: {ex.Message}");
                                }
                            }
                            
                            // 步骤11.4.5.4：保存绑定数据到本地
                            // 将更新后的绑定数据保存到 SaveMgr，以便下次加载
                            SaveBindings(bindingData);
                            // 步骤11.4.5.5：重新应用所有绑定
                            // 确保所有绑定都已正确应用（虽然上面已经应用了，但为了确保一致性）
                            ApplyBindings(bindingData);
                        }
                        
                        // 步骤11.4.6：清理资源
                        // 释放改键操作资源
                        if (operation != null)
                        {
                            operation.Dispose();  // 释放资源
                        }
                        rebindingOperation = null;  // 清空引用
                        
                        // 步骤11.4.7：调用成功回调
                        // 通知调用者改键成功，并传递新的绑定路径
                        if (currentRebinding.HasValue)
                        {
                            var rebinding = currentRebinding.Value;
                            rebinding.callback?.Invoke(true, newPath);  // 调用成功回调，传递新路径
                        }
                        currentRebinding = null;  // 清空当前改键信息
                    }
                    catch (Exception e)
                    {
                        // 步骤11.4.8：异常处理
                        // 如果改键完成过程中发生任何异常，记录错误并清理资源
                        Debug.LogError($"改键完成时发生错误: {e.Message}");
                        // 恢复 Action 状态
                        var actionToEnable = GetAction(actionMapName, actionName);
                        if (wasEnabled && actionToEnable != null)
                        {
                            actionToEnable.Enable();  // 重新启用 Action
                        }
                        // 释放资源
                        try
                        {
                            if (operation != null)
                            {
                                operation.Dispose();  // 释放资源
                            }
                        }
                        catch { }  // 如果释放失败，忽略错误
                        rebindingOperation = null;  // 清空引用
                        // 调用失败回调
                        currentRebinding?.callback?.Invoke(false, null);
                        currentRebinding = null;  // 清空当前改键信息
                    }
                })
                .Start();
        }
        catch (Exception e)
        {
            Debug.LogError($"启动改键操作失败: {e.Message}");
            if (wasEnabled)
            {
                var actionToEnable = GetAction(actionMapName, actionName);
                if (actionToEnable != null)
                {
                    actionToEnable.Enable();
                }
            }
            rebindingOperation = null;
            currentRebinding = null;
            onComplete?.Invoke(false, null);
        }
    }
    
    /// <summary>
    /// 取消改键
    /// </summary>
    public void CancelRebinding()
    {
        if (rebindingOperation != null)
        {
            rebindingOperation.Cancel();
            rebindingOperation.Dispose();
            rebindingOperation = null;
            
            // 重新启用被禁用的 Action
            if (currentRebinding.HasValue)
            {
                var rebinding = currentRebinding.Value;
                var action = GetAction(rebinding.actionMapName, rebinding.actionName);
                if (action != null && !action.enabled)
                {
                    action.Enable();
                }
            }
            
            currentRebinding = null;
        }
    }
    
    /// <summary>
    /// 获取按键显示名称（使用字段名）
    /// </summary>
    public string GetBindingDisplayString(string fieldName)
    {
        if (!fieldActionMap.ContainsKey(fieldName))
            return "未绑定";
        
        var (actionMapName, actionName, bindingIndex) = fieldActionMap[fieldName];
        return GetBindingDisplayString(actionMapName, actionName, bindingIndex);
    }
    
    /// <summary>
    /// 重置所有绑定到默认值
    /// </summary>
    /// <typeparam name="T">按键绑定数据类型</typeparam>
    /// <param name="bindingData">绑定数据实例</param>
    /// <returns>是否重置成功</returns>
    public bool ResetAllBindings<T>(T bindingData) where T : class, IKeyBindingData
    {
        if (bindingData == null)
        {
            Debug.LogError("绑定数据不能为空！");
            return false;
        }

        if (inputActionAsset == null)
        {
            Debug.LogWarning("InputActionAsset未初始化！");
            return false;
        }
        
        try
        {
            // 移除所有覆盖
            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                foreach (var action in actionMap.actions)
                {
                    action.RemoveAllBindingOverrides();
                }
            }
            
            // 重置数据结构到默认值
            foreach (var fieldName in fieldActionMap.Keys)
            {
                // 恢复到缓存的默认值（如果有），否则设为空
                string defaultPath = defaultBindings.ContainsKey(fieldName) ? defaultBindings[fieldName] : "";
                bindingData.SetBindingPath(fieldName, defaultPath);
            }
            
            SaveBindings(bindingData);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"重置按键绑定失败: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 改键结果信息
    /// </summary>
    public struct RebindingResult
    {
        public bool Success;
        public string NewPath;
        public string DisplayName;        // 按键显示名称
        public string ErrorMessage;       // 错误信息（如果有）
    }
    
    /// <summary>
    /// 获取按键的显示名称（人类可读格式）
    /// </summary>
    /// <param name="bindingPath">按键路径（如 "<Keyboard>/w"）</param>
    /// <returns>显示名称（如 "W"）</returns>
    public string GetKeyDisplayName(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath))
            return "未绑定";
        
        try
        {
            return InputControlPath.ToHumanReadableString(
                bindingPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        catch
        {
            return bindingPath; // 如果转换失败，返回原始路径
        }
    }
    
    #region --- 私有辅助方法 ---
    
    private InputAction GetAction(string actionMapName, string actionName)
    {
        if (inputActionAsset == null)
            return null;
        
        var actionMap = inputActionAsset.FindActionMap(actionMapName);
        if (actionMap == null)
            return null;
        
        return actionMap.FindAction(actionName);
    }
    
    /// <summary>
    /// 获取过滤后的键鼠绑定列表
    /// 这个方法会过滤掉复合绑定本身和非键鼠绑定，只返回可以改键的键鼠绑定
    /// </summary>
    /// <param name="actionMapName">ActionMap名称</param>
    /// <param name="actionName">Action名称</param>
    /// <returns>过滤后的键鼠绑定列表</returns>
    private List<InputBinding> GetKeyboardMouseBindings(string actionMapName, string actionName)
    {
        // 步骤1：根据ActionMap名称和Action名称查找对应的Action
        var action = GetAction(actionMapName, actionName);
        // 如果Action不存在，返回空列表
        if (action == null)
            return new List<InputBinding>();
        
        // 步骤2：过滤绑定列表
        // 只返回可以改键的绑定：
        // 1. 排除复合绑定本身（isComposite == true，不能直接改键）
        //    例如：WASD 复合绑定本身不能改键，但它的子绑定（W、S、A、D）可以改键
        // 2. 包含复合绑定的子绑定（isPartOfComposite == true，可以改键）
        //    例如：WASD 复合绑定的子绑定 W、S、A、D 可以单独改键
        // 3. 包含单个按键绑定（既不是复合也不是子绑定，可以改键）
        //    例如：单独的 Fire 动作绑定到鼠标左键，可以改键
        
        // 步骤2.1：使用 LINQ 过滤绑定列表
        return action.bindings.Where(b => 
            // 条件1：排除复合绑定本身
            // isComposite == true 的绑定是复合绑定本身，不能直接改键
            !b.isComposite && 
            // 条件2：只包含键鼠绑定
            // groups 包含 "Keyboard&Mouse" 或 groups 为空（默认键鼠绑定）
            (b.groups.Contains("Keyboard&Mouse") || 
             string.IsNullOrEmpty(b.groups))
        ).ToList();  // 转换为列表返回
        
        // 注意：过滤后的列表索引（bindingIndex）与原始列表索引（originalIndex）不同
        // 例如：
        // 原始列表：[0]Gamepad, [1]WASD复合, [2]W键, [3]S键
        // 过滤后列表：[0]W键(原始索引2), [1]S键(原始索引3)
        // bindingIndex = 0 对应 originalIndex = 2
    }
    
    private string GetBindingDisplayString(string actionMapName, string actionName, int bindingIndex)
    {
        var action = GetAction(actionMapName, actionName);
        if (action == null)
            return "未绑定";

        var bindings = GetKeyboardMouseBindings(actionMapName, actionName);
        if (bindingIndex >= bindings.Count)
            return "未绑定";

        // 优先使用effectivePath（实际生效的绑定，包括覆盖）
        string path = bindings[bindingIndex].effectivePath;
        if (string.IsNullOrEmpty(path))
        {
            // 如果没有有效路径，尝试使用path（默认绑定）
            path = bindings[bindingIndex].path;
        }

        if (string.IsNullOrEmpty(path))
            return "未绑定";

        try
        {
            return InputControlPath.ToHumanReadableString(
                path,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
        }
        catch
        {
            return path; // 如果转换失败，返回原始路径
        }
    }

    private void CopyBindingData<T>(T source, T target) where T : class, IKeyBindingData
    {
        Type type = typeof(T);
        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(string))
            {
                string value = source.GetBindingPath(field.Name);
                target.SetBindingPath(field.Name, value);
            }
        }
    }
    
    #endregion
}



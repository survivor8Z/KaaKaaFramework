#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按键绑定代码生成器
/// 根据 InputActionAsset 自动生成 PlayerKeyBindings.cs 类
/// </summary>
public class KeyBindingsCodeGenerator : EditorWindow
{
    private InputActionAsset inputActionAsset;
    private string className = "PlayerKeyBindings";
    private string outputPath = "Assets/Scripts/KaaKaaFramework/Input/InputSystem/";
    private Vector2 scrollPosition;
    
    [MenuItem("Tools/Input System/生成按键绑定数据类")]
    public static void ShowWindow()
    {
        GetWindow<KeyBindingsCodeGenerator>("按键绑定代码生成器");
    }
    
    [MenuItem("Assets/Input System/生成按键绑定数据类", false, 1)]
    public static void GenerateFromSelection()
    {
        // 获取选中的 InputActionAsset
        var selected = Selection.activeObject as InputActionAsset;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择一个 InputActionAsset 文件！", "确定");
            return;
        }
        
        // 打开窗口并设置选中的资源
        var window = GetWindow<KeyBindingsCodeGenerator>("按键绑定代码生成器");
        window.inputActionAsset = selected;
        window.outputPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(selected)) + "/";
    }
    
    private void OnGUI()
    {
        GUILayout.Label("按键绑定代码生成器", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // InputActionAsset 选择
        EditorGUILayout.LabelField("InputActionAsset:", EditorStyles.boldLabel);
        inputActionAsset = (InputActionAsset)EditorGUILayout.ObjectField(
            inputActionAsset, 
            typeof(InputActionAsset), 
            false
        );
        
        EditorGUILayout.Space();
        
        // 类名设置
        EditorGUILayout.LabelField("类名:", EditorStyles.boldLabel);
        className = EditorGUILayout.TextField(className);
        
        EditorGUILayout.Space();
        
        // 输出路径设置
        EditorGUILayout.LabelField("输出路径:", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField(outputPath);
        if (GUILayout.Button("选择文件夹", GUILayout.Width(100)))
        {
            string path = EditorUtility.SaveFolderPanel("选择输出文件夹", outputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                // 转换为相对路径
                if (path.StartsWith(Application.dataPath))
                {
                    outputPath = "Assets" + path.Substring(Application.dataPath.Length) + "/";
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // 预览区域
        if (inputActionAsset != null)
        {
            EditorGUILayout.LabelField("预览:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            
            var bindings = ScanBindings(inputActionAsset);
            foreach (var binding in bindings)
            {
                EditorGUILayout.LabelField($"- {binding.actionMapName}/{binding.actionName} [{binding.bindingIndex}]");
            }
            
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
        }
        
        // 生成按钮
        GUI.enabled = inputActionAsset != null && !string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(outputPath);
        if (GUILayout.Button("生成代码", GUILayout.Height(30)))
        {
            GenerateCode();
        }
        GUI.enabled = true;
    }
    
    /// <summary>
    /// 扫描 InputActionAsset 中的所有可改键绑定
    /// </summary>
    private List<BindingInfo> ScanBindings(InputActionAsset asset)
    {
        var bindings = new List<BindingInfo>();
        
        // 遍历所有 ActionMap
        foreach (var actionMap in asset.actionMaps)
        {
            // 遍历所有 Action
            foreach (var action in actionMap.actions)
            {
                // 获取过滤后的键鼠绑定列表
                var keyboardMouseBindings = GetKeyboardMouseBindings(action);
                
                // 为每个可改键的绑定创建信息
                for (int i = 0; i < keyboardMouseBindings.Count; i++)
                {
                    var binding = keyboardMouseBindings[i];
                    
                    // 尝试从绑定路径中提取键名（用于生成更友好的字段名）
                    string keyName = ExtractKeyName(binding.path);
                    
                    bindings.Add(new BindingInfo
                    {
                        actionMapName = actionMap.name,
                        actionName = action.name,
                        bindingIndex = i,
                        bindingPath = binding.path,
                        displayName = GetDisplayName(action.name, i, keyboardMouseBindings.Count, keyName),
                        keyName = keyName
                    });
                }
            }
        }
        
        return bindings;
    }
    
    /// <summary>
    /// 从绑定路径中提取键名
    /// 例如：<Keyboard>/w -> W, <Mouse>/leftButton -> LeftButton
    /// </summary>
    private string ExtractKeyName(string bindingPath)
    {
        if (string.IsNullOrEmpty(bindingPath))
            return "";
        
        try
        {
            // 使用 Unity Input System 的方法转换为可读字符串
            string readable = InputControlPath.ToHumanReadableString(
                bindingPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            
            // 移除空格，转换为有效的标识符
            readable = readable.Replace(" ", "");
            return readable;
        }
        catch
        {
            // 如果转换失败，尝试手动提取
            int lastSlash = bindingPath.LastIndexOf('/');
            if (lastSlash >= 0 && lastSlash < bindingPath.Length - 1)
            {
                string key = bindingPath.Substring(lastSlash + 1);
                // 转换为 PascalCase
                if (key.Length > 0)
                {
                    return char.ToUpper(key[0]) + key.Substring(1);
                }
            }
            return "";
        }
    }
    
    /// <summary>
    /// 获取过滤后的键鼠绑定列表（与 InputBindingMgr 中的逻辑一致）
    /// </summary>
    private List<InputBinding> GetKeyboardMouseBindings(InputAction action)
    {
        if (action == null)
            return new List<InputBinding>();
        
        // 过滤规则：排除复合绑定本身，只包含键鼠绑定
        return action.bindings.Where(b => 
            !b.isComposite && // 排除复合绑定本身
            (b.groups.Contains("Keyboard&Mouse") || 
             string.IsNullOrEmpty(b.groups))
        ).ToList();
    }
    
    /// <summary>
    /// 生成显示名称
    /// </summary>
    private string GetDisplayName(string actionName, int bindingIndex, int totalCount, string keyName)
    {
        // 如果只有一个绑定，直接使用 Action 名称
        if (totalCount == 1)
        {
            return actionName;
        }
        
        // 如果有多个绑定，尝试使用键名
        if (!string.IsNullOrEmpty(keyName))
        {
            return $"{actionName}-{keyName}";
        }
        
        // 如果无法提取键名，使用索引
        return $"{actionName}-{bindingIndex}";
    }
    
    /// <summary>
    /// 生成代码
    /// </summary>
    private void GenerateCode()
    {
        if (inputActionAsset == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择 InputActionAsset！", "确定");
            return;
        }
        
        // 扫描所有绑定
        var bindings = ScanBindings(inputActionAsset);
        
        if (bindings.Count == 0)
        {
            EditorUtility.DisplayDialog("警告", "未找到任何可改键的绑定！", "确定");
            return;
        }
        
        // 生成代码
        string code = GenerateClassCode(className, bindings);
        
        // 确保输出目录存在
        string fullPath = Path.Combine(Application.dataPath, outputPath.Substring(7)); // 移除 "Assets/"
        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }
        
        // 写入文件
        string filePath = Path.Combine(outputPath, className + ".cs");
        filePath = filePath.Replace('\\', '/');
        
        File.WriteAllText(filePath, code, Encoding.UTF8);
        
        // 刷新资源
        AssetDatabase.Refresh();
        
        // 选中生成的文件
        var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(filePath);
        if (asset != null)
        {
            Selection.activeObject = asset;
            EditorUtility.FocusProjectWindow();
        }
        
        EditorUtility.DisplayDialog("成功", $"代码已生成到：\n{filePath}\n\n共生成 {bindings.Count} 个绑定字段", "确定");
    }
    
    /// <summary>
    /// 生成类代码
    /// </summary>
    private string GenerateClassCode(string className, List<BindingInfo> bindings)
    {
        var sb = new StringBuilder();
        
        // 文件头注释
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {className}");
        sb.AppendLine("/// 自动生成的按键绑定数据类");
        sb.AppendLine("/// 根据 InputActionAsset 自动生成，请勿手动修改");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[Serializable]");
        sb.AppendLine($"public class {className} : IKeyBindingData");
        sb.AppendLine("{");
        
        // 生成字段
        // 先统计每个 Action 的绑定数量，用于生成更好的字段名
        var actionBindingCounts = new Dictionary<string, int>();
        foreach (var binding in bindings)
        {
            string key = $"{binding.actionMapName}_{binding.actionName}";
            if (!actionBindingCounts.ContainsKey(key))
            {
                actionBindingCounts[key] = 0;
            }
            actionBindingCounts[key]++;
        }
        
        // 用于跟踪已使用的字段名，确保唯一性
        var usedFieldNames = new HashSet<string>();
        
        foreach (var binding in bindings)
        {
            // 获取该 Action 的绑定总数
            string key = $"{binding.actionMapName}_{binding.actionName}";
            int totalCount = actionBindingCounts[key];
            
            // 生成字段名（确保是有效的 C# 标识符）
            string baseFieldName = GenerateFieldName(
                binding.actionMapName, 
                binding.actionName, 
                binding.bindingIndex, 
                binding.keyName,
                totalCount
            );
            
            // 确保字段名唯一
            string fieldName = EnsureUniqueFieldName(baseFieldName, usedFieldNames, binding.bindingIndex);
            
            // 记录已使用的字段名
            usedFieldNames.Add(fieldName);
            
            // 生成特性
            sb.AppendLine($"    [KeyBinding(\"{binding.actionMapName}\", \"{binding.actionName}\", {binding.bindingIndex}, \"{binding.displayName}\")]");
            sb.AppendLine($"    public string {fieldName} = \"\";");
            sb.AppendLine();
        }
        
        // 实现接口方法
        sb.AppendLine("    // 实现接口方法");
        sb.AppendLine("    private Dictionary<string, string> bindings = new Dictionary<string, string>();");
        sb.AppendLine();
        sb.AppendLine("    public string GetBindingPath(string fieldName)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (bindings.ContainsKey(fieldName))");
        sb.AppendLine("            return bindings[fieldName];");
        sb.AppendLine();
        sb.AppendLine("        // 使用反射获取字段值（作为默认值）");
        sb.AppendLine("        var field = GetType().GetField(fieldName);");
        sb.AppendLine("        if (field != null && field.FieldType == typeof(string))");
        sb.AppendLine("        {");
        sb.AppendLine("            string value = (string)field.GetValue(this);");
        sb.AppendLine("            if (!string.IsNullOrEmpty(value))");
        sb.AppendLine("            {");
        sb.AppendLine("                bindings[fieldName] = value;");
        sb.AppendLine("                return value;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        return \"\";");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public void SetBindingPath(string fieldName, string bindingPath)");
        sb.AppendLine("    {");
        sb.AppendLine("        bindings[fieldName] = bindingPath;");
        sb.AppendLine();
        sb.AppendLine("        // 同时更新字段值");
        sb.AppendLine("        var field = GetType().GetField(fieldName);");
        sb.AppendLine("        if (field != null && field.FieldType == typeof(string))");
        sb.AppendLine("        {");
        sb.AppendLine("            field.SetValue(this, bindingPath);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        
        return sb.ToString();
    }
    
    /// <summary>
    /// 生成字段名（确保是有效的 C# 标识符）
    /// </summary>
    private string GenerateFieldName(string actionMapName, string actionName, int bindingIndex, string keyName, int totalCount)
    {
        string baseName;
        
        // 如果只有一个绑定，直接使用 Action 名称
        if (totalCount == 1)
        {
            baseName = actionName;
        }
        // 如果有多个绑定，尝试使用键名
        else if (!string.IsNullOrEmpty(keyName))
        {
            // 使用 Action名称 + 键名
            baseName = $"{actionName}{keyName}";
        }
        // 如果无法提取键名，使用索引
        else
        {
            baseName = $"{actionName}{bindingIndex}";
        }
        
        // 确保是有效的 C# 标识符
        // 移除特殊字符
        baseName = System.Text.RegularExpressions.Regex.Replace(baseName, @"[^a-zA-Z0-9_]", "");
        
        // 如果以数字开头，添加下划线前缀
        if (baseName.Length > 0 && char.IsDigit(baseName[0]))
        {
            baseName = "_" + baseName;
        }
        
        // 首字母大写（PascalCase）
        if (baseName.Length > 0)
        {
            baseName = char.ToUpper(baseName[0]) + baseName.Substring(1);
        }
        
        // 如果为空，使用默认名称
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = $"Binding{bindingIndex}";
        }
        
        return baseName;
    }
    
    /// <summary>
    /// 确保字段名唯一
    /// 如果字段名已存在，添加后缀（如绑定索引）使其唯一
    /// </summary>
    private string EnsureUniqueFieldName(string baseFieldName, HashSet<string> usedFieldNames, int bindingIndex)
    {
        // 如果字段名未被使用，直接返回
        if (!usedFieldNames.Contains(baseFieldName))
        {
            return baseFieldName;
        }
        
        // 如果字段名已被使用，尝试添加后缀
        // 首先尝试添加绑定索引
        string candidateName = $"{baseFieldName}{bindingIndex}";
        if (!usedFieldNames.Contains(candidateName))
        {
            return candidateName;
        }
        
        // 如果添加索引后仍然冲突，继续添加数字后缀
        int suffix = 0;
        do
        {
            candidateName = $"{baseFieldName}{bindingIndex}_{suffix}";
            suffix++;
        } while (usedFieldNames.Contains(candidateName) && suffix < 1000); // 防止无限循环
        
        return candidateName;
    }
    
    /// <summary>
    /// 绑定信息结构
    /// </summary>
    private struct BindingInfo
    {
        public string actionMapName;
        public string actionName;
        public int bindingIndex;
        public string bindingPath;
        public string displayName;
        public string keyName;  // 从绑定路径提取的键名（如 "W", "LeftButton"）
    }
}
#endif


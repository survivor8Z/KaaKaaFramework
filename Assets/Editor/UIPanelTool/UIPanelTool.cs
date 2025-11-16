using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor.Compilation;
using System.Reflection;
using UnityEngine.EventSystems;
using UnityEditor.Callbacks;
using System.Runtime.Serialization;

/// <summary>
/// UI控件绑定工具
/// </summary>
public class UIPanelTool : EditorWindow
{
    // 工作流状态
    private enum UIToolState
    {
        Empty,              // 空状态
        ObjectSelected,     // 已选择根对象
        ControlsScanned,   // 已扫描控件
        CodeGenerated      // 已生成代码
    }

    // 显示模式
    private enum DisplayMode
    {
        Tree,    // 树状显示
        Linear   // 线性显示
    }

    // 核心数据
    private GameObject targetGameObject;                    // 目标根对象
    private string panelName = "";                          // 面板名称
    private string savePath = "Assets/Scripts";            // 保存路径
    private List<ControlMappingItem> controlMappings;       // 控件映射列表
    private UIToolState currentState = UIToolState.Empty;  // 当前状态

    // UI相关
    private Vector2 scrollPosition;                         // 滚动位置
    private string searchFilter = "";                       // 搜索过滤
    private string typeFilter = "全部";                    // 类型过滤（"全部"表示不过滤）
    private Dictionary<int, string> editingFieldNames;     // 正在编辑的字段名
    private GameObject lastSelectedObject;                  // 上次选择的对象，用于检测变化
    
    // 树状结构相关
    private List<ControlMappingItem> treeRoots;             // 树根节点列表
    private HashSet<ControlMappingItem> expandedNodes;      // 展开的节点
    private HashSet<ControlMappingItem> selectedItems;      // 选中的节点（支持多选）
    private ControlMappingItem lastSelectedItem;            // 上次选中的节点（用于Shift选择）
    
    // 显示模式
    private DisplayMode displayMode = DisplayMode.Tree;     // 当前显示模式

    // 默认排除的控件名称
    private static readonly List<string> defaultExcludeNames = new List<string>
    {
        "Image", "Text (TMP)", "RawImage", "Background", "Checkmark",
        "Label", "Text (Legacy)", "Arrow", "Placeholder", "Fill",
        "Handle", "Viewport", "Scrollbar Horizontal", "Scrollbar Vertical"
    };

    // 编译后处理的数据（静态，跨编译保持）
    private static List<ControlMappingItem> pendingMappings = null;

    // 用于标记是否需要处理待处理的映射
    private static bool needProcessPendingMappings = false;

    // 用于延迟处理的帧计数器
    private static int delayFrameCount = 0;

    // EditorPrefs 键名
    private const string PREF_KEY_PENDING_MAPPINGS = "UIPanelTool_PendingMappings";
    private const string PREF_KEY_NEED_PROCESS = "UIPanelTool_NeedProcess";

    /// <summary>
    /// 可序列化的映射数据（用于保存到 EditorPrefs）
    /// </summary>
    [Serializable]
    private class SerializableMappingData
    {
        public string controlName;
        public string fieldName;
        public string controlTypeName;  // Type 保存为字符串
        public string path;
        public string panelName;
        public string targetObjectPath;

        public ControlMappingItem ToControlMappingItem()
        {
            Type controlType = GetControlTypeFromName(controlTypeName);
            if (controlType == null)
            {
                Debug.LogWarning($"[UIPanelTool] 无法找到类型: {controlTypeName}");
                return null;
            }

            // 创建映射项（gameObject 和 component 会在绑定时重新查找）
            var item = new ControlMappingItem(controlName, controlType, path, null, null);
            item.fieldName = fieldName;
            item.panelName = panelName;
            item.targetObjectPath = targetObjectPath;
            return item;
        }

        /// <summary>
        /// 根据类型名称获取类型
        /// </summary>
        private static Type GetControlTypeFromName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            // 先尝试直接获取
            Type type = Type.GetType(typeName);
            if (type != null) return type;

            // 尝试 UnityEngine.UI 命名空间
            type = Type.GetType($"UnityEngine.UI.{typeName}");
            if (type != null) return type;

            // 尝试 TMPro 命名空间
            type = Type.GetType($"TMPro.{typeName}");
            if (type != null) return type;

            // 从所有已加载的程序集中查找
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null) return type;

                    // 尝试通过名称匹配
                    var types = assembly.GetTypes();
                    foreach (var t in types)
                    {
                        if (t.Name == typeName)
                        {
                            return t;
                        }
                    }
                }
                catch
                {
                    // 忽略无法获取类型的程序集
                }
            }

            return null;
        }

        public static SerializableMappingData FromControlMappingItem(ControlMappingItem item)
        {
            return new SerializableMappingData
            {
                controlName = item.controlName,
                fieldName = item.fieldName,
                controlTypeName = item.controlType?.FullName ?? item.controlType?.Name ?? "",
                path = item.path,
                panelName = item.panelName,
                targetObjectPath = item.targetObjectPath
            };
        }
    }

    [Serializable]
    private class SerializableMappingList
    {
        public List<SerializableMappingData> mappings = new List<SerializableMappingData>();
    }

    /// <summary>
    /// 获取或初始化 pendingMappings（从 EditorPrefs 加载）
    /// </summary>
    private static List<ControlMappingItem> GetPendingMappings()
    {
        if (pendingMappings == null)
        {
            pendingMappings = LoadPendingMappingsFromPrefs();
        }
        return pendingMappings;
    }

    /// <summary>
    /// 从 EditorPrefs 加载待处理的映射
    /// </summary>
    private static List<ControlMappingItem> LoadPendingMappingsFromPrefs()
    {
        List<ControlMappingItem> result = new List<ControlMappingItem>();

        string json = EditorPrefs.GetString(PREF_KEY_PENDING_MAPPINGS, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                SerializableMappingList data = JsonUtility.FromJson<SerializableMappingList>(json);
                if (data != null && data.mappings != null)
                {
                    foreach (var serialized in data.mappings)
                    {
                        var item = serialized.ToControlMappingItem();
                        if (item != null)
                        {
                            result.Add(item);
                        }
                    }
                    Debug.Log($"[UIPanelTool] 从 EditorPrefs 加载了 {result.Count} 个待处理映射");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[UIPanelTool] 加载待处理映射失败: {e.Message}");
            }
        }

        return result;
    }

    /// <summary>
    /// 保存待处理的映射到 EditorPrefs
    /// </summary>
    private static void SavePendingMappingsToPrefs()
    {
        if (pendingMappings == null || pendingMappings.Count == 0)
        {
            EditorPrefs.DeleteKey(PREF_KEY_PENDING_MAPPINGS);
            Debug.Log("[UIPanelTool] 清除 EditorPrefs 中的待处理映射");
            return;
        }

        try
        {
            SerializableMappingList data = new SerializableMappingList();
            foreach (var item in pendingMappings)
            {
                data.mappings.Add(SerializableMappingData.FromControlMappingItem(item));
            }

            string json = JsonUtility.ToJson(data);
            EditorPrefs.SetString(PREF_KEY_PENDING_MAPPINGS, json);
            Debug.Log($"[UIPanelTool] 保存了 {pendingMappings.Count} 个待处理映射到 EditorPrefs");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UIPanelTool] 保存待处理映射失败: {e.Message}");
        }
    }

    [MenuItem("Tools/UI/打开绑定控件面板")]
    private static void Entry()
    {
        var win = GetWindow<UIPanelTool>("绑定控件面板");
        win.Show();
    }

    /// <summary>
    /// 清理名称：移除括号，空格转下划线
    /// </summary>
    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        
        // 移除括号（只移除括号字符本身）
        name = name.Replace("(", "").Replace(")", "");
        
        // 空格转下划线
        name = name.Replace(" ", "_");
        
        // 移除其他特殊字符（保留字母、数字、下划线）
        name = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        
        return name;
    }

    /// <summary>
    /// 转换为类名：首字母大写
    /// </summary>
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        
        name = SanitizeName(name);
        if (string.IsNullOrEmpty(name)) return name;
        
        // 只需要首字母大写
        if (char.IsLower(name[0]))
        {
            return char.ToUpper(name[0]) + name.Substring(1);
        }
        
        return name;
    }

    /// <summary>
    /// 转换为变量名：首字母小写
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        
        name = SanitizeName(name);
        if (string.IsNullOrEmpty(name)) return name;
        
        // 只需要首字母小写
        if (char.IsUpper(name[0]))
        {
            return char.ToLower(name[0]) + name.Substring(1);
        }
        
        return name;
    }

    private void OnEnable()
    {
        if (controlMappings == null)
        {
            controlMappings = new List<ControlMappingItem>();
        }
        if (editingFieldNames == null)
        {
            editingFieldNames = new Dictionary<int, string>();
        }
        if (treeRoots == null)
        {
            treeRoots = new List<ControlMappingItem>();
        }
        if (expandedNodes == null)
        {
            expandedNodes = new HashSet<ControlMappingItem>();
        }
        if (selectedItems == null)
        {
            selectedItems = new HashSet<ControlMappingItem>();
        }
        
        // 从 EditorPrefs 加载保存路径
        string savedPath = EditorPrefs.GetString("UIPanelTool_SavePath", "Assets/Scripts");
        if (!string.IsNullOrEmpty(savedPath))
        {
            savePath = savedPath;
        }
    }

    private void OnGUI()
    {
        DrawToolbar();

        switch (currentState)
        {
            case UIToolState.Empty:
                DrawEmptyState();
                break;
            case UIToolState.ObjectSelected:
                DrawObjectSelectedState();
                break;
            case UIToolState.ControlsScanned:
                DrawControlsScannedState();
                break;
            case UIToolState.CodeGenerated:
                DrawCodeGeneratedState();
                break;
        }
    }

    /// <summary>
    /// 绘制工具栏
    /// </summary>
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        if (GUILayout.Button("重置", EditorStyles.toolbarButton))
        {
            ResetTool();
        }

        EditorGUILayout.Space();

        if (targetGameObject != null)
        {
            EditorGUILayout.LabelField($"目标对象: {targetGameObject.name}", EditorStyles.toolbarButton);
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// 绘制空状态
    /// </summary>
    private void DrawEmptyState()
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.HelpBox("请选择一个GameObject作为UI面板的根对象", MessageType.Info);
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("根对象:", GUILayout.Width(60));
        GameObject newTarget = (GameObject)EditorGUILayout.ObjectField(targetGameObject, typeof(GameObject), true);
        
        // 检测对象变化，自动设置面板名称
        if (newTarget != targetGameObject)
        {
            targetGameObject = newTarget;
            if (targetGameObject != null)
            {
                // 如果面板名称为空或者是上次对象的名字，则自动更新
                if (string.IsNullOrEmpty(panelName) || 
                    (lastSelectedObject != null && panelName == ToPascalCase(lastSelectedObject.name)))
                {
                    panelName = ToPascalCase(targetGameObject.name);
                }
            }
            lastSelectedObject = targetGameObject;
        }
        
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        if (targetGameObject != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("面板名称:", GUILayout.Width(60));
            panelName = EditorGUILayout.TextField(panelName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // 保存路径选择
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("保存路径:", GUILayout.Width(60));
            savePath = EditorGUILayout.TextField(savePath);
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                // 将Unity路径转换为系统路径
                string initialPath = Application.dataPath;
                if (!string.IsNullOrEmpty(savePath) && savePath.StartsWith("Assets/"))
                {
                    initialPath = savePath.Replace("Assets/", Application.dataPath + "/");
                }
                
                string selectedPath = EditorUtility.OpenFolderPanel("选择代码保存路径", initialPath, "");
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    // 转换为相对于Assets的路径
                    if (selectedPath.StartsWith(Application.dataPath))
                    {
                        savePath = "Assets" + selectedPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                        // 保存到 EditorPrefs
                        EditorPrefs.SetString("UIPanelTool_SavePath", savePath);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择Assets目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (GUILayout.Button("扫描控件", GUILayout.Height(30)))
            {
                if (string.IsNullOrEmpty(panelName))
                {
                    EditorUtility.DisplayDialog("错误", "请输入面板名称", "确定");
                    return;
                }
                if (string.IsNullOrEmpty(savePath))
                {
                    EditorUtility.DisplayDialog("错误", "请选择保存路径", "确定");
                    return;
                }
                ScanControlsForSelectedObject();
            }
        }
    }

    /// <summary>
    /// 绘制对象已选择状态
    /// </summary>
    private void DrawObjectSelectedState()
    {
        EditorGUILayout.HelpBox("已选择对象，请点击扫描控件", MessageType.Info);
        if (GUILayout.Button("扫描控件", GUILayout.Height(30)))
        {
            ScanControlsForSelectedObject();
        }
    }

    /// <summary>
    /// 绘制控件已扫描状态
    /// </summary>
    private void DrawControlsScannedState()
    {
        // 处理Delete键（拖拽已在OnGUI中处理）
        HandleDeleteKey();

        EditorGUILayout.LabelField("控件列表", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // 搜索框和显示模式切换
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(50));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        
        // 显示模式切换按钮
        GUILayout.FlexibleSpace();
        string modeText = displayMode == DisplayMode.Tree ? "树状" : "线性";
        if (GUILayout.Button($"显示: {modeText}", GUILayout.Width(80)))
        {
            displayMode = displayMode == DisplayMode.Tree ? DisplayMode.Linear : DisplayMode.Tree;
        }
        EditorGUILayout.EndHorizontal();
        
        // 类型过滤
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("类型:", GUILayout.Width(50));
        string[] typeOptions = new string[] { "全部", "Button", "Toggle", "Slider", "InputField", "ScrollRect", "Dropdown", "Text", "TextMeshProUGUI", "Image", "非UI控件" };
        int currentTypeIndex = Array.IndexOf(typeOptions, typeFilter);
        if (currentTypeIndex < 0) currentTypeIndex = 0;
        int newTypeIndex = EditorGUILayout.Popup(currentTypeIndex, typeOptions, GUILayout.Width(150));
        typeFilter = typeOptions[newTypeIndex];
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 控件列表
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawControlList();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(10);

        // 底部操作按钮
        DrawBottomActions();
    }

    /// <summary>
    /// 绘制控件列表（根据显示模式）
    /// </summary>
    private void DrawControlList()
    {
        if (controlMappings == null || controlMappings.Count == 0)
        {
            EditorGUILayout.HelpBox("没有找到控件", MessageType.Info);
            return;
        }

        // 表头（根据显示模式决定是否显示勾选框列）
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (displayMode == DisplayMode.Tree)
        {
            EditorGUILayout.LabelField("", GUILayout.Width(20)); // 复选框列（仅树状模式）
        }
        EditorGUILayout.LabelField("控件名", GUILayout.Width(150));
        EditorGUILayout.LabelField("字段名", GUILayout.Width(150));
        EditorGUILayout.LabelField("类型", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // 根据显示模式调用不同的绘制方法
        if (displayMode == DisplayMode.Tree)
        {
            DrawTreeView();
        }
        else
        {
            DrawLinearView();
        }
    }

    /// <summary>
    /// 绘制树状视图
    /// </summary>
    private void DrawTreeView()
    {
        // 绘制树状结构
        if (treeRoots != null && treeRoots.Count > 0)
        {
            foreach (var root in treeRoots)
            {
                DrawTreeNode(root, 0);
            }
        }
        else
        {
            // 如果没有树结构，回退到平铺显示
            foreach (var item in controlMappings)
            {
                DrawTreeNode(item, 0);
            }
        }
    }

    /// <summary>
    /// 绘制线性视图（只显示将要创建绑定的UI控件）
    /// </summary>
    private void DrawLinearView()
    {
        // 筛选出将要创建绑定的UI控件（isUIControl为true且isSelected为true）
        var selectedUIControls = controlMappings.Where(item => item.isUIControl && item.isSelected).ToList();

        if (selectedUIControls.Count == 0)
        {
            EditorGUILayout.HelpBox("没有选中的UI控件", MessageType.Info);
            return;
        }

        // 应用搜索过滤和类型过滤
        selectedUIControls = selectedUIControls.Where(item =>
        {
            // 文本搜索过滤
            bool textMatch = true;
            if (!string.IsNullOrEmpty(searchFilter))
            {
                textMatch = item.controlName.ToLower().Contains(searchFilter.ToLower()) ||
                           item.fieldName.ToLower().Contains(searchFilter.ToLower());
            }
            
            // 类型过滤
            bool typeMatch = true;
            if (typeFilter != "全部")
            {
                if (typeFilter == "非UI控件")
                {
                    typeMatch = !item.isUIControl;
                }
                else
                {
                    typeMatch = item.isUIControl && item.controlType != null && 
                               item.controlType.Name == typeFilter;
                }
            }
            
            return textMatch && typeMatch;
        }).ToList();

        // 绘制每个选中的UI控件
        foreach (var item in selectedUIControls)
        {
            DrawLinearItem(item);
        }
    }

    /// <summary>
    /// 绘制线性视图中的单个项目
    /// </summary>
    private void DrawLinearItem(ControlMappingItem item)
    {
        if (item == null || !item.isUIControl) return;

        // 获取行矩形，使用紧凑布局
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        
        // 计算各列的位置（紧凑对齐，无额外间距，不包含勾选框）
        float nameWidth = 150f;
        float fieldNameWidth = 150f;
        float typeWidth = 100f;
        
        float currentX = rowRect.x;
        
        // 选择状态（用于Shift多选）
        bool isSelectedInTree = selectedItems.Contains(item);
        Color originalColor = GUI.color;
        Color originalBackgroundColor = GUI.backgroundColor;
        
        if (isSelectedInTree)
        {
            GUI.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.5f);
        }
        
        // 控件名（可点击选择）
        Rect nameRect = new Rect(currentX, rowRect.y, nameWidth, rowRect.height);
        
        // 绘制背景（如果选中）
        if (isSelectedInTree)
        {
            EditorGUI.DrawRect(nameRect, new Color(0.5f, 0.7f, 1f, 0.3f));
        }
        
        // 检测点击和双击
        if (Event.current.type == EventType.MouseDown && nameRect.Contains(Event.current.mousePosition))
        {
            // 双击时ping GameObject
            if (Event.current.clickCount == 2 && item.gameObject != null)
            {
                EditorGUIUtility.PingObject(item.gameObject);
                Selection.activeGameObject = item.gameObject;
                Event.current.Use();
            }
            else
            {
                // 单击时处理选择
                HandleItemSelection(item, Event.current.shift);
                Event.current.Use();
                Repaint();
            }
        }
        
        EditorGUI.LabelField(nameRect, item.controlName);
        currentX += nameWidth;

            // 字段名（可编辑）
        Rect fieldNameRect = new Rect(currentX, rowRect.y, fieldNameWidth, rowRect.height);
        int originalIndex = controlMappings.IndexOf(item);
            string controlID = $"FieldName_{originalIndex}";
            GUI.SetNextControlName(controlID);
            string currentFieldName = editingFieldNames.ContainsKey(originalIndex) 
                ? editingFieldNames[originalIndex] 
                : item.fieldName;

        string newFieldName = EditorGUI.TextField(fieldNameRect, currentFieldName);
            
            // 处理字段名编辑
            if (newFieldName != currentFieldName)
            {
                editingFieldNames[originalIndex] = newFieldName;
            }

            // 确认字段名（失去焦点或按Enter）
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == controlID)
                {
                    ConfirmFieldName(originalIndex, newFieldName);
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.Layout)
            {
                if (GUI.GetNameOfFocusedControl() != controlID && editingFieldNames.ContainsKey(originalIndex))
                {
                    ConfirmFieldName(originalIndex, editingFieldNames[originalIndex]);
                }
            }
        currentX += fieldNameWidth;
        
        // 类型
        Rect typeRect = new Rect(currentX, rowRect.y, typeWidth, rowRect.height);
        if (item.controlType != null)
        {
            EditorGUI.LabelField(typeRect, item.controlType.Name);
        }
        else
        {
            EditorGUI.LabelField(typeRect, "GameObject");
        }
        
        GUI.color = originalColor;
        GUI.backgroundColor = originalBackgroundColor;
    }

    /// <summary>
    /// 绘制树节点（递归）
    /// </summary>
    private void DrawTreeNode(ControlMappingItem item, int depth)
    {
        if (item == null) return;

        // 应用搜索过滤和类型过滤
        bool shouldShow = true;
        
        // 文本搜索过滤
        bool textMatch = true;
        if (!string.IsNullOrEmpty(searchFilter))
        {
            textMatch = item.controlName.ToLower().Contains(searchFilter.ToLower()) ||
                       (item.isUIControl && item.fieldName.ToLower().Contains(searchFilter.ToLower()));
        }
        
        // 类型过滤
        bool typeMatch = true;
        if (typeFilter != "全部")
        {
            if (typeFilter == "非UI控件")
            {
                typeMatch = !item.isUIControl;
            }
            else
            {
                typeMatch = item.isUIControl && item.controlType != null && 
                           item.controlType.Name == typeFilter;
            }
        }
        
        shouldShow = textMatch && typeMatch;
        
        // 如果当前项不匹配，检查是否有子项匹配
        if (!shouldShow && !HasMatchingChild(item))
        {
            return;
        }

        // 获取行矩形，使用紧凑布局
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        
        // 计算各列的位置（紧凑对齐，无额外间距）
        float indentWidth = depth * 15f;
        float foldoutWidth = 14f;
        float toggleWidth = 20f;
        float nameWidth = 150f;
        float fieldNameWidth = 150f;
        float typeWidth = 100f;
        
        float currentX = rowRect.x + indentWidth;
        
        // 展开/折叠按钮
        bool hasChildren = item.children != null && item.children.Count > 0;
        bool isExpanded = expandedNodes.Contains(item);
        
        if (hasChildren)
        {
            Rect foldoutRect = new Rect(currentX, rowRect.y, foldoutWidth, rowRect.height);
            bool newExpanded = EditorGUI.Foldout(foldoutRect, isExpanded, "", true);
            if (newExpanded != isExpanded)
            {
                if (newExpanded)
                {
                    expandedNodes.Add(item);
                }
                else
                {
                    expandedNodes.Remove(item);
                }
            }
        }
        currentX += foldoutWidth;
        
        // 勾选框区域（仅UI控件显示勾选框）
        Rect toggleRect = new Rect(currentX, rowRect.y, toggleWidth, rowRect.height);
        if (item.isUIControl)
        {
            item.isSelected = EditorGUI.Toggle(toggleRect, item.isSelected);
        }
        currentX += toggleWidth;
        
        // 选择状态（用于Shift多选）
        bool isSelectedInTree = selectedItems.Contains(item);
        Color originalColor = GUI.color;
        Color originalBackgroundColor = GUI.backgroundColor;
        
        if (isSelectedInTree)
        {
            GUI.backgroundColor = new Color(0.5f, 0.7f, 1f, 0.5f);
        }
        
        // 控件名（可点击选择）
        Rect nameRect = new Rect(currentX, rowRect.y, nameWidth, rowRect.height);
        
        // 绘制背景（如果选中）
        if (isSelectedInTree)
        {
            EditorGUI.DrawRect(nameRect, new Color(0.5f, 0.7f, 1f, 0.3f));
        }
        
        // 检测点击和双击
        if (Event.current.type == EventType.MouseDown && nameRect.Contains(Event.current.mousePosition))
        {
            // 双击时ping GameObject
            if (Event.current.clickCount == 2 && item.gameObject != null)
            {
                EditorGUIUtility.PingObject(item.gameObject);
                Selection.activeGameObject = item.gameObject;
                Event.current.Use();
            }
            else
            {
                // 单击时处理选择
                HandleItemSelection(item, Event.current.shift);
                Event.current.Use();
                Repaint();
            }
        }
        
        EditorGUI.LabelField(nameRect, item.controlName);
        currentX += nameWidth;
        
        // 字段名（仅UI控件可编辑）
        Rect fieldNameRect = new Rect(currentX, rowRect.y, fieldNameWidth, rowRect.height);
        if (item.isUIControl)
        {
            int originalIndex = controlMappings.IndexOf(item);
            string controlID = $"FieldName_{originalIndex}";
            GUI.SetNextControlName(controlID);
            string currentFieldName = editingFieldNames.ContainsKey(originalIndex) 
                ? editingFieldNames[originalIndex] 
                : item.fieldName;

            string newFieldName = EditorGUI.TextField(fieldNameRect, currentFieldName);
            
            // 处理字段名编辑
            if (newFieldName != currentFieldName)
            {
                editingFieldNames[originalIndex] = newFieldName;
            }

            // 确认字段名（失去焦点或按Enter）
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
            {
                if (GUI.GetNameOfFocusedControl() == controlID)
                {
                    ConfirmFieldName(originalIndex, newFieldName);
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.Layout)
            {
                if (GUI.GetNameOfFocusedControl() != controlID && editingFieldNames.ContainsKey(originalIndex))
                {
                    ConfirmFieldName(originalIndex, editingFieldNames[originalIndex]);
                }
            }
        }
        else
        {
            // 非UI控件显示"-"
            EditorGUI.LabelField(fieldNameRect, "-");
        }
        currentX += fieldNameWidth;
        
        // 类型
        Rect typeRect = new Rect(currentX, rowRect.y, typeWidth, rowRect.height);
        if (item.isUIControl && item.controlType != null)
        {
            EditorGUI.LabelField(typeRect, item.controlType.Name);
        }
        else
        {
            EditorGUI.LabelField(typeRect, "GameObject");
        }
        
        GUI.color = originalColor;
        GUI.backgroundColor = originalBackgroundColor;

        // 绘制子节点
        if (hasChildren && isExpanded)
        {
            foreach (var child in item.children)
            {
                DrawTreeNode(child, depth + 1);
            }
        }
    }

    /// <summary>
    /// 检查节点或其子节点是否匹配搜索条件
    /// </summary>
    private bool HasMatchingChild(ControlMappingItem item)
    {
        if (item == null || item.children == null) return false;
        
        foreach (var child in item.children)
        {
            // 文本搜索匹配
            bool textMatch = true;
            if (!string.IsNullOrEmpty(searchFilter))
            {
                textMatch = child.controlName.ToLower().Contains(searchFilter.ToLower()) ||
                           (child.isUIControl && child.fieldName.ToLower().Contains(searchFilter.ToLower()));
            }
            
            // 类型匹配
            bool typeMatch = true;
            if (typeFilter != "全部")
            {
                if (typeFilter == "非UI控件")
                {
                    typeMatch = !child.isUIControl;
                }
                else
                {
                    typeMatch = child.isUIControl && child.controlType != null && 
                               child.controlType.Name == typeFilter;
                }
            }
            
            bool matches = textMatch && typeMatch;
            if (matches || HasMatchingChild(child))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 处理项目选择（支持Shift多选）
    /// </summary>
    private void HandleItemSelection(ControlMappingItem item, bool isShiftPressed)
    {
        if (isShiftPressed && lastSelectedItem != null && lastSelectedItem != item)
        {
            // Shift选择：选择从lastSelectedItem到item之间的所有项目
            List<ControlMappingItem> allItems = GetAllItemsInOrder();
            int lastIndex = allItems.IndexOf(lastSelectedItem);
            int currentIndex = allItems.IndexOf(item);
            
            if (lastIndex >= 0 && currentIndex >= 0)
            {
                int start = Mathf.Min(lastIndex, currentIndex);
                int end = Mathf.Max(lastIndex, currentIndex);
                
                for (int i = start; i <= end; i++)
                {
                    selectedItems.Add(allItems[i]);
                }
            }
        }
        else
        {
            // 普通选择：清除之前的选择，只选择当前项
            if (!Event.current.control && !Event.current.command)
            {
                selectedItems.Clear();
            }
            selectedItems.Add(item);
        }
        
        lastSelectedItem = item;
    }

    /// <summary>
    /// 获取所有项目的有序列表（用于Shift选择）
    /// </summary>
    private List<ControlMappingItem> GetAllItemsInOrder()
    {
        List<ControlMappingItem> result = new List<ControlMappingItem>();
        CollectItemsInOrder(treeRoots, result);
        return result;
    }

    /// <summary>
    /// 递归收集项目（深度优先）
    /// </summary>
    private void CollectItemsInOrder(List<ControlMappingItem> roots, List<ControlMappingItem> result)
    {
        if (roots == null) return;
        
        foreach (var root in roots)
        {
            result.Add(root);
            if (root.children != null && root.children.Count > 0)
            {
                CollectItemsInOrder(root.children, result);
            }
        }
    }

    /// <summary>
    /// 全选选中的行（将selectedItems中的项设置为选中）
    /// </summary>
    private void SelectAllUIControls()
    {
        if (selectedItems == null || selectedItems.Count == 0) return;
        
        foreach (var item in selectedItems)
        {
            if (item != null && item.isUIControl)
            {
                item.isSelected = true;
            }
        }
        
        Repaint();
    }

    /// <summary>
    /// 取消选择选中的行（将selectedItems中的项设置为未选中）
    /// </summary>
    private void UnselectAllUIControls()
    {
        if (selectedItems == null || selectedItems.Count == 0) return;
        
        foreach (var item in selectedItems)
        {
            if (item != null && item.isUIControl)
            {
                item.isSelected = false;
            }
        }
        
        Repaint();
    }

    /// <summary>
    /// 删除选中的项目（取消勾选）
    /// </summary>
    private void DeleteSelectedItems()
    {
        if (selectedItems == null || selectedItems.Count == 0) return;
        
        // 收集要取消勾选的项目（避免在迭代时修改集合）
        var itemsToUnselect = new List<ControlMappingItem>(selectedItems);
        
        // 只取消勾选，不真正删除项目
        foreach (var item in itemsToUnselect)
        {
            if (item != null && item.isUIControl)
            {
                item.isSelected = false;
            }
        }
        
        // 清除选择状态
        selectedItems.Clear();
        lastSelectedItem = null;
        
        Repaint();
    }

    /// <summary>
    /// 处理Delete键删除（在线性模式下只取消勾选）
    /// </summary>
    private void HandleDeleteKey()
    {
        // 只在线性模式下处理删除
        if (displayMode != DisplayMode.Linear) return;
        
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Delete)
        {
            if (selectedItems.Count > 0)
            {
                // 收集要取消勾选的项目（避免在迭代时修改集合）
                var itemsToUnselect = new List<ControlMappingItem>(selectedItems);
                
                // 只取消勾选，不真正删除项目
                foreach (var item in itemsToUnselect)
                {
                    if (item != null && item.isUIControl)
                    {
                        item.isSelected = false;
                    }
                }
                
                // 清除选择状态
                selectedItems.Clear();
                lastSelectedItem = null;
                
                Event.current.Use();
                Repaint();
            }
        }
    }

    /// <summary>
    /// 确认字段名
    /// </summary>
    private void ConfirmFieldName(int index, string newFieldName)
    {
        if (index < 0 || index >= controlMappings.Count) return;

        var item = controlMappings[index];
        
        // 验证字段名
        if (ValidateFieldName(newFieldName, index))
        {
            item.fieldName = newFieldName;
        }
        else
        {
            // 验证失败，恢复原值
            item.fieldName = item.controlName;
        }

        editingFieldNames.Remove(index);
    }

    /// <summary>
    /// 验证字段名
    /// </summary>
    private bool ValidateFieldName(string fieldName, int excludeIndex)
    {
        if (string.IsNullOrEmpty(fieldName))
        {
            EditorUtility.DisplayDialog("错误", "字段名不能为空", "确定");
            return false;
        }

        // 检查C#标识符
        if (!IsValidCSharpIdentifier(fieldName))
        {
            EditorUtility.DisplayDialog("错误", $"字段名 '{fieldName}' 不是有效的C#标识符", "确定");
            return false;
        }

        // 检查唯一性
        for (int i = 0; i < controlMappings.Count; i++)
        {
            if (i == excludeIndex) continue;
            
            string otherFieldName = editingFieldNames.ContainsKey(i) 
                ? editingFieldNames[i] 
                : controlMappings[i].fieldName;

            if (otherFieldName == fieldName)
            {
                EditorUtility.DisplayDialog("错误", $"字段名 '{fieldName}' 已存在", "确定");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 检查是否为有效的C#标识符
    /// </summary>
    private bool IsValidCSharpIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier)) return false;
        if (char.IsDigit(identifier[0])) return false;

        foreach (char c in identifier)
        {
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        // 检查是否为C#关键字
        string[] keywords = { "class", "public", "private", "protected", "void", "int", "string", "bool", "float", "double" };
        if (Array.Exists(keywords, k => k == identifier))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 绘制底部操作按钮
    /// </summary>
    private void DrawBottomActions()
    {
        // 根据显示模式显示不同的操作按钮
        EditorGUILayout.BeginHorizontal();
        if (displayMode == DisplayMode.Tree)
        {
            // 树状模式：显示全选和取消选择按钮
            if (GUILayout.Button("勾选选择的控件", GUILayout.Height(25)))
            {
                SelectAllUIControls();
            }
            if (GUILayout.Button("取消勾选", GUILayout.Height(25)))
            {
                UnselectAllUIControls();
            }
        }
        else
        {
            // 线性模式：显示删除按钮
            if (GUILayout.Button("取消勾选", GUILayout.Height(25)))
            {
                DeleteSelectedItems();
            }
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("生成代码", GUILayout.Height(35)))
        {
            ConfirmAllEditingFieldNames();
            
            // 检查是否有重复的字段名
            if (HasDuplicateFieldNames(out List<string> duplicateNames))
            {
                string duplicateList = string.Join(", ", duplicateNames);
                EditorUtility.DisplayDialog("错误", 
                    $"检测到重复的字段名，请修改后再生成代码：\n{duplicateList}", 
                    "确定");
            }
            else
            {
            GenerateCode();
            }
        }
    }

    /// <summary>
    /// 确认所有正在编辑的字段名
    /// </summary>
    private void ConfirmAllEditingFieldNames()
    {
        var keys = new List<int>(editingFieldNames.Keys);
        foreach (var key in keys)
        {
            ConfirmFieldName(key, editingFieldNames[key]);
        }
    }

    /// <summary>
    /// 检查是否有重复的字段名
    /// </summary>
    private bool HasDuplicateFieldNames(out List<string> duplicateNames)
    {
        duplicateNames = new List<string>();
        
        if (controlMappings == null || controlMappings.Count == 0)
        {
            return false;
        }

        // 统计每个字段名的出现次数（只对UI控件）
        var fieldNameCount = new Dictionary<string, int>();
        
        for (int i = 0; i < controlMappings.Count; i++)
        {
            var item = controlMappings[i];
            
            // 只检查UI控件
            if (!item.isUIControl) continue;
            
            // 获取当前字段名（包括正在编辑的）
            string fieldName = editingFieldNames.ContainsKey(i)
                ? editingFieldNames[i]
                : item.fieldName;

            if (string.IsNullOrEmpty(fieldName)) continue;

            if (fieldNameCount.ContainsKey(fieldName))
            {
                fieldNameCount[fieldName]++;
            }
            else
            {
                fieldNameCount[fieldName] = 1;
            }
        }

        // 找出所有重复的字段名
        foreach (var kvp in fieldNameCount)
        {
            if (kvp.Value > 1)
            {
                duplicateNames.Add(kvp.Key);
            }
        }

        return duplicateNames.Count > 0;
    }

    /// <summary>
    /// 绘制代码已生成状态
    /// </summary>
    private void DrawCodeGeneratedState()
    {
        EditorGUILayout.HelpBox("代码已生成，等待编译完成后将自动挂载脚本并绑定控件", MessageType.Info);
    }

    /// <summary>
    /// 扫描控件
    /// </summary>
    private void ScanControlsForSelectedObject()
    {
        if (targetGameObject == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择根对象", "确定");
            return;
        }

        controlMappings.Clear();
        editingFieldNames.Clear();

        // 扫描所有GameObject
        ScanAllGameObjects(targetGameObject);

        // 自动处理重复的字段名（只对UI控件）
        ResolveDuplicateFieldNames();

        // 构建树状结构
        BuildTreeStructure();

        currentState = UIToolState.ControlsScanned;
        Debug.Log($"扫描完成，找到 {controlMappings.Count} 个控件");
    }

    /// <summary>
    /// 检查GameObject是否包含可绑定的UI组件
    /// </summary>
    private bool IsUIControl(GameObject go)
    {
        if (go == null) return false;

        // 检查所有可绑定的UI组件类型
        return go.GetComponent<Button>() != null ||
               go.GetComponent<Toggle>() != null ||
               go.GetComponent<Slider>() != null ||
               go.GetComponent<InputField>() != null ||
               go.GetComponent<ScrollRect>() != null ||
               go.GetComponent<Dropdown>() != null ||
               go.GetComponent<Text>() != null ||
               go.GetComponent<TextMeshProUGUI>() != null ||
               go.GetComponent<Image>() != null;
    }

    /// <summary>
    /// 获取GameObject上的UI组件，优先选择主要类型，如果有多个主要类型则返回null并报错
    /// </summary>
    private Component GetUIControlComponent(GameObject go)
    {
        if (go == null) return null;

        List<Component> primaryComponents = new List<Component>();  // 主要类型：Button, ScrollRect, Toggle, Slider, InputField, Dropdown
        List<Component> secondaryComponents = new List<Component>(); // 辅助类型：Image, Text, TextMeshProUGUI

        // 收集主要类型组件
        Component comp = go.GetComponent<Button>();
        if (comp != null) primaryComponents.Add(comp);
        comp = go.GetComponent<ScrollRect>();
        if (comp != null) primaryComponents.Add(comp);
        comp = go.GetComponent<Toggle>();
        if (comp != null) primaryComponents.Add(comp);
        comp = go.GetComponent<Slider>();
        if (comp != null) primaryComponents.Add(comp);
        comp = go.GetComponent<InputField>();
        if (comp != null) primaryComponents.Add(comp);
        comp = go.GetComponent<Dropdown>();
        if (comp != null) primaryComponents.Add(comp);

        // 收集辅助类型组件
        comp = go.GetComponent<Image>();
        if (comp != null) secondaryComponents.Add(comp);
        comp = go.GetComponent<Text>();
        if (comp != null) secondaryComponents.Add(comp);
        comp = go.GetComponent<TextMeshProUGUI>();
        if (comp != null) secondaryComponents.Add(comp);

        // 优先处理主要类型
        if (primaryComponents.Count > 1)
        {
            // 多个主要类型组件，报错
            List<string> componentNames = new List<string>();
            foreach (var component in primaryComponents)
            {
                componentNames.Add(component.GetType().Name);
            }
            string componentNamesStr = string.Join(", ", componentNames);
            EditorUtility.DisplayDialog("错误", 
                $"GameObject '{go.name}' 包含多个主要UI组件：{componentNamesStr}\n每个GameObject只能有一个主要UI组件。", 
                "确定");
            return null;
        }
        else if (primaryComponents.Count == 1)
        {
            // 只有一个主要类型组件，返回它（忽略辅助类型）
            return primaryComponents[0];
        }
        else if (secondaryComponents.Count > 0)
        {
            // 没有主要类型，但有辅助类型，返回第一个辅助类型（作为后备）
            return secondaryComponents[0];
        }
        else
        {
            // 没有UI组件
            return null;
        }
    }

    /// <summary>
    /// 递归扫描所有GameObject，为每个创建ControlMappingItem
    /// </summary>
    private void ScanAllGameObjects(GameObject root)
    {
        if (root == null) return;

        // 递归扫描所有子对象
        ScanGameObjectRecursive(root, root);
    }

    /// <summary>
    /// 递归扫描GameObject
    /// </summary>
    private void ScanGameObjectRecursive(GameObject current, GameObject root)
    {
        if (current == null) return;

        // 检查对象名是否在默认排除列表中
        bool isExcludedName = defaultExcludeNames.Contains(current.name);

        // 获取UI组件
        Component uiComponent = GetUIControlComponent(current);
        bool hasUIControl = uiComponent != null;

        // 如果是有UI组件的对象，或者不是排除名称的对象，都添加到列表
        // 注意：即使名字在排除列表中，如果有UI组件也会添加到列表（用于显示），但默认不选择
        if (hasUIControl || !isExcludedName)
        {
            string path = GetControlPath(current, root);
            string controlName = current.name;

            // 确定类型和字段名
            Type controlType = null;
            string fieldName = "";
            
            if (hasUIControl)
            {
                controlType = uiComponent.GetType();
                fieldName = ToCamelCase(controlName);
            }
            else
            {
                // 非UI控件，类型为null
                controlType = null;
                fieldName = "";
            }

            // 创建映射项
            var mapping = new ControlMappingItem(controlName, controlType, path, current, uiComponent);
            mapping.fieldName = fieldName;
            mapping.isUIControl = hasUIControl;
            
            // 设置默认选择状态：
            // - 如果名字在defaultExcludeNames列表中，即使有UI组件也默认不选择（isSelected = false）
            // - 否则，只有UI控件默认选中（isSelected = true），非UI控件不选中（isSelected = false）
            if (isExcludedName)
            {
                mapping.isSelected = false; // 默认名称的控件默认不选择
            }
            else
            {
                mapping.isSelected = hasUIControl; // 只有UI控件默认选中
            }
            
            controlMappings.Add(mapping);
        }

        // 递归处理子对象
        foreach (Transform child in current.transform)
        {
            ScanGameObjectRecursive(child.gameObject, root);
        }
    }

    /// <summary>
    /// 获取控件路径
    /// </summary>
    private string GetControlPath(GameObject control, GameObject root)
    {
        List<string> pathParts = new List<string>();
        Transform current = control.transform;

        while (current != null && current.gameObject != root)
        {
            pathParts.Insert(0, current.name);
            current = current.parent;
        }

        return string.Join("/", pathParts);
    }

    /// <summary>
    /// 自动处理重复的字段名，添加数字后缀
    /// </summary>
    private void ResolveDuplicateFieldNames()
    {
        if (controlMappings == null || controlMappings.Count == 0) return;

        // 使用HashSet跟踪已使用的字段名（只对UI控件）
        var usedFieldNames = new HashSet<string>();
        
        for (int i = 0; i < controlMappings.Count; i++)
        {
            var item = controlMappings[i];
            
            // 只处理UI控件
            if (!item.isUIControl || string.IsNullOrEmpty(item.fieldName))
            {
                continue;
            }
            
            string originalFieldName = item.fieldName;
            string finalFieldName = originalFieldName;
            
            // 如果字段名已使用，添加数字后缀
            if (usedFieldNames.Contains(finalFieldName))
            {
                int suffix = 1;
                do
                {
                    finalFieldName = originalFieldName + suffix;
                    suffix++;
                } while (usedFieldNames.Contains(finalFieldName));
                
                item.fieldName = finalFieldName;
                Debug.Log($"[UIPanelTool] 字段名重复，将 '{item.controlName}' 的字段名改为 '{finalFieldName}'");
            }
            
            usedFieldNames.Add(finalFieldName);
        }
    }

    /// <summary>
    /// 构建树状结构
    /// </summary>
    private void BuildTreeStructure()
    {
        if (controlMappings == null || controlMappings.Count == 0)
        {
            treeRoots.Clear();
            return;
        }

        // 清除所有节点的父子关系
        foreach (var item in controlMappings)
        {
            item.parent = null;
            item.children.Clear();
        }

        // 创建GameObject到ControlMappingItem的映射
        Dictionary<GameObject, ControlMappingItem> goToItemMap = new Dictionary<GameObject, ControlMappingItem>();
        foreach (var item in controlMappings)
        {
            if (item.gameObject != null)
            {
                goToItemMap[item.gameObject] = item;
            }
        }

        // 构建父子关系
        treeRoots.Clear();
        foreach (var item in controlMappings)
        {
            if (item.gameObject == null) continue;

            Transform parentTransform = item.gameObject.transform.parent;
            
            // 查找父节点（如果父节点也在映射列表中）
            ControlMappingItem parentItem = null;
            while (parentTransform != null && parentItem == null)
            {
                if (goToItemMap.TryGetValue(parentTransform.gameObject, out parentItem))
                {
                    break;
                }
                parentTransform = parentTransform.parent;
            }

            if (parentItem != null)
            {
                // 找到父节点，建立关系
                item.parent = parentItem;
                parentItem.children.Add(item);
            }
            else
            {
                // 没有父节点，是根节点
                treeRoots.Add(item);
            }
        }

        // 默认展开所有节点
        expandedNodes.Clear();
        foreach (var item in controlMappings)
        {
            if (item.children.Count > 0)
            {
                expandedNodes.Add(item);
            }
        }
    }

    /// <summary>
    /// 生成代码
    /// </summary>
    private void GenerateCode()
    {
        if (targetGameObject == null || string.IsNullOrEmpty(panelName))
        {
            EditorUtility.DisplayDialog("错误", "目标对象或面板名称为空", "确定");
            return;
        }

        // 获取选中的UI控件（只处理UI控件）
        var selectedMappings = controlMappings.Where(item => item.isSelected && item.isUIControl).ToList();
        if (selectedMappings.Count == 0)
        {
            EditorUtility.DisplayDialog("错误", "请至少选择一个UI控件", "确定");
            return;
        }

        // 规范化路径（提前定义，供后续使用）
        string normalizedPath = savePath.Replace('\\', '/').TrimEnd('/');
        if (!normalizedPath.StartsWith("Assets/"))
        {
            normalizedPath = "Assets/" + normalizedPath.TrimStart('/');
        }
        string systemPath = normalizedPath.Replace("Assets/", Application.dataPath + "/");
        
        // 编译前检查：检查子类是否有编译错误
        string childFilePath = System.IO.Path.Combine(systemPath, $"{panelName}.cs");
        if (System.IO.File.Exists(childFilePath))
        {
            if (!CheckCompileErrors(childFilePath))
            {
                // 有编译错误，询问是否继续
                bool forceGenerate = EditorUtility.DisplayDialog("编译错误警告", 
                    "检测到子类中存在编译错误，这可能导致代码生成后无法自动挂载脚本。\n\n" +
                    "建议先修复编译错误后再生成代码。\n\n" +
                    "是否强制生成？", "强制生成", "取消");
                
                if (!forceGenerate)
                {
                    Debug.Log("[UIPanelTool] 用户取消了代码生成（存在编译错误）");
                    return;
                }
            }
        }

        // 加载模板
        string baseTemplatePath = "Assets/Editor/UIPanelTool/UIConfigBase.txt";
        string childTemplatePath = "Assets/Editor/UIPanelTool/UIConfig.txt";

        TextAsset baseTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(baseTemplatePath);
        TextAsset childTemplate = AssetDatabase.LoadAssetAtPath<TextAsset>(childTemplatePath);

        if (baseTemplate == null || childTemplate == null)
        {
            EditorUtility.DisplayDialog("错误", "找不到模板文件", "确定");
            return;
        }

        // 生成字典声明代码
        string dictionaryCode = GenerateDictionaryCode(selectedMappings);

        // 生成事件监听代码
        string listenerCode = GenerateListenerCode(selectedMappings);

        // 生成事件响应函数代码
        string functionCode = GenerateFunctionCode(selectedMappings);

        // 生成Base类代码
        string baseCode = string.Format(baseTemplate.text, 
            panelName + "Base",
            dictionaryCode,
            listenerCode,
            functionCode);

        // 生成子类代码
        string childCode = string.Format(childTemplate.text, panelName, panelName + "Base");

        // 保存文件（路径已在前面定义）
        if (!System.IO.Directory.Exists(systemPath))
        {
            System.IO.Directory.CreateDirectory(systemPath);
        }
        
        string baseFilePath = System.IO.Path.Combine(systemPath, $"{panelName}Base.cs");
        childFilePath = System.IO.Path.Combine(systemPath, $"{panelName}.cs");

        // 安全检查：检查文件是否已存在
        bool baseFileExists = System.IO.File.Exists(baseFilePath);
        bool childFileExists = System.IO.File.Exists(childFilePath);

        bool shouldWriteBase = true;
        bool shouldWriteChild = true;

        // 合并模式：0=完全覆盖, 1=智能合并, 2=取消
        int mergeMode = 0;
        
        if (baseFileExists || childFileExists)
        {
            string message = "检测到以下文件已存在：\n";
            if (baseFileExists) message += $"• {panelName}Base.cs\n";
            if (childFileExists) message += $"• {panelName}.cs\n";
            message += "\n请选择处理方式：";

            // 如果Base类存在，提供智能合并选项
            if (baseFileExists && childFileExists)
            {
                int choice = EditorUtility.DisplayDialogComplex("警告", message, 
                    "智能合并（推荐）", "取消", "完全覆盖");
                
                if (choice == 0) // 智能合并
                {
                    mergeMode = 1;
                    shouldWriteBase = true;
                    shouldWriteChild = false; // 子类不覆盖
                }
                else if (choice == 1) // 取消
                {
                    Debug.Log("[UIPanelTool] 用户取消了代码生成");
                    return;
                }
                else // 完全覆盖
                {
                    mergeMode = 0;
                    shouldWriteBase = true;
                    shouldWriteChild = true;
                }
            }
            else if (baseFileExists)
            {
                int choice = EditorUtility.DisplayDialogComplex("警告", message + 
                    $"\n\nBase类 {panelName}Base.cs 已存在", 
                    "智能合并（推荐）", "取消", "完全覆盖");
                
                if (choice == 0) // 智能合并
                {
                    mergeMode = 1;
                    shouldWriteBase = true;
                }
                else if (choice == 1) // 取消
                {
                    Debug.Log("[UIPanelTool] 用户取消了代码生成");
                    return;
                }
                else // 完全覆盖
                {
                    mergeMode = 0;
                    shouldWriteBase = true;
                }
            }
            else if (childFileExists)
            {
                bool overwrite = EditorUtility.DisplayDialog("警告", message + 
                    $"\n\n是否覆盖 {panelName}.cs？\n注意：子类中的自定义代码将被丢失！", "覆盖", "取消");
                if (!overwrite)
                {
                    Debug.Log("[UIPanelTool] 用户取消了代码生成");
                    return;
                }
            }
        }

        // 根据用户选择写入文件
        if (shouldWriteBase)
        {
            string finalBaseCode = baseCode;
            
            // 如果是智能合并模式，合并现有代码
            if (mergeMode == 1 && baseFileExists)
            {
                try
                {
                    string existingBaseCode = System.IO.File.ReadAllText(baseFilePath, Encoding.UTF8);
                    finalBaseCode = MergeBaseClass(existingBaseCode, baseCode, selectedMappings, childFilePath);
                    Debug.Log($"[UIPanelTool] 已智能合并 Base 类: {panelName}Base.cs");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[UIPanelTool] 智能合并失败，使用完全覆盖: {ex.Message}");
                    finalBaseCode = baseCode; // 回退到完全覆盖
                }
            }
            
            System.IO.File.WriteAllText(baseFilePath, finalBaseCode, Encoding.UTF8);
            Debug.Log($"[UIPanelTool] 已生成/覆盖 Base 类: {panelName}Base.cs");
        }
        else
        {
            Debug.Log($"[UIPanelTool] 跳过 Base 类（文件已存在）: {panelName}Base.cs");
        }

        if (shouldWriteChild)
        {
        System.IO.File.WriteAllText(childFilePath, childCode, Encoding.UTF8);
            Debug.Log($"[UIPanelTool] 已生成/覆盖子类: {panelName}.cs");
        }
        else
        {
            Debug.Log($"[UIPanelTool] 跳过子类（文件已存在）: {panelName}.cs");
        }

        AssetDatabase.Refresh();

        // 保存映射信息
        SaveMappingData(selectedMappings);

        // 注册编译完成事件（双重保障）
        CompilationPipeline.assemblyCompilationFinished -= CompilationPipeline_assemblyCompilationFinished;
        CompilationPipeline.assemblyCompilationFinished += CompilationPipeline_assemblyCompilationFinished;

        Debug.Log($"代码已生成，等待编译完成。待处理映射数量: {pendingMappings.Count}");

        EditorUtility.DisplayDialog("成功", "代码已生成，编译完成后将自动挂载脚本并绑定控件", "确定");
        currentState = UIToolState.CodeGenerated;
    }

    /// <summary>
    /// 生成字段声明代码
    /// </summary>
    private string GenerateDictionaryCode(List<ControlMappingItem> mappings)
    {
        StringBuilder sb = new StringBuilder();
        
        foreach (var mapping in mappings)
        {
            string fieldName = mapping.fieldName;
            Type type = mapping.controlType;
            string typeName = GetTypeName(type);
            
            // 每行都需要4个空格缩进（模板中{1}的位置有4个空格）
            sb.AppendLine($"    public {typeName} {fieldName};");
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 获取类型名称
    /// </summary>
    private string GetTypeName(Type type)
    {
        if (type == typeof(Button)) return "Button";
        if (type == typeof(Toggle)) return "Toggle";
        if (type == typeof(Slider)) return "Slider";
        if (type == typeof(InputField)) return "InputField";
        if (type == typeof(ScrollRect)) return "ScrollRect";
        if (type == typeof(Dropdown)) return "Dropdown";
        if (type == typeof(Text)) return "Text";
        if (type == typeof(TextMeshProUGUI)) return "TextMeshProUGUI";
        if (type == typeof(Image)) return "Image";
        return type.Name;
    }

    /// <summary>
    /// 生成事件监听代码
    /// </summary>
    private string GenerateListenerCode(List<ControlMappingItem> mappings)
    {
        StringBuilder sb = new StringBuilder();
        
        foreach (var mapping in mappings)
        {
            string fieldName = mapping.fieldName;
            Type type = mapping.controlType;

            if (type == typeof(Button))
            {
                // 每行都需要8个空格缩进（模板中{2}的位置在Start方法内，有8个空格）
                sb.AppendLine($"        {fieldName}.onClick.AddListener(On{fieldName}Click);");
            }
            else if (type == typeof(Slider))
            {
                sb.AppendLine($"        {fieldName}.onValueChanged.AddListener(On{fieldName}ValueChanged);");
            }
            else if (type == typeof(Toggle))
            {
                sb.AppendLine($"        {fieldName}.onValueChanged.AddListener(On{fieldName}ValueChanged);");
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 生成事件响应函数代码
    /// </summary>
    private string GenerateFunctionCode(List<ControlMappingItem> mappings)
    {
        StringBuilder sb = new StringBuilder();
        
        foreach (var mapping in mappings)
        {
            string fieldName = mapping.fieldName;
            Type type = mapping.controlType;

            if (type == typeof(Button))
            {
                // 每行都需要4个空格缩进（模板中{3}的位置有4个空格）
                sb.AppendLine($"    protected virtual void On{fieldName}Click(){{}}");
            }
            else if (type == typeof(Slider))
            {
                sb.AppendLine($"    protected virtual void On{fieldName}ValueChanged(float value){{}}");
            }
            else if (type == typeof(Toggle))
            {
                sb.AppendLine($"    protected virtual void On{fieldName}ValueChanged(bool value){{}}");
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// 智能合并Base类：保留已有的事件函数实现，只更新字段声明和事件监听
    /// </summary>
    private string MergeBaseClass(string existingCode, string newCode, List<ControlMappingItem> newMappings, string childFilePath = null)
    {
        // 解析现有代码，提取事件函数实现
        Dictionary<string, string> existingFunctions = ExtractEventFunctions(existingCode);
        
        // 提取子类中的override方法（如果子类文件存在）
        Dictionary<string, string> childOverrideMethods = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(childFilePath) && System.IO.File.Exists(childFilePath))
        {
            try
            {
                string childCode = System.IO.File.ReadAllText(childFilePath, Encoding.UTF8);
                childOverrideMethods = ExtractChildOverrideMethods(childCode);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UIPanelTool] 读取子类文件失败: {ex.Message}");
            }
        }
        
        // 使用新代码作为基础，但替换事件函数部分
        string[] newCodeLines = newCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        StringBuilder mergedCode = new StringBuilder();
        
        bool inFunctionsSection = false;
        bool functionsReplaced = false;
        
        // 遍历新代码，替换事件函数部分
        for (int i = 0; i < newCodeLines.Length; i++)
        {
            string line = newCodeLines[i];
            string trimmedLine = line.Trim();
            
            // 检测事件函数区域开始
            if (trimmedLine.Contains("//自动生成的对应进行监听事件的响应函数"))
            {
                inFunctionsSection = true;
                mergedCode.AppendLine(line);
                
                // 添加合并后的事件函数
                foreach (var mapping in newMappings)
                {
                    string fieldName = mapping.fieldName;
                    Type type = mapping.controlType;
                    string functionName = "";
                    string functionSignature = "";
                    
                    if (type == typeof(Button))
                    {
                        functionName = $"On{fieldName}Click";
                        functionSignature = $"    protected virtual void On{fieldName}Click()";
                    }
                    else if (type == typeof(Slider))
                    {
                        functionName = $"On{fieldName}ValueChanged";
                        functionSignature = $"    protected virtual void On{fieldName}ValueChanged(float value)";
                    }
                    else if (type == typeof(Toggle))
                    {
                        functionName = $"On{fieldName}ValueChanged";
                        functionSignature = $"    protected virtual void On{fieldName}ValueChanged(bool value)";
                    }
                    
                    if (!string.IsNullOrEmpty(functionName))
                    {
                        // 如果已有实现，使用已有的；否则使用新的空实现
                        if (existingFunctions.ContainsKey(functionName))
                        {
                            string normalizedFunction = NormalizeFunctionIndentation(existingFunctions[functionName]);
                            mergedCode.AppendLine(normalizedFunction);
                        }
                        else
                        {
                            mergedCode.AppendLine(functionSignature + " { }");
                        }
                    }
                }
                
                // 检查子类中的override方法，如果Base类中没有对应的virtual方法，则添加它们
                HashSet<string> addedFunctionNames = new HashSet<string>();
                foreach (var mapping in newMappings)
                {
                    string fieldName = mapping.fieldName;
                    Type type = mapping.controlType;
                    string functionName = "";
                    
                    if (type == typeof(Button))
                    {
                        functionName = $"On{fieldName}Click";
                    }
                    else if (type == typeof(Slider) || type == typeof(Toggle))
                    {
                        functionName = $"On{fieldName}ValueChanged";
                    }
                    
                    if (!string.IsNullOrEmpty(functionName))
                    {
                        addedFunctionNames.Add(functionName);
                    }
                }
                
                // 添加子类中需要但Base类中缺失的virtual方法
                // 或者保留Base类中已有但新字段列表中没有的方法（如果子类中有override）
                foreach (var kvp in childOverrideMethods)
                {
                    string functionName = kvp.Key;
                    string functionSignature = kvp.Value;
                    
                    // 如果这个函数不在新字段列表中
                    if (!addedFunctionNames.Contains(functionName))
                    {
                        // 如果Base类中原本有这个方法，保留它
                        if (existingFunctions.ContainsKey(functionName))
                        {
                            mergedCode.AppendLine();
                            mergedCode.AppendLine($"    // TODO: 字段已删除，但子类中仍有override实现，请手动清理");
                            string normalizedFunction = NormalizeFunctionIndentation(existingFunctions[functionName]);
                            mergedCode.AppendLine(normalizedFunction);
                        }
                        // 如果Base类中也没有，添加新的空方法
                        else
                        {
                            mergedCode.AppendLine();
                            mergedCode.AppendLine($"    // TODO: 字段已删除，但子类中仍有override实现，请手动清理");
                            mergedCode.AppendLine($"    {functionSignature} {{ }}");
                        }
                    }
                }
                
                functionsReplaced = true;
                // 跳过新代码中的事件函数部分
                i++;
                while (i < newCodeLines.Length)
                {
                    string nextLine = newCodeLines[i];
                    string nextTrimmed = nextLine.Trim();
                    
                    // 如果遇到类结束的大括号，保留它
                    if (nextTrimmed == "}")
                    {
                        mergedCode.AppendLine(nextLine);
                        break;
                    }
                    
                    // 跳过空的事件函数定义
                    if (nextTrimmed.StartsWith("protected virtual void On") && 
                        (nextTrimmed.Contains("Click()") || nextTrimmed.Contains("ValueChanged")))
                    {
                        // 跳过整个函数定义（包括大括号）
                        int braceCount = 0;
                        bool inFunction = false;
                        while (i < newCodeLines.Length)
                        {
                            string funcLine = newCodeLines[i];
                            foreach (char c in funcLine)
                            {
                                if (c == '{')
                                {
                                    braceCount++;
                                    inFunction = true;
                                }
                                else if (c == '}')
                                {
                                    braceCount--;
                                    if (braceCount == 0 && inFunction)
                                    {
                                        i++;
                                        goto NextFunction;
                                    }
                                }
                            }
                            i++;
                        }
                        NextFunction:
                        continue;
                    }
                    
                    i++;
                }
                break;
            }
            else
            {
                mergedCode.AppendLine(line);
            }
        }
        
        // 如果事件函数部分没有被替换，在末尾添加
        if (!functionsReplaced)
        {
            mergedCode.AppendLine();
            mergedCode.AppendLine("    //自动生成的对应进行监听事件的响应函数");
            
            foreach (var mapping in newMappings)
            {
                string fieldName = mapping.fieldName;
                Type type = mapping.controlType;
                string functionName = "";
                string functionSignature = "";
                
                if (type == typeof(Button))
                {
                    functionName = $"On{fieldName}Click";
                    functionSignature = $"    protected virtual void On{fieldName}Click()";
                }
                else if (type == typeof(Slider))
                {
                    functionName = $"On{fieldName}ValueChanged";
                    functionSignature = $"    protected virtual void On{fieldName}ValueChanged(float value)";
                }
                else if (type == typeof(Toggle))
                {
                    functionName = $"On{fieldName}ValueChanged";
                    functionSignature = $"    protected virtual void On{fieldName}ValueChanged(bool value)";
                }
                
                if (!string.IsNullOrEmpty(functionName))
                {
                    if (existingFunctions.ContainsKey(functionName))
                    {
                        string normalizedFunction = NormalizeFunctionIndentation(existingFunctions[functionName]);
                        mergedCode.AppendLine(normalizedFunction);
                    }
                    else
                    {
                        mergedCode.AppendLine(functionSignature + " { }");
                    }
                }
            }
            
            // 检查子类中的override方法，如果Base类中没有对应的virtual方法，则添加它们
            HashSet<string> addedFunctionNames = new HashSet<string>();
            foreach (var mapping in newMappings)
            {
                string fieldName = mapping.fieldName;
                Type type = mapping.controlType;
                string functionName = "";
                
                if (type == typeof(Button))
                {
                    functionName = $"On{fieldName}Click";
                }
                else if (type == typeof(Slider) || type == typeof(Toggle))
                {
                    functionName = $"On{fieldName}ValueChanged";
                }
                
                if (!string.IsNullOrEmpty(functionName))
                {
                    addedFunctionNames.Add(functionName);
                }
            }
            
            // 添加子类中需要但Base类中缺失的virtual方法
            // 或者保留Base类中已有但新字段列表中没有的方法（如果子类中有override）
            foreach (var kvp in childOverrideMethods)
            {
                string functionName = kvp.Key;
                string functionSignature = kvp.Value;
                
                // 如果这个函数不在新字段列表中
                if (!addedFunctionNames.Contains(functionName))
                {
                    // 如果Base类中原本有这个方法，保留它
                    if (existingFunctions.ContainsKey(functionName))
                    {
                        mergedCode.AppendLine();
                        mergedCode.AppendLine($"    // TODO: 字段已删除，但子类中仍有override实现，请手动清理");
                        string normalizedFunction = NormalizeFunctionIndentation(existingFunctions[functionName]);
                        mergedCode.AppendLine(normalizedFunction);
                    }
                    // 如果Base类中也没有，添加新的空方法
                    else
                    {
                        mergedCode.AppendLine();
                        mergedCode.AppendLine($"    // TODO: 字段已删除，但子类中仍有override实现，请手动清理");
                        mergedCode.AppendLine($"    {functionSignature} {{ }}");
                    }
                }
            }
            
            mergedCode.AppendLine("}");
        }
        
        return mergedCode.ToString();
    }

    /// <summary>
    /// 从现有Base类代码中提取事件函数实现
    /// </summary>
    private Dictionary<string, string> ExtractEventFunctions(string code)
    {
        Dictionary<string, string> functions = new Dictionary<string, string>();
        
        // 匹配事件函数：protected virtual void OnXXXClick() 或 OnXXXValueChanged(float/bool value)
        Regex functionRegex = new Regex(@"protected\s+virtual\s+void\s+(On\w+(?:Click|ValueChanged))\s*\([^)]*\)\s*\{", RegexOptions.Multiline);
        MatchCollection matches = functionRegex.Matches(code);
        
        foreach (Match match in matches)
        {
            string functionName = match.Groups[1].Value;
            int startPos = match.Index;
            int braceDepth = 0;
            int pos = startPos;
            bool inFunction = false;
            
            // 找到函数体的开始
            while (pos < code.Length)
            {
                if (code[pos] == '{')
                {
                    braceDepth++;
                    inFunction = true;
                }
                else if (code[pos] == '}')
                {
                    braceDepth--;
                    if (braceDepth == 0 && inFunction)
                    {
                        // 函数结束
                        string functionCode = code.Substring(startPos, pos - startPos + 1);
                        functions[functionName] = functionCode;
                        break;
                    }
                }
                pos++;
            }
        }
        
        return functions;
    }

    /// <summary>
    /// 从子类代码中提取override的事件函数签名
    /// 返回字典：方法名 -> 完整方法签名（包括参数）
    /// </summary>
    private Dictionary<string, string> ExtractChildOverrideMethods(string childCode)
    {
        Dictionary<string, string> overrideMethods = new Dictionary<string, string>();
        
        // 匹配override事件函数：protected override void OnXXXClick() 或 OnXXXValueChanged(float/bool value)
        // 捕获完整的方法签名（包括参数类型）
        Regex overrideRegex = new Regex(@"protected\s+override\s+void\s+(On\w+(?:Click|ValueChanged))\s*\(([^)]*)\)", RegexOptions.Multiline);
        MatchCollection matches = overrideRegex.Matches(childCode);
        
        foreach (Match match in matches)
        {
            string functionName = match.Groups[1].Value;
            string parameters = match.Groups[2].Value.Trim();
            
            // 构建完整的方法签名
            string fullSignature = $"protected virtual void {functionName}({parameters})";
            overrideMethods[functionName] = fullSignature;
        }
        
        return overrideMethods;
    }

    /// <summary>
    /// 规范化方法代码的缩进，确保函数签名有4个空格的缩进
    /// </summary>
    private string NormalizeFunctionIndentation(string functionCode)
    {
        if (string.IsNullOrEmpty(functionCode))
            return functionCode;

        string[] lines = functionCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        StringBuilder normalized = new StringBuilder();
        
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.TrimStart();
            
            // 跳过空行
            if (string.IsNullOrEmpty(trimmed))
            {
                normalized.AppendLine();
                continue;
            }
            
            // 第一行（函数签名）必须有4个空格缩进
            if (i == 0)
            {
                normalized.AppendLine($"    {trimmed}");
            }
            // 其他行保持相对缩进（相对于第一行）
            else
            {
                // 计算原行的缩进（相对于第一行）
                int originalIndent = line.Length - line.TrimStart().Length;
                int firstLineIndent = lines[0].Length - lines[0].TrimStart().Length;
                
                // 计算相对缩进
                int relativeIndent = originalIndent - firstLineIndent;
                
                // 如果第一行没有缩进，其他行保持原缩进
                if (firstLineIndent == 0)
                {
                    normalized.AppendLine(line);
                }
                // 否则，第一行4个空格，其他行保持相对缩进
                else
                {
                    int newIndent = 4 + Math.Max(0, relativeIndent);
                    normalized.AppendLine(new string(' ', newIndent) + trimmed);
                }
            }
        }
        
        return normalized.ToString().TrimEnd('\r', '\n');
    }

    /// <summary>
    /// 检查子类文件是否有编译错误
    /// </summary>
    private bool CheckCompileErrors(string filePath)
    {
        try
        {
            // 读取文件内容
            string fileContent = System.IO.File.ReadAllText(filePath, Encoding.UTF8);
            
            // 基本语法检查：检查大括号和括号是否匹配
            int openBraces = 0;
            int closeBraces = 0;
            int openParens = 0;
            int closeParens = 0;
            
            foreach (char c in fileContent)
            {
                if (c == '{') openBraces++;
                else if (c == '}') closeBraces++;
                else if (c == '(') openParens++;
                else if (c == ')') closeParens++;
            }
            
            // 基本语法检查
            if (openBraces != closeBraces || openParens != closeParens)
            {
                Debug.LogWarning($"[UIPanelTool] 检测到语法错误：大括号或括号不匹配");
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UIPanelTool] 检查编译错误时发生异常: {ex.Message}");
            return true; // 如果检查失败，假设没有错误，允许继续
        }
    }

    /// <summary>
    /// 保存映射数据
    /// </summary>
    private void SaveMappingData(List<ControlMappingItem> selectedMappings)
    {
        if (targetGameObject == null || string.IsNullOrEmpty(panelName))
        {
            Debug.LogWarning("SaveMappingData: targetGameObject 或 panelName 为空");
            return;
        }

        string path = GetGameObjectPath(targetGameObject);

        // 获取或初始化 pendingMappings
        var mappings = GetPendingMappings();
        
        // 移除该面板的旧数据
        mappings.RemoveAll(m => m.panelName == panelName);

        // 保存新的映射数据
        foreach (var mapping in selectedMappings)
        {
            mapping.panelName = panelName;
            mapping.targetObjectPath = path;
            mappings.Add(mapping);
        }

        // 保存到 EditorPrefs
        SavePendingMappingsToPrefs();

        Debug.Log($"[UIPanelTool] SaveMappingData: 保存了 {selectedMappings.Count} 个控件的映射信息，面板: {panelName}");
    }

    /// <summary>
    /// 获取GameObject路径
    /// </summary>
    private string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return "";

        if (PrefabUtility.IsPartOfPrefabInstance(obj))
        {
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(obj);
        }

        if (obj.scene.IsValid())
        {
            return $"{obj.scene.path}:{obj.name}";
        }

        return obj.name;
    }

    /// <summary>
    /// 编译完成后的处理（通过CompilationPipeline事件）
    /// </summary>
    private static void CompilationPipeline_assemblyCompilationFinished(string arg1, CompilerMessage[] arg2)
    {
        Debug.Log($"[UIPanelTool] 编译完成事件触发: {arg1}");
        
        // 检查编译错误
        if (arg2 != null && arg2.Length > 0)
        {
            bool hasError = false;
            foreach (var msg in arg2)
            {
                if (msg.type == CompilerMessageType.Error)
                {
                    Debug.LogError($"[UIPanelTool] 编译错误: {msg.message} (文件: {msg.file}, 行: {msg.line})");
                    hasError = true;
                }
            }
            if (hasError)
            {
                Debug.LogWarning("[UIPanelTool] 检测到编译错误，可能影响脚本挂载");
                return;
            }
        }
        
        // 检查是否是主程序集（支持多种命名方式）
        bool isMainAssembly = arg1.Contains("Assembly-CSharp") ||
                             arg1.Contains("Assembly-CSharp-Editor") ||
                             arg1.EndsWith(".dll") && !arg1.Contains("Editor");

        if (isMainAssembly)
        {
            // 从 EditorPrefs 加载数据
            var mappings = GetPendingMappings();
            Debug.Log($"[UIPanelTool] 主程序集编译完成: {arg1}，待处理映射数量: {mappings.Count}");

            if (mappings.Count > 0)
            {
                // 使用 update 回调延迟处理，确保程序集完全加载
                needProcessPendingMappings = true;
                EditorPrefs.SetBool(PREF_KEY_NEED_PROCESS, true);
                delayFrameCount = 2; // 等待2帧
                EditorApplication.update += OnDelayedProcessUpdate;
            }
        }
    }

    /// <summary>
    /// 脚本重载后的处理（更可靠的方法）
    /// </summary>
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        // 从 EditorPrefs 重新加载数据（因为静态变量在编译后会被重置）
        pendingMappings = null; // 清空缓存，强制重新加载
        var mappings = GetPendingMappings();

        // 从 EditorPrefs 加载标志
        needProcessPendingMappings = EditorPrefs.GetBool(PREF_KEY_NEED_PROCESS, false);

        Debug.Log($"[UIPanelTool] OnScriptsReloaded: 脚本重载完成，待处理映射数量: {mappings.Count}, needProcessPendingMappings: {needProcessPendingMappings}");

        if (mappings.Count > 0)
        {
            needProcessPendingMappings = true;
            EditorPrefs.SetBool(PREF_KEY_NEED_PROCESS, true);
            delayFrameCount = 2; // 等待2帧确保程序集完全加载
            Debug.Log($"[UIPanelTool] OnScriptsReloaded: 设置需要处理标志，待处理映射数量: {mappings.Count}");

            // 使用 update 回调延迟处理，确保所有程序集都已加载
            EditorApplication.update += OnDelayedProcessUpdate;
        }
        else
        {
            needProcessPendingMappings = false;
            EditorPrefs.SetBool(PREF_KEY_NEED_PROCESS, false);
            Debug.Log("[UIPanelTool] OnScriptsReloaded: 没有待处理的映射");
        }
    }

    /// <summary>
    /// 延迟处理的更新回调（替代 delayCall）
    /// </summary>
    private static void OnDelayedProcessUpdate()
    {
        delayFrameCount--;

        if (delayFrameCount <= 0)
        {
            // 移除更新回调
            EditorApplication.update -= OnDelayedProcessUpdate;

            // 确保从 EditorPrefs 加载最新数据
            var mappings = GetPendingMappings();
            bool needProcess = EditorPrefs.GetBool(PREF_KEY_NEED_PROCESS, false);

            Debug.Log($"[UIPanelTool] OnDelayedProcessUpdate: 延迟完成，开始处理映射，待处理映射数量: {mappings.Count}, needProcessPendingMappings: {needProcess}");

            if (needProcess && mappings.Count > 0)
            {
                EditorPrefs.SetBool(PREF_KEY_NEED_PROCESS, false);
                ProcessPendingMappings();
            }
            else
            {
                Debug.Log("[UIPanelTool] OnDelayedProcessUpdate: 不需要处理或映射已清空");
            }
        }
    }

    /// <summary>
    /// 处理待处理的映射
    /// </summary>
    private static void ProcessPendingMappings()
    {
        // 确保从 EditorPrefs 加载最新数据
        var mappings = GetPendingMappings();

        Debug.Log($"[UIPanelTool] ProcessPendingMappings: 被调用，当前待处理映射数量: {mappings.Count}");

        if (mappings == null || mappings.Count == 0)
        {
            Debug.Log("[UIPanelTool] ProcessPendingMappings: 没有待处理的映射，直接返回");
            SavePendingMappingsToPrefs(); // 清除 EditorPrefs
            return;
        }

        Debug.Log($"[UIPanelTool] ProcessPendingMappings: 开始处理 {mappings.Count} 个待处理映射");
        
        // 按面板名称分组
        var groupedByPanel = mappings.GroupBy(m => m.panelName).ToList();
        var processedPanels = new List<string>();
        
        foreach (var group in groupedByPanel)
        {
            string panelName = group.Key;
            var panelMappings = group.ToList();
            
            if (panelMappings.Count == 0) continue;
            
            try
            {
                // 获取目标对象路径（所有映射应该有相同的targetObjectPath）
                string targetObjectPath = panelMappings[0].targetObjectPath;
                Debug.Log($"[UIPanelTool] 处理面板: {panelName}，目标路径: {targetObjectPath}，控件数量: {panelMappings.Count}");
                
                // 先查找类型，如果找不到就跳过
                Type panelType = GetPanelTypeStatic(panelName);
                if (panelType == null)
                {
                    Debug.LogWarning($"[UIPanelTool] 编译完成后仍未找到类型: {panelName}，跳过挂载和绑定。请检查代码是否正确生成。");
                    continue;
                }
                
                GameObject targetObj = FindGameObjectByPathStatic(targetObjectPath);
                if (targetObj == null)
                {
                    Debug.LogWarning($"[UIPanelTool] 找不到目标GameObject: {targetObjectPath}。请确保场景已打开且对象存在。");
                    continue;
                }
                
                bool scriptAttached = false;
                Component existingComponent = targetObj.GetComponent(panelType);
                if (existingComponent == null)
                {
                    try
                    {
                        existingComponent = targetObj.AddComponent(panelType);
                        if (existingComponent != null)
                        {
                    EditorUtility.SetDirty(targetObj);
                    if (targetObj.scene.IsValid())
                    {
                        EditorSceneManager.MarkSceneDirty(targetObj.scene);
                    }
                            Debug.Log($"[UIPanelTool] ✓ 已挂载脚本 {panelName} 到 {targetObj.name}");
                    scriptAttached = true;
                }
                else
                {
                            Debug.LogError($"[UIPanelTool] ✗ 挂载脚本 {panelName} 失败：AddComponent返回null");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UIPanelTool] ✗ 挂载脚本 {panelName} 时发生异常: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.Log($"[UIPanelTool] 脚本 {panelName} 已存在于 {targetObj.name}");
                    scriptAttached = true;
                }
                
                // 绑定控件（即使挂载失败也尝试绑定，因为可能脚本已经存在）
                bool bindingSuccess = false;
                if (scriptAttached)
                {
                    bindingSuccess = PerformBinding(targetObj, panelType, panelMappings);
                }
                
                // 如果脚本已挂载（无论绑定是否成功），都标记为已处理
                if (scriptAttached)
                {
                    processedPanels.Add(panelName);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"处理面板 {panelName} 时发生错误: {e.Message}\n{e.StackTrace}");
            }
        }

        // 移除已处理的映射
        mappings.RemoveAll(m => processedPanels.Contains(m.panelName));

        // 保存到 EditorPrefs
        SavePendingMappingsToPrefs();

        Debug.Log($"[UIPanelTool] ProcessPendingMappings: 处理完成，剩余待处理映射数量: {mappings.Count}");

        // 如果还有待处理的，保持事件监听
        if (mappings.Count == 0)
        {
            needProcessPendingMappings = false;
            EditorPrefs.SetBool(PREF_KEY_NEED_PROCESS, false);
            CompilationPipeline.assemblyCompilationFinished -= CompilationPipeline_assemblyCompilationFinished;
            Debug.Log("[UIPanelTool] ProcessPendingMappings: 所有映射已处理完成，清除标志");
        }
    }

    /// <summary>
    /// 执行绑定操作
    /// </summary>
    private static bool PerformBinding(GameObject targetObj, Type panelType, List<ControlMappingItem> mappings)
    {
        try
        {
            Component component = targetObj.GetComponent(panelType);
            if (component == null)
            {
                Debug.LogWarning($"GameObject上不存在组件: {panelType.Name}");
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(component);

            int successCount = 0;
            // 绑定每个控件到对应字段
            foreach (var mapping in mappings)
            {
                GameObject controlObj = FindControlByPath(targetObj, mapping.path);
                if (controlObj == null)
                {
                    Debug.LogWarning($"找不到控件: {mapping.path}");
                    continue;
                }

                // 获取控件组件
                Type controlType = mapping.controlType;
                Component controlComponent = controlObj.GetComponent(controlType);
                if (controlComponent == null)
                {
                    Debug.LogWarning($"控件上不存在组件: {controlType.Name}");
                    continue;
                }

                // 绑定到字段
                SerializedProperty fieldProp = serializedObject.FindProperty(mapping.fieldName);
                if (fieldProp != null)
                {
                    fieldProp.objectReferenceValue = controlComponent;
                    successCount++;
                }
                else
                {
                    Debug.LogWarning($"找不到字段: {mapping.fieldName}");
                }
            }

            serializedObject.ApplyModifiedProperties();
            Debug.Log($"✓ 已绑定 {successCount}/{mappings.Count} 个控件");
            return successCount > 0;
        }
        catch (Exception e)
        {
            Debug.LogError($"执行绑定时发生错误: {e.Message}\n{e.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// 根据路径查找GameObject
    /// </summary>
    private static GameObject FindGameObjectByPathStatic(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("FindGameObjectByPathStatic: 路径为空");
            return null;
        }

        Debug.Log($"FindGameObjectByPathStatic: 查找路径 {path}");

        // 场景路径格式: "ScenePath:GameObjectName" 或 "ScenePath:Parent/Child"
        if (path.Contains(":"))
        {
            string[] parts = path.Split(':');
            if (parts.Length == 2)
            {
                Scene scene = SceneManager.GetSceneByPath(parts[0]);
                if (scene.IsValid())
                {
                    string objectPath = parts[1];
                    GameObject[] roots = scene.GetRootGameObjects();
                    
                    // 如果路径包含 /，需要递归查找
                    if (objectPath.Contains("/"))
                    {
                        string[] pathParts = objectPath.Split('/');
                        foreach (var root in roots)
                        {
                            Transform current = root.transform;
                            bool found = true;
                            foreach (string part in pathParts)
                            {
                                current = current.Find(part);
                                if (current == null)
                                {
                                    found = false;
                                    break;
                                }
                            }
                            if (found)
                            {
                                Debug.Log($"通过路径找到对象: {current.name}");
                                return current.gameObject;
                            }
                        }
                    }
                    else
                    {
                        // 简单名称，先尝试根对象
                        foreach (var root in roots)
                        {
                            if (root.name == objectPath)
                            {
                                Debug.Log($"通过名称找到根对象: {root.name}");
                                return root;
                            }
                            Transform found = root.transform.Find(objectPath);
                            if (found != null)
                            {
                                Debug.Log($"通过 Find 找到对象: {found.name}");
                                return found.gameObject;
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"场景无效: {parts[0]}");
                }
            }
        }

        // 尝试通过名称查找
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name == path && !EditorUtility.IsPersistent(obj))
            {
                Debug.Log($"通过全局查找找到对象: {obj.name}");
                return obj;
            }
        }

        Debug.LogWarning($"未找到对象: {path}");
        return null;
    }

    /// <summary>
    /// 根据路径查找控件
    /// </summary>
    private static GameObject FindControlByPath(GameObject root, string path)
    {
        if (string.IsNullOrEmpty(path)) return root;

        string[] parts = path.Split('/');
        Transform current = root.transform;

        foreach (string part in parts)
        {
            current = current.Find(part);
            if (current == null)
            {
                return null;
            }
        }

        return current.gameObject;
    }

    /// <summary>
    /// 获取控件类型
    /// </summary>
    private static Type GetControlType(string typeName)
    {
        Type type = Type.GetType($"UnityEngine.UI.{typeName}");
        if (type != null) return type;

        type = Type.GetType($"TMPro.{typeName}");
        if (type != null) return type;

        type = Type.GetType(typeName);
        return type;
    }

    /// <summary>
    /// 获取面板类型（静态方法）
    /// </summary>
    private static Type GetPanelTypeStatic(string typeName)
    {
        Debug.Log($"[UIPanelTool] GetPanelTypeStatic: 开始查找类型 {typeName}");
        
        // 先尝试直接获取（需要完整命名空间）
        Type panelType = Type.GetType(typeName);
        if (panelType != null)
        {
            Debug.Log($"[UIPanelTool] 通过 Type.GetType 找到类型: {panelType.FullName}");
            return panelType;
        }
        
        // 从所有已加载的程序集中查找
        var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        Debug.Log($"[UIPanelTool] 当前已加载 {assemblies.Length} 个程序集");

        foreach (var assembly in assemblies)
        {
            string assemblyName = assembly.GetName().Name;
            // 检查主程序集（可能是Assembly-CSharp或其他名称）
            if (assemblyName.Contains("Assembly-CSharp") && !assemblyName.Contains("Editor"))
            {
                try
                {
                    // 先尝试完整名称
                    panelType = assembly.GetType(typeName);
                    if (panelType != null)
                    {
                        Debug.Log($"[UIPanelTool] 从 {assemblyName} 通过 GetType 找到类型: {panelType.FullName}");
                        return panelType;
                    }
                    
                    // 遍历所有类型进行名称匹配
                    var types = assembly.GetTypes();
                    Debug.Log($"[UIPanelTool] {assemblyName} 中有 {types.Length} 个类型，开始遍历查找...");
                    foreach (var type in types)
                    {
                        if (type.Name == typeName)
                        {
                            Debug.Log($"[UIPanelTool] 通过名称匹配找到类型: {type.FullName}");
                            return type;
                        }
                    }
                    Debug.LogWarning($"[UIPanelTool] 在 {assemblyName} 中未找到类型: {typeName}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UIPanelTool] 获取类型时发生错误: {e.Message}\n{e.StackTrace}");
                }
            }
        }

        // 如果还没找到，尝试在所有程序集中搜索（排除Editor程序集）
        Debug.Log($"[UIPanelTool] 在所有非Editor程序集中搜索类型: {typeName}");
        foreach (var assembly in assemblies)
        {
            string assemblyName = assembly.GetName().Name;
            if (!assemblyName.Contains("Editor") && !assemblyName.Contains("Unity"))
            {
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (type.Name == typeName)
                        {
                            Debug.Log($"[UIPanelTool] 在程序集 {assemblyName} 中找到类型: {type.FullName}");
                            return type;
                        }
                    }
                }
                catch
                {
                    // 忽略某些程序集可能无法获取类型的异常
                }
            }
        }

        Debug.LogError($"[UIPanelTool] 未找到类型: {typeName}。请检查：1) 代码是否正确生成 2) 是否有编译错误 3) 类型名称是否正确");
        return null;
    }

    /// <summary>
    /// 重置工具状态
    /// </summary>
    private void ResetTool()
    {
        targetGameObject = null;
        lastSelectedObject = null;
        panelName = "";
        // savePath 不重置，保持用户选择
        controlMappings?.Clear();
        editingFieldNames?.Clear();
        treeRoots?.Clear();
        expandedNodes?.Clear();
        selectedItems?.Clear();
        lastSelectedItem = null;
        searchFilter = "";
        typeFilter = "全部";
        currentState = UIToolState.Empty;
    }
}
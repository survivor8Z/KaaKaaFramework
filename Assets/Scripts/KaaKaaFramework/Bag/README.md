# 物品和背包系统

基于鸭科夫（Duckov）设计思路实现的物品和背包系统，采用**组合优于继承**和**约定大于配置**的设计理念。

## 设计理念

1. **组合优于继承**：通过Unity的MonoBehaviour组件系统实现功能组合，避免类爆炸
2. **约定大于配置**：使用CustomDataCollection存储动态数据，通过约定key名称访问
3. **简化开发流程**：策划可以在Inspector中配置，无需修改代码
4. **易于扩展**：新增功能只需添加新组件或新Behavior

## 系统结构

```
Bag/
├── Core/                    # 核心数据类
│   ├── CustomDataType.cs    # 自定义数据类型枚举
│   ├── CustomData.cs        # 自定义数据项
│   ├── CustomDataCollection.cs  # 自定义数据集合
│   ├── Tag.cs               # Tag类
│   └── TagCollection.cs     # Tag集合
├── Item/                    # Item相关
│   ├── Item.cs              # Item核心类
│   └── ItemComponent.cs     # Item组件基类
├── Components/              # 组件系统
│   ├── UsageUtilities.cs   # 可使用组件
│   ├── UsageBehavior.cs     # 使用行为基类
│   ├── SlotCollection.cs    # Slot集合组件
│   ├── Slot.cs              # Slot槽位
│   └── StatCollection.cs   # Stat集合组件
├── Behaviors/               # 使用行为实现
│   ├── HealBehavior.cs      # 治疗行为
│   ├── RestoreHungerBehavior.cs  # 恢复饱食度行为
│   └── DeadByChanceBehavior.cs   # 概率死亡行为
├── Inventory/               # 背包系统
│   └── Inventory.cs         # 背包类
├── Settings/                # 高级设置
│   └── ItemSettingBase.cs  # Item设置基类
└── Interfaces/             # 接口
    └── IHealth.cs           # 生命值接口
```

## 快速开始

### 1. 创建物品预制体

1. 创建GameObject，添加`Item`组件
2. 配置基础属性：
   - TypeID：物品唯一ID
   - DisplayName：显示名称
   - Icon：图标
   - MaxStackCount：最大堆叠数
   - Weight：重量
   - Tags：标签列表

### 2. 添加功能组件

#### 可使用的物品

1. 添加`UsageUtilities`组件
2. 添加`UsageBehavior`子类（如`HealBehavior`）
3. 在Inspector中配置参数

#### 可装备的物品（有配件槽位）

1. 添加`SlotCollection`组件
2. 在Inspector中配置Slot列表
3. 设置每个Slot的requireTags和excludeTags

#### 有属性的物品

1. 添加`StatCollection`组件
2. 在Inspector中配置Stat列表

### 3. 使用代码示例

```csharp
// 检查物品是否可使用
if (item.IsUsable(player))
{
    item.Use(player);
}

// 获取动态数据
float durability = item.GetFloat("Durability", 100f);
bool isGun = item.GetBool("IsGun", false);

// 设置动态数据
item.SetFloat("Durability", 50f);
item.SetBool("IsGun", true, true); // 第三个参数表示不存在时创建

// 背包操作
inventory.Add(item);
inventory.Remove(item);
Item foundItem = inventory.FindItem(typeID);
```

## 约定数据Key

系统使用约定大于配置的方式，以下是一些常用的数据Key：

### Variables（运行时数据，需要存档）
- `Durability`：耐久度（Float）
- `UseDurability`：是否使用耐久度（Bool）
- `IsGun`：是否是枪（Bool）
- `BulletCount`：子弹数量（Int）

### Constants（常量数据，不需要存档）
- `Caliber`：口径（String）
- `Capacity`：容量（Int）

## 扩展指南

### 创建新的使用行为

1. 继承`UsageBehavior`类
2. 实现`CanBeUsed`和`Use`方法
3. 在Inspector中配置参数

```csharp
public class MyCustomBehavior : UsageBehavior
{
    [SerializeField] private int customValue = 10;

    public override bool CanBeUsed(Item item, object user)
    {
        // 检查逻辑
        return true;
    }

    public override void Use(Item item, object user)
    {
        // 使用逻辑
        Debug.Log($"使用了自定义行为，值：{customValue}");
    }
}
```

### 创建高级设置组件

1. 继承`ItemSettingBase`类
2. 实现`SetMarkerParam`方法设置标记
3. 提供额外的便捷方法

```csharp
public class ItemSetting_Gun : ItemSettingBase
{
    protected override void SetMarkerParam(Item item)
    {
        item.SetBool("IsGun", true, true);
    }

    public int GetBulletCount()
    {
        return Item.GetInt("BulletCount", 0);
    }

    public bool IsFull()
    {
        int capacity = (int)Item.GetStatValue("Capacity");
        return GetBulletCount() >= capacity;
    }
}
```

## 注意事项

1. **Health接口**：使用`HealBehavior`时，需要在使用者对象上实现`IHealth`接口
2. **组件依赖**：所有`ItemComponent`子类必须挂载在带有`Item`组件的GameObject上
3. **数据分离**：`Variables`用于运行时数据（需要存档），`Constants`用于配置数据（不需要存档）
4. **Tag匹配**：Slot系统使用Tag进行匹配，确保物品和槽位的Tag配置正确

## 设计优势

1. ✅ **组合优于继承**：通过组件组合实现功能，避免类爆炸
2. ✅ **约定大于配置**：使用CustomDataCollection存储动态数据，约定key名称
3. ✅ **简化开发**：策划可以在Inspector中配置，无需修改代码
4. ✅ **易于扩展**：新增功能只需添加新组件或新Behavior
5. ✅ **数据分离**：Variables（存档数据）和Constants（配置数据）分离


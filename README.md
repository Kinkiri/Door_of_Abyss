# Door of Abyss

战棋+卡牌策略游戏，Godot 4.7 + C# (.NET 8.0)。

---

## 目录

- [技术栈](#技术栈)
- [项目结构（程序员）](#项目结构程序员)
- [架构概览（程序员）](#架构概览程序员)
- [ECA 效果系统（程序员）](#eca-效果系统程序员)
- [策划配置手册](#策划配置手册)
  - [1. 核心战斗流程](#1-核心战斗流程)
  - [2. 条件（Condition）](#2-条件condition)
  - [3. 动作（Action）](#3-动作action)
  - [4. 值源（ValueSource）](#4-值源valuesource)
  - [5. Buff 系统](#5-buff-系统)
  - [6. 被动效果](#6-被动效果)
  - [7. 卡牌配置](#7-卡牌配置)
  - [8. 目标系统](#8-目标系统)
  - [9. 部署与门经济系统](#9-部署与门经济系统)
  - [10. 测试系统](#10-测试系统)
  - [11. 常见配置示例](#11-常见配置示例)
  - [12. 文本转 .tres 工具（策划友好）](#12-文本转-tres-工具策划友好)
  - [13. 动画系统](#13-动画系统)
  - [14. 装备系统](#14-装备系统)

---

# 程序员部分

## 技术栈

| 项目 | 值 |
|---|---|
| 引擎 | Godot 4.7 |
| 语言 | C# (.NET 8.0) |
| 渲染 | Forward Plus (D3D12) |
| 物理 | Jolt Physics 3D（预留） |
| 分辨率 | 1920 x 1080，Canvas Items + Expand |

## 项目结构（程序员）

```
Scripts/                         ~7300 行 C#
├── Data/                        数据模板层（Resource，编辑期零依赖）
│   ├── Actions/                 效果系统（多态子类，运行期经服务定位器访问 Manager）
│   │   ├── GameAction.cs        抽象基类：Execute(模板) + Revert(虚) + AnimationDuration
│   │   ├── DamageAction.cs      伤害（触发战斗被动）
│   │   ├── HealAction.cs        治疗
│   │   ├── SummonUnitAction.cs  召唤
│   │   ├── DrawCardAction.cs    抽牌
│   │   ├── AutoAttackAction.cs  自动攻击
│   │   ├── ModifyStatAction.cs  属性修改（可逆）
│   │   ├── ModifyCostAction.cs  增减费用
│   │   ├── ApplyBuffAction.cs   施加 Buff
│   │   ├── ModifyBuffAction.cs  修改 Buff 回合/叠层
│   │   ├── BranchAction.cs      分支（条件→Then/Else）
│   │   ├── RepeatAction.cs      循环（值源x次数）
│   │   ├── RemoveBuffAction.cs  驱散 Buff
│   │   ├── MoveUnitAction.cs    强制位移（传送/击退/拉拽）
│   │   └── SetStatAction.cs     设置属性为精确值（不可逆）
│   ├── Targeting/               目标筛选器（抽象基类 + 多态子类，替代 Shape+Filter 枚举）
│   │   ├── TargetFilter.cs      抽象基类：ApplyUnits/ApplyCells/GetShape/IsUnitMatch
│   │   ├── ShapeTargetFilter.cs 形状候选源：Shape + AreaRange
│   │   ├── PropertyTargetFilter.cs 静态属性筛选中间基类
│   │   ├── TeamTargetFilter.cs  相对阵营筛选
│   │   ├── UnitTypeTargetFilter.cs 单位类型筛选
│   │   ├── TagTargetFilter.cs   标签筛选（任一匹配）
│   │   ├── WorldTargetFilter.cs 世界观筛选（无=不限制）
│   │   ├── FactionTargetFilter.cs 势力筛选（无=不限制）
│   │   ├── UnitIDTargetFilter.cs 单位 ID 筛选（任一匹配）
│   │   ├── ConditionTargetFilter.cs 动态过滤：Conditions（配合值源筛运行时属性）
│   │   ├── ExtremeTargetFilter.cs 极值筛选（值源排序取最高/最低 N 个）
│   │   ├── AndTargetFilter.cs   AND 组合（形状节点生成 + 其余过滤）
│   │   ├── OrTargetFilter.cs    OR 组合（任一命中）
│   │   └── NotTargetFilter.cs   NOT 组合（补集）
│   ├── Condition/               条件系统（ECA）
│   │   ├── Condition.cs         抽象基类：IsMet(Context)
│   │   ├── CompareCondition.cs  通用比较（两个 ValueSource）
│   │   ├── HasBuffCondition.cs  检查 Buff
│   │   ├── RandomCondition.cs   概率
│   │   ├── AndCondition.cs      AND 复合
│   │   ├── OrCondition.cs       OR 复合
│   │   └── NotCondition.cs      NOT 复合
│   ├── ValueSource/             值源系统
│   │   ├── ValueSource.cs       抽象基类：GetValue(Context)
│   │   ├── ConstantValue.cs     常量
│   │   ├── UnitStatValue.cs     单位属性
│   │   ├── BuffInfoValue.cs     Buff 信息
│   │   ├── RandomValue.cs       随机数
│   │   ├── FormulaValue.cs      公式嵌套（Add/Sub/Mul/Div/Max/Min/Percent）
│   │   ├── RoundCountValue.cs   当前回合数
│   │   ├── UnitCountValue.cs    场上单位数量
│   │   ├── DistanceValue.cs     曼哈顿距离
│   │   └── BattleCostValue.cs   费用
│   ├── Card/
│   │   ├── CardData.cs          卡牌基类（TargetFilter/Conditions/Actions）
│   │   ├── DeckData.cs          卡组
│   │   ├── SpellCardData.cs     法术
│   │   ├── UnitCardData.cs      单位卡（含 UnitData）
│   │   └── EquipmentCardData.cs 装备卡
│   ├── Levels/
│   │   ├── LevelData.cs         关卡配置
│   │   └── WaveData.cs          波次配置
│   ├── Map/
│   │   ├── BlockData.cs         地形模板
│   │   └── MapData.cs           地图数据
│   ├── Units/
│   │   ├── UnitData.cs          单位模板
│   │   ├── DoorData.cs          门（水晶）数据模板，含 DeployRange
│   │   └── EquipmentData.cs     装备数据
│   ├── Library/
│   │   ├── Library.cs           库基类（按 ID 查找模板）
│   │   ├── CardLibrary.cs       卡牌库（含 ValidateAll 启动校验）
│   │   ├── UnitLibrary.cs       单位库
│   │   └── LevelLibrary.cs      关卡库
│   ├── BuffData.cs              Buff 模板
│   ├── EffectData.cs            被动效果模板
│   └── PlayerData.cs            玩家全局数据（含门列表）
├── Enum/                        枚举定义（含 World/Faction/Rarity/Tag 等世界观数据）
│   ├── BattlePhase.cs           BattlePhase + Team
│   ├── BuffInfoType.cs          BuffInfoType
│   ├── CardType.cs              CardType
│   ├── CompareOp.cs             CompareOp
│   ├── ConditionTarget.cs       ConditionTarget
│   ├── EventType.cs             事件枚举（12 种）
│   ├── Faction.cs               Faction（势力）
│   ├── FormulaOp.cs             FormulaOp
│   ├── ModifyStatType.cs        ModifyStatType
│   ├── PassiveTarget.cs         PassiveTarget
│   ├── Rarity.cs                Rarity（稀有度）
│   ├── Tag.cs                   Tag（标签）
│   ├── TargetKind.cs            TargetKind（目标结果类型：Unit/Cell）
│   ├── TargetShape.cs           TargetShape
│   ├── TeamFilter.cs            TeamFilter（相对阵营过滤，原 TargetFilter 改名）
│   ├── UnitTybe.cs              UnitType
│   ├── ValueTarget.cs           ValueTarget
│   └── World.cs                 World（世界观）
├── Instance/                    运行时实例层（纯 C# class，不继承 Godot 类型）
│   ├── Buff.cs                  Buff 运行时
│   ├── Card.cs                  卡牌运行时
│   ├── Cell.cs                  格子运行时
│   ├── Context.cs               ECA 上下文 DTO（含 Map/ActiveUnits 战场数据）
│   └── Unit.cs                  单位运行时
├── Manager/                     逻辑层（Godot Node 单例，服务定位器）
│   ├── ActionQueue.cs           动作序列器（逐个执行 + 动画间隔 + 插队）
│   ├── BattleManager.cs         战斗阶段 + 费用 + 胜利 + 行为执行 + 波次
│   ├── BuffManager.cs           Buff 生命周期（发事件驱动视图）
│   ├── CardManager.cs           牌库/手牌/弃牌（发事件驱动视图）
│   ├── EnemyAI.cs               敌方 AI（按距玩家门排序 + 最短路径寻路 + 被堵留AP）
│   ├── EventBus.cs              事件总线（被动效果订阅/触发，Tag 支持）
│   ├── InitManager.cs           初始化调度
│   ├── MapManager.cs            地图管理
│   ├── SelectionManager.cs      输入 + 选中 + 范围 + 卡牌流程
│   └── UnitManager.cs           单位生命周期（发事件驱动视图）
├── View/                        视图层（事件驱动渲染）
│   ├── BuffView.cs              Buff 图标（Node2D，内含 TextureRect + Label）
│   ├── CardView.cs              卡牌展示
│   ├── DragCamera2D.cs          拖拽摄像机
│   ├── HandPanel.cs             手牌面板（订阅 CardManager 事件）
│   ├── MapView.cs               地图渲染 + 高亮
│   ├── RoundView.cs             回合面板 + 结束回合按钮
│   ├── UnitView.cs              单位视觉 + 内建动画（入场/受伤/治疗/死亡/移动/Buff）
│   └── UnitViewManager.cs       订阅 UnitManager/BuffManager 事件，创建/销毁 UnitView 与 BuffView
├── Tests/
│   └── TestRunner.cs            全面系统性测试（45+ 用例，场景内集成运行）
├── Tools/
│   └── TextToResourceImporter.cs  文本转 .tres 工具（EditorScript）
└── Utils/
	├── MapExporter.cs           地图导出工具
	├── PathFinder.cs            BFS 寻路（纯算法）
	└── TargetResolver.cs        目标解析器（纯函数，战场数据由调用方经 Context 传入）
```

## 架构概览（程序员）

### 三层架构

```
Data（Resource 层）         ← 编辑期零依赖（编辑器配置）；运行期经服务定位器访问 Manager（设计如此）
  BlockData / UnitData / CardData / BuffData / EffectData / GameAction 子类
  Condition 子类 / ValueSource 子类
	  ↑
Instance（纯 C# class）     ← 不继承 Godot 类型，不含管理器引用
  Cell / Unit / Card / Buff / Context
	  ↑
Manager（Godot Node）       ← 逻辑枢纽，InitManager 统一调度 Init 顺序
  InitManager → 所有 Manager.Init()；EventBus 亦属本层
	  ↑
View（Node/Control）        ← 事件驱动渲染，订阅 Manager 事件自建视图
  UnitViewManager / MapView / UnitView / BuffView / RoundView / HandPanel / CardView
```

### ECA 执行流程

```
EventBus.Fire(EventType, Context, subject)
  → 遍历订阅者
	→ 触发次数检查（MaxTriggerCount）
	→ 创建 effectCtx（目标解析：PassiveTarget 或 TargetFilter）
	→ 条件检查（Conditions，任意不满足则 skip）
	→ 执行 Actions
```

### ActionQueue 序列化

```
BattleManager.OnCardPlay
  └─ ActionQueue.Enqueue(actions[], ctx, onComplete)
	   └─ ProcessNext() → 执行第一个 → 等待 AnimationDuration 秒
			└─ ProcessNext() → 第二个 → ...
			└─ 队列空 → onComplete 回调
	   └─ EnqueueFront(action, ctx) — 插队到头部
```

支持插队（反击/连击/被动触发），空队列回调通知调用方。发射 `ActionStarted` 信号供 View 层播动画。

### Buff 生命周期

```
ApplyBuff → 发 BuffApplied 事件 → UnitViewManager 创建 BuffView（挂在 UnitView.BuffContainer 下）
  → TickAllBuffs（RoundEnd，倒计时 + OnRoundEndActions）
  → RemoveBuff（Revert x StackCount → 取消被动 → OnExpireActions → 发 BuffRemoved 事件销毁图标）
```

### 设计约定

- `InitManager` 统一调度所有 `Manager.Init()`，杜绝 `_Ready` 执行顺序竞态
- Manager 之间通过事件解耦：`SelectionManager` 发请求，`BattleManager` 订阅执行
- Manager 只发事件不碰视图：`UnitManager.OnUnitSpawned` / `BuffManager.BuffApplied` → `UnitViewManager` 创建 UnitView/BuffView（对齐 CardManager → HandPanel）
- `EventBus` 是单向调用：Manager 调 `Fire()`，EventBus 不反向依赖 Manager
- `EventBus.Subscribe` 支持 `tag` 参数，用于 Buff 到期时 `UnsubscribeByTag` 单独清理
- `UnitView` 显示运行时值（`Unit.AttackPower` / `Unit.MaxHP`），非模板值，Buff 修改即时可见
- 敌方单位名字红色、显示 `EnemyIndicator` 精灵（不是着色）

### ECA 相关类关系

```
EffectData（被动配置）                      CardData（卡牌配置）
  TriggerEvent : EventType                    Conditions : Condition[]
  Conditions : Condition[]                    Actions : GameAction[]
  Actions : GameAction[]
  MaxTriggerCount : int

BuffData（Buff 配置）
  OnApplyActions : GameAction[]      ← 按 StackCount 倍数执行
  OnExpireActions : GameAction[]
  OnRoundEndActions : GameAction[]
  PassiveEffects : EffectData[]

CompareCondition                         FormulaValue
  Left : ValueSource ←┐                    Left : ValueSource
  Op : CompareOp       │ 值源可互相嵌套     Op : FormulaOp
  Right : ValueSource ─┘                   Right : ValueSource

DamageAction / HealAction / ModifyStatAction / SetStatAction / ...
  Value : int                    ← 静态数值（向后兼容）
  ValueSource : ValueSource      ← 动态值源（覆盖 Value）
  AnimationDuration : float      ← 动画时长（ActionQueue 使用）
```

### Context 可用字段

```csharp
public class Context {
	Unit SourceUnit;       // 效果来源单位
	Unit TargetUnit;       // 单目标
	Unit[] TargetUnits;    // 多目标
	Team SourceTeam, TargetTeam;
	Card SourceCard;
	Cell TargetCell, SourceCell;
	Cell[] TargetCells;
}
```

---

# 策划配置手册

## 1. 核心战斗流程

每一回合按以下顺序自动推进：

```
GameStart（游戏开始）
  ├─ 加载地图
  ├─ 手动放门
  └─ 初始化卡组 → 抽 2 张牌
  ↓ 自动
RoundStart（回合开始）
  ├─ 所有单位行动点恢复满上限，费用 +2
  ├─ 抽 1 张牌，生成波次
  ├─ RoundStart 被动效果
  └─ 重置触发计数器
  ↓
PlayerAction ← 玩家出牌/移动/攻击，点按钮推进
  ↓
EnemyAction ← AI 自动行动（按距玩家门排序 + BFS寻路 + 多AP用尽）
  ↓
RoundEnd（回合结束）
  ├─ RoundEnd 被动效果
  ├─ Buff 倒计时 + OnRoundEndActions
  ├─ 到期 Buff 移除（还原属性 + OnExpireActions）
  └─ 检查胜利条件
  ↓ 回到 RoundStart
```

默认顺序。若无敌对可行动单位则自动跳过，最多跳 3 次防死循环。

**胜利条件：** 所有波次出完 + 场上无敌方 → 玩家胜。门被摧毁 → 玩家败。

## 2. 条件（Condition）

条件放在 `EffectData.Conditions` 或 `CardData.Conditions` 中。多个条件之间是 AND 关系，支持复合嵌套。

### 2.1 比较条件（CompareCondition）

用两个值源进行比较，是最通用的条件。

| 字段 | 说明 |
|---|---|
| `Left` | 左边值源 |
| `Op` | Less / LessEqual / Greater / GreaterEqual / Equal / NotEqual |
| `Right` | 右边值源 |

**示例：** 目标当前 HP <= 最大 HP 的 50%
```
CompareCondition
  Left   = UnitStatValue(Target, MaxHP, CurrentHP=true)
  Op     = LessEqual
  Right  = FormulaValue(Percent, UnitStatValue(Target, MaxHP, false), ConstantValue(50))
```

### 2.2 布尔条件

| 条件 | 字段 | 说明 |
|---|---|---|
| `HasBuffCondition` | CheckTarget, BuffID, Has | 检查 Buff 是否存在 |
| `HasEquipmentCondition` | CheckTarget, EquipmentID, Has | 检查装备是否存在（EquipmentID 留空=任意装备） |
| `RandomCondition` | Probability=0.0~1.0 | 概率判定 |

### 2.3 复合条件

| 条件 | 字段 | 说明 |
|---|---|---|
| `AndCondition` | Conditions=Condition[] | 全部通过 |
| `OrCondition` | Conditions=Condition[] | 任一通过 |
| `NotCondition` | Condition=Condition | 取反 |

**示例：** HP<50% 且没有免疫，或者 30% 概率
```
OrCondition
  +-- AndCondition
  |   +-- CompareCondition (CurrentHP < MaxHP x 50%)
  |   +-- NotCondition -> HasBuffCondition(BuffID=免疫)
  +-- RandomCondition(Probability=0.3)
```

## 3. 动作（Action）

所有动作在编辑器中通过 New Resource 创建。数字字段有两个模式：`Value`（固定值）和 `ValueSource`（动态值源，有值时覆盖 Value）。

### 3.1 基础动作

| 动作 | 字段 | 可逆 | 说明 |
|---|---|---|---|
| DamageAction | Value/ValueSource | x | 伤害，触发战斗被动。遍历 `TargetUnits` 支持多目标 |
| HealAction | Value/ValueSource | x | 治疗。遍历 `TargetUnits` 支持多目标 |
| DrawCardAction | Value/ValueSource | x | 抽牌 |
| ModifyCostAction | Value/ValueSource | x | 增减费用 |

### 3.2 召唤

| 动作 | 说明 |
|---|---|
| SummonUnitAction | 通用召唤：配置 `UnitData`（Resource 引用）或 `UnitID`（字符串查 UnitLibrary，亡语重生等循环引用场景用字符串）可直接召唤任意单位（无部署范围限制，可用于法术/被动）；两者都未配置时回退到单位卡自身（`UnitCardData.UnitData`），且仅此路径保留"己方门部署范围内"检查。可选 `SpawnBuff`/`SpawnBuffStacks`：召唤成功后自动给新单位施加 Buff（单位卡打出时带 Buff 的实现方式） |

部署限制出牌前由 `SelectionManager.ValidateCardTarget` 拦截（范围外点击不出牌、不扣费），`SummonUnitAction` 内保留同检查作为双保险。

### 3.3 自动攻击

| 动作 | 说明 |
|---|---|
| AutoAttackAction | 自动攻击最近敌方，用自身攻击力和范围 |

### 3.4 属性修改（可逆）

| 动作 | 字段 | 说明 |
|---|---|---|
| ModifyStatAction | TargetStat, Value/ValueSource, RequiredTags | 加减属性值。Buff 到期自动还原。`RequiredTags`（可选）：仅当目标单位带任一指定 Tag 时生效（null/空 = 不限制），Tag 来自 UnitData 模板、战斗中不变，施加/还原条件对称可逆安全 |

MaxHP 规则：施加时当前 HP 随上限**同步增加相同值**（只增不减）；还原时上限减回、当前 HP **不随上限减少**，仅超出新上限时截断。

**行动点（AP）双值：** `ActionPoints`（当前）+ `MaxActionPoints`（上限）。行动消耗当前值；**每回合开始当前 = 上限**（不从模板取，Buff/装备加的上限在恢复时生效）；**上限最小不低于 1**。ModifyStatAction 修改上限：施加时当前随上限同步增加，还原时上限减回（clamp ≥1）、当前仅截断（同 MaxHP 语义）。

**体力单值：** 体力 = 移动范围半径（曼哈顿距离），单值无"上限/剩余"之分（移动不消耗体力，只扣 AP）。

**叠层刷新**：已有 Buff 再次施加时，只对**新增层数**执行 OnApplyActions（旧层效果保留，不先还原旧层）——避免"还原不扣当前 HP + 全量重施"导致的血量虚增（如 2 层义肢 3/6 血再上 2 层 → 5/8，而非错误的 7/8）。

### 3.5 Buff 动作

| 动作 | 字段 | 说明 |
|---|---|---|
| ApplyBuffAction | BuffData, InitialStacks | 施加 Buff。**遍历 `TargetUnits` 支持多目标**（如 Shape=All）。
| ModifyBuffAction | BuffID, TurnsDelta, StacksDelta | 修改回合/叠层。**回合/叠层最小减到 0，不能为负**；`RemainingTurns=-1`（永久 Buff）忽略回合修改，其他非法负值警告并按 0 处理；叠层归零移除 |
| RemoveBuffAction | BuffID | 无条件整个移除（驱散） |

### 3.6 控制流

| 动作 | 字段 | 说明 |
|---|---|---|
| BranchAction | Condition, ThenActions[], ElseActions[] | 条件真->Then，假->Else。支持嵌套 |
| RepeatAction | Times(ValueSource), MaxIterations, Actions[] | 重复 N 次。MaxIterations 防死锁 |

### 3.7 强制位移

| 动作 | 字段 | 说明 |
|---|---|---|
| MoveUnitAction | Mode=Teleport/Push/Pull, Distance | Teleport=传送，Push=击退，Pull=拉拽。不耗 AP。曼哈顿方向 |

### 3.8 属性设置（不可逆）

| 动作 | 字段 | 说明 |
|---|---|---|
| SetStatAction | TargetStat, Value/ValueSource | 设为精确值。**不可逆**，不能放 Buff OnApplyActions 里 |

### 3.9 可逆性汇总

| 可逆 | 动作 |
|---|---|
| ✓ | ModifyStatAction、ModifyBuffAction（减层）、SetStatAction（单独不可逆但不在 Buff 中用） |
| x | DamageAction、HealAction、SummonUnitAction、DrawCardAction、AutoAttackAction、ModifyCostAction、ApplyBuffAction、RemoveBuffAction、MoveUnitAction、BranchAction、RepeatAction |

## 4. 值源（ValueSource）

值源放在动作的 `ValueSource` 字段或条件的 `Left`/`Right` 中。有值时覆盖 `Value`。

| 值源 | 字段 | 说明 |
|---|---|---|
| ConstantValue | Value | 固定数值 |
| UnitStatValue | Unit=Source/Target, Stat, CurrentHP=true/false | 单位属性 |
| BuffInfoValue | Unit, BuffID, Info=StackCount/RemainingTurns | Buff 叠层/回合，不存在时返回 DefaultValue |
| EquipmentInfoValue | Unit, Info=HasEquipment/各Bonus | 装备信息，无装备时 HasEquipment=0、其余返回 DefaultValue |
| RandomValue | Min, Max | [Min, Max] 随机整数 |
| FormulaValue | Op, Left, Right | 嵌套运算（Add/Sub/Mul/Div/Max/Min/Percent） |
| RoundCountValue | - | 当前回合数 |
| UnitCountValue | FilterTeam=All/Player/Enemy, OnlyAlive, IncludeDoor | 单位数量 |
| DistanceValue | From=Source/Target, To=Source/Target | **曼哈顿距离** |
| BattleCostValue | Type=Current/Max | 当前/最大费用 |

### FormulaValue 运算

| Op | 含义 | 说明 |
|---|---|---|
| Add/Sub/Mul/Div | + - x / | Div 分母为 0 时返回 0 |
| Max/Min | 取大/小值 | |
| Percent | A x B / 100 | B=50 表示 50% |

### 值源可放置的位置

| 位置 | 字段 |
|---|---|
| CompareCondition | Left, Right |
| DamageAction / HealAction / DrawCardAction | ValueSource |
| ModifyStatAction / SetStatAction / ModifyCostAction | ValueSource |
| FormulaValue | Left, Right |
| RepeatAction | Times |

## 5. Buff 系统

### BuffData 字段

| 字段 | 说明 |
|---|---|
| BuffID | 唯一标识 |
| Duration | -1=永久, 0=直接移除, N=持续 N 回合 |
| MaxStack | -1=无限叠, 0=禁用, N=最多 N 层 |
| OnApplyActions | **按层数倍数执行** |
| OnExpireActions / OnRoundEndActions | 到期/每回合效果 |
| PassiveEffects | 期间被动，移除时自动取消 |
| Icon | 图标纹理 |

### 叠层 = 效果倍率

InitialStacks=2 + ModifyStatAction(ATK,+1) -> ATK+2。
已有 Buff 时再次施加，叠层数按 `InitialStacks` 增长（不是固定 +1），**只对新增层数执行 OnApplyActions**（旧层效果保留）。
ModifyBuffAction(StacksDelta=-1) -> 减 1 层，还原 1 次 -> ATK-1。
归零 -> 移除，还原全部 + OnExpireActions。

完整生命周期见程序员部分。

## 6. 被动效果

配置在 `UnitData.PassiveEffects[]` 中。

### EffectData 字段

| 字段 | 说明 |
|---|---|
| TriggerEvent | 触发事件 |
| Target | Self=自身, EventTarget=事件另一方（TargetFilters 为空时生效） |
| TargetFilters | 目标筛选器数组（默认 And；非空时自动解析目标，忽略 Target） |
| MaxTriggerCount | 每回合最多 N 次，0=不限制 |
| Conditions | ECA 条件 |
| Actions | 动作序列 |

### 触发事件

| 事件 | 说明 |
|---|---|
| RoundStart / RoundEnd | 回合开始/结束 |
| OnSpawn | 单位登场 |
| OnDealDamage / OnTakeDamage | 造成/受到伤害 |
| OnKill | 击杀 |
| OnBuffApplied / OnBuffRemoved | Buff 施加/移除 |
| OnUnitAct | 单位行动（移动/攻击/出牌） |
| **OnBeforeDamage** | 伤害计算前（减伤/易伤/护盾） |
| **OnUnitDeath** | 单位死亡（亡语） |
| **OnMove** | 移动后（不含攻击/出牌） |

### 被动示例

**吸血鬼：** 造成伤害时治疗自身 1，每回合限 2 次
```
TriggerEvent=OnDealDamage, Target=Self, MaxTriggerCount=2
Actions=[HealAction{Value=1}]
```

**防御塔：** 回合结束自动攻击
```
TriggerEvent=RoundEnd, Target=Self, MaxTriggerCount=1
Actions=[AutoAttackAction]
```

## 7. 卡牌配置

### 通用字段

| 字段 | 说明 |
|---|---|
| CardID / CardName / Description | 标识和文本 |
| Type | Unit/Spell/Environment/Equipment/Special |
| Shape / Filter / AreaRange | 目标和范围 |
| Cost | 费用 |
| Actions | 打出效果 |
| Conditions | 打出条件（不满足不出牌不扣费） |

### UnitCardData 额外

| 字段 | 说明 |
|---|---|
| UnitData | 召唤的单位模板 |

### 卡牌示例

**火球术：** `Type=Spell, Cost=1`
`TargetFilters=[Shape(单体), Team(敌方)]`
`Actions=[DamageAction{Value=2}]`

**小兵：** `Type=Unit, Cost=2`
`TargetFilters=[Shape(单体格子)]`（Kind=Cell）
`Actions=[SummonUnitAction]`

**变强：** `Type=Spell, Cost=1`
`TargetFilters=[Shape(单体), Team(友方)]`
`Actions=[ApplyBuffAction{BuffData=<强壮.tres>, InitialStacks=2}]`

**风羽：** `Type=Spell, Cost=2`
`TargetFilters=[Shape(全体), Team(友方)]`
`Actions=[DrawCardAction{Value=1}, ApplyBuffAction{BuffData=<风羽.tres>}]`
说明：全体友方本回合攻击距离+1 并抽 1 张牌。

### 7.4 全图目标（Shape=All）

Shape=All 时预览高亮只显示 TargetFilters 匹配且有存活单位的格子，不会全地图渲染。

### 7.5 资源校验

打开游戏时 `CardLibrary.ValidateAll()` 自动校验所有卡牌：
- `UnitCardData` 的 TargetFilters 形状必须为 `SingleCell`
- `EquipmentCardData` 的 TargetFilters 形状必须为 `SingleUnit`
- `CardID` 不能为空/重复
- `Cost` 不能为负、TargetFilters 扩散半径不能为负
- 问题项输出警告，不影响加载流程

## 8. 目标系统

目标选择由 **TargetFilter**（Resource）描述：抽象基类 + 多态子类，可组合嵌套（与 Condition/ValueSource 同风格）。替代旧 Shape + Filter 两个枚举。

### 类层次

```
TargetFilter（抽象基类）
│  Kind: Unit / Cell（结果集是单位还是格子）
│  ApplyUnits / ApplyCells / GetShape / GetAreaRange / IsUnitMatch / GetTeamFilter
│
├── ShapeTargetFilter        // 形状候选源：Shape + AreaRange（唯一的形状节点，生成候选）
├── PropertyTargetFilter     // 静态属性筛选的中间基类（复用遍历过滤/格子透传）
│   ├── TeamTargetFilter     // 相对阵营 Team（Ally/Enemy 相对来源）
│   ├── UnitTypeTargetFilter // 单位类型 UnitTypes
│   ├── TagTargetFilter      // 标签 Tags（任一匹配）
│   ├── WorldTargetFilter    // 世界观 World（无=不限制）
│   ├── FactionTargetFilter  // 势力 Faction（无=不限制）
│   └── UnitIDTargetFilter   // 单位 ID UnitIDs（任一匹配；排除用 Not 组合）
├── ConditionTargetFilter    // 动态过滤：Conditions（配合值源筛运行时属性，如 HP≤50%Max）
├── ExtremeTargetFilter      // 极值后处理：按值源排序取最高/最低 N 个（数量不足全要）
├── AndTargetFilter          // AND 组合：自动找第一个形状节点生成候选，其余节点全部过滤（顺序无关）
├── OrTargetFilter           // OR 组合：任一子过滤器命中即保留
└── NotTargetFilter          // NOT 组合：全量 − 子过滤器命中集（补集）
```

### 语义约定

- **`CardData.TargetFilters` / `EffectData.TargetFilters` 是数组，默认 And 组合**：`[Shape(单体), Team(敌方)]` ≡ `And[Shape, Team]`，无需手动包 And（运行时经 `TargetFilter.CombineAnd` 组合）
- **数组为 null/空 = 无目标**（无目标法术直接打出）；被动效果无 TargetFilters 时用 `Target`（Self/EventTarget）
- **形状节点**（ShapeTargetFilter）忽略上游候选自行生成；**过滤节点**（Attribute/Condition/组合）对上游候选过滤
- **单挂过滤类** = 从全量开始（`[Team(敌方)]` 单独 ≡ 全体敌方）
- 阵营是**相对语义**（Ally/Enemy 相对效果来源阵营）；Neutral 单位不命中敌方过滤
- `GetShape()/GetAreaRange()/GetTeamFilter()` 穿透组合递归，供 UI 预览/校验与高亮图标使用

### 形状（TargetShape 枚举）

| Shape | 说明 |
|---|---|
| None | 无目标（一般直接用 TargetFilters=null 或空数组） |
| SingleUnit / SingleCell | 点选单位/格子 |
| AreaDiamond / AreaSquare | 菱形/方形扩散（半径 = ShapeTargetFilter.AreaRange） |
| All | 全地图 |

### 典型配置

| 目标 | TargetFilters 配置（数组默认 And） |
|---|---|
| 敌方单体 | `[Shape(单体), Team(敌方)]` |
| 友方单体 | `[Shape(单体), Team(友方)]` |
| 菱形 2 格敌方 | `[Shape(菱形,2), Team(敌方)]` |
| 全体友方 | `[Shape(全体), Team(友方)]` |
| 残血敌方（HP≤50%Max） | `[Shape(全体), Team(敌方), Cond(HP≤50%Max)]` |
| 生命最低的 3 个友方 | `[Shape(全体), Team(友方), Extreme(生命值, 最低, 3)]`（值源+方向+数量；不足全要） |
| 建筑或科技标签 | `[Shape(全体), Or(Type(建筑), Tag(科技))]` |
| 圣主教势力 | `[Shape(全体), Faction(圣主教)]`（World/Faction 默认"无"=不限制） |
| 只对小兵生效 | `[Shape(全体), UnitID(小兵)]` |
| 不对小兵生效 | `[Shape(全体), Not(UnitID(小兵))]` |
| 召唤格子（Kind=Cell） | `[Shape(单体格子)]` |
| 无目标法术 | `null` 或空数组 |

选中卡牌悬停地图自动预览目标范围。

---

## 9. 部署与门经济系统

### 9.1 部署范围

单位召唤只能在己方门的部署范围内放置。

| 字段 | 位置 | 说明 |
|---|---|---|
| `DeployRange` | `DoorData` | 曼哈顿距离，默认 2。每个门独立配置，多门时范围取并集 |
| `DoorDatas[]` | `PlayerData.tres` | 玩家门列表，支持多门。每回合累加所有存活门的收益 |
| `CostPerRound` | `DoorData` | 每回合此门回复的费用，默认 2 |
| `DrawPerRound` | `DoorData` | 每回合此门提供的抽牌数，默认 1 |

**限制逻辑：**
- `SummonUnitAction.Apply()` — 目标在任意门范围内即可，否则拒绝
- `SelectionManager.ComputeCardPreview()` — 预览只显示范围内格子
- `MapView.RenderCardPreview()` — 选中召唤卡时用图集(0,0)渲染部署范围高亮

### 9.2 门经济（多门叠加）

硬编码的 `CostPerRound = 2` 已删除。每回合 `OnEnterRoundStart()` 遍历场上所有我方门：

```
场上有 1 个门 (CostPerRound=2, DrawPerRound=1) → +2费, 抽1张
场上有 2 个门 (CostPerRound=2, DrawPerRound=1) → +4费, 抽2张
场上无门 → +0费, 不抽牌
```

### 9.3 门放置流程

```
PlayerData.DoorDatas = [圣晶.tres, 水晶.tres]
						 ↓
OnEnterGameStart
  → 放置门 [1/2] 圣晶（手动或自动）
  → 放置门 [2/2] 水晶
  → FinishGameStart（初始化卡组 → 抽2 → 推进）
```

---

## 10. 测试系统

`Scripts/Tests/TestRunner.cs` - 全面系统性单元测试，直接在场景中运行。

**用法：** 在场景根节点加 Node，挂载 TestRunner.cs，运行即可。45+ 用例覆盖：
- ValueSource 运算（6 种公式 + 嵌套）
- Condition 复合（And/Or/Not + Compare/HasBuff/Random）
- Buff 生命周期（叠层/倒计时/还原/驱散）
- ModifyBuffAction（减层归零/负值 clamp 到 0/永久 Buff 回合忽略）
- ECA 集成（条件满足执行/MaxTriggerCount 限制）
- DamageUnit（正常扣血/过量/击杀）
- MaxStack/Duration 边界值

测试完毕自动 `QueueFree()`，不影响游戏。

## 11. 常见配置示例

### 11.1 强力击 - 造成"自身攻击力 x 2"伤害

```
DamageAction {
  ValueSource = FormulaValue(Mul,
	UnitStatValue(Source, AttackPower),
	ConstantValue(2))
}
```

### 11.2 义肢 - 每层基础加成 + Tag 额外加成 + 行动后减层

**BuffData：** Duration=-1, MaxStack=-1
```
OnApplyActions = [
  ModifyStatAction(ATK,+1),                                // 基础：每层 ATK+1
  ModifyStatAction(MaxHP,+1),                              // 基础：每层 MaxHP+1
  ModifyStatAction(ATK,+1, RequiredTags=[攻击义肢]),        // Tag 额外：带 Tag 才生效
  ModifyStatAction(MaxHP,+1, RequiredTags=[生命义肢]),
  ModifyStatAction(体力,+1, RequiredTags=[体力义肢]),
  ModifyStatAction(行动点,+1, RequiredTags=[行动义肢]),
  ModifyStatAction(射程,+1, RequiredTags=[距离义肢])
]
PassiveEffects = [EffectData {
  TriggerEvent=OnUnitAct, MaxTriggerCount=1
  Actions=[ModifyBuffAction { BuffID=义肢, StacksDelta=-1 }]
}]
```

**Tag → 额外加成映射：** `攻击义肢`→攻击力、`生命义肢`→生命上限、`体力义肢`→体力（移动范围）、`行动义肢`→行动点上限、`距离义肢`→攻击范围。单位带哪个 Tag，对应属性额外 +1（按层数倍数）。

**卡牌：** `ApplyBuffAction { BuffData=<义肢.tres>, InitialStacks=2 }`

### 11.3 50% 概率回合结束治疗 2 点（被动）

```
TriggerEvent=RoundEnd, Target=Self
Conditions=[RandomCondition{Probability=0.5}]
Actions=[HealAction{Value=2}]
```

### 11.4 范围献祭 - 菱形 2 格所有敌方 3 伤

```
TriggerEvent=RoundEnd
TargetFilters=[Shape(菱形,2), Team(敌方)]
Actions=[DamageAction{Value=3}]
```

### 11.5 意外之财 - 获得 3 费

```
Type=Spell, Cost=0, TargetFilters=null
Actions=[ModifyCostAction{Value=3}]
```

### 11.6 抽取等于场上敌人数的牌

```
Actions=[DrawCardAction{
  ValueSource=UnitCountValue{FilterTeam=Enemy, OnlyAlive=true}
}]
```

### 11.7 处决 - HP<30% 才造成 5 伤害

```
Conditions=[CompareCondition{
  Left=UnitStatValue(Target,CurrentHP)
  Op=LessEqual
  Right=FormulaValue(Percent, UnitStatValue(Target,MaxHP), ConstantValue(30))
}]
Actions=[DamageAction{Value=5}]
```

### 11.8 连击 - 造成"目标攻击力"次 1 伤

```
Actions=[RepeatAction{
  Times=UnitStatValue(Unit=Target, Stat=AttackPower)
  MaxIterations=20
  Actions=[DamageAction{Value=1}]
}]
```

### 11.9 亡语 - 死亡时对击杀者造成 3 伤

```
UnitData PassiveEffects=[EffectData{
  TriggerEvent=OnUnitDeath, Target=EventTarget
  Actions=[DamageAction{Value=3}]
}]
```

**亡语重生（原地召唤新的自己）：** 死亡时所在格子已先被释放，事件附带 `TargetCell`/`SourceTeam`，用通用召唤（`UnitID` 字符串查库，避免循环引用）原地重生同阵营单位：

```
UnitData PassiveEffects=[EffectData{
  TriggerEvent=OnUnitDeath, Target=Self
  Actions=[SummonUnitAction{UnitID="小兵"}]   // 小兵 = 自己
}]
```

> 注意：`OnUnitDeath` 允许死者触发自身被动，EventBus 不拦截已死单位的亡语订阅。

### 11.10 强风术 - 击退 2 格

```
Type=Spell, Cost=1
TargetFilters=[Shape(单体), Team(敌方)]
Actions=[MoveUnitAction{Mode=Push, Distance=2}]
```

### 11.11 吸取 - 造成等于两单位距离的伤害

```
Actions=[DamageAction{
  ValueSource=DistanceValue(From=Source, To=Target)
}]
```

### 11.12 整齐划一 - 全体友方攻击力设为 5

```
Type=Spell, Cost=2
TargetFilters=[Shape(全体), Team(友方)]
Actions=[SetStatAction{TargetStat=AttackPower, Value=5}]
```

## 12. 文本转 .tres 工具（策划友好）

策划在 Excel 中填中文文本，在 Godot 中运行一次 EditorScript 即可生成游戏资源。

### 12.1 文件

| 文件 | 说明 |
|---|---|
| `Scripts/Tools/TextToResourceImporter.cs` | EditorScript，在 Godot 中打开 → File → Run |
| `Resource/DataConfigs/cards.txt` | 卡牌文本 |
| `Resource/DataConfigs/units.txt` | 单位文本 |
| `Resource/DataConfigs/buffs.txt` | Buff 文本 |
| `Resource/DataConfigs/策划填写规范.csv` | Excel 模板（含完整说明和示例） |

### 12.2 卡牌格式

```
ID | 类型 | 目标形状 | 过滤 | 费用 | 范围 | 世界观 | 势力 | 标签 | 稀有度 | 描述 | 条件 | 动作
```

> "目标形状" + "过滤" 两列由 importer 自动生成 TargetFilters 数组（默认 And）：
> 有过滤时 → `[Shape(形状,范围), Team(过滤)]`；无过滤时 → `[Shape]`；
> "无" → null（无目标法术）。范围列仅对菱形/方形生效。

| 列 | 可选值 | 示例 |
|---|---|---|
| 类型 | 法术, 单位, 装备, 环境, 特殊 | 法术 |
| 目标形状 | 敌方单体, 友方单体, 格子, 全体, 菱形, 方形, 无 | 敌方单体 |
| 过滤 | 敌方, 友方, 所有 | 敌方 |
| 费用 | 数字+"费" | 1费 |
| 范围 | "范围"+数字（菱形/方形 的扩散半径） | 范围1 |
| 世界观 | World 枚举值 | 曼斯维森 |
| 势力 | Faction 枚举值 | 圣主教 |
| 标签 | 逗号分隔 | 科技,宗教 |
| 稀有度 | Basic, Intermediate, Advanced, Legendary | Basic |
| 条件 | 条件 DSL（可选，留空=无限制） | HP>50 |
| 动作 | 动作 DSL | 伤害:2 |

### 12.3 单位格式

```
ID | 名称 | HP | ATK | AP | 体力 | 射程 | 类型 | 世界观 | 势力 | 标签 | 稀有度 | 描述 | 被动
```

所有数值列纯数字。类型：小队, 建筑, 门, 障碍, 召唤, 特殊。AP = 行动点上限（每回合恢复满），体力 = 移动范围半径（曼哈顿距离）。

### 12.4 Buff 格式

```
ID | 名称 | 持续 | 最大层数 | 描述 | 动作
```

持续：数字+"回合"（1回合），或"永久"。最大层数："无限叠" 或数字。

### 12.5 动作 DSL

| DSL | 说明 | 示例 |
|---|---|---|
| `伤害:{expr}` | 伤害 | `伤害:2`, `伤害:ATK`, `伤害:(ATK+3)*2` |
| `治疗:{expr}` | 治疗 | `治疗:Percent(MaxHP,30)` |
| `Buff:ID#N` | 施加 N 层 Buff | `Buff:强壮#2` |
| `移除Buff:ID` | 驱散 | `移除Buff:中毒` |
| `减Buff:ID#N` | 减 N 层 | `减Buff:中毒#1` |
| `抽牌:{expr}` | 抽 N 张 | `抽牌:1` |
| `属性:Stat±{expr}` | 属性增减（可逆） | `属性:攻击力+1` |
| `设置:Stat={expr}` | 属性设置（不可逆） | `设置:攻击力=ATK*2` |
| `消耗:{expr}` | 增减费用 | `消耗:-1` |
| `召唤` | 召唤自身绑定的单位 | `召唤:小兵` |
| `召唤:ID, Buff:ID#N`（单位卡动作列） | 召唤单位并自动施加 N 层 Buff（仅单位卡打出时生效，法术/亡语召唤不带） | `召唤:小兵, Buff:义肢#1` |
| `?{条件} then :: else` | 条件分支 | `?{HP<50} 治疗:3 :: 伤害:2` |

### 12.6 值源表达式

| 写法 | 类型 | 例 |
|---|---|---|
| 数字 | 常量 | `3`, `100` |
| `ATK` / `攻击力` | 来源攻击力 | |
| `HP` / `生命` | 目标当前生命 | |
| `MaxHP` / `最大生命` | 目标生命上限 | |
| `体力` | 来源体力（移动范围半径） | |
| `射程` / `距离` | 来源攻击距离 | |
| `行动` / `行动次数` | 来源当前行动点数 | |
| `回合数` | 当前回合 | |
| `费用` / `最大费用` | 当前或最大费用 | |
| `友方数` / `敌方数` / `全单位数` | 存活单位计数 | |
| `A+B` / `A-B` / `A*B` / `A/B` | 四则运算 | `(ATK+3)*2` |
| `Percent(A,B)` | A * B / 100 | `Percent(MaxHP, 30)` → 30%HP |
| `max(A,B)` / `min(A,B)` | 最大/最小 | `max(ATK, 5)` |
| `[A..B]` / `随机(A,B)` | [A,B] 随机整数 | `[1..6]` |
| `Buff层(ID)` | 目标 Buff 叠层数 | `Buff层(强壮)` |
| `Buff回合(ID)` | 目标 Buff 剩余回合 | `Buff回合(中毒)` |
| `距离(来源,目标)` | 曼哈顿距离 | |

### 12.7 条件 DSL

| 写法 | 说明 | 例 |
|---|---|---|
| `{值} < <= > >= == != {值}` | 比较 | `HP<50`, `(ATK+3)>=MaxHP` |
| `有Buff(ID)` / `无Buff(ID)` | Buff 检测 | `有Buff(强壮)` |
| `概率:N` | 概率 | `概率:0.3` |
| `AND` / `OR` / `NOT` | 逻辑复合 | `HP<50 AND 有Buff(中毒)` |
| `(...)` | 分组 | `(HP<30 OR 有Buff(免疫)) AND 回合数>3` |
| `;` | 多条件分隔（自动 AND） | `HP>50 ; 有Buff(强壮) ; 概率:0.5` |

### 12.8 被动效果 DSL（用于单位被动列）

| 事件 | 说明 | 示例 |
|---|---|---|
| `亡语` | 死亡时 | `亡语:菱形,敌方,伤害:3` |
| `生成时` | 登场时 | `生成时:属性:攻击力+1` |
| `回合开始` / `回合结束` | 回合边界 | `回合结束:菱形,友方,治疗:3` |
| `攻击后` / `受伤时` / `击杀后` | 战斗事件 | `击杀后:属性:攻击力+1` |
| `受伤前` | 伤害计算前（可修改） | `受伤前:属性:攻击力-1` |
| `行动后` / `移动后` | 行动事件 | |
| `Buff施加时` / `Buff移除时` | Buff 事件 | |

有范围格式：`事件:形状,过滤,动作`（如 `亡语:菱形,敌方,伤害:3`）
无范围格式：`事件:动作`（如 `生成时:属性:攻击力+1`）
多个被动用逗号分隔。

### 12.9 策划工作流

```
1. 打开 Excel 模板（策划填写规范.csv） → 看到说明和示例
2. 在下方空白行填入新数据
3. 复制对应段到 cards.txt / units.txt / buffs.txt
4. 在 Godot 中打开 TextToResourceImporter.cs → File → Run
5. .tres 文件自动生成到 Resource/Data/ 下
```

## 13. 动画系统

所有视觉动画内建于 `UnitView`，无需额外节点。

### 13.1 动画一览

| 动画 | 触发方式 | 实现位置 |
|---|---|---|
| 召唤入场 | `_Ready` | `Scale=0` → `Back.Out` 弹到 1 |
| 受伤闪红 | `UpdateView` 检测 HP 下降 | `modulate` 闪红 0.12s → 恢复白 |
| 治疗闪绿 | `UpdateView` 检测 HP 上升 | `modulate` 闪绿 0.12s → 恢复白 |
| 死亡消散 | `UpdateView` 检测 `IsDead` | 缩放 0 + 淡出 0.35s → `QueueFree` |
| 移动着陆 | `UpdateView` 检测 GridPos 变化 | `Back.Out` 缩放弹跳 1.15→1 |
| Buff 弹跳 | `ActionQueue.OnActionExecuted` | `PlayBuffBounce()` 缩放 1.25→1 |
| 攻击闪白 | `ActionQueue.OnActionExecuted` | `modulate` 亮白 0.05s + 恢复 0.08s |
| 浮动数字 | `UpdateView` 检测 HP 变化 | 预制体 `FloatLabel` 显示 1s 后隐藏 |

### 13.2 死亡动画流程

```
DamageUnit → DestroyUnit → RemoveUnit
  ├─ IsDead=true
  ├─ UpdateUnit() → UpdateView() 检测 IsDead → PlayDeathAnimation()（事件驱动，不再每帧轮询）
  └─ 发 OnUnitRemoved 事件 → UnitViewManager 清理视图引用（不 QueueFree）

UnitView.PlayDeathAnimation()
  → Tween: scale 0 + modulate 淡出
  → TweenCallback: QueueFree         ← 动画播完才销毁
```

### 13.3 攻击走 ActionQueue

玩家攻击 / AI 攻击不再直接调用 `DamageUnit`，改为通过 `DamageAction` + `ActionQueue`：

```
OnUnitAttack / AIDoAttack
  → AP--（即时扣减）
  → ActionQueue.Enqueue([DamageAction])
	→ Execute → DamageUnit（伤害即时生效）
	→ OnActionExecuted → UnitView 攻击者闪白 + 目标闪红
	→ 等待 AnimationDuration
	→ 回调: CheckVictory, OnUnitAct
```

BattleManager 的 `OnUnitMove`/`OnUnitAttack`/`AIDoAttack`/`AIDoMove` 均通过 ActionQueue 执行，确保动画时序一致。

### 13.4 浮动数字配置

在单位预制体 `Scenes/Prefabs/Units/单位视图.tscn` 中：
1. 在 `UnitView` 节点下添加 `Label`，默认 `Visible=false`
2. 拖入 Inspector 的 `FloatLabel` 字段
3. `FloatLifetime`（显示秒数，默认 1s）和 `FloatRise`（上飘像素，默认 28px）可在 Inspector 调节

## 14. 装备系统

装备 = 可逆属性加成 + 可选被动效果。一单位一件，重复装备**自动替换**（旧装备完整还原后再装新）。

### 14.1 EquipmentData 字段

| 字段 | 说明 |
|---|---|
| EquipmentID / EquipmentName / Description | 标识和文本 |
| Icon | 装备图标（EquipmentView 显示，无图标时显示名字） |
| AttackBonus | 攻击力加成 |
| MaxHealthBonus | 生命上限加成（装备时当前 HP 随上限同步增加） |
| AttackDistanceBonus | 攻击距离加成 |
| StaminaBonus | 体力加成（移动范围半径） |
| ActionPointBonus | 行动点上限加成（装备时当前行动点随上限同步增加） |
| OnApplyActions | 附加动作（装备时 Execute、移除时 Revert），与 bonus 字段**叠加** |
| PassiveEffects | 装备期间被动效果（移除时自动取消订阅） |

### 14.2 可逆性

属性加成**走 ModifyStatAction 执行**（非 0 的 bonus 字段自动转换为 ModifyStatAction，随后执行 OnApplyActions）。移除装备时按施加同序 Revert：

```
Equip:  [bonus 非0 → ModifyStatAction...] + OnApplyActions → Execute
Remove: 同一动作序列 → Revert（MaxHP 语义同 ModifyStatAction：卸下时当前 HP 不随上限减少，超出截断）
```

### 14.3 装备卡配置

| 字段 | 值 |
|---|---|
| Type | Equipment |
| Shape | SingleUnit |
| Filter | Ally（推荐，装备给己方单位） |
| Cost | 费用 |
| Actions | `[EquipAction]`（给目标单位装备） |
| EquipmentData | 拖入装备模板 |

### 14.4 替换语义

单位已有装备时再次使用装备卡 → 先完整移除旧装备（属性还原 + 取消被动 + 图标消失）→ 装上新装备。单位死亡时自动卸载装备。

### 14.5 相关值源 / 条件

| 类型 | 类 | 说明 |
|---|---|---|
| 值源 | EquipmentInfoValue | Info=HasEquipment / AttackBonus / MaxHealthBonus / AttackDistanceBonus / StaminaBonus / ActionPointBonus |
| 条件 | HasEquipmentCondition | CheckTarget, EquipmentID（留空=任意装备）, Has |

### 14.6 示例

**长剑：** 攻击力+2
```
EquipmentData：EquipmentID=长剑, AttackBonus=2
装备卡：Type=Equipment, Cost=1, TargetFilters=[Shape(单体), Team(友方)], Actions=[EquipAction]
```

**护心镜：** 生命上限+3 且回合结束治疗 2
```
EquipmentData：EquipmentID=护心镜, MaxHealthBonus=3,
  PassiveEffects=[EffectData{ TriggerEvent=RoundEnd, Target=Self,
	Actions=[HealAction{Value=2}] }]
```

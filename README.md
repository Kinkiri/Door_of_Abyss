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
  - [9. 测试系统](#9-测试系统)
  - [10. 常见配置示例](#10-常见配置示例)

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
Scripts/                         ~5500 行 C#
├── Data/                        数据模板层（Resource）
│   ├── Actions/                 效果系统（多态子类）
│   │   ├── GameAction.cs        抽象基类：Execute(模板) + Revert(虚) + AnimationDuration
│   │   ├── DamageAction.cs      伤害
│   │   ├── HealAction.cs        治疗
│   │   ├── SummonUnitAction.cs  召唤
│   │   ├── DrawCardAction.cs    抽牌
│   │   ├── AutoAttackAction.cs  自动攻击
│   │   ├── ModifyStatAction.cs  属性修改（5 种 StatType）
│   │   ├── ModifyCostAction.cs  增减费用
│   │   ├── ApplyBuffAction.cs   施加 Buff
│   │   ├── ModifyBuffAction.cs  修改 Buff 回合/叠层
│   │   ├── BranchAction.cs      分支（条件→Then/Else）
│   │   ├── RepeatAction.cs      循环（值源x次数）
│   │   ├── RemoveBuffAction.cs  驱散 Buff
│   │   ├── MoveUnitAction.cs    强制位移（传送/击退/拉拽）
│   │   └── SetStatAction.cs     设置属性为精确值（不可逆）
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
│   ├── BuffData.cs              Buff 模板
│   ├── BlockData.cs             地形模板
│   ├── Card/
│   │   ├── CardData.cs          卡牌基类（Shape/Filter/AreaRange/Conditions）
│   │   ├── DeckData.cs          卡组
│   │   ├── SpellCardData.cs     法术
│   │   └── UnitCardData.cs      单位卡（含 UnitData）
│   ├── Units/
│   │   ├── UnitData.cs          单位模板
│   │   └── DoorData.cs          门（水晶）数据模板，含 DeployRange
│   ├── EffectData.cs            被动效果模板
│   ├── LevelData.cs             关卡配置
│   ├── MapData.cs               地图数据
│   ├── PlayerData.cs            玩家全局数据
│   ├── TargetResolver.cs        目标解析器
│   ├── UnitData.cs              单位模板
│   └── WaveData.cs              波次配置
├── Enum/
│   ├── BattlePhase.cs           BattlePhase + Team
│   ├── BuffInfoType.cs          BuffInfoType
│   ├── CardType.cs              CardType
│   ├── CompareOp.cs             CompareOp
│   ├── ConditionTarget.cs       ConditionTarget
│   ├── EventType.cs             事件枚举（12 种）
│   ├── FormulaOp.cs             FormulaOp
│   ├── ModifyStatType.cs        ModifyStatType
│   ├── PassiveTarget.cs         PassiveTarget
│   ├── TargetFilter.cs          TargetFilter
│   ├── TargetShape.cs           TargetShape
│   ├── UnitTybe.cs              UnitType
│   └── ValueTarget.cs           ValueTarget
├── Instance/                    运行时实例层（纯 C# class，不继承 Godot 类型）
│   ├── Buff.cs                  Buff 运行时
│   ├── Card.cs                  卡牌运行时
│   ├── Cell.cs                  格子运行时
│   ├── Context.cs               ECA 上下文 DTO
│   ├── EventBus.cs              事件总线（Tag 支持）
│   └── Unit.cs                  单位运行时
├── Manager/                     逻辑层
│   ├── ActionQueue.cs           动作序列器（逐个执行 + 动画间隔 + 插队）
│   ├── BattleManager.cs         战斗阶段 + 费用 + 胜利 + 行为执行 + 波次
│   ├── BuffManager.cs           Buff 生命周期 + BuffView 创建/销毁
│   ├── CardManager.cs           牌库/手牌/弃牌
│   ├── EnemyAI.cs               敌方 AI（按距玩家门排序 + 最短路径寻路 + 被堵留AP）
│   ├── InitManager.cs           初始化调度
│   ├── MapManager.cs            地图管理
│   ├── SelectionManager.cs      输入 + 选中 + 范围 + 卡牌流程
│   └── UnitManager.cs           单位生命周期
├── Tests/
│   └── TestRunner.cs            全面系统性测试（45+ 用例，内置运行器）
├── Tools/
│   └── MapExporter.cs           地图导出工具
├── UI/
│   └── DragCamera2D.cs          摄像机 + 阶段推进
├── Utils/
│   └── PathFinder.cs            BFS 寻路
└── View/
	├── BuffView.cs              单 Buff 图标（图标/数字/悬停描述）
	├── CardView.cs              卡牌视觉
	├── HandPanel.cs             手牌面板
	├── MapView.cs               地形 + 高亮
	├── RoundView.cs             阶段 UI
	└── UnitView.cs              单位视觉 + 敌方标志 + 名字着色 + 悬停描述
```

## 架构概览（程序员）

### 三层架构

```
Data（Resource 层）         ← 零依赖，编辑器配置
  BlockData / UnitData / CardData / BuffData / EffectData / GameAction 子类
  Condition 子类 / ValueSource 子类
	  ↑
Instance（纯 C# class）     ← 不继承 Godot 类型，不含管理器引用
  Cell / Unit / Card / Buff / Context / EventBus
	  ↑
Manager（Godot Node）       ← 逻辑枢纽，InitManager 统一调度 Init 顺序
  InitManager → 所有 Manager.Init()
	  ↑
View（Node/Control）        ← 事件驱动渲染
  MapView / UnitView / BuffView / RoundView / HandPanel
```

### ECA 执行流程

```
EventBus.Fire(EventType, Context, subject)
  → 遍历订阅者
	→ 触发次数检查（MaxTriggerCount）
	→ 创建 effectCtx（目标解析：PassiveTarget 或 Shape/Filter）
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
ApplyBuff → CreateBuffView（挂在 UnitView.BuffContainer 下）
  → TickAllBuffs（RoundEnd，倒计时 + OnRoundEndActions）
  → RemoveBuff（Revert x StackCount → 取消被动 → OnExpireActions → DestroyBuffView）
```

### 设计约定

- `InitManager` 统一调度所有 `Manager.Init()`，杜绝 `_Ready` 执行顺序竞态
- Manager 之间通过事件解耦：`SelectionManager` 发请求，`BattleManager` 订阅执行
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
  ├─ 所有单位回复 AP，费用 +2
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
| SummonUnitAction | 召唤 SourceCard 的单位，卡牌必须为 UnitCardData |

### 3.3 自动攻击

| 动作 | 说明 |
|---|---|
| AutoAttackAction | 自动攻击最近敌方，用自身攻击力和范围 |

### 3.4 属性修改（可逆）

| 动作 | 字段 | 说明 |
|---|---|---|
| ModifyStatAction | TargetStat, Value/ValueSource | 加减属性值。Buff 到期自动还原 |

MaxHP 规则：施加时只加上限不减当前 HP，还原时截断。

### 3.5 Buff 动作

| 动作 | 字段 | 说明 |
|---|---|---|
| ApplyBuffAction | BuffData, InitialStacks | 施加 Buff。**遍历 `TargetUnits` 支持多目标**（如 Shape=All）。
| ModifyBuffAction | BuffID, TurnsDelta, StacksDelta | 修改回合/叠层。**减层逐层还原**，归零移除 |
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
已有 Buff 时再次施加，叠层数按 `InitialStacks` 增长（不是固定 +1）。
ModifyBuffAction(StacksDelta=-1) -> 减 1 层，还原 1 次 -> ATK-1。
归零 -> 移除，还原全部 + OnExpireActions。

完整生命周期见程序员部分。

## 6. 被动效果

配置在 `UnitData.PassiveEffects[]` 中。

### EffectData 字段

| 字段 | 说明 |
|---|---|
| TriggerEvent | 触发事件 |
| Target | Self=自身, EventTarget=事件另一方 |
| Shape/Filter/AreaRange | 范围模式 |
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

**火球术：** `Type=Spell, Shape=SingleUnit, Filter=Enemy, Cost=1`
`Actions=[DamageAction{Value=2}]`

**小兵：** `Type=Unit, Shape=SingleCell, Cost=2`
`Actions=[SummonUnitAction]`

**变强：** `Type=Spell, Shape=SingleUnit, Filter=Ally, Cost=1`
`Actions=[ApplyBuffAction{BuffData=<强壮.tres>, InitialStacks=2}]`

**风羽：** `Type=Spell, Shape=All, Filter=Ally, Cost=2`
`Actions=[DrawCardAction{Value=1}, ApplyBuffAction{BuffData=<风羽.tres>}]`
说明：全体友方本回合攻击距离+1 并抽 1 张牌。

### 7.4 全图目标（Shape=All）

Shape=All 时预览高亮只显示 Filter 匹配且有存活单位的格子，不会全地图渲染。

### 7.5 资源校验

打开游戏时 `CardLibrary.ValidateAll()` 自动校验所有卡牌：
- `UnitCardData` 的 Shape 必须为 `SingleCell`
- `CardID` 不能为空/重复
- `Cost`、`AreaRange` 等 int 字段不能为负
- 问题项输出警告，不影响加载流程

## 8. 目标系统

### Shape

| Shape | 说明 |
|---|---|
| None | 无目标 |
| SingleUnit / SingleCell | 点选单位/格子 |
| AreaDiamond / AreaSquare | 菱形/方形扩散 |
| All | 全地图 |

### Filter

| Filter | 说明 |
|---|---|
| All / Enemy / Ally | 所有/敌方/友方 |

选中卡牌悬停地图自动预览目标范围。

---

## 9. 部署系统

单位召唤只能在己方门周围的部署范围内放置。

| 字段 | 位置 | 说明 |
|---|---|---|
| `DeployRange` | `DoorData` | 曼哈顿距离，默认 2。每个门独立配置 |
| `PlayerData.DoorData` | `PlayerData.tres` | 玩家门数据，创建 `DoorData` Resource 拖入 |

**限制逻辑：**
- `SummonUnitAction.Apply()` — 超出范围拒绝执行
- `SelectionManager.ComputeCardPreview()` — 预览只显示范围内格子
- `MapView.RenderCardPreview()` — 选中召唤卡时用图集(0,0)渲染部署范围高亮

---

## 10. 测试系统

`Scripts/Tests/TestRunner.cs` - 全面系统性单元测试，直接在场景中运行。

**用法：** 在场景根节点加 Node，挂载 TestRunner.cs，运行即可。45+ 用例覆盖：
- ValueSource 运算（6 种公式 + 嵌套）
- Condition 复合（And/Or/Not + Compare/HasBuff/Random）
- Buff 生命周期（叠层/倒计时/还原/驱散）
- ModifyBuffAction（减层归零/负数拒绝）
- ECA 集成（条件满足执行/MaxTriggerCount 限制）
- DamageUnit（正常扣血/过量/击杀）
- MaxStack/Duration 边界值

测试完毕自动 `QueueFree()`，不影响游戏。

## 10. 常见配置示例

### 10.1 强力击 - 造成"自身攻击力 x 2"伤害

```
DamageAction {
  ValueSource = FormulaValue(Mul,
	UnitStatValue(Source, AttackPower),
	ConstantValue(2))
}
```

### 10.2 义肢3 - ATK+3, MaxHP+3, 行动后减层

**BuffData：** Duration=-1, MaxStack=-1
```
OnApplyActions = [
  ModifyStatAction(ATK,+3),
  ModifyStatAction(MaxHP,+3)
]
PassiveEffects = [EffectData {
  TriggerEvent=OnUnitAct, MaxTriggerCount=1
  Actions=[ModifyBuffAction { BuffID=义肢, StacksDelta=-1 }]
}]
```

**卡牌：** `ApplyBuffAction { BuffData=<义肢.tres>, InitialStacks=3 }`

### 10.3 50% 概率回合结束治疗 2 点（被动）

```
TriggerEvent=RoundEnd, Target=Self
Conditions=[RandomCondition{Probability=0.5}]
Actions=[HealAction{Value=2}]
```

### 10.4 范围献祭 - 菱形 2 格所有敌方 3 伤

```
TriggerEvent=RoundEnd, Shape=AreaDiamond, Filter=Enemy, AreaRange=2
Actions=[DamageAction{Value=3}]
```

### 10.5 意外之财 - 获得 3 费

```
Type=Spell, Shape=None, Cost=0
Actions=[ModifyCostAction{Value=3}]
```

### 10.6 抽取等于场上敌人数的牌

```
Actions=[DrawCardAction{
  ValueSource=UnitCountValue{FilterTeam=Enemy, OnlyAlive=true}
}]
```

### 10.7 处决 - HP<30% 才造成 5 伤害

```
Conditions=[CompareCondition{
  Left=UnitStatValue(Target,CurrentHP)
  Op=LessEqual
  Right=FormulaValue(Percent, UnitStatValue(Target,MaxHP), ConstantValue(30))
}]
Actions=[DamageAction{Value=5}]
```

### 10.8 连击 - 造成"目标攻击力"次 1 伤

```
Actions=[RepeatAction{
  Times=UnitStatValue(Unit=Target, Stat=AttackPower)
  MaxIterations=20
  Actions=[DamageAction{Value=1}]
}]
```

### 10.9 亡语 - 死亡时对击杀者造成 3 伤

```
UnitData PassiveEffects=[EffectData{
  TriggerEvent=OnUnitDeath, Target=EventTarget
  Actions=[DamageAction{Value=3}]
}]
```

> 注意：`OnUnitDeath` 允许死者触发自身被动，EventBus 不拦截已死单位的亡语订阅。

### 10.10 强风术 - 击退 2 格

```
Shape=SingleUnit, Filter=Enemy, Cost=1
Actions=[MoveUnitAction{Mode=Push, Distance=2}]
```

### 10.11 吸取 - 造成等于两单位距离的伤害

```
Actions=[DamageAction{
  ValueSource=DistanceValue(From=Source, To=Target)
}]
```

### 10.12 整齐划一 - 全体友方攻击力设为 5

```
Shape=All, Filter=Ally, Cost=2
Actions=[SetStatAction{TargetStat=AttackPower, Value=5}]
```

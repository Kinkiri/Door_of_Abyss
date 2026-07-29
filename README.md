# Door of Abyss

战棋+卡牌策略游戏，Godot 4.7 + C# (.NET 8.0)。

> **许可证说明：** 本项目代码部分采用 [MIT](LICENSE) 开源协议。
> **美术资源、音乐音效、字体、关卡配置等非代码资产保留所有权利**，仅供个人学习与交流，严禁任何形式的商业使用。

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
  - [9. 常见配置示例](#9-常见配置示例)

---

# 程序员部分

## 技术栈

| 项目 | 值 |
|---|---|
| 引擎 | Godot 4.7 |
| 语言 | C# (.NET 8.0) |
| 渲染 | Forward Plus (D3D12) |
| 物理 | Jolt Physics 3D（预留） |
| 分辨率 | 1920 × 1080，Canvas Items + Expand |

## 项目结构（程序员）

```
Scripts/                         ~5000 行 C#
├── Data/                        数据模板层（Resource）
│   ├── Actions/                 效果系统（多态子类）
│   │   ├── GameAction.cs        抽象基类：Execute(模板) + Revert(虚)
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
│   │   └── RepeatAction.cs      循环（值源×次数）
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
│   │   └── FormulaValue.cs      公式嵌套（Add/Sub/Mul/Div/Max/Min/Percent）
│   ├── BuffData.cs              Buff 模板
│   ├── BlockData.cs             地形模板
│   ├── Card/
│   │   ├── CardData.cs          卡牌基类（Shape/Filter/AreaRange/Conditions）
│   │   ├── DeckData.cs          卡组
│   │   ├── SpellCardData.cs     法术
│   │   └── UnitCardData.cs      单位卡（含 UnitData）
│   ├── EffectData.cs            被动效果模板
│   ├── LevelData.cs             关卡配置
│   ├── MapData.cs               地图数据
│   ├── PlayerData.cs            玩家全局数据
│   ├── TargetResolver.cs        目标解析器
│   ├── UnitData.cs              单位模板
│   └── WaveData.cs              波次配置
├── Enum/
│   ├── BattlePhase.cs           BattlePhase + Team
│   ├── BuffInfoType.cs          BuffInfoType（StackCount / RemainingTurns）
│   ├── CardType.cs              CardType（Unit / Spell 等）
│   ├── CompareOp.cs             CompareOp 比较操作
│   ├── ConditionTarget.cs       ConditionTarget（Source / Target）
│   ├── EventType.cs             事件枚举（9 种）
│   ├── FormulaOp.cs             FormulaOp 公式运算
│   ├── ModifyStatType.cs        ModifyStatType 可修改属性
│   ├── PassiveTarget.cs         PassiveTarget（Self / EventTarget）
│   ├── TargetFilter.cs          TargetFilter 阵营过滤
│   ├── TargetShape.cs           TargetShape 范围形状
│   ├── UnitTybe.cs              UnitType（含 Door）
│   └── ValueTarget.cs           ValueTarget（Source / Target）
├── Instance/                    运行时实例层（纯 C# class，不继承 Godot 类型）
│   ├── Buff.cs                  Buff 运行时
│   ├── Card.cs                  卡牌运行时
│   ├── Cell.cs                  格子运行时
│   ├── Context.cs               ECA 上下文 DTO
│   ├── EventBus.cs              事件总线（Tag 支持）
│   └── Unit.cs                  单位运行时
├── Manager/                     逻辑层
│   ├── BattleManager.cs         战斗阶段 + 费用 + 胜利 + 行为执行 + 波次
│   ├── BuffManager.cs           Buff 生命周期 + BuffView 管理
│   ├── CardManager.cs           牌库/手牌/弃牌
│   ├── EnemyAI.cs               敌方 AI
│   ├── InitManager.cs           初始化调度
│   ├── MapManager.cs            地图管理
│   ├── SelectionManager.cs      输入 + 选中 + 范围 + 卡牌流程
│   └── UnitManager.cs           单位生命周期
├── Tools/
│   └── MapExporter.cs           地图导出工具
├── UI/
│   └── DragCamera2D.cs          摄像机 + 阶段推进
├── Utils/
│   └── PathFinder.cs            BFS 寻路
└── View/
    ├── BuffView.cs              单 Buff 图标
    ├── CardView.cs              卡牌视觉
    ├── HandPanel.cs             手牌面板
    ├── MapView.cs               地形 + 高亮
    ├── RoundView.cs             阶段 UI
    └── UnitView.cs              单位视觉
```

## 架构概览（程序员）

### 三层架构

```
Data（Resource 层）         ← 零依赖，编辑器配置
  BlockData / UnitData / CardData / BuffData / EffectData
  GameAction 子类 / Condition 子类 / ValueSource 子类
      ↑
Instance（纯 C# class）     ← 不继承 Godot 类型，不含管理器引用
  Cell / Unit / Card / Buff / Context / EventBus
      ↑
Manager（Godot Node）       ← 逻辑枢纽，InitManager 统一调度 Init 顺序
  InitManager → 所有 Manager.Init()（按依赖顺序）
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

### Buff 生命周期

```
ApplyBuff → CreateBuffView（挂在 UnitView.BuffContainer 下）
  → TickAllBuffs（RoundEnd，倒计时 + OnRoundEndActions）
  → RemoveBuff（Revert × StackCount → 取消被动 → OnExpireActions → DestroyBuffView）
```

### 设计约定

- `InitManager` 统一调度所有 `Manager.Init()`，杜绝 `_Ready` 执行顺序竞态
- Manager 之间通过事件解耦：`SelectionManager` 发 `UnitMoveRequest/UnitAttackRequest/CardPlayRequest`，`BattleManager` 订阅执行
- `EventBus` 是单向调用：Manager 调 `Fire()`，EventBus 不反向依赖 Manager
- `EventBus.Subscribe` 支持 `tag` 参数，用于 Buff 到期时 `UnsubscribeByTag` 单独清理
- `UnitView` 显示运行时值（`Unit.AttackPower` / `Unit.MaxHP`），非模板值，Buff 修改即时可见
- Buff 的叠层 = 效果倍率。`StackCount=N` → `OnApplyActions` 执行 N 次，减层时逐层 Revert

### ECA 相关类关系

```
EffectData（被动配置）
  ├── TriggerEvent : EventType
  ├── Conditions : Condition[]        ← ECA 的条件
  ├── Actions : GameAction[]          ← ECA 的动作
  └── MaxTriggerCount : int

CardData（卡牌配置）
  ├── Conditions : Condition[]        ← 打出条件
  └── Actions : GameAction[]          ← 打出效果

BuffData（Buff 配置）
  ├── OnApplyActions : GameAction[]    ← 施加时（×StackCount）
  ├── OnExpireActions : GameAction[]  ← 到期时
  ├── OnRoundEndActions : GameAction[] ← 每回合
  └── PassiveEffects : EffectData[]   ← 期间被动

CompareCondition                    FormulaValue
  ├── Left : ValueSource ←┐           ├── Left : ValueSource
  ├── Op : CompareOp      │ 值源可    ├── Op : FormulaOp
  └── Right : ValueSource │ 互相嵌套  └── Right : ValueSource
                          └───────────
DamageAction / HealAction / ModifyStatAction / ...
  ├── Value : int                    ← 静态数值（向后兼容）
  └── ValueSource : ValueSource      ← 动态值源（覆盖 Value）
```

### Context 可用字段（所有 ECA 共享）

```csharp
public class Context {
    public Unit SourceUnit;       // 效果来源单位
    public Unit TargetUnit;       // 单目标单位
    public Unit[] TargetUnits;    // 多目标
    public Team SourceTeam;
    public Team TargetTeam;
    public Card SourceCard;       // 来源卡牌
    public Cell SourceCell;
    public Cell TargetCell;
    public Cell[] TargetCells;
}
```

---

# 策划配置手册

## 1. 核心战斗流程

每一回合按以下顺序自动推进：

```
GameStart（游戏开始）
  ├─ 加载地图（从 LevelData.MapData）
  ├─ 手动放门（玩家选位置放水晶）
  └─ 初始化卡组 → 抽 2 张牌
  ↓ 自动
RoundStart（回合开始）
  ├─ 所有单位回复 AP
  ├─ 费用 +2（上限 10）
  ├─ 抽 1 张牌
  ├─ 生成当前回合的波次敌人
  ├─ 触发 RoundStart 被动效果
  └─ 重置被动触发计数器
  ↓ 自动
PlayerAction（玩家行动） ← 玩家出牌/移动/攻击，点按钮推进
  ↓
EnemyAction（敌方行动） ← AI 自动行动
  ↓ 全部完成后自动
RoundEnd（回合结束）
  ├─ 触发 RoundEnd 被动效果
  ├─ Buff 倒计时（减 1 回合）
  ├─ 执行 Buff 的 OnRoundEndActions
  ├─ 到期的 Buff 自动移除
  └─ 检查胜利条件
  ↓ 自动回到 RoundStart
```

**胜利条件：**
- 玩家方：所有波次出完 + 场上没有敌方单位
- 敌方：摧毁玩家的门（水晶）

---

## 2. 条件（Condition）

条件放在 `EffectData.Conditions` 或 `CardData.Conditions` 中。
多个条件之间是 **AND** 关系（全部满足才通过）。条件支持复合嵌套。

### 2.1 比较条件（CompareCondition）

用两个值源进行比较，是最通用的条件。

| 字段 | 说明 |
|---|---|
| `Left` | 左边的值源 |
| `Op` | Less / LessEqual / Greater / GreaterEqual / Equal / NotEqual |
| `Right` | 右边的值源 |

**示例：** 目标当前 HP ≤ 最大 HP 的 50%
```
CompareCondition
  Left   = UnitStatValue { Unit=Target, Stat=MaxHP, CurrentHP=true }
  Op     = LessEqual
  Right  = FormulaValue {
    Op    = Percent
    Left  = UnitStatValue { Unit=Target, Stat=MaxHP, CurrentHP=false }
    Right = ConstantValue { Value=50 }
  }
```

### 2.2 布尔条件

| 条件 | 字段 | 说明 |
|---|---|---|
| `HasBuffCondition` | CheckTarget=Source/Target, BuffID, Has=true/false | 检查单位是否有某 Buff |
| `RandomCondition` | Probability=0.0~1.0 | 概率判定，通过概率为 Probability |

### 2.3 复合条件（支持任意嵌套）

| 条件 | 字段 | 说明 |
|---|---|---|
| `AndCondition` | Conditions = Condition[] | 所有子条件都通过才通过 |
| `OrCondition` | Conditions = Condition[] | 任一子条件通过即通过 |
| `NotCondition` | Condition = Condition | 子条件不通过才通过 |

**示例：** 目标 HP < 50% 且没有免疫 Buff，或者 30% 概率
```
OrCondition
  ├── AndCondition
  │   ├── CompareCondition (当前 HP < 最大 HP × 50%)
  │   └── NotCondition → HasBuffCondition { BuffID="免疫", Has=true }
  └── RandomCondition { Probability=0.3 }
```

---

## 3. 动作（Action）

所有动作在编辑器中通过 "New Resource" 创建子类的实例。

### 3.1 数值字段说明

每个带数值的动作都有两个字段，二选一：

| 字段 | 类型 | 说明 |
|---|---|---|
| `Value` | int | 静态数值。简单场景直接用 |
| `ValueSource` | ValueSource | 动态值源。有值时覆盖 Value |

### 3.2 基础动作

| 动作 | 关键字段 | 可逆 | 说明 |
|---|---|---|---|
| `DamageAction` | Value / ValueSource | × | 造成伤害，触发 OnDealDamage/OnKill |
| `HealAction` | Value / ValueSource | × | 治疗，不超过最大生命 |
| `DrawCardAction` | Value / ValueSource | × | 抽 N 张牌 |
| `ModifyCostAction` | Value / ValueSource | × | 增减费用（正=加，负=扣） |

### 3.3 召唤动作

| 动作 | 说明 |
|---|---|
| `SummonUnitAction` | 召唤 SourceCard 绑定的单位（卡牌必须是 UnitCardData） |

### 3.4 自动攻击

| 动作 | 说明 |
|---|---|
| `AutoAttackAction` | 自动攻击范围内最近的敌方。**用单位自身的攻击力和攻击范围**，不填数值 |

### 3.5 属性修改（可逆）

| 动作 | 关键字段 | 说明 |
|---|---|---|
| `ModifyStatAction` | TargetStat (AttackPower/MaxHP/Stamina/AttackDistance/ActionPoints), Value / ValueSource | 修改单位属性。Buff 到期时自动还原 |

**MaxHP 规则：** 施加时只加上限不减当前 HP；还原时如果当前 HP 超出新上限则截断。

### 3.6 Buff 动作

| 动作 | 关键字段 | 说明 |
|---|---|---|
| `ApplyBuffAction` | BuffData, InitialStacks=1 | 施加 Buff，初始层数可自定义 |
| `ModifyBuffAction` | BuffID, TurnsDelta, StacksDelta | 修改 Buff 回合/叠层。**减层时逐层还原属性**，归零时移除 |

### 3.8 控制流动作

| 动作 | 字段 | 说明 |
|---|---|---|
| `BranchAction` | Condition, ThenActions[], ElseActions[]（可选） | **分支**。条件满足时执行 ThenActions，否则执行 ElseActions。支持多层嵌套 |
| `RepeatAction` | Times（ValueSource）, MaxIterations=999, Actions[] | **循环**。重复执行子动作 N 次，N 来自值源。MaxIterations 为硬防死循环上限 |

### 3.9 可逆性汇总

| 可逆 | 动作 |
|---|---|
| ✓ | ModifyStatAction、ModifyBuffAction（减层操作） |
| × | DamageAction、HealAction、SummonUnitAction、DrawCardAction、AutoAttackAction、ModifyCostAction、ApplyBuffAction |

---

## 4. 值源（ValueSource）

值源是一个独立的数值来源，可以放在动作的 `ValueSource` 字段里，也可以放在条件的 `Left`/`Right` 里。
**当值源不为空时，覆盖对应的固定数值字段。**

| 值源 | 字段 | 说明 |
|---|---|---|
| `ConstantValue` | Value | 固定数值 |
| `UnitStatValue` | Unit=Source/Target, Stat, CurrentHP=true/false | 从单位读取属性。CurrentHP=true 取当前血量，false 取最大血量 |
| `BuffInfoValue` | Unit=Source/Target, BuffID, Info=StackCount/RemainingTurns | 读取单位上 Buff 的叠层数或剩余回合。Buff 不存在时返回 DefaultValue |
| `RandomValue` | Min, Max | 返回 [Min, Max] 之间的随机整数 |
| `FormulaValue` | Op, Left, Right | 对两个子值源做运算，**支持任意嵌套** |

### FormulaValue 支持的运算

| Op | 含义 | 说明 |
|---|---|---|
| `Add` | A + B | 加法 |
| `Sub` | A − B | 减法 |
| `Mul` | A × B | 乘法 |
| `Div` | A ÷ B | 除法（B=0 时返回 0） |
| `Max` | Max(A, B) | 取较大值 |
| `Min` | Min(A, B) | 取较小值 |
| `Percent` | A × B ÷ 100 | 百分比。B=50 表示 50%，不是 0.5 |

### 值源可放置的位置

| 位置 | 字段 |
|---|---|
| CompareCondition | Left, Right |
| DamageAction | ValueSource |
| HealAction | ValueSource |
| DrawCardAction | ValueSource |
| ModifyStatAction | ValueSource |
| ModifyCostAction | ValueSource |
| FormulaValue | Left, Right |

**示例：** 造成"目标攻击力 × 2 + 3"点伤害
```
DamageAction {
  ValueSource = FormulaValue {
    Op = Add
    Left = FormulaValue {
      Op = Mul
      Left = UnitStatValue { Unit=Target, Stat=AttackPower }
      Right = ConstantValue { Value=2 }
    }
    Right = ConstantValue { Value=3 }
  }
}
```

---

## 5. Buff 系统

### 5.1 BuffData 字段

| 字段 | 说明 |
|---|---|
| `BuffID` | Buff 唯一标识，用于叠层判定和 ModifyBuffAction 查找 |
| `BuffName` | 显示名称 |
| `Duration` | -1=永久，0=直接移除，N=持续 N 回合（当前回合计入） |
| `MaxStack` | -1=无限叠，0=禁用，N=最多 N 层 |
| `OnApplyActions` | **施加时执行，按层数倍数执行**（初始 3 层就执行 3 次） |
| `OnExpireActions` | 到期移除时执行，不受层数影响 |
| `OnRoundEndActions` | 每回合结束时执行（倒计时之后、归零判定之前） |
| `PassiveEffects` | 持续期间的被动效果，Buff 移除时自动取消订阅 |
| `Icon` | Buff 图标纹理 |

### Duration 含义

| 值 | 行为 |
|---|---|
| 0 | 直接移除，不倒计时 |
| 1 | 当前回合，RoundEnd 移除 |
| 3 | 持续 3 回合（RoundN→Round N+2 结束） |
| -1 | **永久**，不倒计时，只能通过死亡或驱散移除 |

### MaxStack 含义

| 值 | 行为 |
|---|---|
| 0 | 禁用，ApplyBuff 拒绝 |
| 1（默认） | 不可叠层，再次施加只刷新 Duration |
| 3 | 最多 3 层，再次施加增层+刷新 |
| -1 | **无限叠加** |

### 层数 = 效果倍率

`OnApplyActions` 按层数倍数执行：

- `InitialStacks=2` + `ModifyStatAction(ATK,+1)` → 施加 2 次 → **ATK+2**
- 触发 ModifyBuffAction(StacksDelta=-1) → 减 1 层，还原 1 次 → ATK-1
- 层数归零 → Buff 移除 → 还原全部效果 + OnExpireActions

### Buff 完整流程

```
ApplyBuff
  → 已有同 ID：还原旧层(x oldStack) → 更新 StackCount → 施加新层(x newStack)
  → 新建：创建 Buff → 执行 OnApplyActions(x StackCount)
           → 订阅 PassiveEffects → 创建 BuffView → Fire(OnBuffApplied)
    ↓
RoundEnd → TickAllBuffs
  → Duration > 0：RemainingTurns-1
  → 执行 OnRoundEndActions
  → 归零 → RemoveBuff
    ↓
RemoveBuff
  → Revert OnApplyActions(x StackCount)
  → UnsubscribeByTag（取消被动）
  → 执行 OnExpireActions
  → 销毁 BuffView → Fire(OnBuffRemoved)
```

---

## 6. 被动效果

配置在 `UnitData.PassiveEffects[]` 中，单位登场时自动注册。

### EffectData 字段

| 字段 | 说明 |
|---|---|
| `TriggerEvent` | 触发事件 |
| `Target` | Self=自身，EventTarget=事件另一方 |
| `Shape / Filter / AreaRange` | 范围模式。Shape≠None 时自动搜索目标 |
| `MaxTriggerCount` | 每回合最多触发 N 次。0=不限制。RoundStart 重置 |
| `Conditions` | 执行条件，空则无条件执行 |
| `Actions` | 动作序列 |

### 触发事件一览

| 事件 | 谁触发 | 说明 |
|---|---|---|
| `RoundStart` | BattleManager | 每回合开始 |
| `RoundEnd` | BattleManager | 每回合结束 |
| `OnSpawn` | UnitManager | 单位登场 |
| `OnDealDamage` | 攻击流程 | 造成伤害后，subject=攻击者 |
| `OnTakeDamage` | 攻击流程 | 受到伤害后，subject=受击者 |
| `OnKill` | 攻击流程 | 击杀后，subject=击杀者 |
| `OnBuffApplied` | BuffManager | Buff 施加后 |
| `OnBuffRemoved` | BuffManager | Buff 移除后 |
| `OnUnitAct` | BattleManager | 单位行动后（移动/攻击/出牌） |

### 被动效果示例

**"吸血鬼"——造成伤害时治疗自身 1 点，每回合限 2 次：**
```
TriggerEvent=OnDealDamage, Target=Self, MaxTriggerCount=2
Actions = [HealAction { Value=1 }]
```

**"防御塔"——回合结束时自动攻击：**
```
TriggerEvent=RoundEnd, Target=Self, MaxTriggerCount=1
Actions = [AutoAttackAction]
```

**"荆棘光环"——受伤时反击 2 点：**
```
TriggerEvent=OnTakeDamage, Target=EventTarget
Actions = [DamageAction { Value=2 }]
```

---

## 7. 卡牌配置

### CardData 通用字段

| 字段 | 说明 |
|---|---|
| `CardID` | 唯一标识 |
| `CardName` | 名称 |
| `Description` | 描述 |
| `Type` | Unit / Spell / Environment / Equipment / Special |
| `Shape` | 目标形状 |
| `Filter` | 阵营过滤 |
| `AreaRange` | 范围扩散半径 |
| `Cost` | 费用 |
| `Icon` | 卡面图标 |
| `Actions` | 打出效果 |
| `Conditions` | **打出条件**。不满足时不出牌不扣费 |

### UnitCardData 额外字段

| 字段 | 说明 |
|---|---|
| `UnitData` | 召唤的单位模板 |

Shape 通常为 SingleCell（点格子放置）。

### 卡牌示例

**火球术——对目标造成 2 伤害：**
```
Type=Spell, Shape=SingleUnit, Filter=Enemy, Cost=1
Actions = [DamageAction { Value=2 }]
```

**小兵——召唤小兵：**
```
Type=Unit, Shape=SingleCell, Cost=2
Actions = [SummonUnitAction]
```

**变强——施加 2 层强壮：**
```
Type=Spell, Shape=SingleUnit, Filter=Ally, Cost=1
Actions = [ApplyBuffAction { BuffData=<强壮.tres>, InitialStacks=2 }]
```

**处决——目标 HP<30% 时造成 5 伤害：**
```
Conditions = [CompareCondition {
  Left=UnitStatValue(Target, CurrentHP)
  Op=LessEqual
  Right=FormulaValue(Percent, UnitStatValue(Target, MaxHP), ConstantValue(30))
}]
Actions = [DamageAction { Value=5 }]
```

---

## 8. 目标系统

### Shape（范围形状）

| Shape | 说明 | 典型用途 |
|---|---|---|
| `None` | 无目标 | 抽牌、费用 |
| `SingleUnit` | 点选一个单位 | 单体伤害/治疗/Buff |
| `SingleCell` | 点选一个格子 | 召唤 |
| `AreaDiamond` | 菱形扩散 | 范围效果 |
| `AreaSquare` | 方形扩散 | 范围效果 |
| `All` | 全地图 | 全屏效果 |

### Filter（阵营过滤）

| Filter | 说明 | 预览颜色 |
|---|---|---|
| `All` | 所有单位 | 橙色 |
| `Enemy` | 敌方 | 红色 |
| `Ally` | 友方 | 蓝色 |

选中卡牌后鼠标悬停地图，自动预览目标范围，非法目标不显示。

---

## 9. 常见配置示例

### 9.1 强力击——造成"自身攻击力 × 2"伤害

```
Card Type=Spell, Shape=SingleUnit, Filter=Enemy, Cost=2
Actions = [DamageAction {
  ValueSource = FormulaValue(Mul, UnitStatValue(Source, AttackPower), ConstantValue(2))
}]
```

### 9.2 义肢3——ATK+3、MaxHP+3，行动后减 1 层（每回合限 1 次）

**BuffData：**
```
BuffID="义肢", Duration=-1, MaxStack=-1
OnApplyActions = [
  ModifyStatAction { TargetStat=AttackPower, Value=3 },
  ModifyStatAction { TargetStat=MaxHP, Value=3 }
]
PassiveEffects = [EffectData {
  TriggerEvent=OnUnitAct, MaxTriggerCount=1
  Actions=[ModifyBuffAction { BuffID="义肢", StacksDelta=-1 }]
}]
```

**卡牌：** `ApplyBuffAction { BuffData=<义肢.tres>, InitialStacks=3 }`

### 9.3 50% 概率回合结束治疗 2 点（被动）

```
TriggerEvent=RoundEnd, Target=Self
Conditions = [RandomCondition { Probability=0.5 }]
Actions = [HealAction { Value=2 }]
```

### 9.4 范围献祭——对菱形 2 格所有敌方造成 3 伤害（被动）

```
TriggerEvent=RoundEnd, Shape=AreaDiamond, Filter=Enemy, AreaRange=2
Actions = [DamageAction { Value=3 }]
```

### 9.5 意外之财——获得 3 费

```
Type=Spell, Shape=None, Cost=0
Actions = [ModifyCostAction { Value=3 }]
```

### 9.6 抽取等于场上敌人数的牌

```
Actions = [DrawCardAction {
  ValueSource = ???  -- 待扩展"数量统计"值源
}]
```

### 9.7 处决——目标 HP<30% 才造成 5 伤害（用分支实现）

```
Card Conditions 里可以直接用 CompareCondition，
但如果在动作序列中间需要分支，用 BranchAction：

Actions = [
  BranchAction {
    Condition = CompareCondition {
      Left=UnitStatValue(Target,CurrentHP)
      Op=LessEqual
      Right=FormulaValue(Percent, UnitStatValue(Target,MaxHP), ConstantValue(30))
    }
    ThenActions = [DamageAction { Value=5 }]
    ElseActions = [HealAction { Value=1 }]  -- 没斩杀成回 1 血意思一下
  }
]
```

### 9.8 连击——造成"目标攻击力"次 1 点伤害（用循环实现）

```
Actions = [
  RepeatAction {
    Times = UnitStatValue { Unit=Target, Stat=AttackPower }
    MaxIterations = 20
    Actions = [DamageAction { Value=1 }]
  }
]
```

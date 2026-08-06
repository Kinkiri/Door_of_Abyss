# Door of Abyss

战棋+卡牌策略游戏，Godot 4.7 + C# (.NET 8.0)。

---

## 目录

- [技术栈](#技术栈)
- [项目结构（程序员）](#项目结构程序员)
- [架构概览（程序员）](#架构概览程序员)
- [ECA 效果系统（程序员）](#eca-效果系统程序员)
- [安卓平台适配（程序员）](#安卓平台适配程序员)
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
  - [15. 环境系统](#15-环境系统)

---

# 程序员部分

## 技术栈

| 项目 | 值 |
|---|---|
| 引擎 | Godot 4.7 |
| 语言 | C# (.NET 8.0) |
| 渲染 | Forward Plus (D3D12) |
| 物理 | Jolt Physics 3D（预留） |
| 平台 | Windows（D3D12）/ Android（arm64，锁定横屏；Vulkan，无 Vulkan 设备回退 OpenGL） |
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
│   │   ├── SetStatAction.cs     设置属性为精确值（不可逆）
│   │   ├── ApplyEnvironmentAction.cs 施加环境（目标格子，替换式覆盖）
│   │   ├── RemoveEnvironmentAction.cs 移除环境（驱散）
│   │   ├── ModifyCellStatAction.cs  修改格子属性（MoveCost 可逆 / 布尔覆盖不可逆）
│   │   ├── TransformUnitAction.cs  变身（换模板 + 全清重置）
│   │   └── RandomTransformAction.cs 随机变身（CardFilter 模板库筛选）
│   ├── Targeting/               目标筛选器（抽象基类 + 多态子类，替代 Shape+Filter 枚举）
│   │   ├── TargetFilter.cs      抽象基类：ApplyUnits/ApplyCells/GetShape/GetCellShape/IsUnitMatch
│   │   ├── ShapeTargetFilter.cs 形状候选源：CustomShape（CellShape，推荐）或 Shape + AreaRange（旧枚举路径）
│   │   ├── PropertyTargetFilter.cs 静态属性筛选中间基类
│   │   ├── TeamTargetFilter.cs  相对阵营筛选
│   │   ├── UnitTypeTargetFilter.cs 单位类型筛选
│   │   ├── TagTargetFilter.cs   标签筛选（任一匹配）
│   │   ├── WorldTargetFilter.cs 世界观筛选（无=不限制）
│   │   ├── FactionTargetFilter.cs 势力筛选（无=不限制）
│   │   ├── UnitIDTargetFilter.cs 单位 ID 筛选（任一匹配）
│   │   ├── ConditionTargetFilter.cs 动态过滤：Conditions（配合值源筛运行时属性）
│   │   ├── ExtremeTargetFilter.cs 极值筛选（值源排序取最高/最低 N 个）
│   │   ├── RandomTargetFilter.cs 随机筛选（从已筛选目标组随机取 N 个，支持动态值源）
│   │   ├── AndTargetFilter.cs   AND 组合（形状节点生成 + 其余过滤）
│   │   ├── OrTargetFilter.cs    OR 组合（任一命中）
│   │   └── NotTargetFilter.cs   NOT 组合（补集）
│   ├── Shapes/                 形状体系（CellShape 多态类，自管格子生成，解析与预览共用）
│   │   ├── CellShape.cs        抽象基类：GetCells(center, ctx) → Cell[]（含中心、越界过滤）
│   │   ├── DiamondShape.cs     菱形扩散（AreaRange + 值源）
│   │   ├── SquareShape.cs      方形扩散（AreaRange + 值源）
│   │   ├── CrossShape.cs       十字（中心 + 四臂各 Length）
│   │   ├── XShape.cs           叉字（中心 + 四对角各 Length）
│   │   ├── RayShape.cs         射线（方向 + 长 + 宽，矩形带）
│   │   ├── TriangleShape.cs    三角形（方向 + 长，每排宽 2i+1 锥形）
│   │   ├── RowShape.cs         整行（中心所在行左右各 Length）
│   │   ├── ColumnShape.cs      整列（中心所在列上下各 Length）
│   │   ├── RingShape.cs        环形（曼哈顿距离恰为 Radius，不含内部）
│   │   └── AllShape.cs         全地图
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
│   │   ├── UnitInfoValue.cs     单位类型（枚举数值）
│   │   ├── BattleCostValue.cs   费用
│   │   └── Cell/                格子坐标值源（Vector2I，抽象基类 CellValueSource）
│   │       ├── CellValueSource.cs   坐标值源基类：GetCell(Context) → Vector2I?（null=无有效坐标）
│   │       ├── UnitCellValue.cs     单位坐标读取（Source/Target/EventOther）
│   │       ├── ContextCellValue.cs  格子坐标读取（ctx.TargetCell/SourceCell）
│   │       ├── OffsetCellValue.cs   坐标偏移（基准 + dx/dy，支持值源覆盖）
│   │       ├── StepCellValue.cs     方向步进（基准沿方向走 N 格，方向/距离支持值源覆盖）
│   │       ├── DirectionValue.cs    方向计算（两点 → CellDirection 4 向枚举值）
│   │       ├── AttackDirectionValue.cs 攻击方向读取（ctx.AttackDirection → 4 向枚举值）
│   │       ├── OppositeDirectionValue.cs 方向取反（Up↔Down、Left↔Right）
│   │       ├── CellCoordValue.cs    坐标分量（X/Y 整数读取，条件比较用）
│   │       └── RandomCellValue.cs   形状内随机一格（可选仅可站立/未占据）
│   ├── Card/
│   │   ├── CardData.cs          卡牌基类（TargetFilter/Conditions/Actions）
│   │   ├── DeckData.cs          卡组
│   │   ├── SpellCardData.cs     法术
│   │   ├── UnitCardData.cs      单位卡（含 UnitData）
│   │   ├── EquipmentCardData.cs 装备卡
│   │   ├── EnvironmentCardData.cs 环境卡（含 EnvironmentData）
│   │   └── Filters/             卡牌筛选器（CardFilter 多态组合，用于筛选抽牌）
│   │       ├── CardFilter.cs    抽象基类：IsMatch(Card) + CombineAnd（数组默认 And）
│   │       ├── CardTypeFilter.cs  卡牌类型筛选（任一匹配）
│   │       ├── CardTagFilter.cs   卡牌标签筛选（任一匹配）
│   │       ├── CardUnitTypeFilter.cs 单位卡单位类型筛选（任一匹配，非单位卡不命中）
│   │       ├── CardFactionFilter.cs  势力筛选（无=不限制）
│   │       ├── CardCostFilter.cs     费用区间筛选（-1=该端不限）
│   │       ├── AndCardFilter.cs   AND 组合
│   │       ├── OrCardFilter.cs    OR 组合
│   │       └── NotCardFilter.cs   NOT 组合（补集）
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
│   ├── EnvironmentData.cs       环境模板（格子 Buff）
│   ├── EffectData.cs            被动效果模板
│   └── PlayerData.cs            玩家全局数据（含门列表）
├── Enum/                        枚举定义（含 World/Faction/Rarity/Tag 等世界观数据）
│   ├── BattlePhase.cs           BattlePhase + Team
│   ├── BuffInfoType.cs          BuffInfoType
│   ├── CardType.cs              CardType
│   ├── CompareOp.cs             CompareOp
│   ├── ConditionTarget.cs       ConditionTarget
│   ├── CellPropertyOverride.cs  格子布尔三态覆盖（Unchanged/ForceTrue/ForceFalse）
│   ├── CellStatType.cs          格子属性类型（MoveCost/CanStand/CanPass）
│   ├── EventType.cs             事件枚举（20 种）
│   ├── Faction.cs               Faction（势力）
│   ├── FormulaOp.cs             FormulaOp
│   ├── ModifyStatType.cs        ModifyStatType
│   ├── PassiveTarget.cs         PassiveTarget
│   ├── Rarity.cs                Rarity（稀有度，中文枚举：初级/中级/高级/顶级）
│   ├── Tag.cs                   Tag（标签）
│   ├── TargetKind.cs            TargetKind（目标结果类型：Unit/Cell）
│   ├── TargetShape.cs           TargetShape
│   ├── TeamFilter.cs            TeamFilter（相对阵营过滤，原 TargetFilter 改名）
│   ├── UnitTybe.cs              UnitType（中文枚举：兵种/建筑/障碍物/召唤物/特殊物/门）
│   ├── ValueTarget.cs           ValueTarget
│   └── World.cs                 World（世界观）
├── Instance/                    运行时实例层（纯 C# class，不继承 Godot 类型）
│   ├── Buff.cs                  Buff 运行时
│   ├── Card.cs                  卡牌运行时
│   ├── Cell.cs                  格子运行时
│   ├── Context.cs               ECA 上下文 DTO（含 Map/ActiveUnits 战场数据）
│   ├── Environment.cs           环境运行时
│   └── Unit.cs                  单位运行时
├── Manager/                     逻辑层（Godot Node 单例，服务定位器）
│   ├── ActionQueue.cs           动作序列器（逐个执行 + 动画间隔 + 插队）
│   ├── BattleManager.cs         战斗阶段 + 费用 + 胜利 + 行为执行 + 波次
│   ├── BuffManager.cs           Buff 生命周期（发事件驱动视图）
│   ├── CardManager.cs           牌库/手牌/弃牌（发事件驱动视图）
│   ├── EnemyAI.cs               敌方 AI（按距玩家门排序 + 最短路径寻路 + 被堵留AP）
│   ├── EnvironmentManager.cs    环境管理器（施加/覆盖/移除/倒计时 + 格子属性统一重算）
│   ├── EventBus.cs              事件总线（被动效果订阅/触发，Tag 支持）
│   ├── InitManager.cs           初始化调度
│   ├── MapManager.cs            地图管理
│   ├── SelectionManager.cs      输入 + 选中（任意阶段可选单位/卡牌）+ 移动攻击高亮 + 出牌流程
│   └── UnitManager.cs           单位生命周期（发事件驱动视图）
├── View/                        视图层（事件驱动渲染）
│   ├── BuffView.cs              Buff 图标（Node2D，内含 TextureRect + Label）
│   ├── CardView.cs              卡牌展示
│   ├── DragCamera2D.cs          拖拽摄像机（丝滑缩放 + 非线性跟随：选中聚焦/行动跟随）
│   ├── EnvironmentViewManager.cs 环境图层渲染（订阅环境事件 SetCell/EraseCell）
│   ├── HandPanel.cs             手牌面板（订阅 CardManager 事件）
│   ├── LevelFadeIn.cs           战斗场景渐变入场（黑幕停留 1s → 0.9s 渐出，CanvasLayer 顶层）
│   ├── MainMenu.cs              主界面（夜色背景/光尘粒子/呼吸标题/竖排菜单/选关·关于面板/退出）
│   ├── MapView.cs               地图渲染 + 高亮（含波次刷怪预告层 WavePreviewLayer）
│   ├── RoundInfoPanel.cs        右上角战斗信息面板（横排：阶段/阵营/回合/费用/手牌 + 结束回合按钮）
│   ├── UnitInfoPanel.cs         左下角信息面板（选中单位/格子/卡牌详情：描述/属性/Buff/装备/环境）
│   ├── UnitView.cs              单位视觉 + 内建动画（入场/受伤/治疗/死亡/移动/Buff）
│   └── UnitViewManager.cs       订阅 UnitManager/BuffManager 事件，创建/销毁 UnitView 与 BuffView
├── Tests/
│   └── TestRunner.cs            全面系统性测试（485 项，场景内集成运行）
├── Tools/
│   └── TextToResourceImporter.cs  文本转 .tres 工具（EditorScript）
└── Utils/
	├── LevelSelection.cs        选关结果传递（静态类：主界面 → BattleManager 关卡覆盖）
	├── MapExporter.cs           地图导出工具（基础地形 + 环境层双导出）
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
EventBus.Fire(EventType, Context, instigator)
  → 遍历订阅者
	→ 触发次数检查（MaxTriggerCount）
	→ 创建 effectCtx = 事件 ctx.Clone() + 订阅者语义覆盖（SourceUnit/SourceTeam/目标解析）
	  （克隆式全量透传：事件载荷 TargetCell/SourceCell/Map/ActType/方向/伤害修饰等自动继承，
	   新增字段零改动；EventOtherUnit 由 ctx.TargetUnit 派生；filter 路径 TargetUnit=null）
	→ 条件检查（Conditions，任意不满足则 skip）
	→ 执行 Actions（DamageModifier 增量 diff 回写事件 ctx）
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
	Unit SourceUnit;        // 效果来源单位（=被动所有者：单位被动=自己，环境被动=施加者，手牌被动=事件来源）
	Unit TargetUnit;        // 单目标（Target=Self 时=自己；EventOther 时=事件另一方）
	Unit[] TargetUnits;     // 多目标（TargetFilters 解析）
	Unit EventOtherUnit;   // 事件另一方（如死亡事件=死者；供值源 Unit=EventOther 读取）
	Team SourceTeam, TargetTeam;
	Card SourceCard;
	Cell TargetCell, SourceCell;
	Cell[] TargetCells;
	UnitActType ActType;    // 行动类型（OnUnitAct：移动/攻击）
	CellDirection? AttackDirection;  // 攻击方向（攻击者→受击者 4 向；攻击事件/OnUnitAct 攻击时填充，非攻击=null）
}
```

---

## 主界面与选关（程序员）

### 主界面（title.tscn + MainMenu.cs）

- **背景**：夜色 JPG 铺满（canvas_items 拉伸 + 冷色调压暗）+ 径向暗角遮罩 + 底部光尘粒子（GPUParticles2D，`_Process` 匀速左右往返，速度/边界为 Export 参数）
- **标题**：白色标题图，呼吸动画（缩放 1.0↔1.05 + 亮度 0.86↔1.0，6.4s 循环）；`PivotOffset = 图片尺寸/2` 保证围绕图片中心缩放
- **菜单**：竖排细字按钮（flat + 半透明白字 + hover 左侧指示条渐显），入场错峰淡入（每行 scale 0.94→1 + alpha，时长 1s）
- **面板**：「关于」「选关」共用通用动画 `AnimatePanelIn/Out`（暗幕渐显 + 面板下滑 60px 弹出，0.35s Cubic；✕/暗幕点击关闭，`_creditsBasePos` 归位防偏移累积）
- **退出**：黑幕淡出 0.5s → `GetTree().Quit()`

### 选关流程

```
点击"开始游戏" → 选关面板滑出（LevelSelectBackdrop + LevelSelectPanel）
  ├─ 左：关卡列表（BuildLevelList 遍历 LevelLibrary.LevelList 生成按钮，默认选中第一个，选中白字高亮）
  ├─ 右：关卡详情（名称/描述/波次汇总/卡组）—— Summarize<T> 按名称统计为 "名字×数量"，无空行
  └─ 右下"进入关卡" → LevelSelection.Selected = 关卡 → 黑幕淡出 → ChangeSceneToFile(Level.tscn)
战斗场景：BattleManager._Ready 读 LevelSelection.Selected 覆盖 Level.tscn 的固定引用（编辑器直跑保留 fallback）
```

### 波次刷怪预告

- **两阶段**：`GameStart` 预计算第 1 波；每回合 `RoundStart` 生成当前波（按预告位置）后立即预告下一波——玩家在整个玩家行动阶段可见
- `PreviewWave(round)`：收集生成区域可站立空格 → 洗牌锁定位置 → 存 `_waveSpawnPlan` → `MapView.RenderWavePreview` 画红色警示格（独立 `WavePreviewLayer`）
- `SpawnWaveForRound`：有预告计划 → 按计划生成并清除预告；无计划（编辑器直跑）→ 回退原随机路径

### 环境瓦片化（2026-08-06）

环境从"运行时手填图集坐标"改为**瓦片地图绘制 + 数据存储**：

```
编辑器 EnvironmentLayer 画瓦片（瓦片 custom data 绑 EnvironmentData，同 BlockData 机制）
  → F5（MapExporter.EnvironmentSourceLayer 导出）
  → MapData.EnvironmentPositions / EnvironmentDatas
  → MapManager.LoadFromMapData 末尾 → EnvironmentManager.LoadPresetEnvironments 静默施加
  → 与动态环境（环境卡）同一生命周期：属性修正/被动/渲染
```

- `EnvironmentLayer` 与 `BaseMapLayer` **平级**（消除嵌套 TileMapLayer 的 transform 继承问题），使用独立 `EnvironmentTileSet.tres`（占位图集，瓦片 0:0 绑毒沼）
- 新增环境：建 EnvironmentData 资源 + 环境图集加瓦片绑资源，无需改代码

---

## 安卓平台适配（程序员）

### 平台状态

- **Godot 4.7.1 .NET**，目标 **Android 12+（arm64-v8a）**，锁定横屏（`display/window/handheld/orientation="landscape"`）
- 渲染：Vulkan（Forward+）；设备不支持 Vulkan 时 Godot 自动回退 OpenGL 3（模拟器常见）
- 打包：标准导出模板（非 gradle），APK 约 100MB（含 .NET 运行时）

### 触摸输入（TouchInputAdapter）

**点击交给 Godot 原生触摸→鼠标模拟**（`emulate_mouse_from_touch=true`，位置天然正确，
SelectionManager / 放门 / Control 按钮直接可用），`Scripts/View/TouchInputAdapter.cs`
只补充原生模拟覆盖不了的手势，**SelectionManager / DragCamera2D / BattleManager 等鼠标逻辑零改动**：

| 触摸手势 | 处理方 |
|---|---|
| 单指点击（选单位/格子/出牌/放门） | Godot 原生触摸→鼠标模拟 |
| 单指拖动（超 24px 阈值） | 适配器：中键拖拽镜头（进入拖拽先注入右键取消，防按下瞬间误选中） |
| 双指捏合 | 适配器：滚轮缩放（锚点 = 两指中点） |
| 选中卡牌后拖动 | Godot 原生模拟鼠标移动（卡牌目标预览跟随） |
| 左上角 ✕ 按钮 | 等效右键（ClearSelection） |

- 触摸到 Control（手牌/按钮）由 GUI 阶段先行消费，适配器天然避让 UI
- 手牌触屏无 hover 放大（点卡即选中，可接受）
- PC 调试：按住 **Ctrl + 左键** 可模拟单指触摸（`EnableDebugMouseSimulation`）

### 导出配置

- `export_presets.cfg`：Android preset（包名 `com.doorofabbyss.game`，arm64-v8a，debug keystore 签名）
- 环境：JDK 17 + Android SDK（platform-tools / platform-34 / build-tools 34.0.0 / NDK r25c）+ 4.7.1 .NET 导出模板
- Godot 编辑器设置需配置 `export/android/java_sdk_path` / `android_sdk_path` / `android_ndk_path`
- 命令行导出：
  ```
  Godot_console --headless --path . --export-debug "Android" build/Android/DoorOfAbyss.apk
  ```
- **导出时 TEMP 需指向空间充足的盘**：apksigner 签名写临时文件，C 盘空间不足会报"磁盘空间不足"

### 注意事项

- **打包后 .tres 被 UID 重映射为 .tres.remap**：`Library.GetAllTresPaths` 已兼容（枚举时去掉 `.remap` 后缀，逻辑路径不变），Windows/Android 导出均正常加载（卡牌库 54 张）
- **模拟器（MuMu/雷电）限制**：Vulkan 不可用自动回退 OpenGL；arm64 APK 经 houdini 指令转译，卡牌库加载可能极慢/卡死（真机原生 arm64 正常）；adb swipe 受 INJECT_EVENTS 权限限制无法自动化拖拽手势
- **待真机验证**：单指拖镜头、双指捏合缩放、安卓端卡牌库加载性能

---

# 策划配置手册

## 1. 核心战斗流程

每一回合按以下顺序自动推进：

```
主界面 → 点"开始游戏" → 选关面板（左列表/右详情/右下进入）
  → 进入关卡（LevelSelection 传递所选关卡）

GameStart（游戏开始）
  ├─ 加载地图（含预置环境，瓦片化自动加载）
  ├─ 预告第 1 波刷怪位置（红色警示格）
  ├─ 手动放门
  └─ 初始化卡组（开局不抽牌，手牌为空，抽牌由回合/效果获得）
  ↓ 自动
RoundStart（回合开始）
  ├─ 所有单位行动点恢复满上限，费用 +2
  ├─ 抽 1 张牌
  ├─ 按上回合预告的位置生成波次（无预告时回退随机）
  ├─ 预告下一波刷怪位置（玩家行动阶段可见）
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

**波次预告：** 刷怪位置在上一回合就开始预告（地图上红色格），玩家有一整个回合布置防线。预告位置在锁定后生成前可被玩家主动占据（占据则生成时跳过该格）。

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
| `HasTagCondition` | CheckTarget, Tags, Has | 检查单位是否带任一指定 Tag（Tag 来自单位模板，战斗中不变） |
| `HasActedCondition` | CheckTarget, HasActed | "本回合已行动过"——读 `Unit.ActionsThisTurn`（移动/攻击各算一次，出牌/被动自动攻击不计，RoundStart 归零）。不依赖 AP 比较（AP 可被透支超过上限） |
| `ActionKindCondition` | Kind=移动/攻击 | 判断 OnUnitAct 触发时的行动类型（`Context.ActType`，由 BattleManager 触发时填充） |
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
| DrawCardAction | Value/ValueSource, Filters | x | 抽牌。配置 `Filters`（CardFilter 数组，默认 And）时**只从牌库随机抽取匹配的牌**（无匹配不抽、不足全要）；不配置则抽牌库顶 |
| ModifyCostAction | Value/ValueSource | x | 增减费用 |

### 3.2 召唤

| 动作 | 说明 |
|---|---|
| SummonUnitAction | 通用召唤：配置 `UnitData`（Resource 引用）或 `UnitID`（字符串查 UnitLibrary，亡语重生等循环引用场景用字符串）可直接召唤任意单位（无部署范围限制，可用于法术/被动）；两者都未配置时回退到单位卡自身（`UnitCardData.UnitData`），且仅此路径保留"己方门部署范围内"检查。可选 `SpawnBuff`/`SpawnBuffStacks`：召唤成功后自动给新单位施加 Buff（单位卡打出时带 Buff 的实现方式）。可选 `SummonPosition`（坐标值源）：非空且有有效坐标时直接在该坐标放置（覆盖 TargetCells/TargetCell，如"来源前方 2 格"/随机格），坐标无效/格子不存在时静默跳过 |

部署限制出牌前由 `SelectionManager.ValidateCardTarget` 拦截（范围外点击不出牌、不扣费），`SummonUnitAction` 内保留同检查作为双保险。

### 3.2.1 变身

| 动作 | 字段 | 说明 |
|---|---|---|
| TransformUnitAction | UnitData / UnitID | **变身**：将目标单位切换为指定模板并完全重置。**语义=完全重置**：清一切 buff/装备（还原加成 + 退订 + 视图销毁；`CanBeChanged=false` 固定 buff 保留）、按新模板刷新属性（满血）、旧被动退订 + 新被动订阅；位置/阵营不变。变身后触发 `OnUnitTransformed`（无 instigator 定向） |
| RandomTransformAction | Filters（CardFilter[]，默认 And） | **随机变身**：从模板库筛匹配的**单位卡**随机取一张（按卡随机、不去重，同单位多卡权重更高）变身成其单位。例：`[CardFactionFilter{擢升之手}, CardCostFilter{MaxCost=6}]` = 随机变为费用≤6 的擢升之手单位。筛选复用 CardFilter + `CardLibrary.GetCards/GetRandomCard` 通用查询（与抽牌共用体系） |

> **设计要点**：变身触发即移除义肢等 buff（全清语义）→ 触发条件不再满足，天然只触发一次。示例：破碎残躯被动 = `TriggerEvent=OnBuffStackChanged`（叠层变化/设置后触发，`TargetUnit`=层数变化单位，instigator 定向自己）+ 条件 `BuffInfoValue{Unit=Source, BuffID=义肢, StackCount} >= 2` + `RandomTransformAction{Filters=[CardFactionFilter{擢升之手}, CardTypeFilter{单位}, CardCostFilter{MaxCost=6}]}`。

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

**仅当前 AP（`CurrentAPOnly=true`）：** 只修改当前行动点，上限不动——**允许当前 AP 超过上限**（"本回合多动一次"类透支效果），只 clamp 下限 0。用于如卡牌"行动点+1，下回合开始-1"：+1 用当前模式，惩罚由额外 Buff 在下回合 RoundStart 被动减层（触发 Revert 还原 +1）自毁。

**体力单值：** 体力 = 移动范围半径（曼哈顿距离），单值无"上限/剩余"之分（移动不消耗体力，只扣 AP）。

**叠层刷新**：已有 Buff 再次施加时，只对**新增层数**执行 OnApplyActions（旧层效果保留，不先还原旧层）——避免"还原不扣当前 HP + 全量重施"导致的血量虚增（如 2 层义肢 3/6 血再上 2 层 → 5/8，而非错误的 7/8）。

### 3.5 Buff 动作

| 动作 | 字段 | 说明 |
|---|---|---|
| ApplyBuffAction | BuffData, InitialStacks, ValueSource | 施加 Buff。**遍历 `TargetUnits` 支持多目标**（如 Shape=All）。`ValueSource` 为动态叠层值源（设置后覆盖 `InitialStacks`，如亡语"转移与死者相同的层数"） |
| ModifyBuffAction | BuffID, TurnsDelta, StacksDelta, WearMode | 修改回合/叠层。**回合/叠层最小减到 0，不能为负**；`RemainingTurns=-1`（永久 Buff）忽略回合修改，其他非法负值警告并按 0 处理；叠层归零移除。**`WearMode=true`（磨损模式）：减层只消耗"行动开始快照（`Buff.StacksAtActionStart`）内的旧层"**——本次行动中新增的层不因本次行动损耗（如"攻击后获得义肢"不被本次攻击磨损） |
| RemoveBuffAction | BuffID | 无条件整个移除（驱散） |
| **RemoveEquipmentAction** | EquipmentID | 移除目标单位上指定 ID 的装备（驱散）。属性加成还原（可逆）+ 取消被动订阅；单位同一时间只能装备一件，ID 不匹配或未装备时不动作 |
| **ModifyDamageAction** | Delta, ValueSource | 修改本次伤害事件的伤害量（正=加伤，负=减伤）。配合**攻击前/受击前**事件使用：攻击者挂加伤（`OnBeforeAttack`）、受击者挂减伤（`OnBeforeTakeDamage`），作用于各自 `ctx.DamageModifier`，`DamageAction` 结算时两侧累加，多个修饰被动可叠加。`ValueSource` 动态增量覆盖 `Delta`（如 `FormulaValue(Mul, PendingDamageValue, ConstantValue(-1))` 把伤害清零 → 致命免伤） |

### 3.6 控制流

| 动作 | 字段 | 说明 |
|---|---|---|
| BranchAction | Condition, ThenActions[], ElseActions[] | 条件真->Then，假->Else。支持嵌套 |
| RepeatAction | Times(ValueSource), MaxIterations, Actions[] | 重复 N 次。MaxIterations 防死锁 |

### 3.7 强制位移

| 动作 | 字段 | 说明 |
|---|---|---|
| MoveUnitAction | Mode=Teleport/Push/Pull, Distance + DistanceValueSource | **作用于 TargetUnits**（与其他动作一致：卡牌=筛选目标，被动=TargetFilter/Self/EventOther 解析），不耗 AP。Teleport=传送到 TargetCell/TargetUnit 所在格（可选 `TeleportPosition` 坐标值源优先指定落点）；**Push=击退（远离锚点）**、**Pull=拉拽（靠近锚点）**，方向与距离均支持值源：`DirectionValueSource`（值源优先，CellDirection 枚举值，如 `DirectionValue` 动态朝向/常量固定向）、`DirectionAnchor`（坐标值源，方向 = 锚点坐标 → 目标，可推离门口/环境格；未配置时锚点=SourceUnit）、`DistanceValueSource`（动态距离）。锚点与方向均缺失时原地不动 |

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
| UnitStatValue | Unit=Source/Target/EventOther, Stat, CurrentHP=true/false | 单位属性（`EventOther`=事件另一方，死亡事件=死者） |
| BuffInfoValue | Unit=Source/Target/EventOther, BuffID, Info=StackCount/RemainingTurns | Buff 叠层/回合，不存在时返回 DefaultValue。**`Unit=EventOther` 读事件另一方的 Buff**（如"获得死者的义肢层数"） |
| EquipmentInfoValue | Unit, Info=HasEquipment/各Bonus | 装备信息，无装备时 HasEquipment=0、其余返回 DefaultValue |
| RandomValue | Min, Max | [Min, Max] 随机整数 |
| FormulaValue | Op, Left, Right | 嵌套运算（Add/Sub/Mul/Div/Max/Min/Percent） |
| RoundCountValue | - | 当前回合数 |
| UnitCountValue | FilterTeam=All/Player/Enemy, OnlyAlive, IncludeDoor | 单位数量 |
| DistanceValue | From=Source/Target, To=Source/Target | **曼哈顿距离** |
| BattleCostValue | Type=Current/Max | 当前/最大费用 |
| **CardInfoValue** | Info=Cost/Type/World/Faction/Rarity | **读取 `ctx.SourceCard` 的卡牌属性**（卡牌被动场景=被抽/被打出的牌；Type/World/Faction/Rarity 返回枚举数值，配合 CompareCondition 做类型判断），无卡牌时返回 DefaultValue |
| **UnitInfoValue** | Unit=Source/Target/EventOther, Info=Type/Team | **读取单位的类型/阵营枚举**（`UnitType`/`Team` 数值，配合 CompareCondition 判断"目标是建筑/兵种/门…"、"死者是友方"等），单位不存在时返回 DefaultValue |
| **PendingDamageValue** | - | **本次伤害的基础伤害值**（攻击前/受击前事件时由 DamageAction 填充 `ctx.PendingDamage`）。配合条件判断"会致死"（如致命免伤） |
| **AttackDirectionValue** | DefaultValue | **本次攻击方向**（攻击者→受击者的曼哈顿 4 向，CellDirection 枚举值；攻击事件/OnUnitAct 攻击时填充，非攻击事件返回 DefaultValue）。配合条件判断"背刺/侧翼" |
| **OppositeDirectionValue** | Direction(方向值源), DefaultValue | **方向取反**：输入方向（CellDirection 枚举值）取反（Up↔Down、Left↔Right）；输入非法/缺失返回 DefaultValue。典型：`Direction=AttackDirectionValue` 得到"目标背后"方向 |

### 4.1 格子坐标值源（CellValueSource）

坐标值源返回 `Vector2I?`（**null = 无有效坐标**，消费方静默跳过）。抽象基类 `CellValueSource` 与 int 版 `ValueSource` 平行；纯坐标计算（读取/偏移/步进）不做地图校验，可链式组合（如 Step → Offset），由最终消费方（召唤/传送/扩散中心）经地图查格校验。

| 坐标值源 | 字段 | 说明 |
|---|---|---|
| UnitCellValue | Unit=Source/Target/EventOther | 单位格子坐标（GridPos） |
| ContextCellValue | Cell=Target/Source | ctx.TargetCell（事件格/环境格/点击格）/ ctx.SourceCell（移动前旧格）坐标 |
| OffsetCellValue | Base(坐标值源), Dx/Dy + DxValueSource/DyValueSource | 基准坐标 + (dx, dy) 偏移；偏移支持固定值与值源覆盖 |
| StepCellValue | Base, Direction + DirectionValueSource, Distance + DistanceValueSource | 基准沿方向走 N 格；方向（CellDirection 4 向）与距离均支持值源覆盖；距离 ≤0 返回基准 |
| DirectionValue | From/To=Source/Target/EventOther | **方向计算**：两点 → `CellDirection` 枚举值（|dx|≥|dy| 取横向，否则纵向；零向量按横向 Right，与 MoveUnitAction 同约定） |
| CellCoordValue | Cell(坐标值源), Info=PosX/PosY, DefaultValue | 坐标 X/Y 分量（int），配合 CompareCondition 做"X 坐标≥5"类判断 |
| RandomCellValue | Base(null→ctx.TargetCell), Shape=菱形/方形/全图, Range + RangeValueSource, RequireStandable(默认 true) | 形状内随机一格；`RequireStandable=true` 只取可站立且未占据的格（召唤落点用），无可选格返回 null |

**典型用法：**
- "来源前方 2 格"：`StepCellValue{Base=UnitCellValue(Source), Direction=Up, Distance=2}`（方向也可用 `DirectionValue` 动态计算，如"朝向目标方向 3 格"）
- "来源右上 1 格"：`OffsetCellValue{Base=UnitCellValue(Source), Dx=1, Dy=-1}`
- "随机空地召唤"：`RandomCellValue{Shape=AreaDiamond, Range=2}` → `SummonPosition`
- "以环境格为中心菱形 2 格扩散"：`ShapeTargetFilter.CenterOverride = ContextCellValue(Target)`（环境被动里 TargetCell=环境所在格）

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
| SummonUnitAction | SummonPosition（坐标值源：指定召唤位置） |
| MoveUnitAction | TeleportPosition（坐标值源：Teleport 指定落点）/ DirectionValueSource（int：Push/Pull 方向）/ DirectionAnchor（坐标值源：Push/Pull 锚点）/ DistanceValueSource（int：Push/Pull 距离） |
| ShapeTargetFilter | AreaRangeValueSource（int）/ CenterOverride（坐标值源：扩散中心覆盖） |
| OffsetCellValue / StepCellValue | Base（坐标值源）+ 偏移/方向/距离（int 值源覆盖） |
| RandomCellValue | Base（坐标值源，null→ctx.TargetCell）+ Range（int 值源覆盖） |

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

**例外：`FixedEffect=true`（固定效果，如义肢）**——OnApplyActions 只执行 **1 次**，效果与层数解耦：叠层/减层不重放/不还原，**有层即生效，归零/移除才一次性还原**。层数仅作计数器（耐久/磨损）。

完整生命周期见程序员部分。

## 6. 被动效果

配置在 `UnitData.PassiveEffects[]` 中。

### EffectData 字段

| 字段 | 说明 |
|---|---|
| TriggerEvent | 触发事件 |
| Target | Self=自身, EventOther=事件另一方（TargetFilters 为空时生效） |
| TargetFilters | 目标筛选器数组（默认 And；非空时自动解析目标，忽略 Target） |
| MaxTriggerCount | 每回合最多 N 次，0=不限制 |
| Conditions | ECA 条件 |
| Actions | 动作序列 |

### 事件角色模型（四角色）

被动触发时，effectCtx（Actions/Conditions/值源可读的上下文）里有四个角色：

| 角色 | 字段 | 计算方式 |
|---|---|---|
| **自己**（被动所有者） | `SourceUnit` | 单位被动=订阅单位**自己**；环境被动=环境**施加者**；手牌被动=事件 ctx 的来源（可能 null） |
| **操作目标** | `TargetUnit` / `TargetUnits` / `TargetCells` | 有 TargetFilters → 筛选器解析（中心=自己格子/环境格/事件格）；无 → `Self`=自己 或 `EventOther`=事件另一方 |
| **对方**（事件另一方） | `EventOtherUnit` | = 事件 ctx 的 `TargetUnit`，随事件视角变化（见下表） |
| ~~事件触发者~~ | `instigator`（**不进 effectCtx**） | 仅用于订阅过滤：Unit 订阅者要求"事件发生在自己身上"，值源/条件读不到 |

> **来源 ≠ 触发者**：effectCtx 的 `SourceUnit` 是"效果施法者"（单位被动恒为自己），**不是**事件触发者（instigator）。事件 ctx 的"事件源"（如 OnUnitDeath 的 ctx.SourceUnit=死者）在 effectCtx 里不可直接读——要读事件另一方，用 `EventOtherUnit`（值源 `Unit=EventOther`）。

**各事件视角的"对方"：**

| 事件 | 对方（`EventOtherUnit` / `Target=EventOther` 时的 `TargetUnit`） |
|---|---|
| OnDealDamage / OnBeforeAttack | 受击者 |
| OnTakeDamage / OnBeforeTakeDamage | 攻击者 |
| OnKill / OnUnitDeath / OnAnyUnitDeath | 死者 |
| OnUnitEnterCell / OnUnitLeaveCell | 进入/离开的单位 |
| OnUnitTransformed | 变身单位 |
| OnBuffStackChanged | 层数变化的单位 |
| OnUseCard / OnDrawCard / RoundStart / RoundEnd / OnMove / OnSpawn | 无（null） |

**典型用法（"自己获得对方的东西"）**：`Target=Self`（操作目标=自己）+ 条件/值源用 `Unit=EventOther` 读对方。例（MK0"友方兵种死亡，获得其义肢"）：

```
TriggerEvent=OnAnyUnitDeath, Target=Self
Conditions=[
  UnitInfoValue{Unit=EventOther, Info=Type} == 0(兵种),
  UnitInfoValue{Unit=EventOther, Info=Team} == UnitInfoValue{Unit=Source, Info=Team}  // 相对阵营
]
Actions=[ApplyBuff{义肢, ValueSource=BuffInfoValue{Unit=EventOther, BuffID=义肢}}]  // 读死者层数
```

### 触发事件

| 事件 | 说明 |
|---|---|
| RoundStart / RoundEnd | 回合开始/结束 |
| OnSpawn | 单位登场 |
| OnDealDamage / OnTakeDamage | 造成/受到伤害 |
| OnKill | 击杀 |
| OnBuffApplied / OnBuffRemoved | Buff 施加/移除 |
| OnUnitAct | 单位行动后（移动/攻击；**出牌不触发**），`ctx.ActType` 区分移动/攻击 |
| **OnUseCard** | 使用卡牌后（出牌成功扣费后、卡牌动作执行前），无 instigator（来源单位经 `SourceUnit` 取） |
| **OnDrawCard** | 抽牌后（`SourceCard`=被抽的牌，`SourceTeam`=抽牌方），无 instigator。**手牌被动只响应"自己被抽到"**（防连锁递归），单位被动可响应任意抽牌 |
| **OnBeforeAttack** | **攻击前**（伤害计算前，攻击者视角，instigator=攻击者）。`SourceUnit`=攻击者，`TargetUnit`=受击者，`ctx.PendingDamage`=本次基础伤害，`ctx.AttackDirection`=攻击方向（攻击者→受击者 4 向）。攻击者挂"加伤"被动（读 `Source`=自己），用 `ModifyDamageAction` 改 `ctx.DamageModifier` |
| **OnBeforeTakeDamage** | **受击前**（伤害计算前，受击者视角，instigator=受击者）。`SourceUnit`=受击者（自己），`TargetUnit`=攻击者，`ctx.PendingDamage`=本次基础伤害，`ctx.AttackDirection`=攻击方向。受击者挂"减伤"被动（读 `Source`=自己），用 `ModifyDamageAction` 改 `ctx.DamageModifier`；`PendingDamageValue` 可判断"会致死"。`DamageAction` 结算时两侧修饰累加：`max(0, 基础伤害 + 攻击侧 + 受击侧)`；`AutoAttackAction` 已统一走 `DamageAction` 链路 |
| **OnUnitDeath** | 单位死亡（亡语） |
| **OnAnyUnitDeath** | **任意单位死亡后**（区别于亡语：无 instigator 定向，**存活**单位的被动均可响应，死者自身不触发）。`TargetUnit`=死者，`TargetCell`=死亡格子，`SourceUnit`=死者。配合 TargetFilters 筛阵营/类型（如 `[Shape(全体), Team(友方)]` = 友方死亡时） |
| **OnMove** | 移动后（不含攻击/出牌） |
| **OnUnitEnterCell** | **单位进入格子后**（移动/传送/召唤）。`TargetCell`=新格子，`TargetUnit`=进入的单位。**环境被动专用**：仅"目标格子==环境所在格"且**起终点环境变化**的订阅者触发——对面格子环境 ID 与本环境相同（含两端都无环境）即同一环境内移动，不触发；无→有 / 有→无 / 环境A→环境B 才触发。`[Shape(单体)]` 命中进入的单位 |
| **OnUnitLeaveCell** | **单位离开格子后**（移动/传送/死亡/移除）。`TargetCell`=原格子，`TargetUnit`=离开的单位。环境被动专用，同上（跨环境移动先 A 离开后 B 进入） |
| **OnUnitTransformed** | **单位变身后**（变身=换模板 + 清 buff/装备 + 换被动）。`TargetUnit`=变身单位。**无 instigator 定向**：所有存活单位被动可响应（参照 OnAnyUnitDeath），用 TargetFilters / `Target=EventOther` 筛变身者 |
| **OnBuffStackChanged** | **Buff 叠层变化/设置后**（新建 initialStacks、叠层刷新、ModifyBuffAction 增减层；归零移除走 OnBuffRemoved 不触发）。`TargetUnit`=层数变化的单位，instigator=该单位（定向，自己响应）。配合条件 `BuffInfoValue{Unit=Source, BuffID, StackCount} > N` 判断层数 |

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
| PassiveEffects | **手牌被动**（EffectData 数组）：卡牌在手牌期间订阅 EventBus 响应任意事件（RoundStart/OnDrawCard/OnUseCard 等），打出或弃牌时自动退订；复用单位被动同款 EffectData |

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
│  ApplyUnits / ApplyCells / GetShape / GetAreaRange / GetCellShape / IsUnitMatch / GetTeamFilter
│
├── ShapeTargetFilter        // 形状候选源（唯一的形状节点，生成候选）：
│                            //   CustomShape（CellShape 多态类，推荐）或 Shape + AreaRange（旧枚举路径，兼容存量 .tres）
├── PropertyTargetFilter     // 静态属性筛选的中间基类（复用遍历过滤/格子透传）
│   ├── TeamTargetFilter     // 相对阵营 Team（Ally/Enemy 相对来源）
│   ├── UnitTypeTargetFilter // 单位类型 UnitTypes
│   ├── TagTargetFilter      // 标签 Tags（任一匹配）
│   ├── WorldTargetFilter    // 世界观 World（无=不限制）
│   ├── FactionTargetFilter  // 势力 Faction（无=不限制）
│   └── UnitIDTargetFilter   // 单位 ID UnitIDs（任一匹配；排除用 Not 组合）
├── ConditionTargetFilter    // 动态过滤：Conditions（配合值源筛运行时属性，如 HP≤50%Max）
├── ExtremeTargetFilter      // 极值后处理：按值源排序取最高/最低 N 个（数量不足全要）
├── RandomTargetFilter       // 随机后处理：从已筛选目标组随机取 N 个（不重复，数量不足全要）
├── AndTargetFilter          // AND 组合：自动找第一个形状节点生成候选，其余节点全部过滤（顺序无关）
├── OrTargetFilter           // OR 组合：任一子过滤器命中即保留
└── NotTargetFilter          // NOT 组合：全量 − 子过滤器命中集（补集）
```

### 形状体系（CellShape，推荐）

形状从 `ShapeTargetFilter` 枚举 switch 解耦为 **CellShape 多态 Resource 体系**（`Scripts/Data/Shapes/`）：每个形状类自管格子生成（`GetCells(center, ctx)`，含中心格、越界自动过滤），**解析与预览共用同一算法**（`SelectionManager` 预览经 `TargetFilter.GetCellShape()` 穿透取形状实例直接生成，不再复制算法）。

```
CellShape（抽象基类）
│  GetCells(center, ctx) → Cell[]      // 以中心格生成形状内格子（含中心，经 ctx.Map 过滤越界）
│  GetCells(center, ctx, sizeOverride) // 尺寸注入重载：sizeOverride≥0 时主尺寸（Length/AreaRange）取它
│                                      //   （攻击范围场景 = 单位射程联动），否则回退自身参数；不改共享实例
│  GetCategory() → TargetShape         // 类别枚举（UI 预览/校验/文本用）
│  GetAreaRange()                      // 扩散半径（预览/文本用，值源动态取值）
│  Describe(size) / DescribeRange()    // 显示描述（"十字 2"；null 形状只显数字）
│
├── DiamondShape    AreaRange + AreaRangeValueSource           // 菱形扩散
├── SquareShape     AreaRange + AreaRangeValueSource           // 方形扩散
├── CrossShape      Length + LengthValueSource                 // 十字：中心 + 上下左右各 Length（4N+1）
├── XShape          Length + LengthValueSource                 // 叉字：中心 + 四对角各 Length（4N+1）
├── RayShape        Direction + DirectionValueSource,          // 射线（矩形带）：含中心排共 Length+1 排，
│                   Length + LengthValueSource, Width + ...    //   每排宽 2×Width+1（Width=0 → 单格宽）
├── TriangleShape   Direction + DirectionValueSource,          // 三角形（锥形）：第 i 排宽 2i+1（1→3→5），
│                   Length + LengthValueSource                 //   共 (Length+1)² 格
├── RowShape        Length + LengthValueSource                 // 整行：中心所在行左右各 Length（2L+1）
├── ColumnShape     Length + LengthValueSource                 // 整列：中心所在列上下各 Length（2L+1）
├── RingShape       Radius + RadiusValueSource                 // 环形：曼哈顿距离恰为 Radius（不含内部；0=仅中心）
└── AllShape        -                                          // 全地图（随机格等场景用）
```

配置方式：`ShapeTargetFilter.CustomShape` 拖入形状类 Resource（方向/长度/宽度支持固定值 + 值源覆盖，如 `DirectionValue` 动态朝向目标）。`Shape` 枚举路径保留兼容存量 .tres（`CustomShape=null` 时生效）；新配置统一走 `CustomShape`。

### 单位攻击范围形状

`UnitData.AttackShape`（CellShape，null = 默认菱形）——单位攻击范围按形状生成（选中高亮 / AI 索敌 / AutoAttackAction 三处共用 `PathFinder.GetAttackRange`/`GetAttackableTargets` 统一入口），**主尺寸自动联动 `AttackDistance`**（装备/变身/Buff 加射程即时生效）。建议用无方向形状（菱形/方形/十字/叉）；射线/三角形等方向形状待单位朝向系统引入。信息面板/卡牌显示形状描述（如"十字 2"）。

### 语义约定

- **`CardData.TargetFilters` / `EffectData.TargetFilters` 是数组，默认 And 组合**：`[Shape(单体), Team(敌方)]` ≡ `And[Shape, Team]`，无需手动包 And（运行时经 `TargetFilter.CombineAnd` 组合）
- **数组为 null/空 = 无目标**（无目标法术直接打出）；被动效果无 TargetFilters 时用 `Target`（Self/EventOther）
- **形状节点**（ShapeTargetFilter）忽略上游候选自行生成；**过滤节点**（Attribute/Condition/组合）对上游候选过滤
- **扩散中心覆盖**：`ShapeTargetFilter.CenterOverride`（坐标值源，与 `AreaRangeValueSource` 同风格）——配置后 SingleCell/AreaDiamond/AreaSquare 以该坐标为中心，**代替默认的 ctx.TargetCell**（被动路径=单位自身格/环境格/事件格，卡牌路径=点击格）；覆盖坐标无效/格子不存在时返回空结果。典型用法：环境被动以"环境格"为固定中心、单位被动以"来源前方 N 格"为扩散中心（`StepCellValue`）
- **单挂过滤类** = 从全量开始（`[Team(敌方)]` 单独 ≡ 全体敌方）
- 阵营是**相对语义**（Ally/Enemy 相对效果来源阵营）；Neutral 单位不命中敌方过滤
- **随机节点**（RandomTargetFilter）为后处理：无形状（GetShape=None），放筛选链末尾从已筛结果随机取 N 个（Fisher-Yates 不重复抽样；数量不足全要；`ValueSource` 动态数量覆盖 Count；单位/格子目标都支持）
- `GetShape()/GetAreaRange()/GetTeamFilter()` 穿透组合递归，供 UI 预览/校验与高亮图标使用

### 形状（TargetShape 枚举）

| Shape | 说明 |
|---|---|
| None | 无目标（一般直接用 TargetFilters=null 或空数组） |
| SingleUnit / SingleCell | 点选单位/格子 |
| AreaDiamond / AreaSquare | 菱形/方形扩散（半径 = ShapeTargetFilter.AreaRange） |
| Cross | 十字（`CrossShape`） |
| X | 叉字（`XShape`） |
| Ray | 射线（`RayShape`：方向 + 长 + 宽） |
| Triangle | 三角形/锥形（`TriangleShape`：方向 + 长，每排宽 2i+1） |
| Row | 整行（`RowShape`：中心所在行左右各 Length） |
| Column | 整列（`ColumnShape`：中心所在列上下各 Length） |
| Ring | 环形（`RingShape`：曼哈顿距离恰为 Radius，不含内部） |
| All | 全地图 |

### 典型配置

| 目标 | TargetFilters 配置（数组默认 And） |
|---|---|
| 敌方单体 | `[Shape(单体), Team(敌方)]` |
| 友方单体 | `[Shape(单体), Team(友方)]` |
| 菱形 2 格敌方 | `[Shape(菱形,2), Team(敌方)]` |
| 十字 2 格敌方 | `[Shape(CustomShape=CrossShape{Length=2}), Team(敌方)]` |
| 朝目标方向射线 3×1 敌方 | `[Shape(CustomShape=RayShape{Direction=DirectionValue(朝向目标), Length=3, Width=0}), Team(敌方)]` |
| 全体友方 | `[Shape(全体), Team(友方)]` |
| 残血敌方（HP≤50%Max） | `[Shape(全体), Team(敌方), Cond(HP≤50%Max)]` |
| 生命最低的 3 个友方 | `[Shape(全体), Team(友方), Extreme(生命值, 最低, 3)]`（值源+方向+数量；不足全要） |
| 随机 1 个敌方 | `[Shape(全体), Team(敌方), Random(1)]`（随机不重复；不足全要） |
| 随机 2 格放环境 | `[Shape(全体), Random(2)]`（Kind=Cell，随机取 2 格） |
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

**用法：** 在场景根节点加 Node，挂载 TestRunner.cs，**默认不运行**（`RunTestsOnReady=false`，防止测试副作用污染战斗全局状态，如修改全局费用）；需要回归时在 Inspector 勾选 `RunTestsOnReady` 后运行。**482 项用例**覆盖：
- ValueSource 运算（6 种公式 + 嵌套）
- Condition 复合（And/Or/Not + Compare/HasBuff/Random）
- Buff 生命周期（叠层/倒计时/还原/驱散）
- ModifyBuffAction（减层归零/负值 clamp 到 0/永久 Buff 回合忽略）
- ECA 集成（条件满足执行/MaxTriggerCount 限制）
- DamageUnit（正常扣血/过量/击杀）
- MaxStack/Duration 边界值
- 事件系统（OnAnyUnitDeath 任意死亡 / ValueTarget.EventOther 事件另一方读取）
- 变身机制（清 buff/装备、固定 buff 保留、buff 叠层触发变身）

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

### 11.2 义肢 - 每层加成 + Tag 额外加成 + 行动后减层

**BuffData：** Duration=-1, MaxStack=10（层数 = 效果倍率；`FixedEffect` 能力已实现但义肢当前未启用）
```
OnApplyActions = [
  ModifyStatAction(ATK,+1),                                // 每层 ATK+1
  ModifyStatAction(MaxHP,+1),                              // 每层 MaxHP+1
  ModifyStatAction(ATK,+1, RequiredTags=[攻击义肢]),        // Tag 额外：带 Tag 每层再 +1
  ModifyStatAction(MaxHP,+1, RequiredTags=[生命义肢]),
  ModifyStatAction(体力,+1, RequiredTags=[体力义肢]),
  ModifyStatAction(行动点,+1, RequiredTags=[行动义肢]),
  ModifyStatAction(射程,+1, RequiredTags=[距离义肢])
]
PassiveEffects = [EffectData {
  TriggerEvent=OnUnitAct, MaxTriggerCount=1
  Conditions=[And(Not(And(ActionKind=移动, HasTag=耐用义肢)),   // 耐用：移动不消耗
				  Not(And(ActionKind=攻击, HasTag=耐打义肢)))]  // 耐打：攻击不消耗
  Actions=[ModifyBuffAction { BuffID=义肢, StacksDelta=-1, WearMode=true }]  // 磨损模式：本次行动中新增的层不磨损
}]
```

**Tag → 额外加成映射：** `攻击义肢`→攻击力、`生命义肢`→生命上限、`体力义肢`→体力（移动范围）、`行动义肢`→行动点上限、`距离义肢`→攻击范围。单位带哪个 Tag，对应属性每层额外 +1（层数倍率）。

**义肢豁免 Tag：** `耐用义肢`=**移动不消耗**（攻击仍消耗）；`耐打义肢`=**攻击不消耗**（移动仍消耗）。普通单位移动/攻击照常消耗。实现：`BattleManager` 触发 `OnUnitAct` 时把 `Context.ActType`（移动/攻击）传给被动，义肢被动加条件 `非(移动 且 带耐用义肢Tag) 且 非(攻击 且 带耐打义肢Tag)`（`ActionKindCondition` + `HasTagCondition` + And/Not 复合）。注意**出牌不再触发 OnUnitAct**（"行动"仅指移动与攻击）。

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
  TriggerEvent=OnUnitDeath, Target=EventOther
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
Actions=[MoveUnitAction{Mode=Push, Distance=2}]   // 目标（敌方）沿"施法者 → 敌方"方向被推开 2 格
```

**变体：** 按施法者攻击力击退（动态距离）、推离指定坐标（如门口）：

```
Actions=[MoveUnitAction{Mode=Push,
  DistanceValueSource=UnitStatValue(Source, AttackPower),   // 击退 = 自身攻击力格数
  DirectionAnchor=ContextCellValue(Target)}]                // 锚点 = 事件目标格（推离该格）
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

### 11.13 重载章鱼 - 亡语将义肢转移给义肢最少的兵种

**单位（建筑，擢升之手）：** 登场自持 7 层义肢；死亡时把义肢**转移**给全场友方兵种（Squad）中**义肢层数最低**的 1 个。

```
UnitData PassiveEffects=[
  EffectData{ TriggerEvent=OnSpawn, Target=Self
	Actions=[ApplyBuffAction{BuffData=<义肢.tres>, InitialStacks=7}] },   // 登场 7 层
  EffectData{ TriggerEvent=OnUnitDeath
	TargetFilters=[Shape(全体), Team(友方), UnitType(兵种),              // 候选：友方兵种
	  Extreme(Value=BuffInfoValue{Unit=Target, BuffID=义肢},             // 按候选自身层数排序
		Mode=Lowest, Count=1)]                                          // 义肢最少 1 个
	Actions=[ApplyBuffAction{BuffData=<义肢.tres>,
	  ValueSource=BuffInfoValue{Unit=Source, BuffID=义肢}}] }            // 转移与死者相同层数
]
```

- **排序值源 `Unit=Target`**：按**候选目标自身**的义肢层数取最少（若误用 `Source` 会读到死者层数，所有候选同值，"最少"失效）
- **转移层数 `Unit=Source`**（死者）+ `ApplyBuffAction.ValueSource` 动态叠层（设置后覆盖 `InitialStacks`）

### 11.14 手牌被动 - 抽到时再抽 1 张 / 回合开始抽 1 张

**卡牌被动**（`CardData.PassiveEffects`）：卡牌在手牌期间订阅 EventBus，打出/弃牌自动退订。抽到时触发 = `TriggerEvent=OnDrawCard`（只响应自己被抽到，防连锁递归）：

```
CardData PassiveEffects=[EffectData{
  TriggerEvent=OnDrawCard
  Actions=[DrawCardAction{Value=1}]     // 抽到这张牌时再抽 1 张
}]
```

手牌期间每回合触发（如"手牌中：回合开始抽 1 张"）：

```
CardData PassiveEffects=[EffectData{
  TriggerEvent=RoundStart
  Actions=[DrawCardAction{Value=1}]
}]
```

> **OnDrawCard 语义**：手牌被动只响应"自己被抽到"（`SourceCard==自己`）；单位被动（`UnitData.PassiveEffects`）可响应任意抽牌，注意用 `MaxTriggerCount` 防连锁（"每当抽牌再抽牌"会自然连锁）。

### 11.15 致命免伤 - 受到致命伤害时伤害改为 0

受击者被动：`OnBeforeTakeDamage`（受击前，`Source`=自己）判断"本次伤害 ≥ 当前 HP"（会致死）→ 把伤害清零（增量 = -基础伤害）。

```
UnitData PassiveEffects=[EffectData{
  TriggerEvent=OnBeforeTakeDamage, Target=Self
  Conditions=[Compare(Left=PendingDamageValue, Op=GreaterEqual,
	Right=UnitStatValue(Source, CurrentHP=true))]       // 本次伤害 ≥ 自己当前 HP → 会致死
  Actions=[ModifyDamageAction{
	ValueSource=FormulaValue(Mul, PendingDamageValue, ConstantValue(-1))  // 增量 = -基础伤害 → 伤害归零
  }]
}]
```

> 原理：`DamageAction` 触发攻击前/受击前事件时在 `ctx.PendingDamage` 暴露基础伤害（`PendingDamageValue` 值源读取）；受击前事件 `SourceUnit`=受击者（自己），条件读 `Source` 即自己的 HP。`ModifyDamageAction` 的 `ValueSource` 动态增量覆盖 `Delta`，结算时 `max(0, 基础伤害 + 两侧修饰)`。改增量即可做变体：减半 = `FormulaValue(Mul, PendingDamageValue, ConstantValue(-1))` 换成百分比、留 1 血 = 增量 `ConstantValue(1-基础伤害)` 等。

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
| `亡语` | 死亡时（只触发死者自身） | `亡语:菱形,敌方,伤害:3` |
| `任意死亡` | 任意单位死亡时（存活单位被动响应，死者不触发） | `任意死亡:属性:攻击力+1` |
| `生成时` | 登场时 | `生成时:属性:攻击力+1` |
| `回合开始` / `回合结束` | 回合边界 | `回合结束:菱形,友方,治疗:3` |
| `攻击后` / `受伤时` / `击杀后` | 战斗事件 | `击杀后:属性:攻击力+1` |
| `攻击前` | 攻击前（伤害计算前，可修改，攻击者视角） | `攻击前:属性:攻击力-1` |
| `受伤前` | 受击前（伤害计算前，可修改，受击者视角） | `受伤前:属性:攻击力-1` |
| `行动后` / `移动后` | 行动事件 | |
| `进入时` / `离开时` | 格子占用变化事件（环境被动用） | `进入时:伤害:1` |
| `出牌后` | 出牌事件 | `出牌后:属性:攻击力+1` |
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

### 13.5 摄像机（DragCamera2D）

- **丝滑缩放**：滚轮触发 Tween 平滑动画（Cubic/EaseOut，`ZoomAnimationDuration` 默认 0.12s），**鼠标锚点缩放**（zoom-to-cursor，放大跟随鼠标位置）；缩放步进为线性（`ZoomStep` 加减），符合"等距离缩放"约定
- **非线性跟随**：`_Process` 每帧指数平滑逼近（`1-exp(-speed·dt)`，起始快接近慢、帧率无关、无过冲）：
  - **选中聚焦**：选中单位时镜头平滑移过去（订阅 `SelectionManager.SelectionUpdated`）
  - **行动跟随**：玩家/AI 单位移动或攻击时跟随行动单位（订阅 `BattleManager.UnitActed`，优先级高于选中）
  - **单位卡联动**：召唤单位后自动选中新单位（`BattleManager.OnCardPlayActionsDone`），摄像机随之聚焦
  - **拖拽接管**：中键拖拽开始即取消跟随；缩放动画期间暂停跟随
- 参数：`EnableFollow` / `FollowSpeed` / `FollowOnSelect` / `FollowOnAct` 均可在 Inspector 调节

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

---

## 15. 环境系统

环境是覆盖在基础地形之上的地图图层，相当于"**格子的 Buff**"。一个格子同时最多一个环境；施加新环境时旧环境**完整还原后替换**（替换式覆盖）。环境可影响此格子的属性（移动消耗/可站立/可穿越），并通过被动效果影响其上的单位。

### 15.1 EnvironmentData 字段

| 字段 | 说明 |
|---|---|
| EnvironmentID / EnvironmentName / Description | 标识和文本 |
| Duration | -1=永久, 0=当回合 RoundEnd 移除, N=持续 N 回合（当前回合计入） |
| MoveCostDelta | 移动消耗修正（正=更难走，负=更好走）。移除时自动还原 |
| CanStandOverride / CanPassOverride | 三态覆盖：Unchanged（不改，沿用基础地形）/ ForceTrue / ForceFalse。单位占据格子时仍强制不可站立/不可穿越（占据优先） |
| AtlasSourceId / AtlasCoords | 环境图层（TileMapLayer）图集坐标，EnvironmentViewManager 渲染用 |
| OnApplyActions | 施加时执行（ctx.TargetCell=环境格子，ctx.TargetUnit=格子上单位）；可逆动作移除时自动还原 |
| OnExpireActions | 到期/移除时执行（一次性效果） |
| OnRoundEndActions | 每回合结束时执行 |
| PassiveEffects | 持续期间被动效果（复用 EffectData；TargetFilters 中心=环境格子，`[Shape(单体)]` 命中格子上单位） |

### 15.2 环境动作

| 动作 | 字段 | 说明 |
|---|---|---|
| ApplyEnvironmentAction | EnvironmentData | 对目标格子施加环境（目标为格子，TargetKind.Cell）；同格已有环境时先完整还原再替换 |
| RemoveEnvironmentAction | EnvironmentID（留空=任意） | 移除目标格子上指定环境（驱散），属性自动还原 + 取消被动 |
| ModifyCellStatAction | TargetStat, Value/ValueSource | 修改格子属性：MoveCost 数值加减**可逆**（Revert 对称减回）；CanStand/CanPass 布尔覆盖**不可逆**（勿放 OnApplyActions，格子布尔覆盖请用 EnvironmentData 三态字段） |

### 15.3 环境被动

环境的 `PassiveEffects` 订阅 EventBus，触发时**中心格子 = 环境所在格**：
- `[Shape(单体)]`（SingleUnit）→ 命中**格子上当前单位**（如"回合结束对格子上单位造成 1 伤"）
- `[Shape(菱形,N)]` → 以环境格为中心扩散
- 事件如 RoundStart/RoundEnd/OnUnitAct 等均可用；来源（SourceUnit）= 环境的施加者
- **进入/离开事件**（`OnUnitEnterCell`/`OnUnitLeaveCell`）：仅"目标格子==环境所在格"的环境触发（EventBus 自动过滤），`[Shape(单体)]` 命中**进入/离开的单位**——"踩陷阱"类效果直接用 `TriggerEvent=OnUnitEnterCell`。**起终点环境变化才触发**：两端环境 ID 相同（含两端都无环境）→ 同一环境内移动不触发；无→有 / 有→无 / 环境A→环境B → 触发（跨环境移动先 A 离开后 B 进入）。触发范围覆盖移动/传送/召唤（进入）、移动/传送/死亡/移除（离开）。注意被动动作若再次移动/召唤单位会嵌套触发，用 `MaxTriggerCount` 防连锁

### 15.4 环境卡配置

| 字段 | 值 |
|---|---|
| Type | Environment |
| Shape | SingleCell（目标为格子，CardLibrary 启动校验强制） |
| Cost | 费用 |
| Actions | `[ApplyEnvironmentAction]`（EnvironmentData 拖入环境模板） |
| EnvironmentData | 拖入环境模板 |

### 15.5 示例

**火焰之地：** 回合结束对格子上单位造成 1 伤害（永久）
```
EnvironmentData：EnvironmentID=火焰之地, Duration=-1, AtlasCoords=(0,0),
  PassiveEffects=[EffectData{ TriggerEvent=RoundEnd,
	TargetFilters=[Shape(单体)],
	Actions=[DamageAction{Value=1}] }]
环境卡：Type=Environment, Cost=1, TargetFilters=[Shape(单体格子)], Actions=[ApplyEnvironmentAction]
```

**沼泽：** 移动消耗 +2 且不可站立
```
EnvironmentData：EnvironmentID=沼泽, Duration=-1, MoveCostDelta=2, CanStandOverride=ForceFalse
```

### 15.6 与单位占位的协调

格子的 CanStand/CanPass 运行时值由 `EnvironmentManager.RefreshCellProperties()` 统一重算：**基础地形值 → 环境覆盖 → 单位占据强制 false**。单位移走/死亡释放格子时也走此入口，保证环境修正不被占位逻辑覆盖。

### 15.7 地图预置环境（瓦片化）

环境可以直接画在关卡地图上（不用卡牌施加）。环境层 `EnvironmentLayer` 与基础地形层**平级**，使用独立的 `EnvironmentTileSet.tres`，瓦片 custom data 绑定 `EnvironmentData`（机制与 BlockData 绑定地形瓦片相同）。

**流程：**

```
1. 编辑器在 EnvironmentLayer 上画瓦片（每类环境一个瓦片，绑对应 EnvironmentData 资源）
2. 按 F5 导出地图（MapExporter 同时导出地形层 + 环境层 → MapData.EnvironmentPositions/EnvironmentDatas）
3. 进入关卡自动加载：MapManager 加载地形后 → EnvironmentManager.LoadPresetEnvironments
   逐格静默施加（属性修正/被动订阅/渲染全走标准流程，与动态环境同生命周期）
```

**新增环境：** 建 EnvironmentData 资源（Resource/Data/Environments/，如毒沼.tres）→ 在环境图集加一个瓦片并绑定该资源 → 地图上画。占位图集当前只有 0:0 一瓦（绑毒沼），等环境美术素材就绪后替换贴图并按瓦片更新各环境资源的 `AtlasSourceId/AtlasCoords`。

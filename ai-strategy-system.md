# AI 策略系统设计

## 目标

策划可以在编辑器中预设不同种类单位的 AI 行为策略，每种策略由多条条件-动作规则组成，按优先级依次评估，第一条满足条件的规则生效。

---

## 数据层

### 新增 `Scripts/Data/AIStrategy.cs`

```

AIStrategy (Resource)             AIStep (Resource)
  ├─ Name: string                  ├─ Condition: AIConditionType
  └─ Steps: AIStep[]               ├─ Param: int          ← 条件阈值
								   └─ Action: AIActionType
```

### 条件类型 (`AIConditionType`)

| 枚举值 | 含义 | Param 含义 |
|---|---|---|
| `Always` | 无条件执行 | 忽略 |
| `SelfHPLessThan` | 自身 HP 低于阈值 | 百分比（0-100） |
| `SelfHPMoreThan` | 自身 HP 高于阈值 | 百分比（0-100） |
| `NearestEnemyInRange` | 最近敌人在攻击范围内 | 忽略（直接用 Unit.AttackDistance） |
| `APGreaterOrEqual` | AP 点数足够 | 需要的最小 AP |
| `HasAllyLowHP` | 有友方单位 HP 低于阈值 | 百分比（0-100） |

### 动作类型 (`AIActionType`)

| 枚举值 | 行为 |
|---|---|
| `AttackNearest` | 攻击攻击范围内最近的敌人 |
| `MoveToNearestEnemy` | 向最近敌人移动一步 |
| `RetreatFromEnemy` | 远离最近敌人 |
| `Skip` | 什么都不做 |

---

## 存储位置

`UnitData` 新增一个字段：

```csharp
[Export] public AIStrategy AIStrategy { get; set; }
```

策划在编辑器中创建 `.tres` 策略文件，拖到单位模板上即可。

---

## 执行逻辑

修改 `EnemyAI.ProcessUnit()`：

```
1. 取 unit.UnitData.AIStrategy
2. 如果为空 → 默认行为（当前逻辑：攻击最近的→否则向最近玩家移动）
3. 遍历 Steps:
   ├─ 评估 Condition(Param) → 是否满足
   └─ 满足 → 执行 Action → break
4. 全部不满足 → 什么都不做
```

条件评估示例：

```
SelfHPLessThan(30):
  → unit.CurrentHP / unit.MaxHP * 100 < 30

NearestEnemyInRange:
  → PathFinder.GetAttackableTargets(...) 不为空

APGreaterOrEqual(1):
  → unit.ActionPoints >= 1
```

---

## 策划配表示例

### 狂战士（血多冲脸，血少继续冲）

| # | 条件 | 动作 |
|---|---|---|
| 1 | SelfHP > 30% | MoveToNearestEnemy |
| 2 | Always | AttackNearest |

### 弓箭手（保持距离）

| # | 条件 | 动作 |
|---|---|---|
| 1 | NearestEnemyInRange | AttackNearest |
| 2 | Always | MoveToNearestEnemy |

### 箭塔（站桩防守）

| # | 条件 | 动作 |
|---|---|---|
| 1 | NearestEnemyInRange | AttackNearest |
| 2 | Always | Skip |

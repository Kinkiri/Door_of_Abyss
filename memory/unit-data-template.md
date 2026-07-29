---
name: unit-data-template
description: UnitData 单位数据模板 —— 当前实际字段
type: project
---

## UnitData 当前字段（Scripts/Data/UnitData.cs）

| 属性 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `UnitID` | string | "UnknownUnit" | 唯一标识符 |
| `UnitName` | string | "未知单位" | 显示名称 |
| `AttackPower` | int | 1 | 攻击力 |
| `HealthPoints` | int | 2 | 生命值上限 |
| `Stamina` | int | 1 | 体力上限（曼哈顿距离） |
| `AttackDistance` | int | 1 | 攻击范围 |
| `ActionPoints` | int | 1 | 每回合行动次数 |
| `Type` | UnitType | Squad | 单位类型（含 Door） |
| `UnitPrefab` | PackedScene | null | 单位预制体 |
| `Description` | string | "暂无描述" | 描述 |

**运行时拷贝：** Unit 构造函数调 `InitializeFromData()`，从 UnitData 拷贝到运行时字段。
**Team 不由 UnitData 管理：** 由 `UnitManager.SpawnUnit(unitData, gridPos, team)` 的 `team` 参数传入。

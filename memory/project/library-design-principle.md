---
name: library-design-principle
description: 不是所有 Data 类型都需要 Library——只有运行时需通过字符串 ID 查找模板的类型才建库
type: project
---

## Library 设计原则（2025-07-23 确认，2025-07-23 更新）

不是所有 Data 类型都需要对应的 `Library` 类。建库条件是：运行时需要**通过字符串 ID** 从一堆资源中查找某个模板。

| 有库 | 原因 |
|---|---|
| `CardLibrary` | 牌库按 CardID 洗牌、抽牌 |
| `UnitLibrary` | 按 UnitID 查找单位模板 |
| `LevelLibrary` | 后续选关界面按名称查找关卡数据 |

| 无需库 | 原因 |
|---|---|
| `BuffData` | 编辑器直接拖入 ApplyBuffAction；运行时由 BuffManager 管理活跃实例 |
| `EffectData` | 内嵌在 UnitData.PassiveEffects 中，不独立查找 |
| `BlockData` | TileMap 自定义数据直接引用 |
| `MapData` / `WaveData` | Manager 的 Export 字段直接拖入 |

**Why:** 用户讨论后确认，不为所有 Data 类型盲目建库。只有出现"按 ID 找模板"的具体场景时才建。2025-07-23 新增 `LevelLibrary` 用于未来选关功能。

**How to apply:** 新增 Data 类型时，先判断它的使用场景——是编辑器直接引用还是运行时 ID 查找。前者不需要库，后者才需要。

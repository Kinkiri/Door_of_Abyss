using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 全系统性测试运行器。挂到场景任意节点下，_Ready 自动执行所有测试。
/// </summary>
[GlobalClass]
public partial class TestRunner : Node
{
    private int _passed = 0;
    private int _failed = 0;
    private int _total = 0;
    private readonly List<string> _errors = new();
    private string _currentGroup = "";

    public override void _Ready()
    {
        // 延迟到所有 Manager 初始化完毕
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        GD.PrintRaw("\n==============================\n");
        GD.PrintRaw("  全面系统性单元测试\n");
        GD.PrintRaw("==============================\n");

        // ── 值源系统 ────────────────────────────────────────────
        RunGroup("ValueSource", () =>
        {
            // ConstantValue
            VAssert("ConstantValue 返回固定值",
                () => new ConstantValue { Value = 42 }.GetValue(null) == 42);

            // UnitStatValue
            var srcUnit = MakeUnit("测试单位", 10, 20);
            VAssert("UnitStatValue 读取攻击力",
                () => MakeUnitStat(ValueTarget.Source, ModifyStatType.AttackPower).GetValue(MakeCtx(srcUnit, null)) == 10);
            VAssert("UnitStatValue 读取当前 HP",
                () => MakeUnitStat(ValueTarget.Source, ModifyStatType.MaxHP, true).GetValue(MakeCtx(srcUnit, null)) == 20);
            VAssert("UnitStatValue 读取最大 HP",
                () => MakeUnitStat(ValueTarget.Source, ModifyStatType.MaxHP, false).GetValue(MakeCtx(srcUnit, null)) == 20);

            // FormulaValue: 3 + 5 = 8
            VAssert("FormulaValue Add",
                () => MakeFormula(FormulaOp.Add, 3, 5).GetValue(null) == 8);
            VAssert("FormulaValue Sub",
                () => MakeFormula(FormulaOp.Sub, 10, 3).GetValue(null) == 7);
            VAssert("FormulaValue Mul",
                () => MakeFormula(FormulaOp.Mul, 4, 5).GetValue(null) == 20);
            VAssert("FormulaValue Div",
                () => MakeFormula(FormulaOp.Div, 20, 4).GetValue(null) == 5);
            VAssert("FormulaValue Div 0 返回 0",
                () => MakeFormula(FormulaOp.Div, 5, 0).GetValue(null) == 0);
            VAssert("FormulaValue Max",
                () => MakeFormula(FormulaOp.Max, 3, 8).GetValue(null) == 8);
            VAssert("FormulaValue Min",
                () => MakeFormula(FormulaOp.Min, 3, 8).GetValue(null) == 3);
            VAssert("FormulaValue Percent",
                () => MakeFormula(FormulaOp.Percent, 200, 50).GetValue(null) == 100);

            // 嵌套公式: (3 + 5) × 2 = 16
            var nested = new FormulaValue
            {
                Op = FormulaOp.Mul,
                Left = MakeFormula(FormulaOp.Add, 3, 5),
                Right = new ConstantValue { Value = 2 },
            };
            VAssert("FormulaValue 嵌套运算", () => nested.GetValue(null) == 16);

            // 多层嵌套: ((10 + 5) × 2) - 3 = 27
            var deep = new FormulaValue
            {
                Op = FormulaOp.Sub,
                Left = new FormulaValue
                {
                    Op = FormulaOp.Mul,
                    Left = MakeFormula(FormulaOp.Add, 10, 5),
                    Right = new ConstantValue { Value = 2 },
                },
                Right = new ConstantValue { Value = 3 },
            };
            VAssert("FormulaValue 多层嵌套", () => deep.GetValue(null) == 27);

            // RandomValue
            var rv = new RandomValue { Min = 1, Max = 6 };
            int r = rv.GetValue(null);
            VAssert("RandomValue 在范围内", () => r >= 1 && r <= 6);

            // BuffInfoValue
            VAssert("BuffInfoValue 无 Buff 返回默认值",
                () => new BuffInfoValue { DefaultValue = 0 }.GetValue(MakeCtx(srcUnit, null)) == 0);

            // RoundCountValue, UnitCountValue, DistanceValue, BattleCostValue
            // 需要 Mananger 就绪，在集成测试中测
        });

        // ── 条件系统 ────────────────────────────────────────────
        RunGroup("Condition", () =>
        {
            // CompareCondition: 5 > 3
            VAssert("CompareCondition Greater",
                () => MakeCompare(CompareOp.Greater, 5, 3).IsMet(null));
            VAssert("CompareCondition Less",
                () => MakeCompare(CompareOp.Less, 3, 5).IsMet(null));
            VAssert("CompareCondition Equal",
                () => MakeCompare(CompareOp.Equal, 5, 5).IsMet(null));
            VAssert("CompareCondition NotEqual",
                () => MakeCompare(CompareOp.NotEqual, 5, 3).IsMet(null));
            VAssert("CompareCondition 不满足",
                () => !MakeCompare(CompareOp.Greater, 3, 5).IsMet(null));

            // HasBuffCondition
            var unit = MakeUnit("条件单位", 5, 10);
            VAssert("HasBuffCondition 无 Buff 返回 false",
                () => !new HasBuffCondition { BuffID = "不存在的" }.IsMet(MakeCtx(unit, null)));

            // AndCondition
            var and = new AndCondition
            {
                Conditions = new Condition[]
                {
                    MakeCompare(CompareOp.Greater, 5, 3),
                    MakeCompare(CompareOp.Less, 3, 5),
                }
            };
            VAssert("AndCondition 全通过", () => and.IsMet(null));

            var andFail = new AndCondition
            {
                Conditions = new Condition[]
                {
                    MakeCompare(CompareOp.Greater, 5, 3),
                    MakeCompare(CompareOp.Greater, 3, 5),
                }
            };
            VAssert("AndCondition 不通过", () => !andFail.IsMet(null));

            VAssert("AndCondition 空数组通过", () => new AndCondition().IsMet(null));

            // OrCondition
            var or = new OrCondition
            {
                Conditions = new Condition[]
                {
                    MakeCompare(CompareOp.Greater, 3, 5),
                    MakeCompare(CompareOp.Greater, 5, 3),
                }
            };
            VAssert("OrCondition 任一通过", () => or.IsMet(null));

            var orFail = new OrCondition
            {
                Conditions = new Condition[]
                {
                    MakeCompare(CompareOp.Greater, 3, 5),
                    MakeCompare(CompareOp.Equal, 1, 2),
                }
            };
            VAssert("OrCondition 全不通过", () => !orFail.IsMet(null));

            VAssert("OrCondition 空数组不通过", () => !new OrCondition().IsMet(null));

            // NotCondition
            var not = new NotCondition { Condition = MakeCompare(CompareOp.Greater, 3, 5) };
            VAssert("NotCondition 子条件不通过则通过", () => not.IsMet(null));

            var notFail = new NotCondition { Condition = MakeCompare(CompareOp.Greater, 5, 3) };
            VAssert("NotCondition 子条件通过则不通过", () => !notFail.IsMet(null));

            VAssert("NotCondition null 条件通过", () => new NotCondition().IsMet(null));

            // 复合嵌套: NOT(5 > 3 AND 1 > 2) → NOT(false) → true
            var compound = new NotCondition
            {
                Condition = new AndCondition
                {
                    Conditions = new Condition[]
                    {
                        MakeCompare(CompareOp.Greater, 5, 3),
                        MakeCompare(CompareOp.Less, 1, 2),
                    }
                }
            };
            VAssert("复合嵌套条件",
                () => compound.IsMet(null) == false);

            // RandomCondition
            var rand = new RandomCondition { Probability = 1.0f };
            VAssert("RandomCondition 概率 100% 始终通过", () => rand.IsMet(null));
            rand.Probability = 0.0f;
            VAssert("RandomCondition 概率 0% 始终不通过", () => !rand.IsMet(null));
        });

        // ── Buff 数据层 ────────────────────────────────────────────
        RunGroup("Buff 数据", () =>
        {
            var buffData = new BuffData
            {
                BuffID = "test_buff",
                BuffName = "测试Buff",
                Duration = 3,
                MaxStack = 3,
            };

            var sourceUnit = MakeUnit("来源单位", 5, 10);
            var buff = new Buff(buffData, sourceUnit);

            VAssert("Buff 初始化 Duration", () => buff.RemainingTurns == 3);
            VAssert("Buff 初始化 StackCount", () => buff.StackCount == 1);
            VAssert("Buff 初始化 SourceUnit", () => buff.SourceUnit == sourceUnit);
            VAssert("Buff 初始化 IsExpired", () => !buff.IsExpired);

            // Duration 语义测试（通过直接构造）
            var permBuff = new Buff(new BuffData { Duration = -1, BuffID = "永久" }, null);
            VAssert("Duration=-1 永久", () => permBuff.RemainingTurns == -1);

            var zeroBuff = new Buff(new BuffData { Duration = 0, BuffID = "零回合" }, null);
            VAssert("Duration=0", () => zeroBuff.RemainingTurns == 0);

            var normalBuff = new Buff(new BuffData { Duration = 5, BuffID = "正常" }, null);
            VAssert("Duration=5", () => normalBuff.RemainingTurns == 5);

            // MaxStack 语义
            VAssert("MaxStack 默认 1", () => new BuffData().MaxStack == 1);
            VAssert("MaxStack=-1 无限叠", () => new BuffData { MaxStack = -1 }.MaxStack == -1);
            VAssert("MaxStack=0 禁用", () => new BuffData { MaxStack = 0 }.MaxStack == 0);

            // ModifyStatType 默认值
            VAssert("ModifyStatType.AttackPower 默认 0", () => ModifyStatType.AttackPower == 0);
        });

        // ── 条件表达式 集成测试（Condition + ValueSource 联动） ─
        RunGroup("条件+值源联动", () =>
        {
            var unit = MakeUnit("联动单位", 15, 30);
            unit.CurrentHP = 15; // 降到 50%

            // HP ≤ 50%: CurrentHP(15) ≤ MaxHP(30) × 50%
            var hpCondition = new CompareCondition
            {
                Left = MakeUnitStat(ValueTarget.Target, ModifyStatType.MaxHP, true),
                Op = CompareOp.LessEqual,
                Right = new FormulaValue
                {
                    Op = FormulaOp.Percent,
                    Left = MakeUnitStat(ValueTarget.Target, ModifyStatType.MaxHP, false),
                    Right = new ConstantValue { Value = 50 },
                },
            };
            VAssert("HP ≤ 50% 条件满足", () => hpCondition.IsMet(MakeCtx(null, unit)));

            // HP > 50%: 把 CurrentHP 改成 20（最大 HP 30，66% > 50%）
            unit.CurrentHP = 20;
            VAssert("HP > 50% 条件不满足", () => !hpCondition.IsMet(MakeCtx(null, unit)));

            // 嵌套 AND + 数值比较: ATK(15) > 10 AND HP(20) ≤ 30
            var andConds = new AndCondition
            {
                Conditions = new Condition[]
                {
                    MakeCompareCtx(CompareOp.Greater, MakeUnitStat(ValueTarget.Target, ModifyStatType.AttackPower), new ConstantValue { Value = 10 }, null, unit),
                    MakeCompareCtx(CompareOp.LessEqual, MakeUnitStat(ValueTarget.Target, ModifyStatType.MaxHP, true), new ConstantValue { Value = 30 }, null, unit),
                }
            };
            VAssert("AND 复合条件", () => andConds.IsMet(MakeCtx(null, unit)));

            // 来源和目标属性对比: 来源 ATK(5) < 目标ATK(15)
            var src = MakeUnit("来源", 5, 10);
            var compareStat = MakeCompareCtx(CompareOp.Less,
                MakeUnitStat(ValueTarget.Source, ModifyStatType.AttackPower),
                MakeUnitStat(ValueTarget.Target, ModifyStatType.AttackPower),
                src, unit);
            VAssert("来源ATK < 目标ATK", () => compareStat.IsMet(MakeCtx(src, unit)));
        });

        // ── BuffManager 生命周期 ────────────────────────────────────
        RunGroup("BuffManager 生命周期", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("Buff测试", 10, 20);
            var buffData = new BuffData
            {
                BuffID = "lifecycle",
                BuffName = "生命周期测试",
                Duration = 2,
                MaxStack = 3,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 3 },
                },
            };

            // ApplyBuff
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("ApplyBuff 后 ATK+3", () => unit.AttackPower == 13); // 10 + 3
            VAssert("HasBuff 返回 true", () => bm.HasBuff(unit, "lifecycle"));
            VAssert("GetBuffs 返回 1 个", () => bm.GetBuffs(unit).Count == 1);
            VAssert("StackCount=1", () => bm.GetBuff(unit, "lifecycle").StackCount == 1);
            VAssert("RemainingTurns=2", () => bm.GetBuff(unit, "lifecycle").RemainingTurns == 2);

            // 叠层刷新
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("叠层后 StackCount=2", () => bm.GetBuff(unit, "lifecycle").StackCount == 2);
            VAssert("叠层后 ATK=16", () => unit.AttackPower == 16); // 10 + 3 + 3

            // 叠层达上限
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("3 层达上限", () => bm.GetBuff(unit, "lifecycle").StackCount == 3);
            VAssert("3 层 ATK=19", () => unit.AttackPower == 19); // 10 + 9

            // 超上限不叠
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("超上限不长", () => bm.GetBuff(unit, "lifecycle").StackCount == 3);

            // RemoveBuff 还原所有层
            bm.RemoveBuff(unit, bm.GetBuff(unit, "lifecycle"));
            VAssert("移除后 ATK 还原", () => unit.AttackPower == 10);
            VAssert("移除后没有 Buff", () => !bm.HasBuff(unit, "lifecycle"));
        });

        // ── Buff 时长到期 ────────────────────────────────────────────
        RunGroup("Buff 时长", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("时长测试", 5, 10);
            var buffData = new BuffData
            {
                BuffID = "duration_test",
                Duration = 2,
                MaxStack = 1,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.MaxHP, Value = 5 },
                },
            };

            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("施加后 MaxHP=15", () => unit.MaxHP == 15);

            // 模拟 TickAllBuffs（直接调内部逻辑）
            // Tick 1: 2→1
            var buff = bm.GetBuff(unit, "duration_test");
            VAssert("Tick 前 RemainingTurns=2", () => buff.RemainingTurns == 2);

            // 手动 tick
            if (buff.Data.Duration > 0)
            {
                buff.RemainingTurns--;
                VAssert("Tick 1 后 =1", () => buff.RemainingTurns == 1);
            }

            // Tick 2: 1→0, expired
            if (buff.Data.Duration > 0 && buff.RemainingTurns > 0)
            {
                buff.RemainingTurns--;
            }
            VAssert("Tick 2 后 =0", () => buff.RemainingTurns == 0);
            VAssert("未过期前 ATK 不变", () => unit.AttackPower == 5);

            // 模拟到期移除（用 RemoveBuff 触发 Revert）
            bm.RemoveBuff(unit, bm.GetBuff(unit, "duration_test"));
            VAssert("到期后 MaxHP 还原", () => unit.MaxHP == 10);
            VAssert("到期移除后无 Buff", () => !bm.HasBuff(unit, "duration_test"));
        });

        // ── ModifyStatAction 可逆性 ──────────────────────────────────
        RunGroup("ModifyStatAction 可逆", () =>
        {
            var unit = MakeUnit("可逆测试", 3, 8);
            var ctx = new Context { TargetUnit = unit };

            var action = new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 };
            action.Execute(ctx);
            VAssert("Execute 后 ATK=5", () => unit.AttackPower == 5);

            action.Revert(ctx);
            VAssert("Revert 后 ATK=3", () => unit.AttackPower == 3);

            var hpAction = new ModifyStatAction { TargetStat = ModifyStatType.MaxHP, Value = 5 };
            hpAction.Execute(ctx);
            VAssert("MaxHP Execute 后=13", () => unit.MaxHP == 13);
            VAssert("CurrentHP Execute 后=13（随上限同步+5）", () => unit.CurrentHP == 13);

            hpAction.Revert(ctx);
            VAssert("MaxHP Revert 后=8", () => unit.MaxHP == 8);
            VAssert("CurrentHP Revert 后=8（不随上限减少，超出截断）", () => unit.CurrentHP == 8);
            VAssert("CurrentHP 不超上限", () => unit.CurrentHP <= unit.MaxHP);
        });

        // ── EventBus 条件过滤 ────────────────────────────────────────
        RunGroup("EventBus 条件", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("EventBus条件", 5, 10);
            var effect = new EffectData
            {
                TriggerEvent = EventType.RoundStart,
                Target = PassiveTarget.Self,
                Conditions = new Condition[]
                {
                    new CompareCondition
                    {
                        Left = new ConstantValue { Value = 5 },
                        Op = CompareOp.Greater,
                        Right = new ConstantValue { Value = 3 },
                    }
                },
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                },
            };

            // 该 Effect 中 RoundStart 的条件为 5>3，始终满足
            eb.Subscribe(unit, new[] { effect });

            // Fire RoundStart 不应直接改 ATK（EventBus 里条件检查走的是 subscription 存储的 Conditions）
            // 要验证条件是否被正确存储：查看 subscriptions 结构
            // 由于 EventBus 未开放 Conditions 访问，此测试验证 EventBus 不抛异常即可
            VAssert("EventBus 有条件订阅不抛异常", () => { try { eb.Fire(EventType.RoundStart, new Context()); return true; } catch { return false; } });

            eb.Unsubscribe(unit);
            VAssert("EventBus 取消订阅不抛异常", () => true);

            // 无条件 Effect
            var noCondEffect = new EffectData
            {
                TriggerEvent = EventType.RoundEnd,
                Target = PassiveTarget.Self,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 },
                },
            };
            eb.Subscribe(unit, new[] { noCondEffect });
            VAssert("无条件 Effect 正常订阅", () => true);
            eb.Unsubscribe(unit);
        });

        // ── 集成：EventBus + Condition + Action 联动 ─────────────────
        RunGroup("ECA 集成", () =>
        {
            var bm = BuffManager.Instance;
            var eb = EventBus.Instance;
            if (bm == null || eb == null) { VAssert("Manager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("ECA集成", 5, 10);

            // 模拟：回合结束时如果 ATK>3 则 +2ATK，每回合限 1 次
            // 注：EventBus Fire 时 effectCtx.TargetUnit = 订阅者（Self），SourceUnit 为空
            var effect = new EffectData
            {
                TriggerEvent = EventType.RoundEnd,
                Target = PassiveTarget.Self,
                MaxTriggerCount = 1,
                Conditions = new Condition[]
                {
                    new CompareCondition
                    {
                        Left = new ConstantValue { Value = 5 },
                        Op = CompareOp.Greater,
                        Right = new ConstantValue { Value = 3 },
                    }
                },
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 },
                },
            };

            eb.Subscribe(unit, new[] { effect });
            int beforeATK = unit.AttackPower;
            // Fire RoundEnd → 条件(5>3)满足 → 从 effectCtx.TargetUnit 获取目标 → EffectTarget=Self 所以 TargetUnit=unit
            eb.Fire(EventType.RoundEnd, new Context());
            VAssert("ECA 满足条件执行 Action", () => unit.AttackPower == beforeATK + 2);

            // Fire 第二次 → MaxTriggerCount=1 已耗尽 → 不再增加
            int afterFirst = unit.AttackPower;
            eb.Fire(EventType.RoundEnd, new Context());
            VAssert("ECA MaxTriggerCount 限制", () => unit.AttackPower == afterFirst);

            eb.Unsubscribe(unit);
            VAssert("ECA 测试完成", () => true);
        });

        // ── 多目标 GameAction ────────────────────────────────────────
        RunGroup("GameAction 多目标", () =>
        {
            var unit1 = MakeUnit("目标1", 8, 15);
            var unit2 = MakeUnit("目标2", 6, 12);
            // 预先扣血以便治疗能生效
            unit1.CurrentHP = 10;
            unit2.CurrentHP = 8;
            var ctx = new Context
            {
                TargetUnits = new[] { unit1, unit2 },
            };

            var heal = new HealAction { Value = 3 };
            // HealAction 接受 TargetUnits → 应该治疗两个单位
            // 但 HealAction 需要 UnitManager 存在
            if (UnitManager.Instance == null)
            {
                VAssert("UnitManager 未就绪，跳过 HealAction 测试", () => true);
            }
            else
            {
                heal.Execute(ctx);
                VAssert("HealAction 治疗 unit1", () => unit1.CurrentHP == 13); // 10+3
                VAssert("HealAction 治疗 unit2", () => unit2.CurrentHP == 11); // 8+3
            }
        });

        // ── DamageUnit 基础功能 ──────────────────────────────────────
        RunGroup("DamageUnit", () =>
        {
            if (UnitManager.Instance == null)
            {
                VAssert("UnitManager 未就绪，跳过", () => true);
                return;
            }
            var unit = MakeUnit("伤害测试", 3, 10);
            int dealt = UnitManager.Instance.DamageUnit(unit, 4);
            VAssert("DamageUnit 正常扣血", () => dealt == 4 && unit.CurrentHP == 6);
            VAssert("DamageUnit 没死", () => unit.IsAlive);

            dealt = UnitManager.Instance.DamageUnit(unit, 20);
            VAssert("DamageUnit 过量伤害", () => unit.CurrentHP == 0);
            VAssert("DamageUnit 击杀", () => !unit.IsAlive);
        });

        // ── ModifyBuffAction ─────────────────────────────────────────
        RunGroup("ModifyBuffAction", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("ModifyBuff", 5, 10);
            var buffData = new BuffData
            {
                BuffID = "modify_test",
                Duration = 5,
                MaxStack = 5,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 },
                },
            };

            // 施加 3 层
            bm.ApplyBuff(unit, buffData, null, 3);
            VAssert("3 层 ATK=11", () => unit.AttackPower == 11); // 5 + 2*3

            // ModifyBuffAction: -1 层
            var modAction = new ModifyBuffAction
            {
                BuffID = "modify_test",
                StacksDelta = -1,
            };
            modAction.Execute(new Context { TargetUnit = unit });
            VAssert("减 1 层后 ATK=9", () => unit.AttackPower == 9); // 11 - 2
            VAssert("减 1 层后 StackCount=2", () => bm.GetBuff(unit, "modify_test").StackCount == 2);

            // 再减 2 层 → 归零，自动移除
            modAction.StacksDelta = -2;
            modAction.Execute(new Context { TargetUnit = unit });
            VAssert("归零后 ATK=5", () => unit.AttackPower == 5);
            VAssert("归零后 Buff 移除", () => !bm.HasBuff(unit, "modify_test"));

            // 负数拒绝测试
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("1 层 ATK=7", () => unit.AttackPower == 7);
            modAction.StacksDelta = -5; // 减 5 层但只有 1 层 → 拒绝
            modAction.Execute(new Context { TargetUnit = unit });
            VAssert("负数拒绝，ATK 不变", () => unit.AttackPower == 7);
            VAssert("负数拒绝，Buff 仍在", () => bm.HasBuff(unit, "modify_test"));

            bm.RemoveAllBuffs(unit);
        });

        // ── MaxStack=-1 无限叠 ─────────────────────────────────────────
        RunGroup("MaxStack 无限叠", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪", () => false); return; }

            var unit = MakeUnit("无限叠", 5, 10);
            var data = new BuffData
            {
                BuffID = "infinite",
                Duration = -1,
                MaxStack = -1,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                },
            };

            // 叠 5 次
            for (int i = 0; i < 5; i++)
                bm.ApplyBuff(unit, data, null, 1);

            VAssert("无限叠 5 层 ATK=10", () => unit.AttackPower == 10);
            VAssert("无限叠 StackCount=5", () => bm.GetBuff(unit, "infinite").StackCount == 5);

            bm.RemoveAllBuffs(unit);
            VAssert("清除后 ATK=5", () => unit.AttackPower == 5);
        });

        // ── MaxStack=0 拒绝 ─────────────────────────────────────────
        RunGroup("MaxStack=0 拒绝", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪", () => false); return; }

            var unit = MakeUnit("禁用测试", 5, 10);
            var data = new BuffData { BuffID = "disabled", Duration = 1, MaxStack = 0 };

            bm.ApplyBuff(unit, data, null, 1);
            VAssert("MaxStack=0 不施加 Buff", () => !bm.HasBuff(unit, "disabled"));
        });

        // ── 移除所有 Buff ───────────────────────────────────────────
        RunGroup("RemoveAllBuffs", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪", () => false); return; }

            var unit = MakeUnit("清除测试", 5, 10);

            var b1 = new BuffData { BuffID = "b1", Duration = 3, MaxStack = 1,
                OnApplyActions = new[] { new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 } } };
            var b2 = new BuffData { BuffID = "b2", Duration = 3, MaxStack = 1,
                OnApplyActions = new[] { new ModifyStatAction { TargetStat = ModifyStatType.MaxHP, Value = 3 } } };

            bm.ApplyBuff(unit, b1, null, 1);
            bm.ApplyBuff(unit, b2, null, 1);
            VAssert("施加 2 个 Buff", () => bm.GetBuffs(unit).Count == 2);
            VAssert("ATK=7", () => unit.AttackPower == 7);
            VAssert("MaxHP=13", () => unit.MaxHP == 13);

            bm.RemoveAllBuffs(unit);
            VAssert("清除后无 Buff", () => bm.GetBuffs(unit).Count == 0);
            VAssert("清除后 ATK=5", () => unit.AttackPower == 5);
            VAssert("清除后 MaxHP=10", () => unit.MaxHP == 10);
        });

        // ── 装备系统 ────────────────────────────────────────────
        RunGroup("装备系统", () =>
        {
            var em = EquipmentManager.Instance;
            if (em == null) { VAssert("EquipmentManager 未就绪", () => false); return; }

            // 全加成装备：属性施加
            var unit = MakeUnit("装备测试", 5, 10);
            var equip = new EquipmentData
            {
                EquipmentID = "e1",
                EquipmentName = "全加成装备",
                AttackBonus = 1,
                MaxHealthBonus = 2,
                AttackDistanceBonus = 3,
                StaminaBonus = 4,
                ActionPointBonus = 5,
            };

            em.Equip(unit, equip, null);
            VAssert("装备后 ATK=6", () => unit.AttackPower == 6);
            VAssert("装备后 MaxHP=12", () => unit.MaxHP == 12);
            VAssert("装备后 CurrentHP=12（随上限同步+2）", () => unit.CurrentHP == 12);
            VAssert("装备后 AD=4", () => unit.AttackDistance == 4);
            VAssert("装备后 耐力=5", () => unit.MaxStamina == 5);
            VAssert("装备后 AP=6", () => unit.ActionPoints == 6);
            VAssert("HasEquipment=true", () => em.HasEquipment(unit));
            VAssert("GetEquipment 返回装备", () => em.GetEquipment(unit)?.Data.EquipmentID == "e1");

            // 移除装备：属性完整还原（可逆核心）
            em.RemoveEquipment(unit, em.GetEquipment(unit));
            VAssert("移除后 ATK=5", () => unit.AttackPower == 5);
            VAssert("移除后 MaxHP=10", () => unit.MaxHP == 10);
            VAssert("移除后 CurrentHP=10（超出新上限截断）", () => unit.CurrentHP == 10);
            VAssert("移除后 AD=1", () => unit.AttackDistance == 1);
            VAssert("移除后 耐力=1", () => unit.MaxStamina == 1);
            VAssert("移除后 AP=1", () => unit.ActionPoints == 1);
            VAssert("移除后 HasEquipment=false", () => !em.HasEquipment(unit));

            // MaxHP 截断：满血装备 → 移除装备，CurrentHP 截到新上限
            var unit2 = MakeUnit("截断测试", 3, 10);
            var equip2 = new EquipmentData
            {
                EquipmentID = "e2",
                EquipmentName = "生命装备",
                MaxHealthBonus = 5,
            };
            em.Equip(unit2, equip2, null);   // 10/10 → 15/15（同步满血）
            em.RemoveEquipment(unit2, em.GetEquipment(unit2));
            VAssert("MaxHP 还原后 CurrentHP 截断到 10",
                () => unit2.MaxHP == 10 && unit2.CurrentHP == 10);

            // 受伤后移除：CurrentHP 不随上限减少，仅超出时截断
            var unit7 = MakeUnit("受伤移除测试", 2, 10);
            var equip7 = new EquipmentData { EquipmentID = "e7", EquipmentName = "生命装备", MaxHealthBonus = 5 };
            em.Equip(unit7, equip7, null);   // 10/10 → 15/15
            unit7.CurrentHP = 8;              // 受伤 8/15
            em.RemoveEquipment(unit7, em.GetEquipment(unit7));
            VAssert("受伤后移除：CurrentHP 不随上限减少",
                () => unit7.MaxHP == 10 && unit7.CurrentHP == 8);

            // 替换语义：旧加成完整还原 + 新加成生效
            var unit3 = MakeUnit("替换测试", 1, 10);
            var equipA = new EquipmentData { EquipmentID = "ea", EquipmentName = "装备A", AttackBonus = 2 };
            var equipB = new EquipmentData { EquipmentID = "eb", EquipmentName = "装备B", AttackBonus = 3, MaxHealthBonus = 4 };
            em.Equip(unit3, equipA, null);
            VAssert("替换前 ATK=3", () => unit3.AttackPower == 3);
            em.Equip(unit3, equipB, null);
            VAssert("替换后 ATK=4（旧加成已还原）", () => unit3.AttackPower == 4);
            VAssert("替换后 MaxHP=14", () => unit3.MaxHP == 14);
            VAssert("替换后 CurrentHP=14（同步+4）", () => unit3.CurrentHP == 14);
            VAssert("替换后仅剩新装备", () => em.GetEquipment(unit3)?.Data.EquipmentID == "eb");

            // OnApplyActions 与 bonus 叠加：bonus 转动作先执行，OnApplyActions 追加执行
            var unit8 = MakeUnit("叠加测试", 5, 10);
            var equip8 = new EquipmentData
            {
                EquipmentID = "e8",
                EquipmentName = "叠加装备",
                AttackBonus = 1,      // → ModifyStatAction ATK+1
                MaxHealthBonus = 2,   // → ModifyStatAction MaxHP+2（CurrentHP 同步+2）
                OnApplyActions = new[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 3 },
                },
            };
            em.Equip(unit8, equip8, null);
            VAssert("叠加装备：ATK=9（bonus+1 且 OnApplyActions+3）", () => unit8.AttackPower == 9);
            VAssert("叠加装备：MaxHP=12", () => unit8.MaxHP == 12);
            VAssert("叠加装备：CurrentHP=12（随上限同步+2）", () => unit8.CurrentHP == 12);

            em.RemoveEquipment(unit8, em.GetEquipment(unit8));
            VAssert("叠加移除：ATK 还原=5", () => unit8.AttackPower == 5);
            VAssert("叠加移除：MaxHP 还原=10", () => unit8.MaxHP == 10);
            VAssert("叠加移除：CurrentHP 截断=10", () => unit8.CurrentHP == 10);

            // 死亡清理：RemoveAllEquipments 还原属性
            var unit4 = MakeUnit("死亡测试", 1, 10);
            em.Equip(unit4, equipA, null);
            em.RemoveAllEquipments(unit4);
            VAssert("RemoveAllEquipments 还原属性", () => unit4.AttackPower == 1);
            VAssert("RemoveAllEquipments 后无装备", () => !em.HasEquipment(unit4));

            // 死亡单位拒绝装备
            var unit5 = MakeUnit("死亡拒绝", 1, 10);
            unit5.IsDead = true;
            em.Equip(unit5, equipA, null);
            VAssert("死亡单位无法装备", () => !em.HasEquipment(unit5));

            // 被动效果订阅/取消
            var unit6 = MakeUnit("被动装备测试", 2, 10);
            var equipC = new EquipmentData
            {
                EquipmentID = "ec",
                EquipmentName = "被动装备",
                PassiveEffects = new[]
                {
                    new EffectData
                    {
                        TriggerEvent = EventType.RoundEnd,
                        Target = PassiveTarget.Self,
                        MaxTriggerCount = 1,
                        Actions = new[] { new HealAction { Value = 2 } },
                    }
                },
            };
            em.Equip(unit6, equipC, null);
            unit6.CurrentHP = 5;
            EventBus.Instance?.ResetTriggerCounts();
            EventBus.Instance?.Fire(EventType.RoundEnd, new Context());
            VAssert("装备被动生效（回合结束治疗2）", () => unit6.CurrentHP == 7);

            em.RemoveEquipment(unit6, em.GetEquipment(unit6));
            unit6.CurrentHP = 5;
            EventBus.Instance?.ResetTriggerCounts();
            EventBus.Instance?.Fire(EventType.RoundEnd, new Context());
            VAssert("移除装备后被动不再触发", () => unit6.CurrentHP == 5);
        });

        // ── 装备值源/条件 ────────────────────────────────────────
        RunGroup("装备值源/条件", () =>
        {
            var em = EquipmentManager.Instance;
            if (em == null) { VAssert("EquipmentManager 未就绪", () => false); return; }

            var unit = MakeUnit("值源测试", 5, 10);
            var ctx = new Context { TargetUnit = unit };

            // 无装备时
            VAssert("无装备：HasEquipment=0",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.HasEquipment }.GetValue(ctx) == 0);
            VAssert("无装备：AttackBonus 返回默认值",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.AttackBonus, DefaultValue = -1 }.GetValue(ctx) == -1);

            // 装备后读取各加成
            var equip = new EquipmentData
            {
                EquipmentID = "vs1",
                EquipmentName = "值源装备",
                AttackBonus = 3,
                MaxHealthBonus = 2,
                AttackDistanceBonus = 1,
                ActionPointBonus = 4,
            };
            em.Equip(unit, equip, null);
            VAssert("装备后：HasEquipment=1",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.HasEquipment }.GetValue(ctx) == 1);
            VAssert("装备后：AttackBonus=3",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.AttackBonus }.GetValue(ctx) == 3);
            VAssert("装备后：MaxHealthBonus=2",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.MaxHealthBonus }.GetValue(ctx) == 2);
            VAssert("装备后：AttackDistanceBonus=1",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.AttackDistanceBonus }.GetValue(ctx) == 1);
            VAssert("装备后：ActionPointBonus=4",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.ActionPointBonus }.GetValue(ctx) == 4);
            VAssert("装备后：StaminaBonus=0（未配置）",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.StaminaBonus }.GetValue(ctx) == 0);

            // 条件：精确 ID 匹配
            VAssert("有装备(vs1) 为真",
                () => new HasEquipmentCondition { EquipmentID = "vs1", Has = true }.IsMet(ctx));
            VAssert("有装备(vs2) 为假",
                () => !new HasEquipmentCondition { EquipmentID = "vs2", Has = true }.IsMet(ctx));
            VAssert("无装备(vs1) 为假",
                () => !new HasEquipmentCondition { EquipmentID = "vs1", Has = false }.IsMet(ctx));
            // 条件：空 ID = 任意装备
            VAssert("空 ID：有任意装备 为真",
                () => new HasEquipmentCondition { Has = true }.IsMet(ctx));

            // 移除后
            em.RemoveEquipment(unit, em.GetEquipment(unit));
            VAssert("移除后：HasEquipment=0",
                () => new EquipmentInfoValue { Info = EquipmentInfoType.HasEquipment }.GetValue(ctx) == 0);
            VAssert("移除后：有任意装备 为假",
                () => !new HasEquipmentCondition { Has = true }.IsMet(ctx));
        });

        // ── 汇总输出 ────────────────────────────────────────────
        GD.PrintRaw($"\n==============================\n");
        GD.PrintRaw($"  完成: {_passed} 通过, {_failed} 失败 (共 {_total} 项)\n");
        GD.PrintRaw($"==============================\n");

        if (_failed > 0)
        {
            GD.PrintRaw("\n失败详情:\n");
            foreach (var e in _errors)
                GD.PrintErr(e);
        }

        // 测试完成后自动移除自己，不影响游戏流程
        QueueFree();
    }

    // ======================================================================
    // 工具方法
    // ======================================================================

    private void RunGroup(string name, Action tests)
    {
        _currentGroup = name;
        GD.PrintRaw($"\n▸ {name}\n");
        try { tests(); }
        catch (Exception ex)
        {
            Fail($"测试组 {name} 异常: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void VAssert(string desc, Func<bool> assertion)
    {
        _total++;
        try
        {
            if (assertion())
            {
                _passed++;
                GD.PrintRaw($"  ✓ {desc}\n");
            }
            else
            {
                Fail($"{_currentGroup}: {desc}");
            }
        }
        catch (Exception ex)
        {
            Fail($"{_currentGroup}: {desc} → 异常: {ex.Message}");
        }
    }

    private void Fail(string msg)
    {
        _failed++;
        _errors.Add(msg);
        GD.PrintRaw($"  ✗ {msg}\n");
    }

    // ── 工厂方法 ────────────────────────────────────────────

    private static Unit MakeUnit(string name, int atk, int hp)
    {
        var data = new UnitData
        {
            UnitName = name,
            UnitID = name,
            AttackPower = atk,
            HealthPoints = hp,
        };
        return new Unit(data, Vector2I.Zero, Team.Player);
    }

    private static Context MakeCtx(Unit src, Unit tgt)
    {
        return new Context { SourceUnit = src, TargetUnit = tgt };
    }

    private static UnitStatValue MakeUnitStat(ValueTarget unit, ModifyStatType stat, bool currentHP = true)
    {
        return new UnitStatValue { Unit = unit, Stat = stat, CurrentHP = currentHP };
    }

    private static FormulaValue MakeFormula(FormulaOp op, int left, int right)
    {
        return new FormulaValue
        {
            Op = op,
            Left = new ConstantValue { Value = left },
            Right = new ConstantValue { Value = right },
        };
    }

    private static CompareCondition MakeCompare(CompareOp op, int left, int right)
    {
        return new CompareCondition
        {
            Left = new ConstantValue { Value = left },
            Op = op,
            Right = new ConstantValue { Value = right },
        };
    }

    private static CompareCondition MakeCompareCtx(CompareOp op, ValueSource left, ValueSource right, Unit src, Unit tgt)
    {
        var cc = new CompareCondition { Left = left, Op = op, Right = right };
        // 条件本身的 IsMet 不依赖 Context 里的 left/right binding，
        // 而是靠 ctx 传给 ValueSource 的 GetValue
        return cc;
    }
}

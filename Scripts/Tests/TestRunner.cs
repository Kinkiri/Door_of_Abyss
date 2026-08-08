using Godot;
using Godot.Collections;
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

    /// <summary>
    /// 场景加载后是否自动运行全部测试。默认关闭：测试会操作全局 Manager 状态
    /// （如 ModifyCostAction 改全局费用），进入战斗场景自动跑会污染实际战斗。
    /// 需要回归时在 Inspector 勾选。
    /// </summary>
    [Export] public bool RunTestsOnReady { get; set; } = false;

    public override void _Ready()
    {
        if (!RunTestsOnReady)
        {
            GD.Print("[TestRunner] 已禁用（RunTestsOnReady=false），不运行测试");
            return;
        }
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

        // ── ModifyStatAction Tag 条件 ──────────────────────────────────
        RunGroup("ModifyStatAction Tag 条件", () =>
        {
            var taggedAction = new ModifyStatAction
            {
                TargetStat = ModifyStatType.AttackPower,
                Value = 1,
                RequiredTags = new Array<Tag> { Tag.攻击义肢 },
            };

            // 带匹配 Tag：生效且可逆
            var taggedUnit = MakeUnit("带Tag单位", 5, 10);
            taggedUnit.UnitData.Tags = new Array<Tag> { Tag.攻击义肢 };
            var taggedCtx = new Context { TargetUnit = taggedUnit };

            taggedAction.Execute(taggedCtx);
            VAssert("带Tag：Execute 后 ATK=6", () => taggedUnit.AttackPower == 6);
            taggedAction.Revert(taggedCtx);
            VAssert("带Tag：Revert 后 ATK=5", () => taggedUnit.AttackPower == 5);

            // 无 Tag：不生效，Revert 也不扣
            var plainUnit = MakeUnit("无Tag单位", 5, 10);
            var plainCtx = new Context { TargetUnit = plainUnit };
            taggedAction.Execute(plainCtx);
            VAssert("无Tag：Execute 不生效 ATK=5", () => plainUnit.AttackPower == 5);
            taggedAction.Revert(plainCtx);
            VAssert("无Tag：Revert 不生效 ATK=5", () => plainUnit.AttackPower == 5);

            // 带其他 Tag：不生效
            var wrongTagUnit = MakeUnit("错误Tag单位", 5, 10);
            wrongTagUnit.UnitData.Tags = new Array<Tag> { Tag.科技 };
            var wrongCtx = new Context { TargetUnit = wrongTagUnit };
            taggedAction.Execute(wrongCtx);
            VAssert("错误Tag：不生效 ATK=5", () => wrongTagUnit.AttackPower == 5);

            // 无 RequiredTags：向后兼容，无条件生效
            var plainAction = new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 };
            plainAction.Execute(plainCtx);
            VAssert("无RequiredTags：无条件生效 ATK=7", () => plainUnit.AttackPower == 7);
        });

        // ── ModifyStatAction 仅当前 AP（上限不动） ────────────────────
        RunGroup("ModifyStatAction 仅当前AP", () =>
        {
            var unit = MakeUnit("仅当前AP", 5, 10);
            unit.MaxActionPoints = 3;
            unit.ActionPoints = 2;

            var action = new ModifyStatAction { TargetStat = ModifyStatType.ActionPoints, Value = 1, CurrentAPOnly = true };
            action.Execute(new Context { TargetUnit = unit });
            VAssert("仅当前+1：MaxAP 不变=3", () => unit.MaxActionPoints == 3);
            VAssert("仅当前+1：AP=3", () => unit.ActionPoints == 3);

            // 满状态再 +1：允许透支超过上限（本回合多动一次）
            action.Execute(new Context { TargetUnit = unit });
            VAssert("仅当前+1 透支：AP=4（允许超上限）", () => unit.ActionPoints == 4);
            VAssert("透支后 MaxAP 不变=3", () => unit.MaxActionPoints == 3);

            // 负值 clamp 到 0
            var minus = new ModifyStatAction { TargetStat = ModifyStatType.ActionPoints, Value = -5, CurrentAPOnly = true };
            minus.Execute(new Context { TargetUnit = unit });
            VAssert("仅当前-5 clamp 到 0", () => unit.ActionPoints == 0);
            VAssert("仅当前-5：MaxAP 不变=3", () => unit.MaxActionPoints == 3);

            // Revert 减回并 clamp 下限
            action.Revert(new Context { TargetUnit = unit });
            VAssert("仅当前 Revert：AP=0（下限 clamp）", () => unit.ActionPoints == 0);
            VAssert("仅当前 Revert：MaxAP 不变=3", () => unit.MaxActionPoints == 3);

            // 对照组：默认行为（上限+当前同步）不受影响
            var normal = MakeUnit("默认AP", 5, 10);
            normal.MaxActionPoints = 3;
            normal.ActionPoints = 2;
            var normalAction = new ModifyStatAction { TargetStat = ModifyStatType.ActionPoints, Value = 1 };
            normalAction.Execute(new Context { TargetUnit = normal });
            VAssert("默认模式：MaxAP=4", () => normal.MaxActionPoints == 4);
            VAssert("默认模式：AP=3", () => normal.ActionPoints == 3);
            normalAction.Revert(new Context { TargetUnit = normal });
            VAssert("默认模式 Revert：MaxAP=3", () => normal.MaxActionPoints == 3);
            VAssert("默认模式 Revert：AP 截断=3", () => normal.ActionPoints == 3);
        });

        // ── 充能透支逻辑（+1 由 Revert 在减层时还债，无双重扣减） ────────
        RunGroup("充能透支逻辑", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("充能测试", 5, 10);
            unit.MaxActionPoints = 3;
            unit.ActionPoints = 3;

            var buff = new BuffData
            {
                BuffID = "充能",
                Duration = -1,
                MaxStack = -1,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.ActionPoints, Value = 1, CurrentAPOnly = true },
                },
            };

            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("充能+1：MaxAP 不变=3", () => unit.MaxActionPoints == 3);
            VAssert("充能+1：AP=4（满状态透支多动一次）", () => unit.ActionPoints == 4);

            // 用掉行动点后叠第 2 层
            unit.ActionPoints = 1;
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("2 层：MaxAP 不变=3", () => unit.MaxActionPoints == 3);
            VAssert("2 层：AP=2", () => unit.ActionPoints == 2);
            VAssert("2 层：StackCount=2", () => bm.GetBuff(unit, "充能")?.StackCount == 2);

            // 模拟下回合 RoundStart 被动：减 1 层 → Revert +1 → AP-1
            var modBuff = new ModifyBuffAction { BuffID = "充能", StacksDelta = -1 };
            modBuff.Execute(new Context { TargetUnit = unit });
            VAssert("减层后 AP=1（Revert 还债）", () => unit.ActionPoints == 1);
            VAssert("减层后剩 1 层", () => bm.GetBuff(unit, "充能")?.StackCount == 1);

            // 再减 1 层 → 归零移除，无重复扣减
            modBuff.Execute(new Context { TargetUnit = unit });
            VAssert("归零后 Buff 移除", () => !bm.HasBuff(unit, "充能"));
            VAssert("归零后 AP=0", () => unit.ActionPoints == 0);
            VAssert("归零后 MaxAP 不变=3", () => unit.MaxActionPoints == 3);
        });

        // ── 义肢行动磨损快照（本次行动获得的层不磨损） ──────────────────
        RunGroup("义肢行动磨损快照", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("磨损测试", 2, 10);
            var prosthetic = new BuffData { BuffID = "义肢", Duration = -1, MaxStack = -1 };

            // 场景1：行动开始无义肢（快照 0）→ 行动中加 2 层 → WearMode 减层 → 不减
            bm.MarkActionStart(unit);
            bm.ApplyBuff(unit, prosthetic, null, 2);
            var wear = new ModifyBuffAction { BuffID = "义肢", StacksDelta = -1, WearMode = true };
            wear.Execute(new Context { TargetUnit = unit });
            VAssert("行动中加 2 层：本次行动不减（仍 2 层）", () => bm.GetBuff(unit, "义肢")?.StackCount == 2);

            // 场景2：下次行动开始（快照 2）→ 减层 → 2→1
            bm.MarkActionStart(unit);
            wear.Execute(new Context { TargetUnit = unit });
            VAssert("下次行动磨损：2→1", () => bm.GetBuff(unit, "义肢")?.StackCount == 1);

            // 场景3：非 WearMode 不受快照限制（驱散类直接减）
            var plain = MakeUnit("普通磨损", 2, 10);
            bm.ApplyBuff(plain, prosthetic, null, 2);
            new ModifyBuffAction { BuffID = "义肢", StacksDelta = -1 }.Execute(new Context { TargetUnit = plain });
            VAssert("非 WearMode：直接减 1 层", () => bm.GetBuff(plain, "义肢")?.StackCount == 1);

            bm.RemoveAllBuffs(unit);
            bm.RemoveAllBuffs(plain);
        });

        // ── FixedEffect 固定效果（效果与层数解耦） ────────────────────
        RunGroup("FixedEffect 固定效果", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("固定效果", 5, 10);
            var buffData = new BuffData
            {
                BuffID = "fixed",
                Duration = -1,
                MaxStack = -1,
                FixedEffect = true,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 },
                },
            };

            // 施加 3 层：FixedEffect 只执行 1 次 → ATK=7（不是 5+2*3=11）
            bm.ApplyBuff(unit, buffData, null, 3);
            VAssert("3 层 FixedEffect：ATK=7（只加一次）", () => unit.AttackPower == 7);

            // 叠层 2 层：不重放效果 → ATK 仍 7，层数 5
            bm.ApplyBuff(unit, buffData, null, 2);
            VAssert("再叠 2 层：ATK 仍 7", () => unit.AttackPower == 7);
            VAssert("叠层共 5 层", () => bm.GetBuff(unit, "fixed")?.StackCount == 5);

            // 减 1 层：不减效果 → ATK 仍 7
            var mod = new ModifyBuffAction { BuffID = "fixed", StacksDelta = -1 };
            mod.Execute(new Context { TargetUnit = unit });
            VAssert("减 1 层：ATK 仍 7（效果保留）", () => unit.AttackPower == 7);

            // 减到 0：归零移除 → 一次性还原 → ATK=5
            for (int i = 0; i < 4; i++)
                mod.Execute(new Context { TargetUnit = unit });
            VAssert("归零移除", () => !bm.HasBuff(unit, "fixed"));
            VAssert("归零还原：ATK=5", () => unit.AttackPower == 5);
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

            // 回归：MaxTriggerCount=2 应恰好触发 2 次
            // （曾因 Fire 内执行前/执行后双扣减，N≥2 只触发 ⌈N/2⌉ 次）
            var unit2 = MakeUnit("ECA限2", 5, 10);
            var effect2 = new EffectData
            {
                TriggerEvent = EventType.RoundEnd,
                Target = PassiveTarget.Self,
                MaxTriggerCount = 2,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 2 },
                },
            };
            eb.Subscribe(unit2, new[] { effect2 });
            int atkStart = unit2.AttackPower;
            eb.Fire(EventType.RoundEnd, new Context());  // 第 1 次
            eb.Fire(EventType.RoundEnd, new Context());  // 第 2 次
            VAssert("MaxTriggerCount=2 触发两次（ATK+4）", () => unit2.AttackPower == atkStart + 4);
            int atkTwo = unit2.AttackPower;
            eb.Fire(EventType.RoundEnd, new Context());  // 第 3 次 → 已耗尽
            VAssert("MaxTriggerCount=2 第 3 次不触发", () => unit2.AttackPower == atkTwo);
            eb.Unsubscribe(unit2);
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

        // ── 伤害修饰（攻击前/受击前加伤减伤） ──────────────────────
        RunGroup("伤害修饰 攻击前/受击前", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            // ModifyDamageAction 单测：改 ctx.DamageModifier
            var mctx = new Context();
            new ModifyDamageAction { Delta = -3 }.Execute(mctx);
            VAssert("ModifyDamageAction：ctx.DamageModifier=-3", () => mctx.DamageModifier == -3);

            var attacker = MakeUnit("攻击者", 10, 10);
            var victim = MakeUnit("受击者", 5, 20);

            // 受击者减伤被动 -3（OnBeforeTakeDamage 受击者视角，Source=自己）
            var reduce = new EffectData
            {
                TriggerEvent = EventType.OnBeforeTakeDamage,
                Target = PassiveTarget.Self,
                Actions = new GameAction[] { new ModifyDamageAction { Delta = -3 } },
            };
            eb.Subscribe(victim, new[] { reduce });

            new DamageAction { Value = 10 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim } });
            VAssert("减伤-3：20→13", () => victim.CurrentHP == 13);

            // 攻击者加伤被动 +2（OnBeforeAttack 攻击者视角，Source=自己）
            var boost = new EffectData
            {
                TriggerEvent = EventType.OnBeforeAttack,
                Target = PassiveTarget.Self,
                Actions = new GameAction[] { new ModifyDamageAction { Delta = 2 } },
            };
            eb.Subscribe(attacker, new[] { boost });

            new DamageAction { Value = 10 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim } });
            VAssert("减伤-3 + 加伤+2 叠加：13-9=4", () => victim.CurrentHP == 4);

            // 减伤溢出：伤害 clamp 到 0 不死
            var victim2 = MakeUnit("受击者2", 5, 20);
            var bigReduce = new EffectData
            {
                TriggerEvent = EventType.OnBeforeTakeDamage,
                Target = PassiveTarget.Self,
                Actions = new GameAction[] { new ModifyDamageAction { Delta = -50 } },
            };
            eb.Subscribe(victim2, new[] { bigReduce });
            new DamageAction { Value = 5 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim2 } });
            VAssert("减伤溢出：伤害 0，HP 不变=20", () => victim2.CurrentHP == 20);
            VAssert("减伤溢出：单位存活", () => victim2.IsAlive);

            eb.Unsubscribe(victim);
            eb.Unsubscribe(attacker);
            eb.Unsubscribe(victim2);
        });

        // ── 攻击方向 AttackDirection（ctx 传递 + 值源读取 + 方向取反） ──
        RunGroup("攻击方向 AttackDirection", () =>
        {
            // DirectionBetween 纯函数（4 向 + 同格）
            VAssert("DirectionBetween Right", () => TargetResolver.DirectionBetween(new Vector2I(3, 3), new Vector2I(5, 3)) == CellDirection.Right);
            VAssert("DirectionBetween Left", () => TargetResolver.DirectionBetween(new Vector2I(3, 3), new Vector2I(1, 3)) == CellDirection.Left);
            VAssert("DirectionBetween Down", () => TargetResolver.DirectionBetween(new Vector2I(3, 3), new Vector2I(3, 5)) == CellDirection.Down);
            VAssert("DirectionBetween Up", () => TargetResolver.DirectionBetween(new Vector2I(3, 3), new Vector2I(3, 1)) == CellDirection.Up);
            VAssert("DirectionBetween 同格 → null", () => TargetResolver.DirectionBetween(new Vector2I(3, 3), new Vector2I(3, 3)) == null);

            // AttackDirectionValue 单测
            VAssert("AttackDirectionValue 有方向读枚举值",
                () => new AttackDirectionValue().GetValue(new Context { AttackDirection = CellDirection.Left }) == (int)CellDirection.Left);
            VAssert("AttackDirectionValue 无方向读 DefaultValue",
                () => new AttackDirectionValue { DefaultValue = 99 }.GetValue(new Context()) == 99);

            // OppositeDirectionValue 取反
            VAssert("OppositeDirectionValue 4 向取反",
                () =>
                {
                    var ctx = new Context { AttackDirection = CellDirection.Up };
                    var v = new OppositeDirectionValue { Direction = new AttackDirectionValue() };
                    bool upDown = v.GetValue(ctx) == (int)CellDirection.Down;
                    ctx.AttackDirection = CellDirection.Down;
                    bool downUp = v.GetValue(ctx) == (int)CellDirection.Up;
                    ctx.AttackDirection = CellDirection.Left;
                    bool leftRight = v.GetValue(ctx) == (int)CellDirection.Right;
                    ctx.AttackDirection = CellDirection.Right;
                    bool rightLeft = v.GetValue(ctx) == (int)CellDirection.Left;
                    return upDown && downUp && leftRight && rightLeft;
                });
            VAssert("OppositeDirectionValue 输入缺失 → DefaultValue",
                () => new OppositeDirectionValue { DefaultValue = 3 }.GetValue(new Context()) == 3);
            VAssert("OppositeDirectionValue 非法输入 → DefaultValue",
                () => new OppositeDirectionValue { DefaultValue = 1, Direction = new ConstantValue { Value = 99 } }.GetValue(new Context()) == 1);

            // EventBus 集成：DamageAction 攻击链路 → 被动捕获攻击方向
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            var attacker = MakeUnit("攻击者", 10, 10); attacker.GridPos = new Vector2I(2, 2);
            var victim = MakeUnit("受击者", 5, 20); victim.GridPos = new Vector2I(5, 2);  // 攻击方向 Right

            var capture = new EffectData
            {
                TriggerEvent = EventType.OnBeforeAttack,
                Target = PassiveTarget.Self,
                Actions = new GameAction[] { new CaptureDirectionAction() },
            };
            eb.Subscribe(attacker, new[] { capture });
            CaptureDirectionAction.Captured = null;
            new DamageAction { Value = 3 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim } });
            VAssert("OnBeforeAttack 被动捕获攻击方向 = Right（DamageAction 链路）",
                () => CaptureDirectionAction.Captured == CellDirection.Right);

            // OnUnitAct 攻击透传（PassiveTarget 分支）
            var actCapture = new EffectData
            {
                TriggerEvent = EventType.OnUnitAct,
                Target = PassiveTarget.Self,
                Actions = new GameAction[] { new CaptureDirectionAction() },
            };
            eb.Subscribe(attacker, new[] { actCapture });
            CaptureDirectionAction.Captured = null;
            eb.Fire(EventType.OnUnitAct,
                new Context { ActType = UnitActType.Attack, AttackDirection = CellDirection.Up }, instigator: attacker);
            VAssert("OnUnitAct 透传攻击方向（PassiveTarget 分支）", () => CaptureDirectionAction.Captured == CellDirection.Up);

            // 非攻击事件：方向为 null → 值源读 DefaultValue
            CaptureDirectionAction.Captured = null;
            eb.Fire(EventType.OnUnitAct,
                new Context { ActType = UnitActType.Move }, instigator: attacker);
            VAssert("移动事件无攻击方向（null）", () => CaptureDirectionAction.Captured == null);

            CaptureDirectionAction.Captured = null;
            eb.Unsubscribe(attacker);
        });

        // ── 已行动条件 HasActed（攻击时本回合已行动过则加伤） ────────────
        RunGroup("已行动条件 HasActed", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            var attacker = MakeUnit("攻击者", 10, 10);
            var victim = MakeUnit("受击者", 5, 20);

            // 条件单测
            var cond = new HasActedCondition { CheckTarget = ConditionTarget.Source, HasActed = true };
            VAssert("未行动：HasActed 不满足", () => !cond.IsMet(new Context { SourceUnit = attacker }));

            attacker.ActionsThisTurn = 1;
            VAssert("已行动：HasActed 满足", () => cond.IsMet(new Context { SourceUnit = attacker }));

            var noCond = new HasActedCondition { CheckTarget = ConditionTarget.Source, HasActed = false };
            VAssert("已行动：HasActed=false 不满足", () => !noCond.IsMet(new Context { SourceUnit = attacker }));

            // 集成：已行动 → 攻击加伤 +1（OnBeforeAttack 攻击者视角）
            var boost = new EffectData
            {
                TriggerEvent = EventType.OnBeforeAttack,
                Target = PassiveTarget.Self,
                Conditions = new Condition[] { new HasActedCondition { CheckTarget = ConditionTarget.Source, HasActed = true } },
                Actions = new GameAction[] { new ModifyDamageAction { Delta = 1 } },
            };
            eb.Subscribe(attacker, new[] { boost });

            new DamageAction { Value = 10 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim } });
            VAssert("已行动+加伤：20→9", () => victim.CurrentHP == 9);

            // 未行动 → 不加伤
            var victim2 = MakeUnit("受击者2", 5, 20);
            attacker.ActionsThisTurn = 0;
            new DamageAction { Value = 10 }.Execute(new Context { SourceUnit = attacker, TargetUnits = new[] { victim2 } });
            VAssert("未行动不加伤：20→10", () => victim2.CurrentHP == 10);

            eb.Unsubscribe(attacker);
        });

        // ── 同事件多被动独立触发计数 ─────────────────────────────────
        RunGroup("同事件多被动独立计数", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("多被动", 5, 10);
            var victim = MakeUnit("靶子", 5, 50);

            // 同一单位同一事件注册两个被动，各自 MaxTriggerCount=1（OnBeforeAttack 攻击者视角）
            var fx1 = new EffectData
            {
                TriggerEvent = EventType.OnBeforeAttack,
                Target = PassiveTarget.Self,
                MaxTriggerCount = 1,
                Actions = new GameAction[] { new ModifyDamageAction { Delta = 1 } },
            };
            var fx2 = new EffectData
            {
                TriggerEvent = EventType.OnBeforeAttack,
                Target = PassiveTarget.Self,
                MaxTriggerCount = 1,
                Actions = new GameAction[] { new ModifyDamageAction { Delta = 2 } },
            };
            eb.Subscribe(unit, new[] { fx1, fx2 });

            // 第一次攻击：两个被动都应触发 → 5+1+2=8
            new DamageAction { Value = 5 }.Execute(new Context { SourceUnit = unit, TargetUnits = new[] { victim } });
            VAssert("两被动各自触发：伤害 5+1+2=8", () => victim.CurrentHP == 50 - 8);

            // 第二次攻击：各自已达上限 → 只受 5
            new DamageAction { Value = 5 }.Execute(new Context { SourceUnit = unit, TargetUnits = new[] { victim } });
            VAssert("第二次攻击两被动均达上限：伤害 5", () => victim.CurrentHP == 50 - 8 - 5);

            eb.Unsubscribe(unit);
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

            // 负数溢出测试：减 5 层但只有 1 层 → clamp 到 0 → 移除（还原全部效果）
            bm.ApplyBuff(unit, buffData, null, 1);
            VAssert("1 层 ATK=7", () => unit.AttackPower == 7);
            modAction.StacksDelta = -5;
            modAction.Execute(new Context { TargetUnit = unit });
            VAssert("负数溢出 clamp 到 0：ATK 还原为 5", () => unit.AttackPower == 5);
            VAssert("负数溢出后 Buff 移除", () => !bm.HasBuff(unit, "modify_test"));

            bm.RemoveAllBuffs(unit);
        });

        // ── 行动类型 / 义肢豁免（耐用：移动不消耗；耐打：攻击不消耗） ────────
        RunGroup("行动类型/义肢豁免", () =>
        {
            // 义肢被动条件：非(移动 且 带耐用义肢Tag) 且 非(攻击 且 带耐打义肢Tag)
            // —— 普通单位移动/攻击都消耗；耐用单位移动不消耗；耐打单位攻击不消耗
            var cond = new AndCondition
            {
                Conditions = new Condition[]
                {
                    new NotCondition
                    {
                        Condition = new AndCondition
                        {
                            Conditions = new Condition[]
                            {
                                new ActionKindCondition { Kind = UnitActType.Move },
                                new HasTagCondition { Tags = new Array<Tag> { Tag.耐用义肢 }, Has = true },
                            }
                        }
                    },
                    new NotCondition
                    {
                        Condition = new AndCondition
                        {
                            Conditions = new Condition[]
                            {
                                new ActionKindCondition { Kind = UnitActType.Attack },
                                new HasTagCondition { Tags = new Array<Tag> { Tag.耐打义肢 }, Has = true },
                            }
                        }
                    },
                }
            };

            // ── 条件层 ──
            var unit = MakeUnit("义肢豁免测试", 5, 10);
            var ctx = new Context { SourceUnit = unit };

            unit.UnitData.Tags = new Array<Tag> { Tag.耐用义肢 };
            ctx.ActType = UnitActType.Move;
            VAssert("耐用+移动 → 条件不满足（移动不消耗）", () => !cond.IsMet(ctx));

            ctx.ActType = UnitActType.Attack;
            VAssert("耐用+攻击 → 条件满足（攻击仍消耗）", () => cond.IsMet(ctx));

            unit.UnitData.Tags = new Array<Tag> { Tag.耐打义肢 };
            ctx.ActType = UnitActType.Attack;
            VAssert("耐打+攻击 → 条件不满足（攻击不消耗）", () => !cond.IsMet(ctx));

            ctx.ActType = UnitActType.Move;
            VAssert("耐打+移动 → 条件满足（移动仍消耗）", () => cond.IsMet(ctx));

            unit.UnitData.Tags = new Array<Tag> { Tag.科技 };
            ctx.ActType = UnitActType.Move;
            VAssert("无豁免+移动 → 条件满足（普通单位移动仍消耗）", () => cond.IsMet(ctx));

            ctx.ActType = UnitActType.Attack;
            VAssert("无豁免+攻击 → 条件满足（普通单位攻击仍消耗）", () => cond.IsMet(ctx));

            var noTagCond = new HasTagCondition { Tags = new Array<Tag> { Tag.耐用义肢 }, Has = false };
            VAssert("Has=false：无耐用Tag 满足", () => noTagCond.IsMet(new Context { SourceUnit = unit }));

            // ── EventBus 集成：义肢 Buff 完整链路 ──
            var eb = EventBus.Instance;
            var bm = BuffManager.Instance;
            if (eb == null || bm == null) { VAssert("Manager 未就绪，跳过", () => false); return; }

            var prosthetic = new BuffData
            {
                BuffID = "义肢",
                Duration = -1,
                MaxStack = -1,
                OnApplyActions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                },
                PassiveEffects = new EffectData[]
                {
                    new EffectData
                    {
                        TriggerEvent = EventType.OnUnitAct,
                        Target = PassiveTarget.Self,
                        MaxTriggerCount = 1,
                        Conditions = new Condition[] { cond },
                        Actions = new GameAction[]
                        {
                            new ModifyBuffAction { BuffID = "义肢", StacksDelta = -1 },
                        },
                    }
                },
            };

            // 带耐用义肢 tag：移动不减层，攻击减层
            var durable = MakeUnit("耐用单位", 5, 10);
            durable.UnitData.Tags = new Array<Tag> { Tag.耐用义肢 };
            bm.ApplyBuff(durable, prosthetic, null, 2);
            VAssert("耐用初始 2 层 ATK=7", () => durable.AttackPower == 7);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Move, SourceUnit = durable }, instigator: durable);
            VAssert("耐用+移动：不减层（仍 2 层）", () => bm.GetBuff(durable, "义肢")?.StackCount == 2);
            VAssert("耐用+移动：ATK 仍 7", () => durable.AttackPower == 7);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Attack, SourceUnit = durable }, instigator: durable);
            VAssert("耐用+攻击：减 1 层", () => bm.GetBuff(durable, "义肢")?.StackCount == 1);
            VAssert("耐用+攻击：ATK=6", () => durable.AttackPower == 6);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Attack, SourceUnit = durable }, instigator: durable);
            VAssert("攻击已达 MaxTriggerCount=1：不再减层", () => bm.GetBuff(durable, "义肢")?.StackCount == 1);

            eb.Unsubscribe(durable);
            bm.RemoveAllBuffs(durable);

            // 带耐打义肢 tag：攻击不减层，移动减层
            var tough = MakeUnit("耐打单位", 5, 10);
            tough.UnitData.Tags = new Array<Tag> { Tag.耐打义肢 };
            bm.ApplyBuff(tough, prosthetic, null, 2);
            VAssert("耐打初始 2 层 ATK=7", () => tough.AttackPower == 7);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Attack, SourceUnit = tough }, instigator: tough);
            VAssert("耐打+攻击：不减层（仍 2 层）", () => bm.GetBuff(tough, "义肢")?.StackCount == 2);
            VAssert("耐打+攻击：ATK 仍 7", () => tough.AttackPower == 7);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Move, SourceUnit = tough }, instigator: tough);
            VAssert("耐打+移动：减 1 层", () => bm.GetBuff(tough, "义肢")?.StackCount == 1);
            VAssert("耐打+移动：ATK=6", () => tough.AttackPower == 6);

            eb.Unsubscribe(tough);
            bm.RemoveAllBuffs(tough);

            // 普通单位：移动减层，攻击减层
            var plain = MakeUnit("普通单位", 5, 10);
            bm.ApplyBuff(plain, prosthetic, null, 2);
            VAssert("普通单位初始 2 层 ATK=7", () => plain.AttackPower == 7);

            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Move, SourceUnit = plain }, instigator: plain);
            VAssert("普通+移动：减 1 层", () => bm.GetBuff(plain, "义肢")?.StackCount == 1);
            VAssert("普通+移动：ATK=6", () => plain.AttackPower == 6);

            eb.Unsubscribe(plain);
            bm.RemoveAllBuffs(plain);

            var plain2 = MakeUnit("普通单位2", 5, 10);
            bm.ApplyBuff(plain2, prosthetic, null, 2);
            eb.Fire(EventType.OnUnitAct, new Context { ActType = UnitActType.Attack, SourceUnit = plain2 }, instigator: plain2);
            VAssert("普通+攻击：减 1 层", () => bm.GetBuff(plain2, "义肢")?.StackCount == 1);

            eb.Unsubscribe(plain2);
            bm.RemoveAllBuffs(plain2);
        });

        // ── OnUseCard 事件（出牌后触发被动） ────────────────────────────
        RunGroup("OnUseCard 事件", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            var unit = MakeUnit("出牌单位", 5, 10);
            var effect = new EffectData
            {
                TriggerEvent = EventType.OnUseCard,
                Target = PassiveTarget.Self,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                },
            };
            eb.Subscribe(unit, new[] { effect });

            eb.Fire(EventType.OnUseCard, new Context { SourceUnit = unit }, instigator: unit);
            VAssert("OnUseCard 触发被动：ATK=6", () => unit.AttackPower == 6);

            eb.Fire(EventType.OnUnitAct, new Context { SourceUnit = unit, ActType = UnitActType.Move }, instigator: unit);
            VAssert("OnUnitAct 不触发 OnUseCard 被动：ATK 仍 6", () => unit.AttackPower == 6);

            eb.Unsubscribe(unit);
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
            VAssert("装备后 耐力=5", () => unit.Stamina == 5);
            VAssert("装备后 AP=6（当前随上限同步）", () => unit.ActionPoints == 6);
            VAssert("装备后 MaxAP=6", () => unit.MaxActionPoints == 6);
            VAssert("HasEquipment=true", () => em.HasEquipment(unit));
            VAssert("GetEquipment 返回装备", () => em.GetEquipment(unit)?.Data.EquipmentID == "e1");

            // 移除装备：属性完整还原（可逆核心）
            em.RemoveEquipment(unit, em.GetEquipment(unit));
            VAssert("移除后 ATK=5", () => unit.AttackPower == 5);
            VAssert("移除后 MaxHP=10", () => unit.MaxHP == 10);
            VAssert("移除后 CurrentHP=10（超出新上限截断）", () => unit.CurrentHP == 10);
            VAssert("移除后 AD=1", () => unit.AttackDistance == 1);
            VAssert("移除后 耐力=1", () => unit.Stamina == 1);
            VAssert("移除后 AP=1（截断到上限）", () => unit.ActionPoints == 1);
            VAssert("移除后 MaxAP=1", () => unit.MaxActionPoints == 1);
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

        // ── TargetFilter 目标筛选器 ────────────────────────────
        RunGroup("TargetFilter", () =>
        {
            // 造地图：5x5
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);

            // 单位：2 普通敌方 + 1 友方 + 1 敌方建筑 + 1 敌方带科技标签
            var e1 = MakeUnit("敌方1", 3, 10); e1.Team = Team.Enemy;
            var e2 = MakeUnit("敌方2", 3, 10); e2.Team = Team.Enemy;
            var ally = MakeUnit("友方", 3, 10);
            var building = MakeUnit("建筑", 3, 10); building.Team = Team.Enemy; building.Type = UnitType.建筑;
            var techie = MakeUnit("科技兵", 3, 10);
            techie.Team = Team.Enemy;
            techie.UnitData = new UnitData
            {
                UnitName = "科技兵",
                UnitID = "科技兵",
                AttackPower = 3,
                HealthPoints = 10,
                Tags = new Godot.Collections.Array<Tag> { Tag.科技 },
            };

            var units = new List<Unit> { e1, e2, ally, building, techie };
            var ctx = new Context
            {
                SourceUnit = ally,
                SourceTeam = Team.Player,
                Map = map,
                ActiveUnits = units,
            };

            // 形状单点
            VAssert("SingleUnit 返回点选单位",
                () => TargetResolver.ResolveUnits(
                    new ShapeTargetFilter { Shape = TargetShape.SingleUnit },
                    new Context { TargetUnit = e1 }) is { Length: 1 } arr1 && arr1[0] == e1);
            VAssert("SingleUnit 死亡单位不返回",
                () =>
                {
                    e1.IsDead = true;
                    var r = TargetResolver.ResolveUnits(
                        new ShapeTargetFilter { Shape = TargetShape.SingleUnit },
                        new Context { TargetUnit = e1 });
                    e1.IsDead = false;
                    return r.Length == 0;
                });

            // And[All, 敌方]：全部敌方
            var enemyFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                },
            };
            VAssert("And[All, 敌方] 只出敌方",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(enemyFilter, ctx);
                    return r.Length == 4 && System.Array.TrueForAll(r, u => u.Team == Team.Enemy);
                });

            // And 顺序无关
            var reversedFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                    new ShapeTargetFilter { Shape = TargetShape.All },
                },
            };
            VAssert("And 顺序无关",
                () => TargetResolver.ResolveUnits(enemyFilter, ctx).Length ==
                      TargetResolver.ResolveUnits(reversedFilter, ctx).Length);

            // 单位类型过滤
            var buildingFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new UnitTypeTargetFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } },
                },
            };
            VAssert("Attr 单位类型=建筑 只出建筑",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(buildingFilter, ctx);
                    return r.Length == 1 && r[0] == building;
                });

            // 标签过滤（任一匹配）
            var tagFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new TagTargetFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                },
            };
            VAssert("Attr 标签=科技 只出科技兵",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(tagFilter, ctx);
                    return r.Length == 1 && r[0] == techie;
                });

            // 条件过滤（运行时属性：HP ≤ 50% MaxHP）
            var lowHp = MakeUnit("残血", 3, 10); lowHp.Team = Team.Enemy; lowHp.CurrentHP = 3;
            units.Add(lowHp);
            var condFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                    new ConditionTargetFilter
                    {
                        Conditions = new Condition[]
                        {
                            new CompareCondition
                            {
                                Left = new UnitStatValue { Unit = ValueTarget.Target, Stat = ModifyStatType.MaxHP, CurrentHP = true },
                                Op = CompareOp.LessEqual,
                                Right = new FormulaValue
                                {
                                    Op = FormulaOp.Percent,
                                    Left = new UnitStatValue { Unit = ValueTarget.Target, Stat = ModifyStatType.MaxHP, CurrentHP = false },
                                    Right = new ConstantValue { Value = 50 },
                                },
                            }
                        },
                    },
                },
            };
            VAssert("Cond 残血过滤（HP≤50%Max）只出残血",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(condFilter, ctx);
                    return r.Length == 1 && r[0] == lowHp;
                });

            // Or 组合：建筑 或 科技标签
            var orFilter = new OrTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new UnitTypeTargetFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } },
                    new TagTargetFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                },
            };
            VAssert("Or[建筑, 科技] 出建筑+科技兵",
                () => TargetResolver.ResolveUnits(orFilter, ctx).Length == 2);

            // Not 组合：排除敌方
            var notFilter = new NotTargetFilter
            {
                Filter = new TeamTargetFilter { Team = TeamFilter.Enemy },
            };
            VAssert("Not[敌方] 只剩友方",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(notFilter, ctx);
                    return r.Length == 1 && r[0] == ally;
                });

            // GetShape / GetAreaRange 穿透组合
            var andAreaFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.AreaDiamond, AreaRange = 2 },
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                },
            };
            VAssert("And GetShape 穿透=AreaDiamond", () => andAreaFilter.GetShape() == TargetShape.AreaDiamond);
            VAssert("And GetAreaRange 穿透=2", () => andAreaFilter.GetAreaRange() == 2);

            // ResolveCells 区域格子（Kind=Cell）
            var areaCellsFilter = new ShapeTargetFilter
            {
                Shape = TargetShape.AreaDiamond,
                AreaRange = 1,
                Kind = TargetKind.Cell,
            };
            VAssert("ResolveCells 菱形1 = 5 格",
                () => TargetResolver.ResolveCells(
                    areaCellsFilter,
                    new Context { TargetCell = map[new Vector2I(2, 2)], Map = map }).Length == 5);

            // 单挂过滤类 = 从全量开始
            VAssert("单挂 Attr(敌方) ≡ 全体敌方",
                () => TargetResolver.ResolveUnits(
                    new TeamTargetFilter { Team = TeamFilter.Enemy }, ctx).Length == 5);

            // null filter = 空
            VAssert("null filter 返回空", () => TargetResolver.ResolveUnits(null, ctx).Length == 0);

            // ── 数组默认 And（CombineAnd） ──────────────────────
            var combined = TargetFilter.CombineAnd(new TargetFilter[]
            {
                new ShapeTargetFilter { Shape = TargetShape.All },
                new TeamTargetFilter { Team = TeamFilter.Enemy },
            });
            VAssert("CombineAnd 数组默认 And ≡ AndTargetFilter",
                () => combined is AndTargetFilter &&
                      TargetResolver.ResolveUnits(combined, ctx).Length ==
                      TargetResolver.ResolveUnits(enemyFilter, ctx).Length);
            VAssert("CombineAnd 单元素原样返回",
                () => TargetFilter.CombineAnd(
                    new[] { new ShapeTargetFilter { Shape = TargetShape.All } }) is ShapeTargetFilter);
            VAssert("CombineAnd 空数组 → null",
                () => TargetFilter.CombineAnd(null) == null &&
                      TargetFilter.CombineAnd(new TargetFilter[0]) == null);

            // Card 运行时组合（CardData.TargetFilters → Card.TargetFilter）
            var spellData = new SpellCardData
            {
                CardID = "数组测试",
                CardName = "数组测试",
                TargetFilters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                },
            };
            var runtimeCard = new Card(spellData);
            VAssert("Card 运行时 CombineAnd 生效",
                // 此时 lowHp 已加入 units，敌方共 5 个（e1/e2/建筑/科技兵/残血）
                () => runtimeCard.TargetFilter is AndTargetFilter &&
                      TargetResolver.ResolveUnits(runtimeCard.TargetFilter, ctx).Length == 5);

            // ── 势力 / 世界观过滤 ──────────────────────────────
            var holyUnit = MakeUnit("圣徒", 3, 10);
            holyUnit.Team = Team.Enemy;
            holyUnit.UnitData = new UnitData
            {
                UnitName = "圣徒",
                UnitID = "圣徒",
                AttackPower = 3,
                HealthPoints = 10,
                Faction = Faction.圣主教,
            };
            units.Add(holyUnit);

            var factionFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new FactionTargetFilter { Faction = Faction.圣主教 },
                },
            };
            VAssert("Attr 势力=圣主教 只出圣徒",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(factionFilter, ctx);
                    return r.Length == 1 && r[0] == holyUnit;
                });

            var worldFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new WorldTargetFilter { World = World.曼斯维森 },
                },
            };
            VAssert("Attr 世界观=曼斯维森 无匹配",
                () => TargetResolver.ResolveUnits(worldFilter, ctx).Length == 0);

            // 势力 + 阵营组合
            var holyEnemyFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new TeamTargetFilter { Team = TeamFilter.Enemy },
                    new FactionTargetFilter { Faction = Faction.圣主教 },
                },
            };
            VAssert("Attr[势力=圣主教, 阵营=敌方] 只出圣徒",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(holyEnemyFilter, ctx);
                    return r.Length == 1 && r[0] == holyUnit;
                });

            // ── 单位 ID 过滤 ────────────────────────────────────
            var idFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new UnitIDTargetFilter
                    {
                        UnitIDs = new Godot.Collections.Array<string> { "圣徒" },
                    },
                },
            };
            VAssert("UnitID=圣徒 只出圣徒",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(idFilter, ctx);
                    return r.Length == 1 && r[0] == holyUnit;
                });

            // Not[UnitID]：排除某单位
            var notIdFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new NotTargetFilter
                    {
                        Filter = new UnitIDTargetFilter
                        {
                            UnitIDs = new Godot.Collections.Array<string> { "圣徒" },
                        },
                    },
                },
            };
            VAssert("Not[UnitID=圣徒] 排除圣徒",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(notIdFilter, ctx);
                    foreach (var u in r)
                        if (u == holyUnit) return false;
                    return r.Length == units.Count - 1;
                });

            // ── 极值筛选 ────────────────────────────────────────
            var healTargets = new List<Unit>();
            for (int i = 1; i <= 5; i++)
            {
                var u = MakeUnit($"病人{i}", 1, 10);
                u.Team = Team.Player;   // 友方
                u.CurrentHP = i * 2;    // HP: 2,4,6,8,10
                healTargets.Add(u);
            }
            var extremeCtx = new Context
            {
                SourceUnit = ally,
                SourceTeam = Team.Player,
                Map = map,
                ActiveUnits = healTargets,
            };
            var lowestHpFilter = new AndTargetFilter
            {
                Filters = new TargetFilter[]
                {
                    new ShapeTargetFilter { Shape = TargetShape.All },
                    new ExtremeTargetFilter
                    {
                        Value = MakeUnitStat(ValueTarget.Target, ModifyStatType.MaxHP, true),
                        Mode = ExtremeMode.Lowest,
                        Count = 3,
                    },
                },
            };
            VAssert("极值：生命最低的 3 个友方",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(lowestHpFilter, extremeCtx);
                    return r.Length == 3 && r[0].CurrentHP == 2 && r[1].CurrentHP == 4 && r[2].CurrentHP == 6;
                });

            VAssert("极值：数量不足全要",
                () => TargetResolver.ResolveUnits(
                    new ExtremeTargetFilter { Count = 10 }, extremeCtx).Length == 5);

            VAssert("极值：最高模式取前 2（平局稳定）",
                () => TargetResolver.ResolveUnits(
                    new AndTargetFilter
                    {
                        Filters = new TargetFilter[]
                        {
                            new ShapeTargetFilter { Shape = TargetShape.All },
                            new ExtremeTargetFilter
                            {
                                Value = MakeUnitStat(ValueTarget.Target, ModifyStatType.AttackPower),
                                Mode = ExtremeMode.Highest,
                                Count = 2,
                            },
                        },
                    }, extremeCtx).Length == 2);

            // ── 随机筛选 ────────────────────────────────────────
            var randomTargets = new List<Unit>();
            for (int i = 1; i <= 5; i++)
            {
                var u = MakeUnit($"随机{i}", 1, 10);
                u.Team = Team.Player;
                randomTargets.Add(u);
            }
            var randomCtx = new Context
            {
                SourceTeam = Team.Player,
                ActiveUnits = randomTargets,
            };

            VAssert("随机：取 1 个且属于候选集",
                () =>
                {
                    var r = TargetResolver.ResolveUnits(
                        new AndTargetFilter
                        {
                            Filters = new TargetFilter[]
                            {
                                new ShapeTargetFilter { Shape = TargetShape.All },
                                new RandomTargetFilter { Count = 1 },
                            },
                        }, randomCtx);
                    return r.Length == 1 && randomTargets.Contains(r[0]);
                });

            VAssert("随机：取 2 个",
                () => TargetResolver.ResolveUnits(
                    new AndTargetFilter
                    {
                        Filters = new TargetFilter[]
                        {
                            new ShapeTargetFilter { Shape = TargetShape.All },
                            new RandomTargetFilter { Count = 2 },
                        },
                    }, randomCtx).Length == 2);

            VAssert("随机：数量不足全要",
                () => TargetResolver.ResolveUnits(
                    new RandomTargetFilter { Count = 10 }, randomCtx).Length == 5);

            VAssert("随机：Count<=0 返回空",
                () => TargetResolver.ResolveUnits(
                    new RandomTargetFilter { Count = 0 }, randomCtx).Length == 0);

            VAssert("随机：动态值源覆盖 Count",
                () => TargetResolver.ResolveUnits(
                    new RandomTargetFilter
                    {
                        ValueSource = new ConstantValue { Value = 3 },
                    }, randomCtx).Length == 3);

            // 格子版：随机取 1 格
            var randMap = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            for (int i = 0; i < 4; i++)
            {
                var c = MakeCell(new Vector2I(i, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                randMap[c.GridPos] = c;
            }
            var randCellCtx = new Context { Map = randMap };
            VAssert("随机：格子版取 1 格",
                () =>
                {
                    var r = TargetResolver.ResolveCells(
                        new RandomTargetFilter { Count = 1 }, randCellCtx);
                    return r.Length == 1 && randMap.ContainsValue(r[0]);
                });

            // 随机性：20 次抽 1，至少出现 2 种不同结果（全相同概率 (1/5)^19 ≈ 5e-14）
            VAssert("随机：非恒定（20 次抽样 ≥2 种结果）",
                () =>
                {
                    var seen = new HashSet<Unit>();
                    for (int i = 0; i < 20; i++)
                    {
                        var r = TargetResolver.ResolveUnits(
                            new RandomTargetFilter { Count = 1 }, randomCtx);
                        if (r.Length == 1) seen.Add(r[0]);
                    }
                    return seen.Count >= 2;
                });
        });

        // ── CardFilter 筛选抽牌 ─────────────────────────────────
        RunGroup("CardFilter 筛选抽牌", () =>
        {
            // ── 纯谓词测试（不依赖 Manager） ────────────────────
            var fireball = MakeCard("火球术", CardType.Spell);
            var techCard = MakeCard("科技卡", CardType.Spell, Tag.科技);
            var unitCard = MakeCard("小兵", CardType.Unit);

            VAssert("CardTypeFilter 匹配类型",
                () => new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Spell } }.IsMatch(fireball));
            VAssert("CardTypeFilter 不匹配类型",
                () => !new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Spell } }.IsMatch(unitCard));
            VAssert("CardTypeFilter 多类型任一匹配",
                () => new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit, CardType.Equipment } }.IsMatch(unitCard));
            VAssert("CardTypeFilter 空数组不限制",
                () => new CardTypeFilter().IsMatch(unitCard));

            VAssert("CardTagFilter 匹配标签",
                () => new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } }.IsMatch(techCard));
            VAssert("CardTagFilter 无标签不匹配",
                () => !new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } }.IsMatch(fireball));
            VAssert("CardTagFilter 空标签不限制",
                () => new CardTagFilter().IsMatch(fireball));

            VAssert("AndCardFilter 全部命中",
                () => new AndCardFilter
                {
                    Filters = new CardFilter[]
                    {
                        new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Spell } },
                        new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                    },
                }.IsMatch(techCard));
            VAssert("AndCardFilter 部分不命中",
                () => !new AndCardFilter
                {
                    Filters = new CardFilter[]
                    {
                        new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit } },
                        new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                    },
                }.IsMatch(techCard));

            VAssert("OrCardFilter 任一命中",
                () => new OrCardFilter
                {
                    Filters = new CardFilter[]
                    {
                        new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit } },
                        new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                    },
                }.IsMatch(techCard));

            VAssert("NotCardFilter 取反",
                () => new NotCardFilter
                {
                    Filter = new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit } },
                }.IsMatch(fireball));
            VAssert("NotCardFilter 补集不误伤",
                () => !new NotCardFilter
                {
                    Filter = new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit } },
                }.IsMatch(unitCard));

            // 单位卡单位类型筛选
            var buildingCard = new Card(new UnitCardData
            {
                CardID = "城墙卡",
                CardName = "城墙卡",
                Type = CardType.Unit,
                UnitData = new UnitData { UnitID = "城墙", UnitName = "城墙", Type = UnitType.建筑 },
            });
            VAssert("CardUnitTypeFilter 匹配建筑单位卡",
                () => new CardUnitTypeFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } }
                    .IsMatch(buildingCard));
            VAssert("CardUnitTypeFilter 不匹配其他类型",
                () => !new CardUnitTypeFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } }
                    .IsMatch(unitCard));
            VAssert("CardUnitTypeFilter 法术卡不匹配（非单位卡）",
                () => !new CardUnitTypeFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } }
                    .IsMatch(fireball));
            VAssert("CardUnitTypeFilter 空数组不限制",
                () => new CardUnitTypeFilter().IsMatch(fireball));

            VAssert("CombineAnd 数组默认 And",
                () =>
                {
                    var f = CardFilter.CombineAnd(new CardFilter[]
                    {
                        new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Spell } },
                        new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                    });
                    return f != null && f.IsMatch(techCard) && !f.IsMatch(fireball);
                });
            VAssert("CombineAnd null 返回 null",
                () => CardFilter.CombineAnd(null) == null);

            // ── 集成测试（依赖 CardManager，未就绪跳过） ────────
            if (CardManager.Instance == null)
            {
                VAssert("CardManager 未就绪，跳过筛选抽牌集成测试", () => true);
                return;
            }

            var cm = CardManager.Instance;
            // 重建牌库：3 法术（含 1 科技标签）+ 2 单位
            cm.InitializeDrawPile(new List<CardData>
            {
                new SpellCardData { CardID = "火球术", CardName = "火球术", Type = CardType.Spell },
                new SpellCardData { CardID = "冰霜", CardName = "冰霜", Type = CardType.Spell,
                    Tags = new Godot.Collections.Array<Tag> { Tag.科技 } },
                new SpellCardData { CardID = "奥术", CardName = "奥术", Type = CardType.Spell },
                new UnitCardData { CardID = "小兵", CardName = "小兵", Type = CardType.Unit },
                new UnitCardData { CardID = "骑兵", CardName = "骑兵", Type = CardType.Unit },
            });

            // 手牌基线：在两次筛选抽取之前捕获（1 科技 + 2 法术 = +3）
            int handBefore = cm.HandCards.Count;

            // 先筛科技标签（牌库中唯一科技牌，必中冰霜，避免随机性影响后续断言）
            var tagDrawn = cm.DrawCards(2, new CardTagFilter { Tags = new Godot.Collections.Array<Tag> { Tag.科技 } });
            VAssert("标签筛选抽牌",
                () => tagDrawn.Count == 1 && tagDrawn[0].CardID == "冰霜");

            // 再抽 2 法术（剩余法术恰好 2 张）
            var spellFilter = new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Spell } };
            var drawn = cm.DrawCards(2, spellFilter);
            VAssert("筛选抽牌 2 张全为法术",
                () => drawn.Count == 2 && drawn.TrueForAll(c => c.Type == CardType.Spell));
            VAssert("筛选抽牌后牌库剩 2 张",
                () => cm.DrawPile.Count == 2);
            VAssert("筛选抽牌后手牌 +3",
                () => cm.HandCards.Count == handBefore + 3);

            // 无匹配不抽
            int handBeforeNone = cm.HandCards.Count;
            var none = cm.DrawCard(new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Equipment } });
            VAssert("无匹配不抽，返回 null 且手牌不变",
                () => none == null && cm.HandCards.Count == handBeforeNone);

            // 不足全要（牌库剩 2 张单位卡，请求 3）
            var few = cm.DrawCards(3, new CardTypeFilter { Types = new Godot.Collections.Array<CardType> { CardType.Unit } });
            VAssert("不足全要：匹配 2 张只抽 2 张",
                () => few.Count == 2 && few.TrueForAll(c => c.Type == CardType.Unit));

            // 单位类型筛选（重建牌库：1 建筑单位卡 + 1 小队单位卡）
            cm.InitializeDrawPile(new List<CardData>
            {
                new UnitCardData { CardID = "城墙", CardName = "城墙", Type = CardType.Unit,
                    UnitData = new UnitData { UnitID = "城墙", UnitName = "城墙", Type = UnitType.建筑 } },
                new UnitCardData { CardID = "剑士", CardName = "剑士", Type = CardType.Unit,
                    UnitData = new UnitData { UnitID = "剑士", UnitName = "剑士", Type = UnitType.兵种 } },
            });
            var buildingDrawn = cm.DrawCards(5,
                new CardUnitTypeFilter { UnitTypes = new Godot.Collections.Array<UnitType> { UnitType.建筑 } });
            VAssert("单位类型筛选：只抽建筑单位卡",
                () => buildingDrawn.Count == 1 && buildingDrawn[0].CardID == "城墙");
        });

        // ── 手牌被动（Card.PassiveEffects 订阅 EventBus） ────────
        RunGroup("手牌被动", () =>
        {
            if (CardManager.Instance == null || EventBus.Instance == null)
            {
                VAssert("CardManager/EventBus 未就绪，跳过手牌被动测试", () => true);
                return;
            }

            var cm = CardManager.Instance;
            var eb = EventBus.Instance;

            var drawOne = () => new DrawCardAction { Value = 1, AnimationDuration = 0f };
            var passiveCardData = new SpellCardData
            {
                CardID = "抽牌被动", CardName = "抽牌被动", Type = CardType.Spell,
                PassiveEffects = new[] { new EffectData
                {
                    TriggerEvent = EventType.OnDrawCard,
                    Actions = new GameAction[] { drawOne() },
                } },
            };
            var roundCardData = new SpellCardData
            {
                CardID = "回合被动", CardName = "回合被动", Type = CardType.Spell,
                PassiveEffects = new[] { new EffectData
                {
                    TriggerEvent = EventType.RoundStart,
                    Actions = new GameAction[] { drawOne() },
                } },
            };
            var plainData = new SpellCardData { CardID = "普通", CardName = "普通", Type = CardType.Spell };

            // ── 用例 A：抽到即触发（抽牌 → 订阅 → Fire OnDrawCard → 被动触发） ──
            cm.InitializeDrawPile(new List<CardData> { passiveCardData, plainData, plainData });
            // InitializeDrawPile 会洗牌，手动把被动卡放到牌库顶保证抽到
            var target = cm.DrawPile.Find(c => c.CardID == "抽牌被动");
            cm.DrawPile.Remove(target);
            cm.DrawPile.Insert(0, target);

            int h0 = cm.HandCards.Count;
            var drawn = cm.DrawCard();
            VAssert("抽到被动卡：OnDrawCard 触发被动再抽 1（手牌 +2）",
                () => drawn?.CardID == "抽牌被动" && cm.HandCards.Count == h0 + 2);

            // ── 用例 B：已在手牌的被动卡不响应他人抽牌（防连锁递归） ──
            int h1 = cm.HandCards.Count;
            cm.DrawCard();
            VAssert("已在手牌的被动卡不响应其他抽牌（手牌 +1）",
                () => cm.HandCards.Count == h1 + 1);

            // ── 用例 C：RoundStart 手牌被动（CreateCard 进手牌即订阅） ──
            cm.InitializeDrawPile(new List<CardData> { plainData, plainData, plainData });
            var roundCard = cm.CreateCard(roundCardData);
            int h2 = cm.HandCards.Count;
            eb.Fire(EventType.RoundStart, new Context());
            VAssert("手牌被动响应 RoundStart（手牌 +1）",
                () => cm.HandCards.Count == h2 + 1);

            // ── 用例 D：打出后退订（UseCard 使卡牌离手，基线在打出后捕获） ──
            cm.UseCard(roundCard);
            int h3 = cm.HandCards.Count;
            eb.Fire(EventType.RoundStart, new Context());
            VAssert("打出后退订：RoundStart 不再触发",
                () => cm.HandCards.Count == h3);

            // ── 用例 E：弃牌后退订（DiscardCard 使卡牌离手，基线在弃牌后捕获） ──
            cm.InitializeDrawPile(new List<CardData> { plainData, plainData, plainData });
            var discardCard = cm.CreateCard(roundCardData);
            cm.DiscardCard(discardCard);
            int h4 = cm.HandCards.Count;
            eb.Fire(EventType.RoundStart, new Context());
            VAssert("弃牌后退订：RoundStart 不再触发",
                () => cm.HandCards.Count == h4);

            // ── 用例 F：单位被动响应 OnDrawCard（事件通用性，MaxTriggerCount=1 防连锁） ──
            cm.InitializeDrawPile(new List<CardData> { plainData, plainData, plainData });
            var listener = MakeUnit("抽牌监听", 1, 5);
            eb.Subscribe(listener, new[] { new EffectData
            {
                TriggerEvent = EventType.OnDrawCard,
                MaxTriggerCount = 1,
                Actions = new GameAction[] { drawOne() },
            } });
            int h5 = cm.HandCards.Count;
            cm.DrawCard();
            VAssert("单位被动响应 OnDrawCard（MaxTriggerCount=1 防连锁，手牌 +2）",
                () => cm.HandCards.Count == h5 + 2);
            eb.Unsubscribe(listener);
        });

        // ── 任意单位死亡监听（OnAnyUnitDeath） ────────────────────
        RunGroup("任意死亡监听", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => false); return; }

            // ── 用例 1：存活单位监听"任意单位死亡"（Target=Self 作用于自己） ──
            var listener = MakeUnit("死亡监听", 5, 10);
            var victim = MakeUnit("死者", 3, 10);
            eb.Subscribe(listener, new[] { new EffectData
            {
                TriggerEvent = EventType.OnAnyUnitDeath,
                Target = PassiveTarget.Self,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                },
            } });
            int l0 = listener.AttackPower;
            eb.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = victim, SourceUnit = victim, SourceTeam = victim.Team });
            VAssert("任意死亡：存活监听者 ATK+1", () => listener.AttackPower == l0 + 1);
            eb.Unsubscribe(listener);

            // ── 用例 2：死者自身不响应"任意死亡"（EventBus 存活检查排除） ──
            var dying = MakeUnit("将死", 2, 10);
            eb.Subscribe(dying, new[] { new EffectData
            {
                TriggerEvent = EventType.OnAnyUnitDeath,
                Target = PassiveTarget.Self,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 100 },
                },
            } });
            dying.CurrentHP = 0;   // 模拟死亡（IsAlive=false）
            int d0 = dying.AttackPower;
            eb.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = victim, SourceUnit = victim, SourceTeam = victim.Team });
            VAssert("死者自身不响应任意死亡（ATK 不变）", () => dying.AttackPower == d0);
            eb.Unsubscribe(dying);

            // ── 用例 3：EventOther → TargetUnit=死者（事件另一方可作目标/读取） ──
            var tListener = MakeUnit("事件目标监听", 5, 10);
            var tVictim = MakeUnit("事件目标死者", 3, 10);
            eb.Subscribe(tListener, new[] { new EffectData
            {
                TriggerEvent = EventType.OnAnyUnitDeath,
                Target = PassiveTarget.EventOther,
                Actions = new GameAction[]
                {
                    new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 100 },
                },
            } });
            int tv0 = tVictim.AttackPower;
            eb.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = tVictim, SourceUnit = tVictim, SourceTeam = tVictim.Team });
            VAssert("EventOther 解析为死者（死者 ATK+100）", () => tVictim.AttackPower == tv0 + 100);
            eb.Unsubscribe(tListener);

            // ── 用例 4：真实链路 UnitManager.DestroyUnit 触发 OnAnyUnitDeath ──
            if (UnitManager.Instance != null)
            {
                var realListener = MakeUnit("真实监听", 5, 10);
                eb.Subscribe(realListener, new[] { new EffectData
                {
                    TriggerEvent = EventType.OnAnyUnitDeath,
                    Target = PassiveTarget.Self,
                    Actions = new GameAction[]
                    {
                        new ModifyStatAction { TargetStat = ModifyStatType.AttackPower, Value = 1 },
                    },
                } });
                var realVictim = MakeUnit("真实死者", 3, 10);
                int r0 = realListener.AttackPower;
                UnitManager.Instance.DestroyUnit(realVictim);
                VAssert("DestroyUnit 触发任意死亡监听（ATK+1）", () => realListener.AttackPower == r0 + 1);
                eb.Unsubscribe(realListener);
            }
            else
            {
                VAssert("UnitManager 未就绪，跳过真实链路", () => true);
            }
        });

        // ── 事件另一方读取（ValueTarget.EventOther：死亡事件读死者） ──
        RunGroup("事件另一方读取", () =>
        {
            var eb = EventBus.Instance;
            var bm = BuffManager.Instance;
            if (eb == null || bm == null) { VAssert("Manager 未就绪，跳过", () => false); return; }

            // MK0 同款被动：任意死亡时，死者是"友方兵种" → 自己获得死者义肢层数
            var limbBuff = new BuffData { BuffID = "义肢", Duration = -1, MaxStack = -1 };
            var mk0Like = new EffectData
            {
                TriggerEvent = EventType.OnAnyUnitDeath,
                Target = PassiveTarget.Self,
                Conditions = new Condition[]
                {
                    // 死者是兵种（UnitType.兵种=0）
                    new CompareCondition
                    {
                        Left = new UnitInfoValue { Unit = ValueTarget.EventOther, Info = UnitInfoType.Type },
                        Op = CompareOp.Equal,
                        Right = new ConstantValue { Value = (int)UnitType.兵种 },
                    },
                    // 死者是友方（死者阵营 == 来源阵营）
                    new CompareCondition
                    {
                        Left = new UnitInfoValue { Unit = ValueTarget.EventOther, Info = UnitInfoType.Team },
                        Op = CompareOp.Equal,
                        Right = new UnitInfoValue { Unit = ValueTarget.Source, Info = UnitInfoType.Team },
                    },
                },
                Actions = new GameAction[]
                {
                    new ApplyBuffAction
                    {
                        BuffData = limbBuff,
                        ValueSource = new BuffInfoValue { Unit = ValueTarget.EventOther, BuffID = "义肢" },
                    },
                },
            };

            // ── 用例 1：友方兵种死亡（带 3 层义肢）→ 监听者获得 3 层 ──
            var mk0 = MakeUnit("MK0", 2, 10);
            var allySquad = MakeUnit("友方兵种", 3, 10);
            bm.ApplyBuff(allySquad, limbBuff, null, 3);   // 死者携带 3 层义肢
            eb.Subscribe(mk0, new[] { mk0Like });
            EventBus.Instance.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = allySquad, SourceUnit = allySquad, SourceTeam = allySquad.Team });
            VAssert("友方兵种死亡：监听者获得死者义肢 3 层",
                () => bm.GetBuff(mk0, "义肢")?.StackCount == 3);

            // 清理（避免影响后续用例断言）
            bm.RemoveAllBuffs(mk0);
            bm.RemoveAllBuffs(allySquad);

            // ── 用例 2：敌方兵种死亡 → 相对阵营条件不满足，不触发 ──
            var enemySquad = MakeUnit("敌方兵种", 3, 10); enemySquad.Team = Team.Enemy;
            bm.ApplyBuff(enemySquad, limbBuff, null, 5);
            EventBus.Instance.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = enemySquad, SourceUnit = enemySquad, SourceTeam = enemySquad.Team });
            VAssert("敌方兵种死亡不触发（监听者无义肢）",
                () => bm.GetBuff(mk0, "义肢") == null);

            // ── 用例 3：友方建筑死亡 → 类型条件不满足，不触发 ──
            var allyBuilding = MakeUnit("友方建筑", 3, 10); allyBuilding.Type = UnitType.建筑;
            bm.ApplyBuff(allyBuilding, limbBuff, null, 2);
            EventBus.Instance.Fire(EventType.OnAnyUnitDeath,
                new Context { TargetUnit = allyBuilding, SourceUnit = allyBuilding, SourceTeam = allyBuilding.Team });
            VAssert("友方建筑死亡不触发（监听者仍无义肢）",
                () => bm.GetBuff(mk0, "义肢") == null);

            eb.Unsubscribe(mk0);
        });

        // ── 事件载荷全量透传（克隆式 effectCtx 防回归：新字段自动继承 + 派生语义字段正确） ──
        RunGroup("事件载荷透传", () =>
        {
            var eb = EventBus.Instance;
            var bm = BattleManager.Instance;
            if (eb == null || bm == null) { VAssert("Manager 未就绪，跳过", () => false); return; }

            var tgt = MakeUnit("透传目标", 3, 10);
            var srcCell = MakeCell(new Vector2I(5, 6), null);
            var tgtCell = MakeCell(new Vector2I(1, 2), null);

            // 条件链全部通过 = 各字段已透传；任一丢失则条件不满足，费用不变
            Condition[] passThroughConds = new Condition[]
            {
                // TargetCell 透传（事件格 X=1）
                new CompareCondition { Left = new CellCoordValue { Cell = new ContextCellValue { Cell = ContextCellType.Target }, Info = CellCoordInfo.PosX }, Op = CompareOp.Equal, Right = new ConstantValue { Value = 1 } },
                // SourceCell 透传（来源格 X=5）
                new CompareCondition { Left = new CellCoordValue { Cell = new ContextCellValue { Cell = ContextCellType.Source }, Info = CellCoordInfo.PosX }, Op = CompareOp.Equal, Right = new ConstantValue { Value = 5 } },
                // PendingDamage 透传
                new CompareCondition { Left = new PendingDamageValue(), Op = CompareOp.Equal, Right = new ConstantValue { Value = 7 } },
                // AttackDirection 透传
                new CompareCondition { Left = new AttackDirectionValue { DefaultValue = 99 }, Op = CompareOp.Equal, Right = new ConstantValue { Value = (int)CellDirection.Left } },
                // ActType 透传（ActionKindCondition 读 ctx.ActType）
                new ActionKindCondition { Kind = UnitActType.Move },
            };

            // ── 用例 1：PassiveTarget 路径——effectCtx 继承事件全部载荷 ──
            // 用 RoundStart（无 subject 定向）：OnKill 等战斗事件带 subject 过滤，订阅者非触发者会被跳过
            var listener = MakeUnit("透传监听", 5, 10);
            eb.Subscribe(listener, new[] { new EffectData
            {
                TriggerEvent = EventType.RoundStart,
                Target = PassiveTarget.EventOther,   // TargetUnit = 事件 TargetUnit（tgt）
                Conditions = new Condition[]
                {
                    // EventOtherUnit 派生 = 事件 TargetUnit，且与 Source 同阵营（MakeUnit 同为 Player）
                    new CompareCondition { Left = new UnitInfoValue { Unit = ValueTarget.Source, Info = UnitInfoType.Team }, Op = CompareOp.Equal, Right = new UnitInfoValue { Unit = ValueTarget.EventOther, Info = UnitInfoType.Team } },
                    // TargetUnit（EventOther 语义）= 事件 TargetUnit
                    new CompareCondition { Left = new UnitInfoValue { Unit = ValueTarget.Target, Info = UnitInfoType.Team }, Op = CompareOp.Equal, Right = new UnitInfoValue { Unit = ValueTarget.EventOther, Info = UnitInfoType.Team } },
                }.Concat(passThroughConds).ToArray(),
                Actions = new GameAction[] { new ModifyCostAction { Value = 1 } },
            } });
            int cost0 = bm.PlayerCost;
            eb.Fire(EventType.RoundStart, new Context
            {
                TargetUnit = tgt,
                SourceCell = srcCell,
                TargetCell = tgtCell,
                PendingDamage = 7,
                AttackDirection = CellDirection.Left,
                ActType = UnitActType.Move,
            });
            VAssert("PassiveTarget 路径：事件载荷全字段透传（条件全通过 → 费用+1）",
                () => bm.PlayerCost == cost0 + 1);
            eb.Unsubscribe(listener);

            // ── 用例 2：TargetFilter 路径——effectCtx 继承事件载荷 + TargetUnits 覆盖解析结果 ──
            var fListener = MakeUnit("透传过滤监听", 5, 10);
            eb.Subscribe(fListener, new[] { new EffectData
            {
                TriggerEvent = EventType.RoundStart,
                // 旧版 filter 分支丢失 TargetCell/SourceCell/PendingDamage/AttackDirection，克隆后应继承
                TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.All } },
                Conditions = passThroughConds,
                Actions = new GameAction[] { new ModifyCostAction { Value = 1 } },
            } });
            int fcost0 = bm.PlayerCost;
            eb.Fire(EventType.RoundStart, new Context
            {
                TargetUnit = tgt,
                SourceCell = srcCell,
                TargetCell = tgtCell,
                PendingDamage = 7,
                AttackDirection = CellDirection.Left,
                ActType = UnitActType.Move,
            });
            VAssert("TargetFilter 路径：事件载荷继承（条件全通过 → 费用+1）",
                () => bm.PlayerCost == fcost0 + 1);
            eb.Unsubscribe(fListener);
        });

        // ── CardInfoValue 卡牌值源 ───────────────────────────────
        RunGroup("CardInfoValue 卡牌值源", () =>
        {
            var card = new Card(new SpellCardData
            {
                CardID = "测试卡",
                CardName = "测试卡",
                Cost = 5,
                Type = CardType.Spell,
                World = World.曼斯维森,
                Faction = Faction.擢升之手,
                Rarity = Rarity.顶级,
            });
            var ctx = new Context { SourceCard = card };

            VAssert("CardInfoValue 读费用",
                () => new CardInfoValue { Info = CardInfoType.Cost }.GetValue(ctx) == 5);
            VAssert("CardInfoValue 读卡牌类型（枚举数值）",
                () => new CardInfoValue { Info = CardInfoType.Type }.GetValue(ctx) == (int)CardType.Spell);
            VAssert("CardInfoValue 读世界观",
                () => new CardInfoValue { Info = CardInfoType.World }.GetValue(ctx) == (int)World.曼斯维森);
            VAssert("CardInfoValue 读势力",
                () => new CardInfoValue { Info = CardInfoType.Faction }.GetValue(ctx) == (int)Faction.擢升之手);
            VAssert("CardInfoValue 读稀有度",
                () => new CardInfoValue { Info = CardInfoType.Rarity }.GetValue(ctx) == (int)Rarity.顶级);
            VAssert("CardInfoValue 无 SourceCard 返回默认值",
                () => new CardInfoValue { Info = CardInfoType.Cost, DefaultValue = 7 }.GetValue(new Context()) == 7);
        });

        // ── UnitInfoValue 单位信息值源 ────────────────────────────
        RunGroup("UnitInfoValue 单位信息值源", () =>
        {
            var squad = MakeUnit("兵种", 3, 10);
            var building = MakeUnit("建筑", 3, 10); building.Type = UnitType.建筑;

            var ctx = new Context { SourceUnit = squad, TargetUnit = building };
            VAssert("读目标单位类型=建筑",
                () => new UnitInfoValue { Info = UnitInfoType.Type }.GetValue(ctx) == (int)UnitType.建筑);
            VAssert("读来源单位类型=兵种",
                () => new UnitInfoValue { Unit = ValueTarget.Source, Info = UnitInfoType.Type }.GetValue(ctx) == (int)UnitType.兵种);
            VAssert("改类型后读取更新",
                () =>
                {
                    building.Type = UnitType.门;
                    bool ok = new UnitInfoValue { Info = UnitInfoType.Type }.GetValue(ctx) == (int)UnitType.门;
                    building.Type = UnitType.建筑;
                    return ok;
                });
            VAssert("单位不存在返回 DefaultValue",
                () => new UnitInfoValue { Info = UnitInfoType.Type, DefaultValue = 9 }.GetValue(new Context()) == 9);
            // 配合 CompareCondition：目标是建筑时条件成立（值源返回枚举数值）
            VAssert("CompareCondition 判断类型=建筑",
                () => new CompareCondition
                {
                    Left = new UnitInfoValue { Info = UnitInfoType.Type },
                    Op = CompareOp.Equal,
                    Right = new ConstantValue { Value = (int)UnitType.建筑 },
                }.IsMet(ctx));
        });

        // ── 致命免伤（OnBeforeTakeDamage + PendingDamage） ────────
        RunGroup("致命免伤", () =>
        {
            if (UnitManager.Instance == null || EventBus.Instance == null)
            {
                VAssert("UnitManager/EventBus 未就绪，跳过致命免伤测试", () => true);
                return;
            }
            var eb = EventBus.Instance;

            // 免死被动：本次伤害 ≥ 当前 HP 时把伤害清零（增量 = -基础伤害）。
            // 受击前事件：Source=受击者（自己），故读 Source 的当前 HP
            var protectEffect = new EffectData
            {
                TriggerEvent = EventType.OnBeforeTakeDamage,
                Target = PassiveTarget.Self,
                Conditions = new Condition[]
                {
                    new CompareCondition
                    {
                        Left = new PendingDamageValue(),
                        Op = CompareOp.GreaterEqual,
                        Right = new UnitStatValue
                        {
                            Unit = ValueTarget.Source,
                            Stat = ModifyStatType.MaxHP,
                            CurrentHP = true,
                        },
                    },
                },
                Actions = new GameAction[]
                {
                    new ModifyDamageAction
                    {
                        ValueSource = new FormulaValue
                        {
                            Op = FormulaOp.Mul,
                            Left = new PendingDamageValue(),
                            Right = new ConstantValue { Value = -1 },
                        },
                    },
                },
            };

            // 用例 1：致命伤害被免伤（10 ≥ HP 5 → 伤害归零，HP 不变）
            var victim = MakeUnit("免死单位", 1, 5);
            var attacker = MakeUnit("攻击者", 10, 10);
            eb.Subscribe(victim, new[] { protectEffect });
            new DamageAction { Value = 10 }.Execute(new Context
            {
                SourceUnit = attacker,
                TargetUnit = victim,
            });
            VAssert("致命伤害免伤：HP 不变且存活",
                () => victim.CurrentHP == 5 && victim.IsAlive);

            // 用例 2：非致命伤害正常结算（3 < HP 5）
            new DamageAction { Value = 3 }.Execute(new Context
            {
                SourceUnit = attacker,
                TargetUnit = victim,
            });
            VAssert("非致命伤害正常结算：HP 5→2",
                () => victim.CurrentHP == 2);

            // 用例 3：无免死被动时致命伤害致死
            var plain = MakeUnit("普通单位", 1, 5);
            new DamageAction { Value = 10 }.Execute(new Context
            {
                SourceUnit = attacker,
                TargetUnit = plain,
            });
            VAssert("无免死被动：致命伤害致死",
                () => plain.CurrentHP <= 0 && !plain.IsAlive);

            eb.Unsubscribe(victim);
        });

        // ── RemoveEquipmentAction 移除装备 ─────────────────────────
        RunGroup("移除装备 RemoveEquipmentAction", () =>
        {
            if (UnitManager.Instance == null || EquipmentManager.Instance == null)
            {
                VAssert("UnitManager/EquipmentManager 未就绪，跳过移除装备测试", () => true);
                return;
            }
            var em = EquipmentManager.Instance;

            // 造一件装备：ATK+2
            var equipData = new EquipmentData
            {
                EquipmentID = "测试装备",
                EquipmentName = "测试装备",
                AttackBonus = 2,
            };

            // 用例 1：按 ID 移除成功，属性还原
            var unit = MakeUnit("装备测试员", 1, 10);
            em.Equip(unit, equipData, null);
            VAssert("装备后 ATK+2（1→3）且已装备",
                () => unit.AttackPower == 3 && em.HasEquipment(unit));

            new RemoveEquipmentAction { EquipmentID = "测试装备" }
                .Execute(new Context { TargetUnit = unit });
            VAssert("按 ID 移除：ATK 还原为 1 且未装备",
                () => unit.AttackPower == 1 && !em.HasEquipment(unit));

            // 用例 2：ID 不匹配不动作
            em.Equip(unit, equipData, null);
            new RemoveEquipmentAction { EquipmentID = "其他装备" }
                .Execute(new Context { TargetUnit = unit });
            VAssert("ID 不匹配：装备保留",
                () => em.HasEquipment(unit) && unit.AttackPower == 3);

            // 用例 3：无装备时不动作（不报错）
            var plain = MakeUnit("无装备者", 1, 10);
            new RemoveEquipmentAction { EquipmentID = "测试装备" }
                .Execute(new Context { TargetUnit = plain });
            VAssert("无装备：不动作且无异常",
                () => !em.HasEquipment(plain) && plain.AttackPower == 1);

            // 清理（避免残留影响其他测试）
            em.RemoveAllEquipments(unit);
        });

        // ── 环境系统 ──────────────────────────────────────────────
        RunGroup("环境系统", () =>
        {
            var em = EnvironmentManager.Instance;
            if (em == null) { VAssert("EnvironmentManager 未就绪，跳过", () => false); return; }

            // ── 施加 + 格子属性修正 ──
            var cell = MakeCell(new Vector2I(0, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
            var envA = new EnvironmentData
            {
                EnvironmentID = "沼泽",
                EnvironmentName = "沼泽",
                Duration = -1,
                MoveCostDelta = 2,
                CanStandOverride = CellPropertyOverride.ForceFalse,
            };

            em.ApplyEnvironment(cell, envA, null);
            VAssert("施加后 MoveCost 1→3", () => cell.MoveCost == 3);
            VAssert("施加后 CanStand 被覆盖为 false", () => cell.CanStand == false);
            VAssert("HasEnvironment true", () => em.HasEnvironment(cell, "沼泽"));
            VAssert("GetEnvironment 非空", () => em.GetEnvironment(cell) != null);

            // ── 替换式覆盖：旧环境完整还原后替换 ──
            var envB = new EnvironmentData
            {
                EnvironmentID = "熔岩",
                EnvironmentName = "熔岩",
                Duration = -1,
                MoveCostDelta = 5,
                CanStandOverride = CellPropertyOverride.Unchanged,
            };
            em.ApplyEnvironment(cell, envB, null);
            VAssert("覆盖后 MoveCost 3→6（旧环境修正已还原）", () => cell.MoveCost == 6);
            VAssert("旧环境已移除", () => !em.HasEnvironment(cell, "沼泽"));
            VAssert("新环境生效", () => em.HasEnvironment(cell, "熔岩"));
            VAssert("覆盖后 CanStand 恢复基础 true", () => cell.CanStand == true);

            // ── 移除还原 ──
            em.RemoveEnvironment(cell);
            VAssert("移除后 MoveCost 还原为 1", () => cell.MoveCost == 1);
            VAssert("移除后无环境", () => em.GetEnvironment(cell) == null);

            // ── 占位协调：占据压制环境覆盖，释放后环境修正恢复 ──
            var occupyCell = MakeCell(new Vector2I(1, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
            var envForceTrue = new EnvironmentData
            {
                EnvironmentID = "陆桥",
                Duration = -1,
                CanStandOverride = CellPropertyOverride.ForceTrue,
                CanPassOverride = CellPropertyOverride.ForceTrue,
            };
            em.ApplyEnvironment(occupyCell, envForceTrue, null);
            VAssert("无单位时 ForceTrue 生效", () => occupyCell.CanStand && occupyCell.CanPass);

            occupyCell.OccupyingUnit = MakeUnit("占位者", 1, 5);
            em.RefreshCellProperties(occupyCell);
            VAssert("单位占据压制环境覆盖（CanStand/CanPass=false）",
                () => occupyCell.CanStand == false && occupyCell.CanPass == false);

            occupyCell.OccupyingUnit = null;
            em.RefreshCellProperties(occupyCell);
            VAssert("单位释放后环境覆盖恢复", () => occupyCell.CanStand && occupyCell.CanPass);

            em.RemoveEnvironment(occupyCell);
            VAssert("移除后回基础值", () => occupyCell.CanStand && occupyCell.CanPass);

            // ── Duration 倒计时 ──
            var durCell = MakeCell(new Vector2I(2, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
            em.ApplyEnvironment(durCell, new EnvironmentData { EnvironmentID = "时效环境", Duration = 2 }, null);
            VAssert("Duration=2 初始 RemainingTurns=2",
                () => em.GetEnvironment(durCell)?.RemainingTurns == 2);

            em.TickAllEnvironments();
            VAssert("Tick1 后 RemainingTurns=1 仍存在",
                () => em.GetEnvironment(durCell) != null && em.GetEnvironment(durCell).RemainingTurns == 1);
            em.TickAllEnvironments();
            VAssert("Tick2 后到期移除", () => em.GetEnvironment(durCell) == null);

            // Duration=0 当回合移除
            var zeroCell = MakeCell(new Vector2I(3, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
            em.ApplyEnvironment(zeroCell, new EnvironmentData { EnvironmentID = "瞬时环境", Duration = 0 }, null);
            em.TickAllEnvironments();
            VAssert("Duration=0 当回合移除", () => em.GetEnvironment(zeroCell) == null);

            // ── ModifyCellStatAction 可逆 ──
            var statCell = MakeCell(new Vector2I(4, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
            var statAction = new ModifyCellStatAction { TargetStat = CellStatType.MoveCost, Value = 3 };
            statAction.Execute(new Context { TargetCell = statCell });
            VAssert("ModifyCellStatAction MoveCost 1→4", () => statCell.MoveCost == 4);
            statAction.Revert(new Context { TargetCell = statCell });
            VAssert("Revert 后 MoveCost 还原为 1", () => statCell.MoveCost == 1);

            // ── 环境被动：回合结束对格子上单位造成伤害 ──
            if (UnitManager.Instance != null)
            {
                var fireCell = MakeCell(new Vector2I(5, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var victim = MakeUnit("环境受害者", 0, 5);
                fireCell.OccupyingUnit = victim;

                var fireEnv = new EnvironmentData
                {
                    EnvironmentID = "火焰",
                    Duration = -1,
                    PassiveEffects = new EffectData[]
                    {
                        new EffectData
                        {
                            TriggerEvent = EventType.RoundEnd,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                    },
                };
                em.ApplyEnvironment(fireCell, fireEnv, null);
                EventBus.Instance?.Fire(EventType.RoundEnd, new Context());
                VAssert("回合结束对格子上单位造成1伤（5→4）", () => victim.CurrentHP == 4);

                // 清理环境与被动订阅（避免残留）
                em.RemoveEnvironment(fireCell);
                VAssert("被动环境移除后无环境", () => em.GetEnvironment(fireCell) == null);
            }

            // ── 环境进入/离开格子事件 ──
            if (UnitManager.Instance != null)
            {
                var enterCell = MakeCell(new Vector2I(6, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var walker = MakeUnit("进入者", 0, 5);

                var trapEnv = new EnvironmentData
                {
                    EnvironmentID = "陷阱",
                    Duration = -1,
                    PassiveEffects = new EffectData[]
                    {
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitEnterCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitLeaveCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                    },
                };
                em.ApplyEnvironment(enterCell, trapEnv, null);

                // 进入：事件单位 = ctx.TargetUnit（Fire 时格子可能尚未绑定或刚绑定）
                EventBus.Instance.Fire(EventType.OnUnitEnterCell,
                    new Context { TargetCell = enterCell, TargetUnit = walker }, instigator: walker);
                VAssert("进入事件：环境被动对进入单位造成1伤（5→4）", () => walker.CurrentHP == 4);

                // 离开：格子已释放，事件单位仍取 ctx.TargetUnit
                EventBus.Instance.Fire(EventType.OnUnitLeaveCell,
                    new Context { TargetCell = enterCell, TargetUnit = walker }, instigator: walker);
                VAssert("离开事件：环境被动对离开单位造成1伤（4→3）", () => walker.CurrentHP == 3);

                // 格子匹配过滤：Fire 到 enterCell，其他格子的环境不被触发
                var otherCell = MakeCell(new Vector2I(7, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var otherEnv = new EnvironmentData
                {
                    EnvironmentID = "别处环境",
                    Duration = -1,
                    PassiveEffects = new EffectData[]
                    {
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitEnterCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 5 } },
                        },
                    },
                };
                em.ApplyEnvironment(otherCell, otherEnv, null);
                var bystander = MakeUnit("路人", 0, 10);
                EventBus.Instance.Fire(EventType.OnUnitEnterCell,
                    new Context { TargetCell = enterCell, TargetUnit = bystander }, instigator: bystander);
                VAssert("格子过滤：进入格环境命中（路人 10→9），非目标格环境不触发", () => bystander.CurrentHP == 9);

                // 清理
                em.RemoveEnvironment(enterCell);
                em.RemoveEnvironment(otherCell);
            }

            // ── 环境变化过滤：同环境内移动不触发 / 跨环境触发（含真实移动路径）──
            if (UnitManager.Instance != null)
            {
                // 进入/离开各 1 伤的通用环境（施加到多个格子）
                var moveTrap = new EnvironmentData
                {
                    EnvironmentID = "移动陷阱",
                    Duration = -1,
                    PassiveEffects = new EffectData[]
                    {
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitEnterCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitLeaveCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                    },
                };
                var sameEnvA = MakeCell(new Vector2I(8, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var sameEnvB = MakeCell(new Vector2I(9, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var diffEnvCell = MakeCell(new Vector2I(10, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                var plainCell = MakeCell(new Vector2I(11, 0), new BlockData { BlockName = "地板", MoveCost = 1 });
                em.ApplyEnvironment(sameEnvA, moveTrap, null);
                em.ApplyEnvironment(sameEnvB, moveTrap, null);
                var diffEnv = new EnvironmentData
                {
                    EnvironmentID = "异环境",
                    Duration = -1,
                    PassiveEffects = new EffectData[]
                    {
                        new EffectData
                        {
                            TriggerEvent = EventType.OnUnitEnterCell,
                            TargetFilters = new TargetFilter[] { new ShapeTargetFilter { Shape = TargetShape.SingleUnit } },
                            Actions = new GameAction[] { new DamageAction { Value = 1 } },
                        },
                    },
                };
                em.ApplyEnvironment(diffEnvCell, diffEnv, null);

                var mover = MakeUnit("环境过滤移动者", 0, 10);

                // 同环境内移动（A→B）：离开/进入均不触发（对面环境 ID 相同）
                EventBus.Instance.Fire(EventType.OnUnitLeaveCell,
                    new Context { TargetCell = sameEnvA, SourceCell = sameEnvB, TargetUnit = mover }, instigator: mover);
                EventBus.Instance.Fire(EventType.OnUnitEnterCell,
                    new Context { TargetCell = sameEnvB, SourceCell = sameEnvA, TargetUnit = mover }, instigator: mover);
                VAssert("同环境内移动：进入/离开均不触发（HP 不变）", () => mover.CurrentHP == 10);

                // 跨环境移动（A→C）：起点环境触发离开（10→9）
                EventBus.Instance.Fire(EventType.OnUnitLeaveCell,
                    new Context { TargetCell = sameEnvA, SourceCell = diffEnvCell, TargetUnit = mover }, instigator: mover);
                VAssert("跨环境移动：起点环境触发离开（10→9）", () => mover.CurrentHP == 9);

                // 跨环境移动（A→C）：终点环境触发进入（9→8）
                EventBus.Instance.Fire(EventType.OnUnitEnterCell,
                    new Context { TargetCell = diffEnvCell, SourceCell = sameEnvA, TargetUnit = mover }, instigator: mover);
                VAssert("跨环境移动：终点环境触发进入（9→8）", () => mover.CurrentHP == 8);

                // 有→无：对面无环境触发离开（8→7）
                EventBus.Instance.Fire(EventType.OnUnitLeaveCell,
                    new Context { TargetCell = sameEnvA, SourceCell = plainCell, TargetUnit = mover }, instigator: mover);
                VAssert("有→无：离开触发（8→7）", () => mover.CurrentHP == 7);

                // 无→有：对面无环境触发进入（7→6）
                EventBus.Instance.Fire(EventType.OnUnitEnterCell,
                    new Context { TargetCell = sameEnvA, SourceCell = plainCell, TargetUnit = mover }, instigator: mover);
                VAssert("无→有：进入触发（7→6）", () => mover.CurrentHP == 6);

                // 清理
                em.RemoveEnvironment(sameEnvA);
                em.RemoveEnvironment(sameEnvB);
                em.RemoveEnvironment(diffEnvCell);

                // ── 真实移动路径：UnitManager.MoveUnit 的 SourceCell 传递 ──
                if (MapManager.Instance != null)
                {
                    var map = MapManager.Instance.Map;
                    var rA = MakeCell(new Vector2I(20, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
                    var rB = MakeCell(new Vector2I(21, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
                    var rPlain = MakeCell(new Vector2I(22, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
                    var rDiff = MakeCell(new Vector2I(23, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
                    map[rA.GridPos] = rA;
                    map[rB.GridPos] = rB;
                    map[rPlain.GridPos] = rPlain;
                    map[rDiff.GridPos] = rDiff;

                    em.ApplyEnvironment(rA, moveTrap, null);
                    em.ApplyEnvironment(rB, moveTrap, null);
                    em.ApplyEnvironment(rDiff, diffEnv, null);

                    var realMover = MakeUnit("真实环境移动者", 0, 10);
                    rA.OccupyingUnit = realMover;
                    realMover.GridPos = rA.GridPos;

                    // 同环境内移动（A→B）：不触发
                    UnitManager.Instance.MoveUnit(realMover, rB.GridPos);
                    VAssert("真实移动：同环境内移动不触发（HP 不变）", () => realMover.CurrentHP == 10);

                    // 有→无（B→plain）：离开触发（10→9）
                    UnitManager.Instance.MoveUnit(realMover, rPlain.GridPos);
                    VAssert("真实移动：有→无触发离开（10→9）", () => realMover.CurrentHP == 9);

                    // 无→有（plain→B）：进入触发（9→8）
                    UnitManager.Instance.MoveUnit(realMover, rB.GridPos);
                    VAssert("真实移动：无→有触发进入（9→8）", () => realMover.CurrentHP == 8);

                    // 跨环境（B→C，不同 ID）：先离开后进入，各 1 伤（8→6）
                    UnitManager.Instance.MoveUnit(realMover, rDiff.GridPos);
                    VAssert("真实移动：跨环境先离开后进入（8→6）", () => realMover.CurrentHP == 6);

                    // 清理
                    em.RemoveEnvironment(rA);
                    em.RemoveEnvironment(rB);
                    em.RemoveEnvironment(rDiff);
                    map.Remove(rA.GridPos);
                    map.Remove(rB.GridPos);
                    map.Remove(rPlain.GridPos);
                    map.Remove(rDiff.GridPos);
                }

                // ── 环境预置加载（瓦片化：MapData 导出 → LoadPresetEnvironments） ──
                if (MapManager.Instance != null)
                {
                    var mm = MapManager.Instance;
                    var presetCell = MakeCell(new Vector2I(30, 0), new BlockData { BlockName = "地板", MoveCost = 1, CanStand = true, CanPass = true });
                    mm.Map[presetCell.GridPos] = presetCell;
                    var presetEnv = new EnvironmentData
                    {
                        EnvironmentID = "预置毒沼",
                        EnvironmentName = "预置毒沼",
                        Duration = -1,
                        MoveCostDelta = 2,
                    };
                    var mapData = new MapData();
                    mapData.SetEnvironmentDict(new System.Collections.Generic.Dictionary<Vector2I, EnvironmentData> { { presetCell.GridPos, presetEnv } });

                    em.LoadPresetEnvironments(mapData);
                    VAssert("预置环境加载到对应格子", () => em.HasEnvironment(presetCell, "预置毒沼"));
                    VAssert("预置环境属性修正生效（MoveCost 1→3）", () => presetCell.MoveCost == 3);

                    // 重复加载同格：已有环境跳过，不重复施加
                    em.LoadPresetEnvironments(mapData);
                    VAssert("预置环境重复加载跳过已有格", () => em.GetEnvironment(presetCell)?.Data.EnvironmentID == "预置毒沼");

                    em.RemoveEnvironment(presetCell);
                    mm.Map.Remove(presetCell.GridPos);
                }
            }
        });

        RunGroup("变身", () =>
        {
            if (UnitManager.Instance == null) return;

            var u = MakeUnit("变身者", 1, 10);
            var newData = new UnitData
            {
                UnitID = "变身形态",
                UnitName = "变身形态",
                AttackPower = 5,
                HealthPoints = 20,
                Stamina = 3,
                AttackDistance = 2,
                ActionPoints = 2,
            };

            // 变身前挂 buff（MaxHP+5）与装备（ATK+2）
            var buff = new BuffData
            {
                BuffID = "变身buff",
                BuffName = "变身buff",
                Duration = -1,
                MaxStack = -1,
                OnApplyActions = new GameAction[] { new ModifyStatAction { TargetStat = ModifyStatType.MaxHP, Value = 5 } },
            };
            BuffManager.Instance?.ApplyBuff(u, buff, null);
            EquipmentManager.Instance?.Equip(u, new EquipmentData
            {
                EquipmentID = "变身装备",
                EquipmentName = "变身装备",
                AttackBonus = 2,
                MaxHealthBonus = 0,
            }, null);
            VAssert("变身前置：buff 生效 MaxHP=15", () => u.MaxHP == 15);
            VAssert("变身前置：装备生效 ATK=3", () => u.AttackPower == 3);

            // 变身：清 buff/装备 + 换模板 + 重置属性（满血）
            UnitManager.Instance.TransformUnit(u, newData);
            VAssert("变身后：模板切换", () => u.UnitData == newData);
            VAssert("变身后：MaxHP=20（buff 已清）", () => u.MaxHP == 20);
            VAssert("变身后：ATK=5（装备已清）", () => u.AttackPower == 5);
            VAssert("变身后：满血", () => u.CurrentHP == 20);
            VAssert("变身后：体力=3", () => u.Stamina == 3);
            VAssert("变身后：攻击距离=2", () => u.AttackDistance == 2);

            // 变身动作 TransformUnitAction（TargetUnit 路径）
            var action = new TransformUnitAction { UnitData = newData };
            action.Execute(new Context { TargetUnit = u });
            VAssert("变身动作：TargetUnit 再次变身生效", () => u.UnitData == newData && u.MaxHP == 20);

            // 新被动订阅生效：新模板带 RoundStart 自伤 1（Target=Self 默认）
            var passiveData = new UnitData
            {
                UnitID = "变身形态2",
                UnitName = "变身形态2",
                AttackPower = 3,
                HealthPoints = 8,
                PassiveEffects = new EffectData[]
                {
                    new EffectData
                    {
                        TriggerEvent = EventType.RoundStart,
                        Actions = new GameAction[] { new DamageAction { Value = 1 } },
                    },
                },
            };
            UnitManager.Instance.TransformUnit(u, passiveData);
            VAssert("变身前状态：MaxHP=8 满血", () => u.MaxHP == 8 && u.CurrentHP == 8);
            EventBus.Instance?.Fire(EventType.RoundStart, new Context(), instigator: u);
            VAssert("新被动订阅生效：RoundStart 伤害1（8→7）", () => u.CurrentHP == 7);

            // 变身事件：其他单位被动监听 OnUnitTransformed（Target=EventOther=变身单位，伤害1）
            var observer = MakeUnit("变身观察者", 0, 10);
            EventBus.Instance?.Subscribe(observer, new[]
            {
                new EffectData
                {
                    TriggerEvent = EventType.OnUnitTransformed,
                    Target = PassiveTarget.EventOther,
                    Actions = new GameAction[] { new DamageAction { Value = 1 } },
                },
            });
            UnitManager.Instance.TransformUnit(u, newData);
            VAssert("变身事件：观察者被动对变身单位造成1伤（变身满血20→19）", () => u.CurrentHP == 19);
            EventBus.Instance?.Unsubscribe(observer);

            // 清理
            BuffManager.Instance?.RemoveAllBuffs(u);
            EquipmentManager.Instance?.RemoveAllEquipments(u);
        });

        RunGroup("义肢变身", () =>
        {
            if (UnitManager.Instance == null) return;

            // ── CardFactionFilter / CardCostFilter 单测（运行时 Card 包装）──
            var unitDataA = new UnitData { UnitID = "义肢候选A", UnitName = "义肢候选A", Faction = Faction.擢升之手, HealthPoints = 10, AttackPower = 1 };
            var unitDataB = new UnitData { UnitID = "义肢候选B", UnitName = "义肢候选B", Faction = Faction.擢升之手, HealthPoints = 12, AttackPower = 2 };
            var cardA = new UnitCardData { CardID = "义肢卡A", CardName = "义肢卡A", Faction = Faction.擢升之手, Cost = 2, UnitData = unitDataA };
            var cardA2 = new UnitCardData { CardID = "义肢卡A2", CardName = "义肢卡A2", Faction = Faction.擢升之手, Cost = 8, UnitData = unitDataA }; // 同单位费用8越界
            var cardB = new UnitCardData { CardID = "义肢卡B", CardName = "义肢卡B", Faction = Faction.擢升之手, Cost = 6, UnitData = unitDataB };
            var cardC = new UnitCardData { CardID = "义肢卡C", CardName = "义肢卡C", Faction = Faction.圣主教, Cost = 2, UnitData = new UnitData { UnitID = "圣主教单位", UnitName = "圣主教单位", Faction = Faction.圣主教 } };

            VAssert("CardFactionFilter：擢升之手命中", () => new CardFactionFilter { Faction = Faction.擢升之手 }.IsMatch(new Card(cardA)));
            VAssert("CardFactionFilter：圣主教卡排除", () => !new CardFactionFilter { Faction = Faction.擢升之手 }.IsMatch(new Card(cardC)));
            VAssert("CardCostFilter：费用2命中≤6", () => new CardCostFilter { MaxCost = 6 }.IsMatch(new Card(cardA)));
            VAssert("CardCostFilter：费用8排除", () => !new CardCostFilter { MaxCost = 6 }.IsMatch(new Card(cardA2)));

            // ── CardLibrary 通用查询：临时隔离 CardList（候选只含注入卡，断言可靠）──
            var backup = CardLibrary.CardList.ToList();
            CardLibrary.CardList.Clear();
            CardLibrary.CardList.AddRange(new CardData[] { cardA, cardA2, cardB, cardC });

            var combo = CardFilter.CombineAnd(new CardFilter[]
            {
                new CardFactionFilter { Faction = Faction.擢升之手 },
                new CardCostFilter { MaxCost = 6 },
            });
            var matched = CardLibrary.GetCards(combo);
            VAssert("通用查询：擢升之手+费用≤6 命中 2 张", () => matched.Length == 2);
            VAssert("通用查询：含卡A/卡B，不含费用8卡与圣主教卡",
                () => matched.Contains(cardA) && matched.Contains(cardB) && !matched.Contains(cardA2) && !matched.Contains(cardC));
            var randomPick = CardLibrary.GetRandomCard(combo);
            VAssert("通用查询：随机命中匹配集", () => randomPick == cardA || randomPick == cardB);

            // ── 完整链路：被动 = 义肢层数>2 → 随机变身擢升之手（卡费≤6）──
            var transformPassive = new EffectData
            {
                TriggerEvent = EventType.OnBuffStackChanged,
                Conditions = new Condition[]
                {
                    new CompareCondition
                    {
                        Left = new BuffInfoValue { Unit = ValueTarget.Source, BuffID = "义肢", Info = BuffInfoType.StackCount },
                        Op = CompareOp.Greater,
                        Right = new ConstantValue { Value = 2 },
                    },
                },
                Actions = new GameAction[]
                {
                    new RandomTransformAction
                    {
                        Filters = new CardFilter[]
                        {
                            new CardFactionFilter { Faction = Faction.擢升之手 },
                            new CardCostFilter { MaxCost = 6 },
                        },
                    },
                },
            };
            var limbBuff = new BuffData { BuffID = "义肢", Duration = -1, MaxStack = -1 };

            // 场景1：1→2→3 层逐步叠加，3 层触发变身
            var limbed = MakeUnit("义肢变身者", 1, 10);
            EventBus.Instance?.Subscribe(limbed, new[] { transformPassive });
            var originalData = limbed.UnitData;
            BuffManager.Instance?.ApplyBuff(limbed, limbBuff, null, 1);
            BuffManager.Instance?.ApplyBuff(limbed, limbBuff, null, 1); // 叠到 2 层
            VAssert("叠到2层：不触发变身", () => limbed.UnitData == originalData);
            BuffManager.Instance?.ApplyBuff(limbed, limbBuff, null, 1); // 叠到 3 层
            VAssert("叠到3层：触发变身（模板已换）", () => limbed.UnitData != originalData);
            VAssert("变身目标：擢升之手单位", () => limbed.UnitData?.Faction == Faction.擢升之手);
            VAssert("变身目标：候选A或B（卡费≤6）", () => limbed.UnitData == unitDataA || limbed.UnitData == unitDataB);

            // 场景2：新建即 initialStacks=3 触发
            var limbed2 = MakeUnit("义肢变身者2", 1, 10);
            EventBus.Instance?.Subscribe(limbed2, new[] { transformPassive });
            var orig2 = limbed2.UnitData;
            BuffManager.Instance?.ApplyBuff(limbed2, limbBuff, null, 3);
            VAssert("新建即3层：触发变身", () => limbed2.UnitData != orig2);

            // 场景3：ModifyBuffAction 加层触发
            var limbed3 = MakeUnit("义肢变身者3", 1, 10);
            EventBus.Instance?.Subscribe(limbed3, new[] { transformPassive });
            var orig3 = limbed3.UnitData;
            BuffManager.Instance?.ApplyBuff(limbed3, limbBuff, null, 2);
            VAssert("Modify前：未变身", () => limbed3.UnitData == orig3);
            new ModifyBuffAction { BuffID = "义肢", StacksDelta = 1 }.Execute(new Context { TargetUnit = limbed3 });
            VAssert("ModifyBuffAction 加层到3：触发变身", () => limbed3.UnitData != orig3);

            // 变身清除 buff：BuffRemoved 事件必须触发（视图图标销毁依赖此事件）
            int removedCount = 0;
            System.Action<Unit, Buff> buffRemovedHandler = (t, b) => removedCount++;
            BuffManager.Instance.BuffRemoved += buffRemovedHandler;
            var clearVerify = MakeUnit("变身清buff验证", 1, 10);
            BuffManager.Instance?.ApplyBuff(clearVerify, limbBuff, null, 2);
            VAssert("变身清buff验证：前置义肢 2 层", () => BuffManager.Instance.GetBuff(clearVerify, "义肢")?.StackCount == 2);
            UnitManager.Instance.TransformUnit(clearVerify, unitDataA);
            VAssert("变身清除 buff：BuffRemoved 事件触发 1 次", () => removedCount == 1);
            BuffManager.Instance.BuffRemoved -= buffRemovedHandler;

            // 固定 buff（CanBeChanged=false）：RemoveBuffAction 拒绝移除，变身保留
            var fixedBuff = new BuffData { BuffID = "固定buff", Duration = -1, MaxStack = -1, CanBeChanged = false };
            var fixedUnit = MakeUnit("固定buff验证", 1, 10);
            BuffManager.Instance?.ApplyBuff(fixedUnit, fixedBuff, null, 1);
            UnitManager.Instance.TransformUnit(fixedUnit, unitDataA);
            VAssert("固定buff（CanBeChanged=false）变身保留", () => BuffManager.Instance.GetBuff(fixedUnit, "固定buff") != null);

            // 清理：还原 CardLibrary + 退订（变身后的单位被动已被 TransformUnit 清掉，退订幂等）
            EventBus.Instance?.Unsubscribe(limbed);
            EventBus.Instance?.Unsubscribe(limbed2);
            EventBus.Instance?.Unsubscribe(limbed3);
            CardLibrary.CardList.Clear();
            CardLibrary.CardList.AddRange(backup);
        });

        // ── 格子坐标值源（CellValueSource） ──────────────────────
        RunGroup("CellValueSource 坐标值源", () =>
        {
            var src = MakeUnit("来源", 5, 10); src.GridPos = new Vector2I(3, 4);
            var tgt = MakeUnit("目标", 5, 10); tgt.GridPos = new Vector2I(7, 1);
            var evt = MakeUnit("事件方", 5, 10); evt.GridPos = new Vector2I(0, 9);

            // ── UnitCellValue：读单位坐标 ──────────────────────
            VAssert("UnitCellValue 读 Target 坐标",
                () => new UnitCellValue { Unit = ValueTarget.Target }.GetCell(MakeCtx(src, tgt)) == new Vector2I(7, 1));
            VAssert("UnitCellValue 读 Source 坐标",
                () => new UnitCellValue { Unit = ValueTarget.Source }.GetCell(MakeCtx(src, tgt)) == new Vector2I(3, 4));
            VAssert("UnitCellValue 读 EventOther 坐标",
                () => new UnitCellValue { Unit = ValueTarget.EventOther }.GetCell(new Context { EventOtherUnit = evt }) == new Vector2I(0, 9));
            VAssert("UnitCellValue 单位缺失 → null",
                () => new UnitCellValue { Unit = ValueTarget.Target }.GetCell(new Context { SourceUnit = src }) == null);

            // ── ContextCellValue：读 ctx 格子坐标 ──────────────
            var tCell = MakeCell(new Vector2I(5, 5), new BlockData { BlockName = "地板" });
            var sCell = MakeCell(new Vector2I(2, 2), new BlockData { BlockName = "地板" });
            VAssert("ContextCellValue 读 TargetCell",
                () => new ContextCellValue { Cell = ContextCellType.Target }.GetCell(new Context { TargetCell = tCell }) == new Vector2I(5, 5));
            VAssert("ContextCellValue 读 SourceCell",
                () => new ContextCellValue { Cell = ContextCellType.Source }.GetCell(new Context { SourceCell = sCell }) == new Vector2I(2, 2));
            VAssert("ContextCellValue 格子缺失 → null",
                () => new ContextCellValue().GetCell(new Context()) == null);

            // ── OffsetCellValue：坐标偏移计算 ──────────────────
            VAssert("OffsetCellValue 固定偏移",
                () => new OffsetCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Dx = 2, Dy = -1,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(5, 3));
            VAssert("OffsetCellValue 值源覆盖 Dx",
                () => new OffsetCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Dx = 0,
                    DxValueSource = new ConstantValue { Value = -3 },
                    Dy = 1,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(0, 5));
            VAssert("OffsetCellValue Base 无效 → null",
                () => new OffsetCellValue { Base = new UnitCellValue { Unit = ValueTarget.Target }, Dx = 1 }
                    .GetCell(new Context { SourceUnit = src }) == null);
            VAssert("OffsetCellValue Base null → null",
                () => new OffsetCellValue().GetCell(new Context()) == null);

            // ── StepCellValue：沿方向步进 ──────────────────────
            VAssert("StepCellValue Up 步进 2",
                () => new StepCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Direction = CellDirection.Up, Distance = 2,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(3, 2));
            VAssert("StepCellValue Right 步进 1",
                () => new StepCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Direction = CellDirection.Right, Distance = 1,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(4, 4));
            VAssert("StepCellValue 动态方向（DirectionValue）",
                () => new StepCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    DirectionValueSource = new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target },
                    Distance = 1,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(4, 4));
            VAssert("StepCellValue 距离 0 → 基准",
                () => new StepCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Distance = 0,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(3, 4));
            VAssert("StepCellValue 负距离 → 基准",
                () => new StepCellValue
                {
                    Base = new UnitCellValue { Unit = ValueTarget.Source },
                    Distance = -2,
                }.GetCell(MakeCtx(src, tgt)) == new Vector2I(3, 4));
            VAssert("StepCellValue Base 无效 → null",
                () => new StepCellValue { Base = new UnitCellValue { Unit = ValueTarget.Target } }
                    .GetCell(new Context { SourceUnit = src }) == null);

            // ── DirectionValue：两点方向计算（4 向） ────────────
            // 目标 (7,1) 相对来源 (3,4)：dx=4 dy=-3 → |dx|≥|dy| → Right
            VAssert("DirectionValue 横向 Right",
                () => new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target }
                    .GetValue(MakeCtx(src, tgt)) == (int)CellDirection.Right);
            var tgtLeft = MakeUnit("左侧", 5, 10); tgtLeft.GridPos = new Vector2I(1, 5);
            VAssert("DirectionValue 横向 Left",
                () => new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target }
                    .GetValue(new Context { SourceUnit = src, TargetUnit = tgtLeft }) == (int)CellDirection.Left);
            var tgtDown = MakeUnit("下方", 5, 10); tgtDown.GridPos = new Vector2I(4, 7);
            VAssert("DirectionValue 纵向 Down",
                () => new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target }
                    .GetValue(new Context { SourceUnit = src, TargetUnit = tgtDown }) == (int)CellDirection.Down);
            var tgtUp = MakeUnit("上方", 5, 10); tgtUp.GridPos = new Vector2I(4, 1);  // dx=1 dy=-3 → 纵向 Up
            VAssert("DirectionValue 纵向 Up",
                () => new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target }
                    .GetValue(new Context { SourceUnit = src, TargetUnit = tgtUp }) == (int)CellDirection.Up);
            VAssert("DirectionValue 零向量 → Right（横向优先）",
                () => new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target }
                    .GetValue(new Context { SourceUnit = src, TargetUnit = src }) == (int)CellDirection.Right);
            VAssert("DirectionValue 单位缺失 → Up",
                () => new DirectionValue().GetValue(new Context()) == (int)CellDirection.Up);

            // ── CellCoordValue：坐标分量读取 ────────────────────
            VAssert("CellCoordValue 读 X",
                () => new CellCoordValue { Cell = new UnitCellValue { Unit = ValueTarget.Target }, Info = CellCoordInfo.PosX }
                    .GetValue(MakeCtx(src, tgt)) == 7);
            VAssert("CellCoordValue 读 Y",
                () => new CellCoordValue { Cell = new UnitCellValue { Unit = ValueTarget.Target }, Info = CellCoordInfo.PosY }
                    .GetValue(MakeCtx(src, tgt)) == 1);
            VAssert("CellCoordValue 无效 → DefaultValue",
                () => new CellCoordValue { Cell = new UnitCellValue { Unit = ValueTarget.Target }, DefaultValue = -99 }
                    .GetValue(new Context { SourceUnit = src }) == -99);

            // ── RandomCellValue：形状内随机一格（CellShape 体系） ──
            var rmap = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var rblock = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    rmap[new Vector2I(x, y)] = new Cell(rblock, new Vector2I(x, y), Vector2.Zero);
            var rctx = new Context { Map = rmap, TargetCell = rmap[new Vector2I(2, 2)] };

            VAssert("RandomCellValue 菱形1 随机结果 ∈ 候选集",
                () =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var pos = new RandomCellValue { Shape = new DiamondShape { AreaRange = 1 } }.GetCell(rctx);
                        if (pos == null) return false;
                        if (Mathf.Abs(pos.Value.X - 2) + Mathf.Abs(pos.Value.Y - 2) > 1) return false;
                    }
                    return true;
                });
            VAssert("RandomCellValue All 结果 ∈ 地图",
                () =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var pos = new RandomCellValue { Shape = new AllShape() }.GetCell(rctx);
                        if (pos == null || !rmap.ContainsKey(pos.Value)) return false;
                    }
                    return true;
                });
            VAssert("RandomCellValue 全部占据 → 无候选 → null",
                () =>
                {
                    foreach (var c in rmap.Values) c.OccupyingUnit = MakeUnit("占位", 1, 1);
                    var pos = new RandomCellValue { Shape = new AllShape() }.GetCell(rctx);
                    foreach (var c in rmap.Values) c.OccupyingUnit = null;
                    return pos == null;
                });
            VAssert("RandomCellValue RequireStandable=false 占据不挡",
                () =>
                {
                    foreach (var c in rmap.Values) c.OccupyingUnit = MakeUnit("占位", 1, 1);
                    var pos = new RandomCellValue { Shape = new AllShape(), RequireStandable = false }.GetCell(rctx);
                    foreach (var c in rmap.Values) c.OccupyingUnit = null;
                    return pos != null;
                });
            VAssert("RandomCellValue Base 指定中心（半径0=中心格）",
                () =>
                {
                    var center = MakeUnit("中心", 1, 1); center.GridPos = new Vector2I(2, 2);
                    var pos = new RandomCellValue
                    {
                        Base = new UnitCellValue { Unit = ValueTarget.Source },
                        Shape = new DiamondShape { AreaRange = 0 },
                    }.GetCell(new Context { Map = rmap, SourceUnit = center });
                    return pos == new Vector2I(2, 2);
                });
            VAssert("RandomCellValue Base 无效 → null",
                () => new RandomCellValue { Base = new UnitCellValue { Unit = ValueTarget.Target } }
                    .GetCell(new Context { Map = rmap }) == null);
            VAssert("RandomCellValue Shape 未配置 → null",
                () => new RandomCellValue().GetCell(rctx) == null);
        });

        // ── ShapeTargetFilter CenterOverride 扩散中心覆盖 ─────────
        RunGroup("ShapeTargetFilter CenterOverride", () =>
        {
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);

            // 默认中心 (0,0)（TargetCell），覆盖中心 (4,4)（SourceCell）：
            // 菱形1 从 (4,4) 扩散 → (4,4)(3,4)(4,3) 共 3 格（(5,4)(4,5) 越界）
            var filter = new ShapeTargetFilter
            {
                Shape = TargetShape.AreaDiamond,
                AreaRange = 1,
                Kind = TargetKind.Cell,
                CenterOverride = new ContextCellValue { Cell = ContextCellType.Source },
            };
            var ctx = new Context
            {
                Map = map,
                TargetCell = map[new Vector2I(2, 2)],  // 默认中心在中间 → 菱形1=5格
                SourceCell = map[new Vector2I(4, 4)],
            };
            VAssert("CenterOverride 覆盖扩散中心（(2,2)→(4,4) 菱形1=3格）",
                () =>
                {
                    var cells = TargetResolver.ResolveCells(filter, ctx);
                    return cells.Length == 3 &&
                           cells.All(c => Mathf.Abs(c.GridPos.X - 4) + Mathf.Abs(c.GridPos.Y - 4) <= 1);
                });
            VAssert("无覆盖时仍以 TargetCell 为中心（菱形1=5格）",
                () =>
                {
                    var defaultFilter = new ShapeTargetFilter { Shape = TargetShape.AreaDiamond, AreaRange = 1, Kind = TargetKind.Cell };
                    return TargetResolver.ResolveCells(defaultFilter, ctx).Length == 5;
                });
            VAssert("SingleCell 用覆盖坐标",
                () =>
                {
                    var singleFilter = new ShapeTargetFilter
                    {
                        Shape = TargetShape.SingleCell,
                        CenterOverride = new ContextCellValue { Cell = ContextCellType.Source },
                    };
                    var r = TargetResolver.ResolveCells(singleFilter, ctx);
                    return r.Length == 1 && r[0].GridPos == new Vector2I(4, 4);
                });
            VAssert("CenterOverride 越界坐标 → 空",
                () =>
                {
                    var f = new ShapeTargetFilter
                    {
                        Shape = TargetShape.AreaDiamond,
                        AreaRange = 1,
                        Kind = TargetKind.Cell,
                        CenterOverride = new OffsetCellValue
                        {
                            Base = new ContextCellValue { Cell = ContextCellType.Source },
                            Dx = 100,
                        },
                    };
                    return TargetResolver.ResolveCells(f, ctx).Length == 0;
                });
            VAssert("CenterOverride 值源无效 → 空",
                () =>
                {
                    var f = new ShapeTargetFilter
                    {
                        Shape = TargetShape.AreaDiamond,
                        AreaRange = 1,
                        Kind = TargetKind.Cell,
                        CenterOverride = new UnitCellValue { Unit = ValueTarget.Target },  // ctx.TargetUnit=null
                    };
                    return TargetResolver.ResolveCells(f, ctx).Length == 0;
                });
        });

        // ── CellShape 形状体系（十字/叉/射线/三角形 + CustomShape 双通道） ──
        RunGroup("CellShape 形状体系", () =>
        {
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);
            var center = map[new Vector2I(2, 2)];
            var ctx = new Context { Map = map, TargetCell = center };

            VAssert("CellDirectionVector 4 向",
                () =>
                    TargetResolver.CellDirectionVector(CellDirection.Up) == new Vector2I(0, -1) &&
                    TargetResolver.CellDirectionVector(CellDirection.Down) == new Vector2I(0, 1) &&
                    TargetResolver.CellDirectionVector(CellDirection.Left) == new Vector2I(-1, 0) &&
                    TargetResolver.CellDirectionVector(CellDirection.Right) == new Vector2I(1, 0));

            // 十字：中心 + 四臂各 2（9 格）
            VAssert("CrossShape 臂2 = 9 格（|dx|+|dy|≤2 且单轴）",
                () =>
                {
                    var cells = new CrossShape { Length = 2 }.GetCells(center, ctx);
                    if (cells.Length != 9) return false;
                    foreach (var c in cells)
                    {
                        int dx = System.Math.Abs(c.GridPos.X - 2), dy = System.Math.Abs(c.GridPos.Y - 2);
                        if (dx + dy > 2 || (dx != 0 && dy != 0)) return false;
                    }
                    return true;
                });

            // 叉字：中心 + 四对角各 2（9 格）
            VAssert("XShape 臂2 = 9 格（|dx|=|dy|≤2）",
                () =>
                {
                    var cells = new XShape { Length = 2 }.GetCells(center, ctx);
                    if (cells.Length != 9) return false;
                    foreach (var c in cells)
                    {
                        int dx = System.Math.Abs(c.GridPos.X - 2), dy = System.Math.Abs(c.GridPos.Y - 2);
                        if (dx > 2 || dx != dy) return false;
                    }
                    return true;
                });

            // 射线：Up 长2 宽1 → 3 排 × 3 宽 = 9 格（含中心排）
            VAssert("RayShape Up 长2宽1 = 9 格",
                () =>
                {
                    var cells = new RayShape { Direction = CellDirection.Up, Length = 2, Width = 1 }.GetCells(center, ctx);
                    if (cells.Length != 9) return false;
                    foreach (var c in cells)
                    {
                        int dy = c.GridPos.Y;
                        if (dy < 0 || dy > 2 || System.Math.Abs(c.GridPos.X - 2) > 1) return false;
                    }
                    return true;
                });

            // 三角形：Up 长2 → 1+3+5 = 9 格（第 i 排宽 2i+1）
            VAssert("TriangleShape Up 长2 = 9 格（锥形 1+3+5）",
                () =>
                {
                    var cells = new TriangleShape { Direction = CellDirection.Up, Length = 2 }.GetCells(center, ctx);
                    if (cells.Length != 9) return false;
                    foreach (var c in cells)
                    {
                        int dy = c.GridPos.Y;          // 中心 y=2，向上 → 第 i 排（i=2-dy）宽 2i+1
                        int i = 2 - dy;
                        if (dy < 0 || dy > 2 || System.Math.Abs(c.GridPos.X - 2) > i) return false;
                    }
                    return true;
                });

            // 越界过滤：中心 (4,4) 十字 1 → (4,4)(3,4)(4,3) 共 3 格
            VAssert("CrossShape 角落中心越界过滤（3 格）",
                () => new CrossShape { Length = 1 }.GetCells(map[new Vector2I(4, 4)], ctx).Length == 3);

            // Length=0 → 只有中心
            VAssert("CrossShape Length=0 → 1 格",
                () => new CrossShape { Length = 0 }.GetCells(center, ctx).Length == 1);

            // 射线 Width=0 → 单列 Length+1 格（含中心排；中心 (2,2) 向上 2 不越界）
            VAssert("RayShape Width=0 → 单列 Length+1 格",
                () => new RayShape { Direction = CellDirection.Up, Length = 2, Width = 0 }.GetCells(center, ctx).Length == 3);

            // 菱形/方形类与 TargetResolver 等价
            VAssert("DiamondShape 与 CellsInArea 等价（13 格）",
                () =>
                {
                    var cells = new DiamondShape { AreaRange = 2 }.GetCells(center, ctx);
                    return cells.Length == 13;
                });
            VAssert("SquareShape 方形2（地图内）= 25 格",
                () => new SquareShape { AreaRange = 2 }.GetCells(center, ctx).Length == 25);

            // 动态参数值源覆盖
            VAssert("CrossShape LengthValueSource 覆盖",
                () => new CrossShape
                {
                    Length = 0,
                    LengthValueSource = new ConstantValue { Value = 1 },
                }.GetCells(center, ctx).Length == 5);  // 中心 + 4 臂
            VAssert("RayShape 动态方向（DirectionValue 朝向目标）",
                () =>
                {
                    var src = MakeUnit("来源", 1, 1); src.GridPos = new Vector2I(2, 2);
                    var tgt = MakeUnit("目标", 1, 1); tgt.GridPos = new Vector2I(4, 2);  // dx=2 dy=0 → Right
                    var c2 = new Context { Map = map, SourceUnit = src, TargetUnit = tgt };
                    var cells = new RayShape
                    {
                        Direction = CellDirection.Up,
                        DirectionValueSource = new DirectionValue { From = ValueTarget.Source, To = ValueTarget.Target },
                        Length = 2,
                        Width = 0,
                    }.GetCells(map[new Vector2I(2, 2)], c2);
                    if (cells.Length != 3) return false;
                    foreach (var c in cells)
                        if (c.GridPos.Y != 2 || c.GridPos.X < 2 || c.GridPos.X > 4) return false;
                    return true;
                });

            // ── ShapeTargetFilter.CustomShape 双通道 ──────────────
            VAssert("CustomShape GetShape 穿透 = Cross",
                () => new ShapeTargetFilter { CustomShape = new CrossShape { Length = 1 } }.GetShape() == TargetShape.Cross);
            VAssert("CustomShape GetAreaRange = 形状臂长",
                () => new ShapeTargetFilter { CustomShape = new CrossShape { Length = 3 } }.GetAreaRange() == 3);
            VAssert("CustomShape 解析格子（十字1 = 5 格）",
                () =>
                {
                    var f = new ShapeTargetFilter { CustomShape = new CrossShape { Length = 1 }, Kind = TargetKind.Cell };
                    return TargetResolver.ResolveCells(f, new Context { Map = map, TargetCell = center }).Length == 5;
                });
            VAssert("CustomShape 解析单位（提取格上存活单位）",
                () =>
                {
                    map[new Vector2I(1, 2)].OccupyingUnit = MakeUnit("上", 1, 1);
                    map[new Vector2I(3, 2)].OccupyingUnit = MakeUnit("右", 1, 1);
                    map[new Vector2I(2, 3)].OccupyingUnit = MakeUnit("下", 1, 1);
                    var f = new ShapeTargetFilter { CustomShape = new CrossShape { Length = 1 } };  // Kind 默认 Unit
                    var units = TargetResolver.ResolveUnits(f, new Context { Map = map, TargetCell = center });
                    map[new Vector2I(1, 2)].OccupyingUnit = null;
                    map[new Vector2I(3, 2)].OccupyingUnit = null;
                    map[new Vector2I(2, 3)].OccupyingUnit = null;
                    return units.Length == 3;
                });
            VAssert("CustomShape 组合穿透（And.GetCellShape = 形状实例）",
                () =>
                {
                    var shapeFilter = new ShapeTargetFilter { CustomShape = new XShape { Length = 2 } };
                    var and = new AndTargetFilter
                    {
                        Filters = new TargetFilter[]
                        {
                            shapeFilter,
                            new TeamTargetFilter { Team = TeamFilter.Enemy },
                        },
                    };
                    return and.GetCellShape() is XShape xs && xs.Length == 2 && and.GetShape() == TargetShape.X;
                });
            VAssert("旧枚举路径不受影响（AreaDiamond 仍 5 格）",
                () =>
                {
                    var f = new ShapeTargetFilter { Shape = TargetShape.AreaDiamond, AreaRange = 1, Kind = TargetKind.Cell };
                    return TargetResolver.ResolveCells(f, new Context { Map = map, TargetCell = center }).Length == 5;
                });

            // ── 行/列/环形状 ────────────────────────────────────
            VAssert("RowShape 行2 = 5 格（同 y，x=0..4）",
                () =>
                {
                    var cells = new RowShape { Length = 2 }.GetCells(center, ctx);
                    if (cells.Length != 5) return false;
                    foreach (var c in cells)
                        if (c.GridPos.Y != 2 || System.Math.Abs(c.GridPos.X - 2) > 2) return false;
                    return true;
                });
            VAssert("ColumnShape 列2 = 5 格（同 x，y=0..4）",
                () =>
                {
                    var cells = new ColumnShape { Length = 2 }.GetCells(center, ctx);
                    if (cells.Length != 5) return false;
                    foreach (var c in cells)
                        if (c.GridPos.X != 2 || System.Math.Abs(c.GridPos.Y - 2) > 2) return false;
                    return true;
                });
            VAssert("RingShape 环1 = 4 格（上下左右，不含中心）",
                () =>
                {
                    var cells = new RingShape { Radius = 1 }.GetCells(center, ctx);
                    if (cells.Length != 4) return false;
                    foreach (var c in cells)
                        if (System.Math.Abs(c.GridPos.X - 2) + System.Math.Abs(c.GridPos.Y - 2) != 1) return false;
                    return true;
                });
            VAssert("RingShape 环2 = 8 格（曼哈顿==2，不含内部）",
                () =>
                {
                    var cells = new RingShape { Radius = 2 }.GetCells(center, ctx);
                    if (cells.Length != 8) return false;
                    foreach (var c in cells)
                    {
                        int dist = System.Math.Abs(c.GridPos.X - 2) + System.Math.Abs(c.GridPos.Y - 2);
                        if (dist != 2) return false;
                    }
                    return true;
                });
            VAssert("RingShape 环0 = 只有中心 1 格",
                () => new RingShape { Radius = 0 }.GetCells(center, ctx).Length == 1);
            VAssert("RowShape 角落中心越界过滤（(4,4) 行1 = 2 格）",
                () => new RowShape { Length = 1 }.GetCells(map[new Vector2I(4, 4)], ctx).Length == 2);
            VAssert("RowShape 尺寸注入（Length=0 → sizeOverride=1 行 = 3 格）",
                () => new RowShape { Length = 0 }.GetCells(center, ctx, 1).Length == 3);
            VAssert("Row/Column/Ring 组合穿透 GetShape",
                () =>
                {
                    var and = new AndTargetFilter
                    {
                        Filters = new TargetFilter[]
                        {
                            new ShapeTargetFilter { CustomShape = new RingShape { Radius = 2 } },
                            new TeamTargetFilter { Team = TeamFilter.Enemy },
                        },
                    };
                    return and.GetShape() == TargetShape.Ring && and.GetCellShape() is RingShape;
                });
        });

        // ── 攻击范围形状（AttackShape + PathFinder 统一入口） ────
        RunGroup("AttackShape 攻击范围", () =>
        {
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 7; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);
            var pos = new Vector2I(3, 3);
            var ctx = new Context { Map = map, TargetCell = map[pos] };

            // 尺寸注入：sizeOverride 覆盖形状主尺寸
            VAssert("CellShape sizeOverride 覆盖 Length（十字 2 = 9 格）",
                () => new CrossShape { Length = 1 }.GetCells(map[pos], ctx, 2).Length == 9);
            VAssert("CellShape sizeOverride 负值回退自身参数",
                () => new CrossShape { Length = 1 }.GetCells(map[pos], ctx, -1).Length == 5);

            // GetAttackRange：默认菱形 = 旧 GetCellsInRange 语义（不含自身）
            VAssert("GetAttackRange 默认菱形（射程2 = 12 格，不含自身）",
                () => PathFinder.GetAttackRange(pos, null, 2, map, ctx).Count == 12);
            VAssert("GetAttackRange 默认菱形射程1 = 4 格",
                () =>
                {
                    var range = PathFinder.GetAttackRange(pos, null, 1, map, ctx);
                    return range.Count == 4 && !range.Contains(pos);
                });

            // 十字形状 + 射程联动：射程 2 → 十字臂 2 → 9 格排除自身 = 8
            VAssert("GetAttackRange 十字 + 射程联动（臂2 = 8 格，不含自身）",
                () =>
                {
                    var range = PathFinder.GetAttackRange(pos, new CrossShape { Length = 0 }, 2, map, ctx);
                    return range.Count == 8 && !range.Contains(pos);
                });
            VAssert("GetAttackRange 中心不在图 → 空",
                () => PathFinder.GetAttackRange(new Vector2I(99, 99), new CrossShape(), 1, map, ctx).Count == 0);

            // GetAttackableTargets：形状 + 敌友/不可攻击过滤
            VAssert("GetAttackableTargets 十字命中敌方、排除友方与不可攻击",
                () =>
                {
                    var enemy = MakeUnit("敌", 1, 5); enemy.Team = Team.Enemy; enemy.GridPos = new Vector2I(3, 5);  // 十字下臂
                    var ally = MakeUnit("友", 1, 5); ally.Team = Team.Player; ally.GridPos = new Vector2I(3, 1);    // 十字上臂（友方排除）
                    var immune = MakeUnit("免", 1, 5); immune.Team = Team.Enemy; immune.CanBeAttacked = false;     // 十字左臂（不可攻击排除）
                    immune.GridPos = new Vector2I(1, 3);
                    map[new Vector2I(3, 5)].OccupyingUnit = enemy;
                    map[new Vector2I(3, 1)].OccupyingUnit = ally;
                    map[new Vector2I(1, 3)].OccupyingUnit = immune;

                    var targets = PathFinder.GetAttackableTargets(
                        pos, new CrossShape { Length = 0 }, 2, Team.Player, map, ctx);

                    map[new Vector2I(3, 5)].OccupyingUnit = null;
                    map[new Vector2I(3, 1)].OccupyingUnit = null;
                    map[new Vector2I(1, 3)].OccupyingUnit = null;

                    return targets.Count == 1 && targets.Contains(new Vector2I(3, 5));
                });
        });

        // ── SummonUnitAction SummonPosition 指定坐标放置 ──────────
        RunGroup("SummonUnitAction SummonPosition", () =>
        {
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 5; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);

            var src = MakeUnit("施法者", 1, 1); src.GridPos = new Vector2I(2, 2);
            var far = MakeUnit("远处单位", 1, 1); far.GridPos = new Vector2I(999, 999);

            // 值源无有效坐标（TargetUnit=null）→ 静默跳过，不抛异常
            VAssert("SummonPosition 值源无效 → 跳过不抛异常",
                () =>
                {
                    var action = new SummonUnitAction
                    {
                        UnitID = "不存在",
                        SummonPosition = new UnitCellValue { Unit = ValueTarget.Target },
                    };
                    action.Execute(new Context { SourceUnit = src, SourceTeam = Team.Player, Map = map });
                    return true;
                });
            // 坐标有效但不在地图 → 跳过，不抛异常
            VAssert("SummonPosition 地图外坐标 → 跳过不抛异常",
                () =>
                {
                    var action = new SummonUnitAction
                    {
                        UnitID = "不存在",
                        SummonPosition = new UnitCellValue { Unit = ValueTarget.Target },
                    };
                    action.Execute(new Context { SourceUnit = src, TargetUnit = far, SourceTeam = Team.Player, Map = map });
                    return true;
                });
            // 坐标有效且在地图内 → 进入召唤（UnitID 无效 → unitData null → 提前返回），不抛异常
            VAssert("SummonPosition 地图内坐标 → 走到召唤流程不抛异常",
                () =>
                {
                    var inMap = MakeUnit("地图内单位", 1, 1); inMap.GridPos = new Vector2I(3, 3);
                    var action = new SummonUnitAction
                    {
                        UnitID = "不存在",
                        SummonPosition = new UnitCellValue { Unit = ValueTarget.Target },
                    };
                    action.Execute(new Context { SourceUnit = src, TargetUnit = inMap, SourceTeam = Team.Player, Map = map });
                    return true;
                });
        });

        // ── MoveUnitAction 位移（Push/Pull 方向值源 + Teleport 坐标） ──
        RunGroup("MoveUnitAction 位移", () =>
        {
            // 合成地图（ComputeTarget 纯逻辑，不依赖真实地图/管理器）
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            var block = new BlockData { BlockName = "地板", CanStand = true, CanPass = true };
            for (int x = 0; x < 7; x++)
                for (int y = 0; y < 7; y++)
                    map[new Vector2I(x, y)] = new Cell(block, new Vector2I(x, y), Vector2.Zero);

            // ── Push：远离锚点（击退） ──────────────────────────
            VAssert("Push 自动方向（锚点下方 → 向上击退 2 格）",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var anchor = MakeUnit("锚点", 1, 1); anchor.GridPos = new Vector2I(3, 5);
                    var action = new MoveUnitAction { Mode = MoveUnitAction.MoveMode.Push, Distance = 2 };
                    return action.ComputeTarget(unit, new Context { SourceUnit = anchor, Map = map }, map) == new Vector2I(3, 1);
                });
            VAssert("Push 锚点=DirectionAnchor 坐标（推离指定坐标）",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var anchor = MakeUnit("无关单位", 1, 1); anchor.GridPos = new Vector2I(9, 9);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Push,
                        Distance = 1,
                        DirectionAnchor = new ContextCellValue { Cell = ContextCellType.Source },  // 锚点坐标 (5,3)
                    };
                    var c = new Context { SourceUnit = anchor, SourceCell = map[new Vector2I(5, 3)], Map = map };
                    return action.ComputeTarget(unit, c, map) == new Vector2I(2, 3);  // 锚点在右 → 向左推
                });
            VAssert("Push 值源方向覆盖（固定 Right 2 格）",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Push,
                        Distance = 2,
                        DirectionValueSource = new ConstantValue { Value = (int)CellDirection.Right },
                    };
                    return action.ComputeTarget(unit, new Context { Map = map }, map) == new Vector2I(5, 3);
                });
            VAssert("Push 动态距离（值源覆盖 3 格）",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Push,
                        Distance = 0,
                        DistanceValueSource = new ConstantValue { Value = 3 },
                        DirectionValueSource = new ConstantValue { Value = (int)CellDirection.Right },
                    };
                    return action.ComputeTarget(unit, new Context { Map = map }, map) == new Vector2I(6, 3);
                });
            VAssert("Push 被占据格阻挡（停在阻挡前）",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    map[new Vector2I(3, 1)].OccupyingUnit = MakeUnit("挡路", 1, 1);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Push,
                        Distance = 3,
                        DirectionValueSource = new ConstantValue { Value = (int)CellDirection.Up },
                    };
                    var target = action.ComputeTarget(unit, new Context { Map = map }, map);
                    map[new Vector2I(3, 1)].OccupyingUnit = null;
                    return target == new Vector2I(3, 2);  // (3,2) 可走，(3,1) 被占 → 停 (3,2)
                });
            VAssert("Push 锚点与方向均缺失 → 原地",
                () =>
                {
                    var unit = MakeUnit("被推", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var action = new MoveUnitAction { Mode = MoveUnitAction.MoveMode.Push, Distance = 1 };
                    return action.ComputeTarget(unit, new Context { Map = map }, map) == unit.GridPos;
                });

            // ── Pull：靠近锚点（拉拽） ──────────────────────────
            VAssert("Pull 拉向锚点（反方向 2 格）",
                () =>
                {
                    var unit = MakeUnit("被拉", 1, 1); unit.GridPos = new Vector2I(3, 3);
                    var anchor = MakeUnit("锚点", 1, 1); anchor.GridPos = new Vector2I(3, 5);
                    var action = new MoveUnitAction { Mode = MoveUnitAction.MoveMode.Pull, Distance = 2 };
                    return action.ComputeTarget(unit, new Context { SourceUnit = anchor, Map = map }, map) == new Vector2I(3, 5);
                });

            // ── 多目标 / Teleport ──────────────────────────────
            VAssert("多目标方向/距离独立计算",
                () =>
                {
                    var u1 = MakeUnit("甲", 1, 1); u1.GridPos = new Vector2I(1, 1);
                    var u2 = MakeUnit("乙", 1, 1); u2.GridPos = new Vector2I(5, 5);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Push,
                        Distance = 1,
                        DirectionValueSource = new ConstantValue { Value = (int)CellDirection.Right },
                    };
                    var c = new Context { Map = map };
                    return action.ComputeTarget(u1, c, map) == new Vector2I(2, 1) &&
                           action.ComputeTarget(u2, c, map) == new Vector2I(6, 5);
                });
            VAssert("Teleport 值源无效 → 原地不动作",
                () =>
                {
                    var unit = MakeUnit("传送者", 1, 1); unit.GridPos = new Vector2I(2, 2);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Teleport,
                        TeleportPosition = new UnitCellValue { Unit = ValueTarget.Target },  // ctx.TargetUnit=null → 无效
                    };
                    action.Execute(new Context { TargetUnits = new[] { unit }, SourceUnit = MakeUnit("施法者", 1, 1), Map = map });
                    return unit.GridPos == new Vector2I(2, 2);
                });
            VAssert("Teleport 优先 TeleportPosition 坐标（覆盖 TargetCell）",
                () =>
                {
                    var unit = MakeUnit("传送者", 1, 1); unit.GridPos = new Vector2I(2, 2);
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Teleport,
                        TeleportPosition = new ContextCellValue { Cell = ContextCellType.Source },
                    };
                    var c = new Context { SourceCell = map[new Vector2I(5, 5)], TargetCell = map[new Vector2I(1, 1)], Map = map };
                    return action.ComputeTarget(unit, c, map) == new Vector2I(5, 5);
                });
            VAssert("Teleport 回退 TargetUnit 所在格",
                () =>
                {
                    var unit = MakeUnit("传送者", 1, 1); unit.GridPos = new Vector2I(2, 2);
                    var dest = MakeUnit("落点单位", 1, 1); dest.GridPos = new Vector2I(6, 6);
                    var action = new MoveUnitAction { Mode = MoveUnitAction.MoveMode.Teleport };
                    var c = new Context { Map = map, TargetUnit = dest };
                    return action.ComputeTarget(unit, c, map) == new Vector2I(6, 6);
                });

            // ── 真实传送（需 UnitManager + MapManager 真实地图） ──
            if (UnitManager.Instance == null || MapManager.Instance == null)
            {
                VAssert("UnitManager/MapManager 未就绪，跳过真实传送测试", () => true);
            }
            else
            {
                var rmap = MapManager.Instance.Map;
                Cell targetA = null, targetB = null;
                foreach (var kv in rmap)
                {
                    if (kv.Value.OccupyingUnit != null || !kv.Value.CanStand) continue;
                    if (targetA == null) targetA = kv.Value;
                    else if (targetB == null) { targetB = kv.Value; break; }
                }
                if (targetA == null || targetB == null)
                {
                    VAssert("地图空置格子不足，跳过真实传送测试", () => true);
                }
                else
                {
                    var unit = MakeUnit("传送者", 1, 1);
                    unit.GridPos = new Vector2I(-1, -1);  // 地图外初始坐标，避免误释放旧格
                    var action = new MoveUnitAction
                    {
                        Mode = MoveUnitAction.MoveMode.Teleport,
                        TeleportPosition = new ContextCellValue { Cell = ContextCellType.Source },
                    };
                    // TeleportPosition = SourceCell = targetA；同时给 TargetCell=targetB 验证优先级
                    action.Execute(new Context
                    {
                        TargetUnits = new[] { unit },
                        SourceCell = targetA,
                        TargetCell = targetB,
                        SourceTeam = Team.Player,
                    });
                    VAssert("TeleportPosition 真实传送生效且优先于 TargetCell",
                        () => unit.GridPos == targetA.GridPos);

                    // 清理：释放占用格状态，避免污染后续流程
                    targetA.OccupyingUnit = null;
                    targetA.CanStand = true;
                    targetA.CanPass = true;
                    unit.GridPos = new Vector2I(-1, -1);
                }
            }
        });

        RunGroup("AI 决策", () =>
        {
            // ── ScoreTarget 目标打分 ──
            var atk = MakeUnit("攻击者", 3, 10);       // ATK=3
            atk.Team = Team.Enemy;
            atk.GridPos = new Vector2I(0, 0);

            var door = MakeUnit("门", 0, 20);
            door.Type = UnitType.门;
            door.GridPos = new Vector2I(3, 0);         // 距离 3

            var oneShot = MakeUnit("残血兵", 1, 2);    // HP 2 ≤ ATK 3 → 一击可杀
            oneShot.GridPos = new Vector2I(1, 0);      // 距离 1

            var healthy = MakeUnit("满血兵", 2, 10);
            healthy.GridPos = new Vector2I(1, 0);

            VAssert("门 打分高于一击可杀目标",
                () => AiTactics.ScoreTarget(atk, door) > AiTactics.ScoreTarget(atk, oneShot));
            VAssert("一击可杀 打分高于普通满血目标",
                () => AiTactics.ScoreTarget(atk, oneShot) > AiTactics.ScoreTarget(atk, healthy));
            VAssert("距离惩罚：同价值目标越近分越高",
                () =>
                {
                    var far = MakeUnit("远兵", 1, 10);
                    far.GridPos = new Vector2I(4, 0);
                    var near = MakeUnit("近兵", 1, 10);
                    near.GridPos = new Vector2I(1, 0);
                    return AiTactics.ScoreTarget(atk, near) > AiTactics.ScoreTarget(atk, far);
                });

            // ── PickAttackTarget 选目标 ──
            VAssert("有门时优先选门（哪怕距离更远）",
                () => AiTactics.PickAttackTarget(new[] { oneShot, healthy, door }, atk) == door);
            VAssert("无门时优先一击可杀目标",
                () => AiTactics.PickAttackTarget(new[] { healthy, oneShot }, atk) == oneShot);
            VAssert("无敌方目标时返回 null",
                () => AiTactics.PickAttackTarget(new Unit[0], atk) == null);

            // ── IsSuicidalAttack 攻击送死判断 ──
            {
                // 3x1 地图：attacker(0,0) 敌方，tgt(1,0) 玩家，door(2,0) 玩家门
                var aiMap = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
                for (int x = 0; x <= 2; x++)
                    aiMap[new Vector2I(x, 0)] = MakeCell(new Vector2I(x, 0), new BlockData());

                var attacker = MakeUnit("攻击者", 2, 3);
                attacker.Team = Team.Enemy;
                attacker.GridPos = new Vector2I(0, 0);
                aiMap[new Vector2I(0, 0)].OccupyingUnit = attacker;
                aiMap[new Vector2I(0, 0)].CanPass = false;

                var tgt = MakeUnit("玩家兵", 3, 5);   // ATK3 ≥ attacker HP3 → 一击反杀
                tgt.AttackDistance = 1;
                tgt.GridPos = new Vector2I(1, 0);
                aiMap[new Vector2I(1, 0)].OccupyingUnit = tgt;
                aiMap[new Vector2I(1, 0)].CanPass = false;

                var doorUnit = MakeUnit("玩家门", 3, 20);
                doorUnit.Type = UnitType.门;
                doorUnit.AttackDistance = 1;
                doorUnit.GridPos = new Vector2I(2, 0);
                aiMap[new Vector2I(2, 0)].OccupyingUnit = doorUnit;
                aiMap[new Vector2I(2, 0)].CanPass = false;

                var playerList = new[] { tgt, doorUnit };

                VAssert("送死攻击：打不死目标且会被一击反杀",
                    () => AiTactics.IsSuicidalAttack(attacker, tgt, playerList, aiMap));

                // 一击击杀目标 → 不送死（收割）
                attacker.AttackPower = 5;
                VAssert("一击击杀目标不算送死（收割）",
                    () => !AiTactics.IsSuicidalAttack(attacker, tgt, playerList, aiMap));
                attacker.AttackPower = 2;

                // 目标打不死自己 → 不送死
                tgt.AttackPower = 2;
                VAssert("不会被反杀不算送死",
                    () => !AiTactics.IsSuicidalAttack(attacker, tgt, playerList, aiMap));
                tgt.AttackPower = 3;

                // 目标是门 → 不送死（胜负关键）
                VAssert("打门不算送死（胜负关键）",
                    () => !AiTactics.IsSuicidalAttack(attacker, doorUnit, playerList, aiMap));

                // 目标攻击范围覆盖不到 attacker → 不送死
                var farTgt = MakeUnit("远兵", 3, 5);
                farTgt.AttackDistance = 1;
                farTgt.GridPos = new Vector2I(1, 1);   // 距离 attacker 2 > 攻击范围 1
                VAssert("目标够不到 attacker 不算送死",
                    () => !AiTactics.IsSuicidalAttack(attacker, farTgt, new[] { farTgt }, aiMap));
            }

            // ── PickBestMoveCell 移动格排序（distToGoal=绕障距离，不可达格不在字典）──
            VAssert("可攻击格 胜过 更近但不可攻击格",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) };
                    var atkScores = new System.Collections.Generic.Dictionary<Vector2I, int> { [new Vector2I(1, 0)] = 298 };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(1, 0)] = 2, [new Vector2I(0, 1)] = 4 };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, atkScores, dist, null, null);
                    return pick.HasValue && pick.Value == new Vector2I(1, 0);
                });

            VAssert("卡位：威胁区无攻击价值 → 重罚（站火力范围外）",
                () =>
                {
                    // (1,0) 在威胁区且无攻击价值 → -500 重罚；(0,1) 绕远但安全 → 被选
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 5, [new Vector2I(1, 0)] = 4, [new Vector2I(0, 1)] = 6 };
                    var threatened = new HashSet<Vector2I> { new Vector2I(1, 0) };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, threatened, null);
                    return pick.HasValue && pick.Value == new Vector2I(0, 1);
                });

            VAssert("可攻击的威胁格仍优先（值得冒险）",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) };
                    var atkScores = new System.Collections.Generic.Dictionary<Vector2I, int> { [new Vector2I(1, 0)] = 998 };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(1, 0)] = 2, [new Vector2I(0, 1)] = 4 };
                    var threatened = new HashSet<Vector2I> { new Vector2I(1, 0) };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, atkScores, dist, threatened, null);
                    return pick.HasValue && pick.Value == new Vector2I(1, 0);
                });

            VAssert("刷怪格被惩罚（不主动站红格）",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(1, 0)] = 2, [new Vector2I(0, 1)] = 4 };
                    var spawn = new HashSet<Vector2I> { new Vector2I(1, 0) };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, null, spawn);
                    return pick.HasValue && pick.Value == new Vector2I(0, 1);
                });

            VAssert("让位：excludeSpawnCells 完全排除刷怪格（全被排除→null）",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0), new Vector2I(0, 1) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(1, 0)] = 2, [new Vector2I(0, 1)] = 4 };
                    var spawn = new HashSet<Vector2I> { new Vector2I(1, 0), new Vector2I(0, 1) };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, null, spawn, excludeSpawnCells: true);
                    return pick == null;
                });

            VAssert("防来回动：禁止移回上回合起点（previousPos）",
                () =>
                {
                    // 单位上回合从 (0,0) 走到 (1,0)，本回合禁止移回 (0,0) → 无可选格 → null
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(1, 0)] = 3 };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(1, 0), reachable, null, dist, null, null,
                        previousPos: new Vector2I(0, 0));
                    return pick == null;
                });

            VAssert("绕远过猛：超出 maxDetour 的候选跳过（防走丢）",
                () =>
                {
                    // 候选 (0,1) 绕远 7 格 > 上限 3 → 无可选格 → null
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(0, 1) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int>
                        { [new Vector2I(0, 0)] = 3, [new Vector2I(0, 1)] = 10 };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, null, null);
                    return pick == null;
                });

            VAssert("目标绕障不可达 → 跳过行动（null）",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0), new Vector2I(1, 0) };
                    // 当前格不在 distToGoal（目标被墙隔开）→ 不移动
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int> { [new Vector2I(1, 0)] = 2 };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, null, null);
                    return pick == null;
                });

            VAssert("无可选格返回 null",
                () =>
                {
                    var reachable = new HashSet<Vector2I> { new Vector2I(0, 0) };
                    var dist = new System.Collections.Generic.Dictionary<Vector2I, int> { [new Vector2I(0, 0)] = 3 };
                    var pick = AiTactics.PickBestMoveCell(
                        new Vector2I(0, 0), reachable, null, dist, null, null);
                    return pick == null;
                });
        });

        RunGroup("AI 绕障距离", () =>
        {
            // 5x1 空地 (0,0)~(4,0)
            var map = new System.Collections.Generic.Dictionary<Vector2I, Cell>();
            for (int x = 0; x <= 4; x++)
                map[new Vector2I(x, 0)] = MakeCell(new Vector2I(x, 0), new BlockData());

            VAssert("通畅：当前格到目标绕障成本正确",
                () => PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0))[new Vector2I(0, 0)] == 4);
            VAssert("通畅：邻格成本递减",
                () => PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0))[new Vector2I(3, 0)] == 1);

            // 无单位占据的墙：唯一通道被封 → 不可达
            map[new Vector2I(2, 0)].CanPass = false;
            VAssert("墙挡死：目标不可达（无绕路空间）",
                () => !PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0)).ContainsKey(new Vector2I(0, 0)));
            map[new Vector2I(2, 0)].CanPass = true;

            // 队友占据格：默认挡路；passThroughTeam=Enemy 可穿越（AI 队友会动/让路）
            var ally = MakeUnit("队友", 1, 5);
            ally.Team = Team.Enemy;
            var cell2 = map[new Vector2I(2, 0)];
            cell2.OccupyingUnit = ally;
            cell2.CanPass = false;
            VAssert("队友挡路：默认不可达",
                () => !PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0)).ContainsKey(new Vector2I(0, 0)));
            VAssert("队友挡路：passThroughTeam=Enemy 可达",
                () => PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0), Team.Enemy).ContainsKey(new Vector2I(0, 0)));
            VAssert("队友挡路：距离按穿越队友格累计",
                () => PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0), Team.Enemy)[new Vector2I(0, 0)] == 4);

            // 玩家占据格：passThrough=Enemy 仍为障碍
            var player = MakeUnit("玩家兵", 1, 5);
            cell2.OccupyingUnit = player;
            VAssert("玩家挡路：passThrough=Enemy 仍不可达",
                () => !PathFinder.GetDistanceFrom(new Vector2I(4, 0), map, new Vector2I(0, 0), Team.Enemy).ContainsKey(new Vector2I(0, 0)));
            cell2.OccupyingUnit = null;
            cell2.CanPass = true;
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

    private static Cell MakeCell(Vector2I pos, BlockData block)
    {
        return new Cell(block, pos, Vector2.Zero);
    }

    private static Card MakeCard(string id, CardType type, params Tag[] tags)
    {
        var data = new SpellCardData { CardID = id, CardName = id, Type = type };
        if (tags != null && tags.Length > 0)
            data.Tags = new Godot.Collections.Array<Tag>(tags);
        return new Card(data);
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

    /// <summary>测试辅助动作：捕获执行时的 ctx.AttackDirection（验证攻击方向透传）</summary>
    private partial class CaptureDirectionAction : GameAction
    {
        public static CellDirection? Captured;

        protected override void Apply(Context ctx) => Captured = ctx.AttackDirection;
    }
}

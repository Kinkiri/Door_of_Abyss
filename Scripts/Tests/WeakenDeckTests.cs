using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 「凛冬戒律」卡组（霜原·禁欲修会，削弱/限制主题）专项测试。
/// 与 TestRunner 同模式：挂到战斗场景任意节点下，勾选 RunTestsOnReady 运行。
/// 覆盖：20 张新卡 + 7 Buff + 3 环境 + 2 装备 + 8 单位的配置加载与机制行为
/// （叠层削弱、AP/体力限制、永久压血、负面装备、变身、环境被动、单位被动）。
/// 测试使用真实 .tres 资源（ResourceLoader 加载），验证配置与逻辑联动。
/// </summary>
[GlobalClass]
public partial class WeakenDeckTests : Node
{
    private int _passed = 0;
    private int _failed = 0;
    private int _total = 0;
    private readonly List<string> _errors = new();
    private string _currentGroup = "";

    /// <summary>场景加载后是否自动运行。默认关闭（同 TestRunner，防污染实际战斗）</summary>
    [Export] public bool RunTestsOnReady { get; set; } = false;

    public override void _Ready()
    {
        if (!RunTestsOnReady)
        {
            GD.Print("[WeakenDeckTests] 已禁用（RunTestsOnReady=false）");
            return;
        }
        CallDeferred(nameof(RunAll));
    }

    private void RunAll()
    {
        GD.PrintRaw("\n==============================\n");
        GD.PrintRaw("  凛冬戒律卡组专项测试\n");
        GD.PrintRaw("==============================\n");

        ResourceLoading();
        BuffMechanics();
        EquipMechanics();
        TransformMechanics();
        EconomyUnits();
        BossSkills();
        UnitPassives();
        EnvironmentPassives();

        GD.PrintRaw($"\n==============================\n");
        GD.PrintRaw($"  结果: {_passed}/{_total} 通过");
        if (_failed > 0)
        {
            GD.PrintRaw($"  ({_failed} 失败)\n");
            GD.PrintErr($"[WeakenDeckTests] 失败 {_failed} 项:\n" + string.Join("\n", _errors));
        }
        else
        {
            GD.PrintRaw("  全部通过 ✓\n");
        }
        QueueFree();
    }

    // ======================================================================
    // 1. 资源加载：40 个新资源全部可加载、关键字段正确
    // ======================================================================
    private void ResourceLoading()
    {
        RunGroup("资源加载", () =>
        {
            var cards = new (string file, string id, CardType type)[]
            {
                ("苦修", "苦修", CardType.Spell),
                ("疲惫", "疲惫", CardType.Spell),
                ("缴械", "缴械", CardType.Spell),
                ("禁足", "禁足", CardType.Spell),
                ("戒律裁决", "戒律裁决", CardType.Spell),
                ("苦行烙印", "苦行烙印", CardType.Spell),
                ("枷锁术", "枷锁术", CardType.Spell),
                ("忏悔", "忏悔", CardType.Spell),
                ("禁魔领域", "禁魔领域", CardType.Environment),
                ("霜沼", "霜沼", CardType.Environment),
                ("凛冬祭坛", "凛冬祭坛", CardType.Environment),
                ("寒铁枷锁", "寒铁枷锁", CardType.Equipment),
                ("惩罚法典", "惩罚法典", CardType.Equipment),
                ("苦行僧", "苦行僧", CardType.Unit),
                ("忏悔者", "忏悔者", CardType.Unit),
                ("裁判官", "裁判官", CardType.Unit),
                ("缄默圣女", "缄默圣女", CardType.Unit),
                ("锁链卫兵", "锁链卫兵", CardType.Unit),
                ("戒律殿堂", "戒律殿堂", CardType.Unit),
                ("苦行信众", "苦行信众", CardType.Unit),
                ("冰晶祭坛", "冰晶祭坛", CardType.Unit),
            };
            foreach (var (file, id, type) in cards)
            {
                var c = LoadResource<CardData>($"res://Resource/Data/Cards/4禁欲修会/{file}.tres");
                VAssert($"卡牌[{id}] 加载且 ID 正确", () => c != null && c.CardID == id);
                VAssert($"卡牌[{id}] 类型={type}", () => c != null && c.Type == type);
                VAssert($"卡牌[{id}] 势力=禁欲修会(5)", () => c != null && (int)c.Faction == 5);
                VAssert($"卡牌[{id}] 标签含冰霜(10)", () => c != null && c.Tags != null && c.Tags.Contains(Tag.冰霜));
            }

            var buffs = new string[] { "苦修", "疲惫", "缴械", "禁足", "枷锁", "腐朽", "泥泞" };
            foreach (var id in buffs)
            {
                var b = LoadResource<BuffData>($"res://Resource/Data/Buff/{id}.tres");
                VAssert($"Buff[{id}] 加载且 ID 正确", () => b != null && b.BuffID == id);
            }

            var envs = new string[] { "禁魔领域", "霜沼", "凛冬祭坛" };
            foreach (var id in envs)
            {
                var e = LoadResource<EnvironmentData>($"res://Resource/Data/Environments/{id}.tres");
                VAssert($"环境[{id}] 加载且 ID 正确", () => e != null && e.EnvironmentID == id);
            }

            var equips = new string[] { "寒铁枷锁", "惩罚法典" };
            foreach (var id in equips)
            {
                var e = LoadResource<EquipmentData>($"res://Resource/Data/Equipment/{id}.tres");
                VAssert($"装备[{id}] 加载且 ID 正确", () => e != null && e.EquipmentID == id);
            }

            var units = new (string id, int hp)[] {
                ("苦行者", 2), ("苦行僧", 5), ("忏悔者", 3), ("裁判官", 4),
                ("缄默圣女", 5), ("锁链卫兵", 5), ("戒律殿堂", 7),
                ("苦行信众", 3), ("冰晶祭坛", 5),
            };
            foreach (var (id, hp) in units)
            {
                var u = LoadResource<UnitData>($"res://Resource/Data/Units/4禁欲修会/{id}.tres");
                VAssert($"单位[{id}] 加载且 ID 正确", () => u != null && u.UnitID == id);
                VAssert($"单位[{id}] HP={hp}", () => u != null && u.HealthPoints == hp);
            }
        });
    }

    // ======================================================================
    // 2. 削弱 Buff 机制（叠层/到期/还原/透支）
    // ======================================================================
    private void BuffMechanics()
    {
        RunGroup("苦修 叠层削弱", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/苦修.tres");
            var unit = MakeUnit("苦修靶", 6, 10);
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("1层苦修 ATK-1", () => unit.AttackPower == 5);
            bm.ApplyBuff(unit, buff, null, 2);
            VAssert("叠层后共3层 ATK-3", () => unit.AttackPower == 3);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "苦修"));
            VAssert("移除苦修 ATK 还原", () => unit.AttackPower == 6);
        });

        RunGroup("缴械 攻击归零", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/缴械.tres");
            var unit = MakeUnit("缴械靶", 4, 10);
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("缴械 ATK-5 → 归零", () => unit.AttackPower <= 0);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "缴械"));
            VAssert("移除缴械 ATK 还原", () => unit.AttackPower == 4);
        });

        RunGroup("疲惫 行动限制", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/疲惫.tres");
            var unit = MakeUnit("疲惫靶", 2, 10);
            unit.MaxActionPoints = 2; unit.ActionPoints = 2;
            unit.Stamina = 2;
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("当前AP-2 → 0", () => unit.ActionPoints == 0);
            VAssert("AP上限不动（仅透支当前）", () => unit.MaxActionPoints == 2);
            VAssert("体力-1", () => unit.Stamina == 1);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "疲惫"));
            VAssert("还原后当前AP回满", () => unit.ActionPoints == 2);
            VAssert("还原后体力恢复", () => unit.Stamina == 2);
        });

        RunGroup("禁足 移动限制", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/禁足.tres");
            var unit = MakeUnit("禁足靶", 2, 10);
            unit.Stamina = 3;
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("体力-9 → 归零", () => unit.Stamina <= 0);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "禁足"));
            VAssert("还原后体力恢复", () => unit.Stamina == 3);
        });

        RunGroup("枷锁 成长削弱", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/枷锁.tres");
            var unit = MakeUnit("枷锁靶", 5, 10);
            unit.Stamina = 3;
            bm.ApplyBuff(unit, buff, null, 2);
            VAssert("2层枷锁 ATK-2 体力-2", () => unit.AttackPower == 3 && unit.Stamina == 1);
            bm.ApplyBuff(unit, buff, null, 2);
            VAssert("再叠2层 ATK-4 体力-4", () => unit.AttackPower == 1 && unit.Stamina == -1);
            var modify = new ModifyBuffAction { BuffID = "枷锁", StacksDelta = -1 };
            modify.Execute(new Context { TargetUnit = unit });
            VAssert("减1层还原 → ATK-3 体力-3", () => unit.AttackPower == 2 && unit.Stamina == 0);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "枷锁"));
            VAssert("移除枷锁全部还原", () => unit.AttackPower == 5 && unit.Stamina == 3);
        });

        RunGroup("腐朽 永久压血", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/腐朽.tres");
            var unit = MakeUnit("腐朽靶", 3, 10);
            unit.CurrentHP = 10;
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("MaxHP-3 → 7", () => unit.MaxHP == 7);
            VAssert("当前HP同步-3 → 7", () => unit.CurrentHP == 7);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "腐朽"));
            VAssert("移除后 MaxHP 恢复", () => unit.MaxHP == 10);
            VAssert("移除后当前HP不恢复（永久损失3血）", () => unit.CurrentHP == 7);
        });

        RunGroup("泥泞 减速", () =>
        {
            var bm = BuffManager.Instance;
            if (bm == null) { VAssert("BuffManager 未就绪，跳过", () => true); return; }
            var buff = LoadResource<BuffData>("res://Resource/Data/Buff/泥泞.tres");
            var unit = MakeUnit("泥泞靶", 1, 5);
            unit.Stamina = 2;
            bm.ApplyBuff(unit, buff, null, 1);
            VAssert("泥泞体力-1", () => unit.Stamina == 1);
            bm.RemoveBuff(unit, bm.GetBuff(unit, "泥泞"));
            VAssert("移除泥泞还原", () => unit.Stamina == 2);
        });
    }

    // ======================================================================
    // 3. 负面装备（寒铁枷锁：装备给敌方）
    // ======================================================================
    private void EquipMechanics()
    {
        RunGroup("寒铁枷锁 负面装备", () =>
        {
            var eqm = EquipmentManager.Instance;
            var bm = BuffManager.Instance;
            if (eqm == null || bm == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var equip = LoadResource<EquipmentData>("res://Resource/Data/Equipment/寒铁枷锁.tres");
            var enemy = MakeUnit("戴枷敌人", 5, 10);
            enemy.Stamina = 2;
            eqm.Equip(enemy, equip, null);
            VAssert("装备后 ATK-2", () => enemy.AttackPower == 3);
            VAssert("装备后 体力-1", () => enemy.Stamina == 1);
            eqm.RemoveAllEquipments(enemy);
            VAssert("卸下后 ATK 还原", () => enemy.AttackPower == 5);
            VAssert("卸下后 体力还原", () => enemy.Stamina == 2);
        });

        RunGroup("惩罚法典 攻击触发枷锁", () =>
        {
            var eqm = EquipmentManager.Instance;
            var eb = EventBus.Instance;
            if (eqm == null || eb == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var equip = LoadResource<EquipmentData>("res://Resource/Data/Equipment/惩罚法典.tres");
            var attacker = MakeUnit("法典持有者", 3, 10);
            var victim = MakeUnit("法典受害者", 4, 10);
            victim.Stamina = 2;
            eqm.Equip(attacker, equip, null);
            // 攻击后事件：SourceUnit=攻击者，TargetUnit=受击者，instigator=攻击者
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = attacker,
                TargetUnit = victim,
                SourceTeam = attacker.Team,
            }, attacker);
            VAssert("造成伤害后目标获得1层枷锁", () => BuffManager.Instance.HasBuff(victim, "枷锁"));
            VAssert("目标 ATK-1", () => victim.AttackPower == 3);
            VAssert("目标 体力-1", () => victim.Stamina == 1);
            eqm.RemoveAllEquipments(attacker);
        });
    }

    // ======================================================================
    // 4. 变身机制（苦行烙印/殉道者 → 苦行者）
    // ======================================================================
    private void TransformMechanics()
    {
        RunGroup("变羊术 苦行者", () =>
        {
            var um = UnitManager.Instance;
            if (um == null) { VAssert("UnitManager 未就绪，跳过", () => true); return; }
            var martyrData = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/苦行者.tres");
            var victim = MakeUnit("被变羊者", 9, 30);
            victim.Stamina = 5;
            victim.Team = Team.Enemy;
            // 施加一个 Buff 验证变身清除
            var bm = BuffManager.Instance;
            var strong = LoadResource<BuffData>("res://Resource/Data/Buff/苦修.tres");
            bm.ApplyBuff(victim, strong, null, 1);

            var action = new TransformUnitAction { UnitData = martyrData };
            action.Execute(new Context { TargetUnit = victim });
            VAssert("变身苦行者：UnitID 正确", () => victim.UnitData.UnitID == "苦行者");
            VAssert("变身重置属性（HP=2）", () => victim.MaxHP == 2 && victim.CurrentHP == 2);
            VAssert("变身重置属性（ATK=1）", () => victim.AttackPower == 1);
            VAssert("变身清空 Buff", () => !bm.HasBuff(victim, "苦修"));
            VAssert("变身保留阵营", () => victim.Team == Team.Enemy);
        });
    }

    // ======================================================================
    // 4.5 产费单位（苦行信众/冰晶祭坛：RoundStart 生产费用/抽牌）
    // ======================================================================
    private void EconomyUnits()
    {
        RunGroup("产费单位", () =>
        {
            var eb = EventBus.Instance;
            var bm = BattleManager.Instance;
            if (eb == null || bm == null) { VAssert("Manager 未就绪，跳过", () => true); return; }

            // 清零费用防止 ModifyCostAction clamp 上限（战斗推进中费用可能接近 MaxCost）
            new ModifyCostAction { Value = -999 }.Execute(new Context());
            var devoteeData = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/苦行信众.tres");
            var devotee = new Unit(devoteeData, Vector2I.Zero, Team.Player);
            eb.Subscribe(devotee, devoteeData.PassiveEffects);
            int before = bm.PlayerCost;
            eb.Fire(EventType.RoundStart, new Context { SourceTeam = Team.Player }, null);
            VAssert("苦行信众：回合开始 +1 费", () => bm.PlayerCost == before + 1);
            eb.Unsubscribe(devotee);

            new ModifyCostAction { Value = -999 }.Execute(new Context());
            var altarData = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/冰晶祭坛.tres");
            var altar = new Unit(altarData, Vector2I.Zero, Team.Player);
            eb.Subscribe(altar, altarData.PassiveEffects);
            int before2 = bm.PlayerCost;
            var cm = CardManager.Instance;
            int handBefore = cm?.HandCards.Count ?? -1;
            bool libraryHasCards = cm != null && cm.DrawPile.Count > 0;
            // 用户调整：冰晶祭坛改为"攻击敌方时产费+抽牌"（TriggerEvent=OnDealDamage）
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = altar,
                TargetUnit = MakeUnit("祭坛目标", 3, 10),
                SourceTeam = Team.Player,
            }, altar);
            VAssert("冰晶祭坛：攻击时 +2 费", () => bm.PlayerCost == before2 + 2);
            if (cm != null)
                VAssert("冰晶祭坛：攻击时抽1张牌", () => !libraryHasCards || cm.HandCards.Count == handBefore + 1);
            eb.Unsubscribe(altar);
        });
    }

    // ======================================================================
    // 4.7 Boss 技能（苦行大主教四被动）
    // ======================================================================
    private void BossSkills()
    {
        RunGroup("Boss 技能", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/敌方/苦行大主教.tres");
            if (data == null) { VAssert("Boss 数据加载失败", () => false); return; }
            VAssert("Boss 4 个被动", () => data.PassiveEffects != null && data.PassiveEffects.Length == 4);
            var boss = new Unit(data, Vector2I.Zero, Team.Enemy);
            eb.Subscribe(boss, data.PassiveEffects);

            // ① 苦行之触：攻击后目标获得禁足 + 苦修
            var victim = MakeUnit("大主教目标", 4, 10);
            victim.Stamina = 3;
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = boss, TargetUnit = victim,
                SourceTeam = Team.Enemy,
            }, boss);
            VAssert("苦行之触：目标获得禁足", () => BuffManager.Instance.HasBuff(victim, "禁足"));
            VAssert("苦行之触：目标体力-9", () => victim.Stamina <= 0);
            VAssert("苦行之触：目标获得苦修（ATK-1）", () => victim.AttackPower == 3);

            // ② 凛冬庇护：受击前伤害-2（DamangeModifier 增量）
            var dmgCtx = new Context
            {
                SourceUnit = boss,
                TargetUnit = MakeUnit("大主教攻击者", 2, 10),
                SourceTeam = Team.Enemy,
            };
            eb.Fire(EventType.OnBeforeTakeDamage, dmgCtx, boss);
            VAssert("凛冬庇护：受击伤害-2", () => dmgCtx.DamageModifier == -2);

            // ③ 苦行光环：回合开始全场玩家 ATK-1
            var um = UnitManager.Instance;
            if (um != null)
            {
                var believer = MakeUnit("大主教信徒", 4, 10);
                um.ActiveUnits.Add(believer);
                eb.Fire(EventType.RoundStart, new Context { SourceTeam = Team.Enemy }, null);
                VAssert("苦行光环：玩家单位 ATK-1", () => believer.AttackPower == 3);
                um.ActiveUnits.Remove(believer);
            }
            eb.Unsubscribe(boss);
        });
    }

    // ======================================================================
    // 5. 单位被动（真实 UnitData 订阅 + EventBus 触发）
    // ======================================================================
    private void UnitPassives()
    {
        RunGroup("苦行僧 受击反削", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/苦行僧.tres");
            var monk = new Unit(data, Vector2I.Zero, Team.Player);
            var attacker = MakeUnit("打苦行僧的", 4, 10);
            eb.Subscribe(monk, data.PassiveEffects);
            eb.Fire(EventType.OnTakeDamage, new Context
            {
                SourceUnit = monk, TargetUnit = attacker,
                SourceTeam = Team.Player,
            }, monk);
            VAssert("受击后攻击者获得3层苦修（ATK-3）", () => attacker.AttackPower == 1);
            eb.Unsubscribe(monk);
        });

        RunGroup("裁判官 攻击上枷锁", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/裁判官.tres");
            var judge = new Unit(data, Vector2I.Zero, Team.Player);
            var victim = MakeUnit("裁判官目标", 4, 10);
            victim.Stamina = 2;
            eb.Subscribe(judge, data.PassiveEffects);
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = judge, TargetUnit = victim,
                SourceTeam = Team.Player,
            }, judge);
            VAssert("攻击后目标 ATK-1", () => victim.AttackPower == 3);
            VAssert("攻击后目标 体力-1", () => victim.Stamina == 1);
            eb.Unsubscribe(judge);
        });

        RunGroup("缄默圣女 攻击缴械", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/缄默圣女.tres");
            var nun = new Unit(data, Vector2I.Zero, Team.Player);
            var victim = MakeUnit("圣女目标", 4, 10);
            eb.Subscribe(nun, data.PassiveEffects);
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = nun, TargetUnit = victim,
                SourceTeam = Team.Player,
            }, nun);
            VAssert("攻击后目标缴械（ATK≤0）", () => victim.AttackPower <= 0);
            eb.Unsubscribe(nun);
        });

        RunGroup("锁链卫兵 攻击锁AP", () =>
        {
            var eb = EventBus.Instance;
            if (eb == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/锁链卫兵.tres");
            var guard = new Unit(data, Vector2I.Zero, Team.Player);
            var victim = MakeUnit("卫兵目标", 3, 10);
            victim.MaxActionPoints = 2; victim.ActionPoints = 2;
            eb.Subscribe(guard, data.PassiveEffects);
            eb.Fire(EventType.OnDealDamage, new Context
            {
                SourceUnit = guard, TargetUnit = victim,
                SourceTeam = Team.Player,
            }, guard);
            VAssert("攻击后目标当前AP-1", () => victim.ActionPoints == 1);
            VAssert("AP上限不动", () => victim.MaxActionPoints == 2);
            eb.Unsubscribe(guard);
        });

        RunGroup("忏悔者 亡语全场降攻", () =>
        {
            var eb = EventBus.Instance;
            var um = UnitManager.Instance;
            if (eb == null || um == null) { VAssert("EventBus 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/忏悔者.tres");
            var penitent = new Unit(data, Vector2I.Zero, Team.Player);
            var e1 = MakeEnemy("敌1", 4, 10);
            var e2 = MakeEnemy("敌2", 3, 10);
            // EventBus filter 路径固定读 UnitManager.ActiveUnits，注册进战场列表
            um.ActiveUnits.Add(e1);
            um.ActiveUnits.Add(e2);
            eb.Subscribe(penitent, data.PassiveEffects);
            eb.Fire(EventType.OnUnitDeath, new Context
            {
                SourceUnit = penitent, TargetUnit = null,
                SourceTeam = Team.Player,
            }, penitent);
            VAssert("亡语全体敌方 ATK-1", () => e1.AttackPower == 3 && e2.AttackPower == 2);
            eb.Unsubscribe(penitent);
            um.ActiveUnits.Remove(e1);
            um.ActiveUnits.Remove(e2);
        });

        RunGroup("戒律殿堂 回合光环", () =>
        {
            var eb = EventBus.Instance;
            var um = UnitManager.Instance;
            if (eb == null || um == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var data = LoadResource<UnitData>("res://Resource/Data/Units/4禁欲修会/戒律殿堂.tres");
            var hall = new Unit(data, Vector2I.Zero, Team.Player);
            var e1 = MakeEnemy("光环敌1", 4, 10);
            var e2 = MakeEnemy("光环敌2", 2, 10);
            // EventBus filter 路径固定读 UnitManager.ActiveUnits，注册进战场列表
            um.ActiveUnits.Add(e1);
            um.ActiveUnits.Add(e2);
            eb.Subscribe(hall, data.PassiveEffects);
            eb.Fire(EventType.RoundStart, new Context
            {
                SourceTeam = Team.Player,
            }, null);
            VAssert("回合开始全体敌方 ATK-1", () => e1.AttackPower == 3 && e2.AttackPower == 1);
            eb.Unsubscribe(hall);
            um.ActiveUnits.Remove(e1);
            um.ActiveUnits.Remove(e2);
        });

    }

    // ======================================================================
    // 6. 环境被动（真实环境数据施加到格子）
    // ======================================================================
    private void EnvironmentPassives()
    {
        RunGroup("凛冬祭坛 进入伤害", () =>
        {
            var em = EnvironmentManager.Instance;
            var um = UnitManager.Instance;
            var eb = EventBus.Instance;
            if (em == null || um == null || eb == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var env = LoadResource<EnvironmentData>("res://Resource/Data/Environments/凛冬祭坛.tres");
            var cell = new Cell(new BlockData(), new Vector2I(0, 0), Vector2.Zero);
            var source = MakeUnit("祭坛施加者", 1, 5);
            em.ApplyEnvironment(cell, env, source);

            var walker = MakeUnit("踩祭坛的人", 2, 10);
            cell.OccupyingUnit = walker;
            walker.GridPos = cell.GridPos;
            // 环境被动：OnUnitEnterCell 仅"目标格子==环境所在格"的订阅者触发
            eb.Fire(EventType.OnUnitEnterCell, new Context
            {
                TargetCell = cell,
                TargetUnit = walker,
                SourceCell = null,
                SourceTeam = Team.Enemy,
            }, walker);
            VAssert("进入祭坛受到2点伤害", () => walker.CurrentHP == 8);
            em.RemoveEnvironment(cell);
        });

        RunGroup("霜沼 进入减速", () =>
        {
            var em = EnvironmentManager.Instance;
            var eb = EventBus.Instance;
            if (em == null || eb == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var env = LoadResource<EnvironmentData>("res://Resource/Data/Environments/霜沼.tres");
            var cell = new Cell(new BlockData(), new Vector2I(0, 0), Vector2.Zero);
            var source = MakeUnit("霜沼施加者", 1, 5);
            em.ApplyEnvironment(cell, env, source);

            var walker = MakeUnit("踩霜沼的人", 2, 10);
            walker.Stamina = 2;
            cell.OccupyingUnit = walker;
            walker.GridPos = cell.GridPos;
            eb.Fire(EventType.OnUnitEnterCell, new Context
            {
                TargetCell = cell,
                TargetUnit = walker,
                SourceCell = null,
                SourceTeam = Team.Enemy,
            }, walker);
            VAssert("进入霜沼获得泥泞（体力-1）", () => BuffManager.Instance.HasBuff(walker, "泥泞") && walker.Stamina == 1);
            VAssert("霜沼移动消耗+2", () => env.MoveCostDelta == 2);
            em.RemoveEnvironment(cell);
        });

        RunGroup("禁魔领域 驱散增益", () =>
        {
            var em = EnvironmentManager.Instance;
            var bm = BuffManager.Instance;
            var mm = MapManager.Instance;
            if (em == null || bm == null || mm == null) { VAssert("Manager 未就绪，跳过", () => true); return; }
            var env = LoadResource<EnvironmentData>("res://Resource/Data/Environments/禁魔领域.tres");
            // EventBus filter 路径固定读 MapManager.Map 解析环境格中心，注册一个临时格
            var pos = new Vector2I(993, 993);
            var cell = new Cell(new BlockData(), pos, Vector2.Zero);
            mm.Map[pos] = cell;
            var source = MakeUnit("领域施加者", 1, 5);
            em.ApplyEnvironment(cell, env, source);

            var target = MakeUnit("领域内单位", 3, 10);
            cell.OccupyingUnit = target;
            target.GridPos = pos;
            var prosthesis = LoadResource<BuffData>("res://Resource/Data/Buff/义肢.tres");
            // 单位在禁魔领域格上获得义肢 → 环境被动立即驱散（OnBuffApplied 连锁，filter 路径 [Shape(菱形,0)]）
            bm.ApplyBuff(target, prosthesis, null, 1);
            VAssert("禁魔领域：义肢被立即驱散", () => !bm.HasBuff(target, "义肢"));
            // 范围外单位获得义肢不受影响（单目标环境格筛选）
            var outside = MakeUnit("领域外单位", 3, 10);
            var outsidePos = new Vector2I(994, 994);
            mm.Map[outsidePos] = new Cell(new BlockData(), outsidePos, Vector2.Zero);
            outside.GridPos = outsidePos;
            bm.ApplyBuff(outside, prosthesis, null, 1);
            VAssert("禁魔领域：范围外义肢保留", () => bm.HasBuff(outside, "义肢"));
            mm.Map.Remove(outsidePos);
            em.RemoveEnvironment(cell);
            mm.Map.Remove(pos);
        });
    }

    // ======================================================================
    // 工具方法（同 TestRunner 模式）
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

    private T LoadResource<T>(string path) where T : Resource
    {
        var r = ResourceLoader.Load<T>(path);
        if (r == null)
            GD.PrintErr($"[WeakenDeckTests] 资源加载失败: {path}");
        return r;
    }

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

    private static Unit MakeEnemy(string name, int atk, int hp)
    {
        var u = MakeUnit(name, atk, hp);
        u.Team = Team.Enemy;
        return u;
    }
}

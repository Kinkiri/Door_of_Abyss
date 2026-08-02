using Godot;
using System;
using System.Collections.Generic;
using System.Text;

// ============================================================================
// TextToResourceImporter — EditorScript
// 读取策划填的文本配置，自动生成 .tres 资源文件
//
// 使用方式：
//   1. 在 Godot 编辑器中打开此脚本
//   2. File → Run（或 Ctrl+Shift+X）
//   3. 控制台输出生成结果
//
// 文本配置放在 res://Resource/DataConfigs/ 下：
//   buffs.txt  →  res://Resource/Data/Buff/
//   units.txt  →  res://Resource/Data/Units/
//   cards.txt  →  res://Resource/Data/Cards/
// ============================================================================

public partial class TextToResourceImporter : EditorScript
{
    // ------ 路径常量 ------
    const string ConfigDir = "res://Resource/DataConfigs/";
    const string BuffOutput = "res://Resource/Data/Buff/";
    const string UnitOutput = "res://Resource/Data/Units/";
    const string CardOutput = "res://Resource/Data/Cards/";

    const string DefaultUnitPrefab = "res://Scenes/Prefabs/单位视图.tscn";

    private int _buffCount, _unitCount, _cardCount;

    public override void _Run()
    {
        _buffCount = _unitCount = _cardCount = 0;

        EnsureDir(BuffOutput);
        EnsureDir(UnitOutput);
        EnsureDir(CardOutput);

        // 第1遍：Buff（最独立，无外部引用）
        ParseBuffFile(ConfigDir + "buffs.txt");

        // 第2遍：单位（被动效果可能引用 Buff）
        ParseUnitFile(ConfigDir + "units.txt");

        // 第3遍：卡牌（动作可能引用 Buff 和 UnitData）
        ParseCardFile(ConfigDir + "cards.txt");

        GD.Print($"===== 导入完成：{_buffCount} 个 Buff，{_unitCount} 个单位，{_cardCount} 张卡牌 =====");
    }

    // ========================================================================
    // 文件级入口
    // ========================================================================

    private void ParseBuffFile(string path)
    {
        var lines = ReadConfigLines(path);
        foreach (var line in lines)
        {
            try
            {
                var buff = ParseBuffLine(line);
                var savePath = $"{BuffOutput}{buff.BuffID}.tres";
                SaveOrOverwrite(buff, savePath);
                _buffCount++;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[Buff] 解析失败: {line}\n  {e.Message}");
            }
        }
    }

    private void ParseUnitFile(string path)
    {
        var lines = ReadConfigLines(path);
        foreach (var line in lines)
        {
            try
            {
                var unit = ParseUnitLine(line);
                var savePath = $"{UnitOutput}{unit.UnitID}.tres";
                SaveOrOverwrite(unit, savePath);
                _unitCount++;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[单位] 解析失败: {line}\n  {e.Message}");
            }
        }
    }

    private void ParseCardFile(string path)
    {
        var lines = ReadConfigLines(path);
        foreach (var line in lines)
        {
            try
            {
                var card = ParseCardLine(line);
                var savePath = $"{CardOutput}{card.CardID}.tres";
                SaveOrOverwrite(card, savePath);
                _cardCount++;
            }
            catch (Exception e)
            {
                GD.PrintErr($"[卡牌] 解析失败: {line}\n  {e.Message}");
            }
        }
    }

    // ========================================================================
    // 行解析 — Buff
    // buffs.txt: ID | 名称 | 持续 | 最大层数 | 描述 | 动作
    // 示例: 强壮 | 强壮 | 1回合 | 无限叠 | 每层ATK+1 | 属性:攻击力+1
    // ========================================================================

    private BuffData ParseBuffLine(string line)
    {
        var cols = SplitLine(line);
        if (cols.Count < 5) throw new FormatException($"至少需要 5 列，当前 {cols.Count} 列");

        var buff = new BuffData();
        buff.BuffID = cols[0];
        buff.BuffName = cols[1];
        buff.Duration = ParseDuration(cols[2]);
        buff.MaxStack = ParseMaxStack(cols[3]);
        buff.Description = cols[4];
        buff.OnApplyActions = cols.Count >= 6 ? ParseActions(cols[5]) : null;
        return buff;
    }

    // ========================================================================
    // 行解析 — 单位
    // units.txt: ID | 名称 | HP | ATK | AP | 体力 | 射程 | 类型 | 世界观 | 势力 | 标签 | 稀有度 | 描述 | 被动
    // 示例: 小兵 | 小兵 | 2 | 1 | 1 | 1 | 1 | 小队 | 测试 | 测试 | 科技 | Basic | 近战小兵
    // ========================================================================

    private UnitData ParseUnitLine(string line)
    {
        var cols = SplitLine(line);
        if (cols.Count < 13) throw new FormatException($"至少需要 13 列，当前 {cols.Count} 列");

        var unit = new UnitData();
        unit.UnitID = cols[0];
        unit.UnitName = cols[1];
        unit.HealthPoints = int.Parse(cols[2]);
        unit.AttackPower = int.Parse(cols[3]);
        unit.ActionPoints = int.Parse(cols[4]);
        unit.Stamina = int.Parse(cols[5]);
        unit.AttackDistance = int.Parse(cols[6]);
        unit.Type = ParseUnitType(cols[7]);
        unit.World = ParseEnum<World>(cols[8]);
        unit.Faction = ParseEnum<Faction>(cols[9]);
        //unit.Tags = ParseEnum<Tag>(cols[10]);
        unit.Rarity = ParseEnum<Rarity>(cols[11]);
        unit.Description = cols[12];
        unit.UnitPrefab = ResourceLoader.Load<PackedScene>(DefaultUnitPrefab);

        // 第 14 列（可选）：被动效果（亡语等）
        if (cols.Count >= 14 && !string.IsNullOrWhiteSpace(cols[13]))
            unit.PassiveEffects = ParsePassiveEffects(cols[13]);

        return unit;
    }

    // ========================================================================
    // 行解析 — 卡牌
    // cards.txt: ID | 类型 | 目标形状 | 过滤 | 费用 | 范围 | 世界观 | 势力 | 标签 | 稀有度 | 描述 | 条件 | 动作
    // col:       0      1       2        3      4      5      6        7      8      9       10      11     12
    // 示例: 火球术 | 法术 | 敌方单体 | 敌方 | 1费 | 范围1 | 曼斯维森 | 圣主教 | 科技,宗教 | Basic | 造成2点伤害 |       | 伤害:2
    //       限血火球| 法术 | 敌方单体 | 敌方 | 1费 | 范围1 | 曼斯维森 | 圣主教 | 科技      | Basic | HP>50才能用   | HP>50 | 伤害:2
    //       小兵   | 单位 | 格子     | 所有 | 0费 |       | 测试     | 测试  | 科技       | Basic | 召唤小兵    |       | 召唤:小兵
    // ========================================================================

    private CardData ParseCardLine(string line)
    {
        var cols = SplitLine(line);
        if (cols.Count < 12) throw new FormatException($"至少需要 12 列，当前 {cols.Count} 列");

        // cols[0]=ID, cols[1]=类型
        var type = ParseCardType(cols[1]);

        CardData card;
        if (type == CardType.Unit)
            card = new UnitCardData();
        else if (type == CardType.Equipment)
            card = new EquipmentCardData();
        else
            card = new SpellCardData();

        card.CardID = cols[0];
        card.CardName = cols[0];  // ID 和名称相同
        card.Type = type;

        // cols[2]=目标形状, cols[3]=过滤
        TargetShape cardShape;
        TeamFilter cardFilter;
        if (type == CardType.Unit)
        {
            cardShape = TargetShape.SingleCell;
            cardFilter = ParseFilter(cols[3]);
        }
        else if (type == CardType.Equipment)
        {
            cardShape = TargetShape.SingleUnit;
            cardFilter = ParseFilter(cols[3]);
        }
        else
        {
            (cardShape, cardFilter) = ParseShapeFilter(cols[2], cols[3]);
        }

        // cols[4]=费用, cols[5]=范围
        card.Cost = ParseCost(cols[4]);
        int areaRange = string.IsNullOrWhiteSpace(cols[5]) ? 0 : ParseRange(cols[5]);
        card.TargetFilters = MakeTargetFilters(cardShape, cardFilter, areaRange);

        // cols[6]=世界观, cols[7]=势力
        card.World = ParseEnum<World>(cols[6]);
        card.Faction = ParseEnum<Faction>(cols[7]);

        // cols[8]=标签
        if (!string.IsNullOrWhiteSpace(cols[8]))
        {
            var tagStrs = cols[8].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tags = new Godot.Collections.Array<Tag>();
            foreach (var t in tagStrs)
                tags.Add(ParseEnum<Tag>(t));
            card.Tags = tags;
        }

        // cols[9]=稀有度
        card.Rarity = ParseEnum<Rarity>(cols[9]);

        // cols[10]=描述
        card.Description = cols[10];

        // cols[11]=条件（可选列，>=13 列时才有）。多个条件用 ; 分隔，自动 AND 组合
        // 例: "HP>50 ; 有Buff(强壮)" → AND(Compare(HP>50), HasBuff(强壮))
        if (cols.Count >= 13 && !string.IsNullOrWhiteSpace(cols[11]))
        {
            var condStrs = cols[11].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var condList = new System.Collections.Generic.List<Condition>();
            foreach (var s in condStrs)
                condList.Add(ParseCondition(s));
            card.Conditions = condList.ToArray();
        }

        // cols[12]=动作列表（>=13 列时是第 13 列，否则回退到第 12 列）
        var actionDsl = cols.Count >= 13 ? cols[12].Trim() : cols[11].Trim();

        if (type == CardType.Unit && card is UnitCardData unitCard)
        {
            // 动作列格式："召唤:ID" 或 "召唤:ID, Buff:BUFFID#N"（附加 Buff 在召唤后自动施加）
            var summonSegs = SplitDslList(actionDsl);
            var summonDsl = summonSegs.Count > 0 ? summonSegs[0].Trim() : actionDsl.Trim();
            var unitId = ParseSummonUnitId(summonDsl);
            if (!string.IsNullOrEmpty(unitId))
            {
                var unitPath = $"{UnitOutput}{unitId}.tres";
                var unitData = ResourceLoader.Load<UnitData>(unitPath);
                if (unitData != null)
                {
                    unitCard.UnitData = unitData;
                    var summon = new SummonUnitAction();

                    // 附加段：Buff:BUFFID#N
                    for (int i = 1; i < summonSegs.Count; i++)
                    {
                        var seg = summonSegs[i].Trim();
                        if (seg.StartsWith("Buff:"))
                        {
                            var rest = seg[5..];
                            var parts = rest.Split('#');
                            var buffId = parts[0].Trim();
                            var stacks = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 1;
                            summon.SpawnBuff = ResourceLoader.Load<BuffData>($"{BuffOutput}{buffId}.tres");
                            summon.SpawnBuffStacks = stacks;
                            if (summon.SpawnBuff == null)
                                GD.PrintErr($"[卡牌 {card.CardID}] 找不到 Buff Resource: {buffId}");
                        }
                        else
                        {
                            GD.PrintErr($"[卡牌 {card.CardID}] 单位卡动作列仅支持 '召唤:ID[, Buff:ID#N]'，忽略: {seg}");
                        }
                    }

                    unitCard.Actions = new GameAction[] { summon };
                }
                else
                    GD.PrintErr($"[卡牌 {card.CardID}] 找不到单位 Resource: {unitPath}");
            }
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(actionDsl))
                card.Actions = ParseActions(actionDsl);
        }

        return card;
    }

    /// <summary>解析"目标形状"和"过滤"两列，返回 (Shape, TeamFilter)</summary>
    private (TargetShape, TeamFilter) ParseShapeFilter(string shapeCol, string filterCol)
    {
        // 先从形状列解析
        var (shape, filterFromShape) = ParseTarget(shapeCol);
        // 过滤列如果非空且有意义则覆盖
        var filter = string.IsNullOrWhiteSpace(filterCol) ? filterFromShape : ParseFilter(filterCol);
        return (shape, filter);
    }

    /// <summary>
    /// 由形状 + 阵营 + 范围生成 TargetFilter 数组（默认 And 逻辑，无需手动包 And）：
    /// [Shape, Attr]；无阵营 → [Shape]；Shape=None → null（无目标）。
    /// </summary>
    private static TargetFilter[] MakeTargetFilters(TargetShape shape, TeamFilter filter, int areaRange)
    {
        if (shape == TargetShape.None)
            return null;

        var shapeFilter = new ShapeTargetFilter
        {
            Shape = shape,
            AreaRange = (shape == TargetShape.AreaDiamond || shape == TargetShape.AreaSquare) && areaRange > 0 ? areaRange : 1,
            Kind = shape == TargetShape.SingleCell ? TargetKind.Cell : TargetKind.Unit,
        };

        if (filter == TeamFilter.All)
            return new[] { shapeFilter };

        return new TargetFilter[]
        {
            shapeFilter,
            new TeamTargetFilter { Team = filter },
        };
    }

    // ========================================================================
    // 动作 DSL 解析
    // 格式: 动作1, 动作2, ...
    // 每个动作: 类型:参数
    // 支持: 伤害:{expr}, 治疗:{expr}, Buff:ID#N, 抽牌:{expr}, 属性:Stat±{expr}, 召唤
    // ========================================================================

    private GameAction[] ParseActions(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl)) return null;

        var parts = SplitDslList(dsl);
        var actions = new List<GameAction>();
        foreach (var part in parts)
        {
            actions.Add(ParseSingleAction(part));
        }
        return actions.Count > 0 ? actions.ToArray() : null;
    }

    private GameAction ParseSingleAction(string dsl)
    {
        dsl = dsl.Trim();

        // 伤害:{expr}
        if (dsl.StartsWith("伤害:"))
        {
            var expr = dsl[3..];
            var action = new DamageAction();
            AssignValueOrSource(action, expr);
            return action;
        }

        // 治疗:{expr}
        if (dsl.StartsWith("治疗:"))
        {
            var expr = dsl[3..];
            var action = new HealAction();
            AssignValueOrSource(action, expr);
            return action;
        }

        // Buff:ID#N
        if (dsl.StartsWith("Buff:"))
        {
            var rest = dsl[5..];
            var parts = rest.Split('#');
            var buffId = parts[0].Trim();
            var stacks = parts.Length > 1 && int.TryParse(parts[1], out var n) ? n : 1;

            var buffPath = $"{BuffOutput}{buffId}.tres";
            var buffData = ResourceLoader.Load<BuffData>(buffPath);
            if (buffData == null)
                GD.PrintErr($"[动作] 找不到 Buff Resource: {buffPath}（请确保 Buff 已先生成）");

            return new ApplyBuffAction
            {
                BuffData = buffData,
                InitialStacks = stacks,
            };
        }

        // 移除Buff:ID
        if (dsl.StartsWith("移除Buff:"))
        {
            var buffId = dsl[9..].Trim();
            return new RemoveBuffAction { BuffID = buffId };
        }

        // 减Buff:ID#N
        if (dsl.StartsWith("减Buff:"))
        {
            var rest = dsl[7..];
            var parts = rest.Split('#');
            var buffId = parts[0].Trim();
            var delta = parts.Length > 1 && int.TryParse(parts[1], out var n) ? -n : -1;
            return new ModifyBuffAction { BuffID = buffId, StacksDelta = delta };
        }

        // 抽牌:{expr}
        if (dsl.StartsWith("抽牌:"))
        {
            var expr = dsl[3..];
            var action = new DrawCardAction();
            AssignValueOrSource(action, expr);
            return action;
        }

        // 属性:Stat±{expr}
        if (dsl.StartsWith("属性:"))
        {
            var rest = dsl[3..];
            var stat = ParseModifyStatType(rest);
            // 提取符号后面的表达式
            var expr = ExtractExprAfterOp(rest, out _);
            var action = new ModifyStatAction { TargetStat = stat };
            AssignValueOrSource(action, expr);
            return action;
        }

        // 设置:Stat={expr}
        if (dsl.StartsWith("设置:"))
        {
            var rest = dsl[3..];
            var eqIdx = rest.IndexOf('=');
            if (eqIdx < 0) throw new FormatException($"设置 格式错误: {dsl}，需要 =");
            var statName = rest[..eqIdx].Trim();
            var stat = ParseModifyStatType(statName);
            var expr = rest[(eqIdx + 1)..].Trim();
            var action = new SetStatAction { TargetStat = stat };
            AssignValueOrSource(action, expr);
            return action;
        }

        // 召唤[:ID] — 指定 ID 时用字符串 UnitID（运行时查 UnitLibrary，避免亡语重生等循环引用），无 ID 走单位卡自身路径
        if (dsl.StartsWith("召唤:"))
        {
            var unitId = dsl[3..].Trim();
            return new SummonUnitAction { UnitID = unitId };
        }
        if (dsl == "召唤")
        {
            return new SummonUnitAction();
        }

        // 消耗:±{expr}
        if (dsl.StartsWith("消耗:"))
        {
            var expr = dsl[3..];
            var action = new ModifyCostAction();
            AssignValueOrSource(action, expr);
            return action;
        }

        // ?{条件} thenActions :: elseActions — BranchAction
        if (dsl.StartsWith("?"))
        {
            return ParseBranchAction(dsl);
        }

        // 条件:... — 不生成动作，由 ParseCardActionsWithConditions 处理
        if (dsl.StartsWith("条件:"))
        {
            throw new FormatException($"条件不能作为独立动作: {dsl}，应放在动作列末尾或用 ?{{...}}");
        }

        throw new FormatException($"未知动作类型: {dsl}");
    }

    /// <summary>解析 BranchAction: ?{条件} thenActions :: elseActions</summary>
    private BranchAction ParseBranchAction(string dsl)
    {
        // ?{条件} thenActions :: elseActions
        int braceEnd = dsl.IndexOf('}');
        if (braceEnd < 0) throw new FormatException($"BranchAction 需要 {{...}} 包裹条件: {dsl}");

        var condDsl = dsl[2..braceEnd].Trim(); // 去掉 ?{
        var rest = dsl[(braceEnd + 1)..].Trim();

        // 用 :: 分隔 then 和 else
        int sepIdx = rest.LastIndexOf("::", StringComparison.Ordinal);
        string thenDsl, elseDsl;
        if (sepIdx >= 0)
        {
            thenDsl = rest[..sepIdx].Trim();
            elseDsl = rest[(sepIdx + 2)..].Trim();
        }
        else
        {
            thenDsl = rest;
            elseDsl = null;
        }

        return new BranchAction
        {
            Condition = ParseCondition(condDsl),
            ThenActions = ParseActions(thenDsl),
            ElseActions = string.IsNullOrWhiteSpace(elseDsl) ? null : ParseActions(elseDsl),
        };
    }

    /// <summary>解析单位卡动作列中的 "召唤:ID" 或 "召唤"，返回 UnitID</summary>
    private string ParseSummonUnitId(string dsl)
    {
        if (dsl.StartsWith("召唤:"))
            return dsl[3..].Trim();
        if (dsl == "召唤")
            return "";
        // 也可能整列就是 ID
        return dsl.Trim();
    }

    // ========================================================================
    // 被动效果解析
    // 格式:  事件{形状,过滤}?:动作  多个被动用逗号分隔
    // 示例:  亡语:菱形,敌方,伤害:3
    //        回合结束:菱形,友方,治疗:Percent(MaxHP,10)
    //        生成时:属性:攻击力+1
    // ========================================================================

    private static readonly System.Collections.Generic.Dictionary<string, EventType> _eventMap = new()
    {
        { "亡语", EventType.OnUnitDeath },
        { "生成时", EventType.OnSpawn },
        { "回合开始", EventType.RoundStart },
        { "回合结束", EventType.RoundEnd },
        { "攻击后", EventType.OnDealDamage },
        { "受伤时", EventType.OnTakeDamage },
        { "击杀后", EventType.OnKill },
        { "行动后", EventType.OnUnitAct },
        { "出牌后", EventType.OnUseCard },
        { "受伤前", EventType.OnBeforeTakeDamage },
        { "攻击前", EventType.OnBeforeAttack },
        { "移动后", EventType.OnMove },
        { "Buff施加时", EventType.OnBuffApplied },
        { "Buff移除时", EventType.OnBuffRemoved },
    };

    private EffectData[] ParsePassiveEffects(string dsl)
    {
        if (string.IsNullOrWhiteSpace(dsl)) return null;

        // 外层用 SplitDslList（识别括号深度），每条被动独立
        var parts = SplitDslList(dsl);
        var effects = new List<EffectData>();
        foreach (var part in parts)
        {
            int colonIdx = part.IndexOf(':');
            if (colonIdx < 0)
            {
                GD.PrintErr($"[被动] 格式错误（缺少冒号）: {part}");
                continue;
            }

            var eventName = part[..colonIdx].Trim();
            if (!_eventMap.TryGetValue(eventName, out var eventType))
            {
                GD.PrintErr($"[被动] 未知事件类型: {eventName}，可选: {string.Join(", ", _eventMap.Keys)}");
                continue;
            }

            var rest = part[(colonIdx + 1)..].Trim();

            // 用 SplitDslList 解析内层（正确处理括号深度），判断是否有形状/过滤
            var segs = SplitDslList(rest);

            EffectData effect;
            if (segs.Count >= 3 && IsShapeName(segs[0]))
            {
                // 有范围: 亡语:菱形,敌方,动作...
                var shape = ParseShapeName(segs[0]);
                var filter = ParseFilterName(segs[1]);
                var actionDsl = string.Join(",", segs.GetRange(2, segs.Count - 2));
                effect = new EffectData
                {
                    TriggerEvent = eventType,
                    MaxTriggerCount = 1,
                    TargetFilters = MakeTargetFilters(shape, filter,
                        shape == TargetShape.AreaSquare || shape == TargetShape.AreaDiamond ? 1 : 0),
                    Actions = ParseActions(actionDsl),
                };
            }
            else
            {
                // 无范围: 生成时:属性:攻击力+1  或  回合开始:伤害:3
                effect = new EffectData
                {
                    TriggerEvent = eventType,
                    MaxTriggerCount = 0, // 0=无限次
                    Target = PassiveTarget.Self,
                    Actions = ParseActions(rest),
                };
            }

            effects.Add(effect);
        }
        return effects.Count > 0 ? effects.ToArray() : null;
    }

    /// <summary>检查字符串是否是已知的形状名</summary>
    private bool IsShapeName(string s) => s switch
    {
        "菱形" or "方形" or "单体" or "格子" or "全体" or "无" => true,
        _ => false,
    };

    // ========================================================================
    // 值源表达式解析（递归下降）
    //
    // expr      → term (('+'|'-') term)*
    // term      → factor (('*'|'/') factor)*
    // factor    → NUMBER
    //           | IDENTIFIER
    //           | IDENTIFIER '(' expr (',' expr)* ')'
    //           | '(' expr ')'
    //           | '[' expr '..' expr ']'
    //
    // 其中 IDENTIFIER 包括: ATK, 攻击力, HP, 生命, MaxHP, 最大生命, ...
    // ========================================================================

    private ValueSource ParseValueSource(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return null;
        var tokens = Tokenize(expr);
        var parser = new ExprParser(tokens);
        return parser.Parse();
    }

    private List<Token> Tokenize(string expr)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < expr.Length)
        {
            char c = expr[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '(') { tokens.Add(new Token(TokenType.LPAREN, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new Token(TokenType.RPAREN, ")")); i++; continue; }
            if (c == '+') { tokens.Add(new Token(TokenType.PLUS, "+")); i++; continue; }
            if (c == '-') { tokens.Add(new Token(TokenType.MINUS, "-")); i++; continue; }
            if (c == '*') { tokens.Add(new Token(TokenType.STAR, "*")); i++; continue; }
            if (c == '/') { tokens.Add(new Token(TokenType.SLASH, "/")); i++; continue; }
            if (c == ',') { tokens.Add(new Token(TokenType.COMMA, ",")); i++; continue; }
            if (c == '[') { tokens.Add(new Token(TokenType.LBRACKET, "[")); i++; continue; }
            if (c == ']') { tokens.Add(new Token(TokenType.RBRACKET, "]")); i++; continue; }
            if (c == '.' && i + 1 < expr.Length && expr[i + 1] == '.')
            { tokens.Add(new Token(TokenType.DOTDOT, "..")); i += 2; continue; }

            // 数字
            if (char.IsDigit(c) || (c == '-' && i + 1 < expr.Length && char.IsDigit(expr[i + 1])))
            {
                int start = i;
                if (c == '-') i++;
                while (i < expr.Length && char.IsDigit(expr[i])) i++;
                tokens.Add(new Token(TokenType.NUMBER, expr[start..i]));
                continue;
            }

            // 标识符（中文或英文）
            if (char.IsLetter(c) || c > 127)
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] > 127)) i++;
                tokens.Add(new Token(TokenType.IDENTIFIER, expr[start..i]));
                continue;
            }

            throw new FormatException($"无法识别的字符 '{c}' 在位置 {i}: {expr}");
        }
        tokens.Add(new Token(TokenType.EOF, ""));
        return tokens;
    }

    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    // 递归下降解析器
    // =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

    private class Token
    {
        public TokenType Type;
        public string Value;
        public Token(TokenType type, string value) { Type = type; Value = value; }
    }

    private enum TokenType
    {
        NUMBER, IDENTIFIER,
        LPAREN, RPAREN, PLUS, MINUS, STAR, SLASH,
        COMMA, DOTDOT, LBRACKET, RBRACKET, EOF
    }

    private class ExprParser
    {
        private List<Token> _tokens;
        private int _pos;

        public ExprParser(List<Token> tokens) { _tokens = tokens; _pos = 0; }

        private Token Peek() => _tokens[_pos];
        private Token Advance() => _tokens[_pos++];
        private Token Expect(TokenType type)
        {
            var t = Peek();
            if (t.Type != type)
                throw new FormatException($"期望 {type}，实际遇到 {t.Value} (type={t.Type})");
            return Advance();
        }

        public ValueSource Parse() => ParseExpr();

        private ValueSource ParseExpr()
        {
            var left = ParseTerm();
            while (Peek().Type == TokenType.PLUS || Peek().Type == TokenType.MINUS)
            {
                var op = Peek().Type == TokenType.PLUS ? FormulaOp.Add : FormulaOp.Sub;
                Advance();
                var right = ParseTerm();
                left = MakeFormula(op, left, right);
            }
            return left;
        }

        private ValueSource ParseTerm()
        {
            var left = ParseFactor();
            while (Peek().Type == TokenType.STAR || Peek().Type == TokenType.SLASH)
            {
                var op = Peek().Type == TokenType.STAR ? FormulaOp.Mul : FormulaOp.Div;
                Advance();
                var right = ParseFactor();
                left = MakeFormula(op, left, right);
            }
            return left;
        }

        private ValueSource ParseFactor()
        {
            var token = Peek();

            // 数字
            if (token.Type == TokenType.NUMBER)
            {
                Advance();
                int val = int.Parse(token.Value);
                return new ConstantValue { Value = val };
            }

            // 括号表达式
            if (token.Type == TokenType.LPAREN)
            {
                Advance();
                var expr = ParseExpr();
                Expect(TokenType.RPAREN);
                return expr;
            }

            // 范围随机: [A..B]
            if (token.Type == TokenType.LBRACKET)
            {
                Advance();
                var left = ParseExpr();
                Expect(TokenType.DOTDOT);
                var right = ParseExpr();
                Expect(TokenType.RBRACKET);
                return new RandomValue
                {
                    Min = ExtractConstant(left),
                    Max = ExtractConstant(right),
                };
            }

            // 标识符（属性名 或 函数名）
            if (token.Type == TokenType.IDENTIFIER)
            {
                Advance();
                var name = token.Value;

                // 函数调用: func(arg1, arg2)
                if (Peek().Type == TokenType.LPAREN)
                {
                    Advance(); // 跳过 (
                    var args = new List<ValueSource>();
                    if (Peek().Type != TokenType.RPAREN)
                    {
                        args.Add(ParseExpr());
                        while (Peek().Type == TokenType.COMMA)
                        {
                            Advance();
                            args.Add(ParseExpr());
                        }
                    }
                    Expect(TokenType.RPAREN);

                    return BuildFunctionCall(name, args);
                }

                return BuildStatRef(name);
            }

            throw new FormatException($"意外 token '{token.Value}'");
        }

        private ValueSource BuildStatRef(string name)
        {
            // UnitStatValue
            if (name == "ATK" || name == "攻击力")
                return new UnitStatValue { Unit = ValueTarget.Source, Stat = ModifyStatType.AttackPower };
            if (name == "HP" || name == "生命" || name == "当前生命")
                return new UnitStatValue { Unit = ValueTarget.Target, Stat = ModifyStatType.MaxHP, CurrentHP = true };
            if (name == "MaxHP" || name == "最大生命")
                return new UnitStatValue { Unit = ValueTarget.Target, Stat = ModifyStatType.MaxHP, CurrentHP = false };
            if (name == "体力")
                return new UnitStatValue { Unit = ValueTarget.Source, Stat = ModifyStatType.Stamina };
            if (name == "射程" || name == "距离" || name == "攻击距离")
                return new UnitStatValue { Unit = ValueTarget.Source, Stat = ModifyStatType.AttackDistance };
            if (name == "行动" || name == "行动次数")
                return new UnitStatValue { Unit = ValueTarget.Source, Stat = ModifyStatType.ActionPoints };

            // 全局量
            if (name == "回合数")
                return new RoundCountValue();
            if (name == "费用")
                return new BattleCostValue { Type = CostValueType.Current };
            if (name == "最大费用")
                return new BattleCostValue { Type = CostValueType.Max };

            // 单位计数
            if (name == "友方数")
                return new UnitCountValue { FilterTeam = UnitCountTeam.Player };
            if (name == "敌方数")
                return new UnitCountValue { FilterTeam = UnitCountTeam.Enemy };
            if (name == "全单位数")
                return new UnitCountValue { FilterTeam = UnitCountTeam.All };

            throw new FormatException($"未知属性: {name}");
        }

        private ValueSource BuildFunctionCall(string name, List<ValueSource> args)
        {
            if ((name == "Percent" || name == "百分比") && args.Count == 2)
                return MakeFormula(FormulaOp.Percent, args[0], args[1]);

            if ((name == "max" || name == "最大值") && args.Count == 2)
                return MakeFormula(FormulaOp.Max, args[0], args[1]);

            if ((name == "min" || name == "最小值") && args.Count == 2)
                return MakeFormula(FormulaOp.Min, args[0], args[1]);

            if ((name == "随机" || name == "Random") && args.Count == 2)
                return new RandomValue
                {
                    Min = ExtractConstant(args[0]),
                    Max = ExtractConstant(args[1]),
                };

            if (name == "Buff层" && args.Count >= 1)
            {
                var buffId = ExtractString(args[0]);
                var result = new BuffInfoValue
                {
                    BuffID = buffId,
                    Info = BuffInfoType.StackCount,
                    Unit = ValueTarget.Target,
                };
                if (args.Count >= 2)
                    result.Unit = ExtractConstant(args[1]) == 0 ? ValueTarget.Source : ValueTarget.Target;
                return result;
            }

            if (name == "Buff回合" && args.Count >= 1)
            {
                var buffId = ExtractString(args[0]);
                var result = new BuffInfoValue
                {
                    BuffID = buffId,
                    Info = BuffInfoType.RemainingTurns,
                    Unit = ValueTarget.Target,
                };
                if (args.Count >= 2)
                    result.Unit = ExtractConstant(args[1]) == 0 ? ValueTarget.Source : ValueTarget.Target;
                return result;
            }

            if ((name == "距离" || name == "曼哈顿距离") && args.Count == 2)
            {
                return new DistanceValue
                {
                    From = ExtractConstant(args[0]) == 0 ? ValueTarget.Source : ValueTarget.Target,
                    To = ExtractConstant(args[1]) == 0 ? ValueTarget.Source : ValueTarget.Target,
                };
            }

            throw new FormatException($"未知函数: {name}({args.Count} 个参数)");
        }

        // 辅助：从表达式中提取常量值（用在 RandomValue 等场景）
        private int ExtractConstant(ValueSource vs)
        {
            if (vs is ConstantValue cv) return cv.Value;
            throw new FormatException("需要常量表达式");
        }

        private string ExtractString(ValueSource vs)
        {
            if (vs is ConstantValue cv)
            {
                // 这里有点 hacky — 标识符在词法分析中被解析成 ConstantValue
                // 实际场景中 Buff 名称会以字面量形式传入
                return cv.Value.ToString();
            }
            throw new FormatException("需要字符串常量");
        }

        private ValueSource MakeFormula(FormulaOp op, ValueSource left, ValueSource right)
        {
            return new FormulaValue { Op = op, Left = left, Right = right };
        }
    }

    // ========================================================================
    // 条件表达式解析（递归下降）
    //
    // condition  → conjunction ('OR' conjunction)*
    // conjunction → simple ('AND' simple)*
    // simple     → 'NOT' simple
    //            | '(' condition ')'
    //            | '概率' ':' NUMBER
    //            | '有Buff' '(' IDENTIFIER ')'
    //            | '无Buff' '(' IDENTIFIER ')'
    //            | valueExpr CMP_OP valueExpr
    //
    // CMP_OP: <, <=, >, >=, ==, !=
    // ========================================================================

    private Condition ParseCondition(string dsl)
    {
        var tokens = CondTokenize(dsl);
        var parser = new CondParser(tokens, this);
        return parser.Parse();
    }

    private List<CondToken> CondTokenize(string text)
    {
        var tokens = new List<CondToken>();
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];

            // 空格跳过，AND/OR/NOT 需要空格分隔，但先跳过
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // 括号
            if (c == '(') { tokens.Add(new CondToken(CondTokenType.LPAREN, "(")); i++; continue; }
            if (c == ')') { tokens.Add(new CondToken(CondTokenType.RPAREN, ")")); i++; continue; }

            // 比较符
            if (c == '<' && i + 1 < text.Length && text[i + 1] == '=')
            { tokens.Add(new CondToken(CondTokenType.LTE, "<=")); i += 2; continue; }
            if (c == '<') { tokens.Add(new CondToken(CondTokenType.LT, "<")); i++; continue; }
            if (c == '>' && i + 1 < text.Length && text[i + 1] == '=')
            { tokens.Add(new CondToken(CondTokenType.GTE, ">=")); i += 2; continue; }
            if (c == '>') { tokens.Add(new CondToken(CondTokenType.GT, ">")); i++; continue; }
            if (c == '=' && i + 1 < text.Length && text[i + 1] == '=')
            { tokens.Add(new CondToken(CondTokenType.EQ, "==")); i += 2; continue; }
            if (c == '!' && i + 1 < text.Length && text[i + 1] == '=')
            { tokens.Add(new CondToken(CondTokenType.NEQ, "!=")); i += 2; continue; }

            // 冒号（概率:N）
            if (c == ':') { tokens.Add(new CondToken(CondTokenType.COLON, ":")); i++; continue; }

            // 算术运算符（值表达式的一部分）
            if (c == '+' || c == '-' || c == '*' || c == '/')
            { tokens.Add(new CondToken(CondTokenType.OP, c.ToString())); i++; continue; }

            // 标识符 / 关键词 / 数字
            if (char.IsLetter(c) || c > 127 || c == '-' || char.IsDigit(c))
            {
                int start = i;
                // 数字开头只取数字，标识符开头取字母+数字+中文
                if (char.IsDigit(c) || c == '-')
                {
                    while (i < text.Length && (char.IsDigit(text[i]) || text[i] == '.')) i++;
                    tokens.Add(new CondToken(CondTokenType.NUMBER, text[start..i]));
                }
                else
                {
                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] > 127 || text[i] == '_')) i++;
                    var word = text[start..i];

                    // 识别关键词
                    if (word == "AND" || word == "and" || word == "&&") word = "AND";
                    else if (word == "OR" || word == "or" || word == "||") word = "OR";
                    else if (word == "NOT" || word == "not" || word == "!") word = "NOT";

                    tokens.Add(new CondToken(CondTokenType.IDENTIFIER, word));
                }
                continue;
            }

            throw new FormatException($"条件表达式无法解析字符 '{c}' 在位置 {i}: {text}");
        }
        tokens.Add(new CondToken(CondTokenType.EOF, ""));
        return tokens;
    }

    private enum CondTokenType
    {
        IDENTIFIER, NUMBER,
        LPAREN, RPAREN,
        LT, LTE, GT, GTE, EQ, NEQ,
        COLON, OP,
        EOF
    }

    private class CondToken
    {
        public CondTokenType Type;
        public string Value;
        public CondToken(CondTokenType type, string value) { Type = type; Value = value; }
    }

    private class CondParser
    {
        private List<CondToken> _tokens;
        private int _pos;
        // 缓存值源解析结果，用于比较运算
        private TextToResourceImporter _parent;

        public CondParser(List<CondToken> tokens, TextToResourceImporter parent = null)
        {
            _tokens = tokens;
            _pos = 0;
            _parent = parent;
        }

        private CondToken Peek() => _tokens[_pos];
        private CondToken Advance() => _tokens[_pos++];
        private CondToken Expect(CondTokenType type)
        {
            var t = Peek();
            if (t.Type != type)
                throw new FormatException($"条件：期望 {type}，实际遇到 '{t.Value}' (type={t.Type})");
            return Advance();
        }

        public Condition Parse() => ParseCondition();

        // condition → conjunction ('OR' conjunction)*
        private Condition ParseCondition()
        {
            var left = ParseConjunction();
            while (Peek().Type == CondTokenType.IDENTIFIER && Peek().Value == "OR")
            {
                Advance();
                var right = ParseConjunction();
                left = new OrCondition
                {
                    Conditions = new Condition[] { left, right }
                };
            }
            return left;
        }

        // conjunction → simple ('AND' simple)*
        private Condition ParseConjunction()
        {
            var left = ParseSimple();
            while (Peek().Type == CondTokenType.IDENTIFIER && Peek().Value == "AND")
            {
                Advance();
                var right = ParseSimple();
                left = new AndCondition
                {
                    Conditions = new Condition[] { left, right }
                };
            }
            return left;
        }

        // simple → 'NOT' simple | '(' condition ')' | 概率:N | 有Buff(?) | valueExpr CMP_OP valueExpr
        private Condition ParseSimple()
        {
            var token = Peek();

            // NOT
            if (token.Type == CondTokenType.IDENTIFIER && token.Value == "NOT")
            {
                Advance();
                var inner = ParseSimple();
                return new NotCondition { Condition = inner };
            }

            // (condition)
            if (token.Type == CondTokenType.LPAREN)
            {
                Advance();
                var inner = ParseCondition();
                Expect(CondTokenType.RPAREN);
                return inner;
            }

            // 概率:N
            if (token.Type == CondTokenType.IDENTIFIER && token.Value == "概率")
            {
                Advance(); // 吃掉 概率
                Expect(CondTokenType.COLON);
                var numToken = Expect(CondTokenType.NUMBER);
                float prob = float.Parse(numToken.Value);
                return new RandomCondition { Probability = prob };
            }

            // 有Buff(ID) 或 无Buff(ID)
            if (token.Type == CondTokenType.IDENTIFIER && token.Value is "有Buff" or "无Buff")
            {
                bool has = token.Value == "有Buff";
                Advance();
                Expect(CondTokenType.LPAREN);
                var idToken = Expect(CondTokenType.IDENTIFIER);
                Expect(CondTokenType.RPAREN);
                return new HasBuffCondition
                {
                    CheckTarget = ConditionTarget.Target,
                    BuffID = idToken.Value,
                    Has = has,
                };
            }

            // valueExpr CMP_OP valueExpr — 贪心收集直到遇到比较符
            var leftExpr = CollectValueExprUntilCmpOp(out var cmpOp);
            if (cmpOp == null)
                throw new FormatException($"条件需要比较运算符(<=>等): 当前位置 '{token.Value}'");

            var rightExpr = CollectRestAsValueExpr();
            return new CompareCondition
            {
                Left = leftExpr,
                Op = cmpOp.Value,
                Right = rightExpr,
            };
        }

        /// <summary>收集 token 直到遇到比较符，拼成字符串交给 ParseValueSource</summary>
        private ValueSource CollectValueExprUntilCmpOp(out CompareOp? op)
        {
            var sb = new StringBuilder();
            op = null;

            while (_pos < _tokens.Count)
            {
                var t = Peek();
                // 比较符 → 记录 op 并停止
                if (t.Type == CondTokenType.LT || t.Type == CondTokenType.LTE ||
                    t.Type == CondTokenType.GT || t.Type == CondTokenType.GTE ||
                    t.Type == CondTokenType.EQ || t.Type == CondTokenType.NEQ)
                {
                    op = t.Type switch
                    {
                        CondTokenType.LT => CompareOp.Less,
                        CondTokenType.LTE => CompareOp.LessEqual,
                        CondTokenType.GT => CompareOp.Greater,
                        CondTokenType.GTE => CompareOp.GreaterEqual,
                        CondTokenType.EQ => CompareOp.Equal,
                        CondTokenType.NEQ => CompareOp.NotEqual,
                        _ => CompareOp.Equal,
                    };
                    Advance();
                    break;
                }
                // AND/OR/NOT 关键词 → 说明左侧已经完整，没有比较符
                if (t.Type == CondTokenType.IDENTIFIER && (t.Value == "AND" || t.Value == "OR" || t.Value == "NOT"))
                    break;
                // EOF
                if (t.Type == CondTokenType.EOF)
                    break;

                // 其他所有 token（数字、标识符、括号、冒号、运算符）→ 值表达式的一部分
                sb.Append(t.Value);
                Advance();
            }

            var expr = sb.ToString().Trim();
            if (string.IsNullOrEmpty(expr))
                throw new FormatException("比较运算符左侧缺少表达式");

            return ParseValueExprFallback(expr);
        }

        /// <summary>收集剩下所有 token 作为值表达式字符串</summary>
        private ValueSource CollectRestAsValueExpr()
        {
            var sb = new StringBuilder();
            while (_pos < _tokens.Count && Peek().Type != CondTokenType.EOF)
            {
                sb.Append(Peek().Value);
                Advance();
            }
            var expr = sb.ToString().Trim();
            if (string.IsNullOrEmpty(expr))
                throw new FormatException("比较运算符右侧缺少表达式");
            return ParseValueExprFallback(expr);
        }

        /// <summary>兜底：通过父 importer 调用 ParseValueSource</summary>
        private ValueSource ParseValueExprFallback(string expr)
        {
            return _parent.ParseValueSource(expr);
        }
    }

    /// <summary>将值或表达式赋给动作的 Value / ValueSource 字段</summary>
    private void AssignValueOrSource(GameAction action, string expr)
    {
        if (string.IsNullOrWhiteSpace(expr)) return;

        // 纯数字 → Value
        if (int.TryParse(expr, out var val))
        {
            SetActionValue(action, val);
            return;
        }

        // 表达式 → ValueSource
        var vs = ParseValueSource(expr);
        if (vs != null)
            SetActionValueSource(action, vs);
    }

    private void SetActionValue(GameAction action, int val)
    {
        // 通过反射设置 Value 属性
        var prop = action.GetType().GetProperty("Value");
        if (prop != null && prop.PropertyType == typeof(int))
            prop.SetValue(action, val);
    }

    private void SetActionValueSource(GameAction action, ValueSource vs)
    {
        var prop = action.GetType().GetProperty("ValueSource");
        if (prop != null)
            prop.SetValue(action, vs);
    }

    /// <summary>从 "属性:攻击力+1" 中提取属性名 "攻击力"</summary>
    private ModifyStatType ParseModifyStatType(string dsl)
    {
        // 从 "属性:攻击力+1" 提取 "攻击力"
        var prefix = dsl;
        // 找 +- 号或 = 号之前的部分
        int split = -1;
        for (int i = 0; i < prefix.Length; i++)
        {
            if (prefix[i] == '+' || prefix[i] == '-' || prefix[i] == '=')
            { split = i; break; }
        }
        var name = split >= 0 ? prefix[..split].Trim() : prefix.Trim();

        return name switch
        {
            "攻击力" or "ATK" => ModifyStatType.AttackPower,
            "生命" or "HP" or "最大生命" or "MaxHP" => ModifyStatType.MaxHP,
            "体力" => ModifyStatType.Stamina,
            "射程" or "距离" or "攻击距离" => ModifyStatType.AttackDistance,
            "行动" or "行动次数" or "AP" => ModifyStatType.ActionPoints,
            _ => throw new FormatException($"未知属性名: {name}"),
        };
    }

    /// <summary>从 "攻击力+1" 中提取符号后面的表达式 "1"</summary>
    private string ExtractExprAfterOp(string dsl, out char op)
    {
        op = '+';
        for (int i = 0; i < dsl.Length; i++)
        {
            if (dsl[i] == '+' || dsl[i] == '-' || dsl[i] == '=')
            {
                op = dsl[i];
                return dsl[(i + 1)..].Trim();
            }
        }
        return "";
    }

    // ------ 枚举/类型解析 ------

    private CardType ParseCardType(string s) => s switch
    {
        "单位" => CardType.Unit,
        "法术" => CardType.Spell,
        "环境" or "场地" => CardType.Environment,
        "装备" => CardType.Equipment,
        "特殊" => CardType.Special,
        _ => CardType.Special,
    };

    private UnitType ParseUnitType(string s) => s switch
    {
        "小队" => UnitType.Squad,
        "建筑" => UnitType.Building,
        "障碍" or "障碍物" => UnitType.Obstacle,
        "召唤" => UnitType.Summon,
        "特殊" => UnitType.Special,
        "门" => UnitType.Door,
        _ => UnitType.Squad,
    };

    private (TargetShape shape, TeamFilter filter) ParseTarget(string s)
    {
        // "敌方单体" → (SingleUnit, Enemy), "友方单体" → (SingleUnit, Ally)
        if (s.Contains("单体"))
        {
            var filter = s.Contains("敌") ? TeamFilter.Enemy :
                         s.Contains("友") ? TeamFilter.Ally :
                         TeamFilter.All;
            return (TargetShape.SingleUnit, filter);
        }
        // "格子" → (SingleCell, All)
        if (s.Contains("格子"))
            return (TargetShape.SingleCell, TeamFilter.All);
        // "全体" → (All, 由 Filter 列决定)
        if (s == "全体" || s.StartsWith("全体"))
            return (TargetShape.All, TeamFilter.All);
        // "菱形" → (AreaDiamond, 由 Filter 列决定)
        if (s.Contains("菱形"))
            return (TargetShape.AreaDiamond, TeamFilter.All);
        // "方形" → (AreaSquare, 由 Filter 列决定)
        if (s.Contains("方形"))
            return (TargetShape.AreaSquare, TeamFilter.All);
        // "无"
        if (s == "无" || string.IsNullOrWhiteSpace(s))
            return (TargetShape.None, TeamFilter.All);

        return (TargetShape.SingleUnit, TeamFilter.All);
    }

    private TeamFilter ParseFilter(string s) => s switch
    {
        "敌方" or "敌人" => TeamFilter.Enemy,
        "友方" or "友" or "友军" => TeamFilter.Ally,
        "所有" or "全部" or "All" => TeamFilter.All,
        _ => TeamFilter.All,
    };

    private TargetShape ParseShapeName(string s) => s switch
    {
        "菱形" => TargetShape.AreaDiamond,
        "方形" => TargetShape.AreaSquare,
        "单体" => TargetShape.SingleUnit,
        "格子" => TargetShape.SingleCell,
        "全体" => TargetShape.All,
        "无" => TargetShape.None,
        _ => TargetShape.None,
    };

    private TeamFilter ParseFilterName(string s) => s switch
    {
        "敌方" or "敌人" => TeamFilter.Enemy,
        "友方" or "友军" => TeamFilter.Ally,
        "所有" or "全部" => TeamFilter.All,
        _ => TeamFilter.All,
    };

    private int ParseCost(string s)
    {
        s = s.Replace("费", "").Trim();
        return int.TryParse(s, out var v) ? v : 0;
    }

    private int ParseRange(string s)
    {
        s = s.Replace("范围", "").Replace("距离", "").Trim();
        return int.TryParse(s, out var v) ? v : 0;
    }

    private int ParseDuration(string s)
    {
        s = s.Replace("回合", "").Trim();
        if (s == "永久" || s == "-1") return -1;
        if (s == "0") return 0;
        return int.TryParse(s, out var v) ? v : 1;
    }

    private int ParseMaxStack(string s)
    {
        if (s == "无限" || s == "无限叠" || s == "-1") return -1;
        if (s == "0") return 0;
        return int.TryParse(s, out var v) ? v : 1;
    }

    private T ParseEnum<T>(string s) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(s)) return default;
        s = s.Trim();
        // 先尝试直接解析（枚举成员名可能是中文）
        if (Enum.TryParse<T>(s, true, out var result))
            return result;
        throw new FormatException($"无法解析枚举 {typeof(T).Name} 的值: {s}");
    }

    // ========================================================================
    // 文件/IO 辅助
    // ========================================================================

    /// <summary>读取文本文件，过滤空行和 # 注释行</summary>
    private List<string> ReadConfigLines(string path)
    {
        var result = new List<string>();
        try
        {
            using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.Print($"配置文件不存在，跳过: {path}");
                return result;
            }
            while (file.GetPosition() < file.GetLength())
            {
                var line = file.GetLine().Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                    continue;
                result.Add(line);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"读取文件失败 {path}: {e.Message}");
        }
        return result;
    }

    /// <summary>按 | 分割行，去除首尾空格</summary>
    private List<string> SplitLine(string line)
    {
        var parts = line.Split('|', StringSplitOptions.TrimEntries);
        return new List<string>(parts);
    }

    /// <summary>按逗号分割 DSL 列表，但忽略括号内的逗号</summary>
    private List<string> SplitDslList(string text)
    {
        var result = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '(' || text[i] == '[' || text[i] == '{') depth++;
            else if (text[i] == ')' || text[i] == ']' || text[i] == '}') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                result.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        if (start < text.Length)
            result.Add(text[start..].Trim());
        return result;
    }

    /// <summary>确保目录存在（Godot 虚拟路径）</summary>
    private void EnsureDir(string dir)
    {
        var godotDir = DirAccess.Open(dir);
        if (godotDir == null)
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(dir));
    }

    /// <summary>保存 Resource，如已存在则覆盖</summary>
    private void SaveOrOverwrite(Resource res, string path)
    {
        var flags = ResourceSaver.SaverFlags.ReplaceSubresourcePaths;
        var error = ResourceSaver.Save(res, path, flags);
        if (error != Error.Ok)
            GD.PrintErr($"保存失败 {path}: {error}");
    }
}

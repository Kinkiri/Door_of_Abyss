using Godot;

/// <summary>
/// 方向取反值源：输入方向（CellDirection 枚举值）取反（Up↔Down、Left↔Right）。
/// 典型用法：背刺判断 = 攻击方向的反方向（`Direction=AttackDirectionValue` 得到"目标背后"方向）与目标朝向比较。
/// 输入无效（非 0-3）或无输入时返回 DefaultValue。
/// </summary>
[GlobalClass]
public partial class OppositeDirectionValue : ValueSource
{
    /// <summary>输入方向值源（DirectionValue / AttackDirectionValue / ConstantValue 等，返回 CellDirection 枚举值）</summary>
    [Export] public ValueSource Direction { get; set; }

    /// <summary>输入无效或无输入时的默认返回值</summary>
    [Export] public int DefaultValue { get; set; } = (int)CellDirection.Up;

    public override int GetValue(Context ctx)
    {
        if (Direction == null) return DefaultValue;
        int d = Direction.GetValue(ctx);
        return d switch
        {
            (int)CellDirection.Up => (int)CellDirection.Down,
            (int)CellDirection.Down => (int)CellDirection.Up,
            (int)CellDirection.Left => (int)CellDirection.Right,
            (int)CellDirection.Right => (int)CellDirection.Left,
            _ => DefaultValue,
        };
    }
}

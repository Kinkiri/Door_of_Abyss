using Godot;

/// <summary>
/// 常量值源，返回固定数值。
/// </summary>
[GlobalClass]
public partial class ConstantValue : ValueSource
{
    [Export] public int Value { get; set; }

    public override int GetValue(Context ctx) => Value;
}

using Godot;

/// <summary>
/// 装备运行时实例，追踪单位当前装备的装备和来源。
/// 纯 C# class，不继承 Godot 类型。
/// </summary>
public partial class Equipment
{
    /// <summary>装备模板数据引用</summary>
    public EquipmentData Data { get; set; }

    /// <summary>装备来源单位（谁打出的装备卡）</summary>
    public Unit SourceUnit { get; set; }

    /// <summary>装备是否已移除</summary>
    public bool IsExpired { get; set; }

    public Equipment() { }

    public Equipment(EquipmentData data, Unit sourceUnit)
    {
        Data = data;
        SourceUnit = sourceUnit;
        IsExpired = false;
    }

    public override string ToString()
    {
        return $"[Equipment {Data?.EquipmentID}] {Data?.EquipmentName}";
    }
}

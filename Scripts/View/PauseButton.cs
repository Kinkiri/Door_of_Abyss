using Godot;

/// <summary>
/// 战斗场景左上角暂停按钮：点击打开暂停菜单（PauseMenu.SetPaused(true)）。
/// 独立小脚本，只做一件事；暂停逻辑全部在 PauseMenu。
/// </summary>
[GlobalClass]
public partial class PauseButton : Button
{
    public override void _Ready()
    {
        Pressed += () => PauseMenu.Instance?.SetPaused(true);
    }
}

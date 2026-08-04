using Godot;
using System.Transactions;
[GlobalClass]
public partial class MainMenu : Control
{
    [Export] private TextureRect _title;
    [Export] private TextureRect _background;
    [Export] private float _maxOffset = 20f;
    [Export] private float _bgFactor = 0.3f;
    [Export] private float _titleFactor = 0.8f;

    private Vector2 _center;
    private Tween _breathTween;

    //public override void _Ready()
    //{
    //    _center = GetViewport().GetVisibleRect().Size / 2;
    //    if (_title != null)
    //        _title.Position = _center - _title.Size / 2;
    //    StartBreathAnimation();
    //    ConnectButtons();
    //}

    private void StartBreathAnimation()
    {
        if (_title == null) return;
        _breathTween = CreateTween();
        _breathTween.SetLoops();
        _breathTween.SetTrans(Tween.TransitionType.Sine);
        _breathTween.SetEase(Tween.EaseType.InOut);
        _breathTween.TweenProperty(_title, "scale", Vector2.One * 1.1f, 3.0f);
        _breathTween.TweenProperty(_title, "scale", Vector2.One, 3.0f);
    }

    //private void ConnectButtons()
    //{
    //    var startBtn = GetNode<Button>("Buttons/StartButton");
    //    var quitBtn = GetNode<Button>("Buttons/QuitButton");

    //    startBtn.MouseEntered += () => AnimateButton(startBtn, true);
    //    startBtn.MouseExited += () => AnimateButton(startBtn, false);
    //    startBtn.ButtonDown += () => AnimateButtonPress(startBtn);
    //    startBtn.ButtonUp += () => AnimateButtonRelease(startBtn);
    //    //startBtn.Pressed += OnStartPressed;

    //    quitBtn.MouseEntered += () => AnimateButton(quitBtn, true);
    //    quitBtn.MouseExited += () => AnimateButton(quitBtn, false);
    //    quitBtn.ButtonDown += () => AnimateButtonPress(quitBtn);
    //    quitBtn.ButtonUp += () => AnimateButtonRelease(quitBtn);
    //    quitBtn.Pressed += OnQuitPressed;
    //}

    //private void AnimateButton(Button btn, bool hover)
    //{
    //    Tween tween = btn.CreateTween();
    //    tween.SetTrans(Tween.TransitionType.Back);
    //    tween.SetEase(Tween.EaseType.Out);
    //    float target = hover ? 1.15f : 1.0f;
    //    tween.TweenProperty(btn, "scale", Vector2.One * target, 0.2f);
    //}

    //private void AnimateButtonPress(Button btn)
    //{
    //    Tween tween = btn.CreateTween();
    //    tween.SetTrans(Tween.TransitionType.Cubic);
    //    tween.SetEase(Tween.EaseType.Out);
    //    tween.TweenProperty(btn, "scale", Vector2.One * 0.9f, 0.1f);
    //}

    //private void AnimateButtonRelease(Button btn)
    //{
    //    Tween tween = btn.CreateTween();
    //    tween.SetTrans(Tween.TransitionType.Back);
    //    tween.SetEase(Tween.EaseType.Out);
    //    tween.TweenProperty(btn, "scale", Vector2.One * 1.0f, 0.2f);
    //}

    //private void OnStartPressed()
    //{
    //    var transition = GetNode<Transition>("/root/Transition");
    //    transition.TransitionTo("res://Scenes/Game/Level.tscn");
    //}
    public void OnButtonDown()
    {

        // 首先获取当前场景树
        SceneTree tree = GetTree();
        // 1）跳转到scene_snd.tscn场景
        tree.ChangeSceneToFile("res://Scenes/Game/Level.tscn");
    }

    //private void OnQuitPressed()
    //{
    //    GetTree().Quit();
    //}

    //public override void _Input(InputEvent @event)
    //{
    //    if (@event is InputEventMouseMotion mouseMotion)
    //    {
    //        Vector2 offset = (mouseMotion.Position - _center) / _center;
    //        if (_background != null)
    //        {
    //            Vector2 bgOff = offset * _maxOffset * _bgFactor;
    //            _background.Position = _center - _background.Size / 2 + bgOff;
    //        }
    //        if (_title != null)
    //        {
    //            Vector2 titleOff = offset * _maxOffset * _titleFactor;
    //            _title.Position = _center - _title.Size / 2 + titleOff;
    //        }
    //    }
    //}
}

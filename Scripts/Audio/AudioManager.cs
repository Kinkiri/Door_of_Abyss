using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 音频管理器（Autoload 单例，与 View 层同级的事件驱动模式）。
/// 与核心逻辑分离：不主动调用规则逻辑，只订阅 Manager 事件/信号响应播放 BGM 与音效。
/// BGM 跨场景延续（Autoload 常驻）；SFX 走播放器池支持重叠播放。
/// 素材约定：音效放 res://Resource/Audio/Sfx/{key}.wav|.ogg|.mp3（缺失仅告警不崩溃）；
///           BGM 路径见 BgmPaths（现有 Resource/Music/*.mp3）。
/// 调试试听：F9 循环播放全部已配置音效 key（素材入库后逐条验收）。
/// </summary>
public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    // ── 总线 ─────────────────────────────────────────────────────────────
    public const string BusMaster = "Master";
    public const string BusMusic = "Music";
    public const string BusSfx = "SFX";
    public const string BusUi = "UI";

    // ── BGM 曲目表：key → 资源路径 ───────────────────────────────────────
    private static readonly Dictionary<string, string> BgmPaths = new()
    {
        ["title"] = "res://Resource/Music/标题-leberch-ethereal-cinematic-512569.mp3",
        ["battle"] = "res://Resource/Music/基础战斗maksymmalko-ethereal-landscape-space-music-301238.mp3",
        ["boss"] = "res://Resource/Music/boss-paulyudin-battle-battle-music-491417.mp3",
    };

    /// <summary>全部音效 key（事件映射 + UI 直调 + 调试预览清单）</summary>
    public static readonly string[] AllSfxKeys =
    {
        "hit", "heal", "summon", "death", "buff", "debuff", "equip", "unequip",
        "move", "teleport", "draw", "card_play", "place_env", "remove_env", "transform",
        "victory", "defeat", "ui_click", "ui_hover", "card_select", "end_turn", "deny",
    };

    /// <summary>SFX 池大小（重叠播放上限）</summary>
    private const int SfxPoolSize = 16;

    /// <summary>UI 音效池大小</summary>
    private const int UiPoolSize = 8;

    /// <summary>调试试听键（F9 循环播放全部已配置音效）</summary>
    private const Key DebugPreviewKey = Key.F9;

    /// <summary>音量设置存档路径（user://）</summary>
    private const string SettingsPath = "user://settings.cfg";

    private const float DefaultMusicVolume = 0.7f;
    private const float DefaultSfxVolume = 0.8f;
    private const float DefaultUiVolume = 0.8f;

    private AudioStreamPlayer _bgmPlayer;
    private readonly List<AudioStreamPlayer> _sfxPool = new();
    private readonly List<AudioStreamPlayer> _uiPool = new();
    private int _sfxRoundRobin;
    private int _uiRoundRobin;
    private readonly Dictionary<string, AudioStream> _sfxCache = new();
    private readonly HashSet<string> _warnedSfx = new();
    private readonly Dictionary<string, int> _lastSfxFrame = new();   // 同帧同 key 去重（防复合动作同帧堆叠爆音）
    private readonly List<string> _debugSfxKeys = new();
    private int _debugSfxIndex;
    private string _currentBgm;
    private float _savedBgmVolume = -1f;

    // 订阅捕获引用：场景切换时旧场景 Manager 已释放，退订须走捕获的旧引用
    // （纯 C# 事件字段，不触引擎，对已释放包装对象 -= 安全）
    private UnitManager _unitManager;
    private BuffManager _buffManager;
    private EquipmentManager _equipmentManager;
    private EnvironmentManager _environmentManager;
    private CardManager _cardManager;
    private BattleManager _battleManager;
    private SelectionManager _selectionManager;

    // ======================================================================
    // 生命周期
    // ======================================================================

    public override void _Ready()
    {
        Instance = this;

        EnsureBuses();
        LoadVolumeSettings();
        CreateBgmPlayer();
        CreateSfxPool();
        CreateUiPool();

        // autoload 的 _Ready 早于场景节点：延迟一帧订阅（此时场景 Manager 的 Instance 均已就绪，
        // 且先于 InitManager 的 InitAll——能赶上门放置等早期事件）
        CallDeferred(nameof(SubscribeEvents));

        // 场景切换后旧场景 Manager 已释放、静态事件残留订阅，需重建
        GetTree().SceneChanged += OnSceneChanged;
    }

    public override void _ExitTree()
    {
        GetTree().SceneChanged -= OnSceneChanged;
        UnsubscribeEvents();
        if (Instance == this) Instance = null;
    }

    private void OnSceneChanged()
    {
        // 新场景节点已 _Ready（Manager Instance 已指向新实例），同步重订阅
        UnsubscribeEvents();
        SubscribeEvents();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == DebugPreviewKey)
        {
            GetViewport().SetInputAsHandled();
            if (_debugSfxKeys.Count == 0)
            {
                GD.Print("[Audio] 试听列表为空");
                return;
            }
            _debugSfxIndex = (_debugSfxIndex + 1) % _debugSfxKeys.Count;
            string k = _debugSfxKeys[_debugSfxIndex];
            GD.Print($"[Audio] 试听 [{_debugSfxIndex + 1}/{_debugSfxKeys.Count}] {k}");
            PlaySfx(k);
        }
    }

    // ======================================================================
    // 总线与播放器
    // ======================================================================

    private static void EnsureBuses()
    {
        EnsureBus(BusMusic);
        EnsureBus(BusSfx);
        EnsureBus(BusUi);
    }

    private static void EnsureBus(string busName)
    {
        if (AudioServer.GetBusIndex(busName) != -1) return;
        AudioServer.AddBus();
        AudioServer.SetBusName(AudioServer.BusCount - 1, busName);
    }

    private void CreateBgmPlayer()
    {
        _bgmPlayer = new AudioStreamPlayer { Bus = BusMusic };
        // Always：暂停时 BGM 继续播放（音量由 DuckBgm 压低），不随树暂停
        _bgmPlayer.ProcessMode = ProcessModeEnum.Always;
        AddChild(_bgmPlayer);
    }

    private void CreateSfxPool()
    {
        for (int i = 0; i < SfxPoolSize; i++)
        {
            var p = new AudioStreamPlayer { Bus = BusSfx };
            // Always：暂停时 UI 反馈音效仍可播放（战斗音效暂停时无触发，无影响）
            p.ProcessMode = ProcessModeEnum.Always;
            AddChild(p);
            _sfxPool.Add(p);
        }
        _debugSfxKeys.AddRange(AllSfxKeys);
    }

    private void CreateUiPool()
    {
        for (int i = 0; i < UiPoolSize; i++)
        {
            var p = new AudioStreamPlayer { Bus = BusUi };
            p.ProcessMode = ProcessModeEnum.Always;
            AddChild(p);
            _uiPool.Add(p);
        }
    }

    // ======================================================================
    // 对外 API
    // ======================================================================

    /// <summary>播放 BGM 曲目（key 见 BgmPaths）；当前已是同曲且播放中则不重启</summary>
    public void PlayBgm(string key)
    {
        if (!BgmPaths.TryGetValue(key, out var path))
        {
            GD.PushWarning($"[Audio] 未配置 BGM: {key}");
            return;
        }
        if (_currentBgm == key && _bgmPlayer.Playing) return;

        var stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            GD.PushWarning($"[Audio] BGM 加载失败: {key} → {path}");
            return;
        }

        _currentBgm = key;
        _bgmPlayer.Stream = stream;
        // 与旧场景节点一致的循环播放（mp3 import 的 loop 为 false，靠播放器层循环）
        _bgmPlayer.Set("parameters/looping", true);
        _bgmPlayer.Play();
        GD.Print($"[Audio] BGM: {key}");
    }

    public void StopBgm()
    {
        _currentBgm = null;
        _bgmPlayer.Stop();
    }

    /// <summary>播放音效（key 见 AllSfxKeys；文件缺失仅告警一次；同帧同 key 去重）</summary>
    public void PlaySfx(string key, float volumeDb = 0f)
    {
        if (string.IsNullOrEmpty(key) || IsDuplicatedThisFrame(key)) return;

        var stream = GetOrLoadSfx(key);
        if (stream == null) return;

        var player = NextPlayer(_sfxPool, ref _sfxRoundRobin);
        if (player == null) return;   // 池全忙：跳过本次，保住已播声音不被掐

        player.VolumeDb = volumeDb;
        player.Stream = stream;
        player.Play();
    }

    /// <summary>播放 UI 音效（走 UI 总线，与战斗音效分轨调音量；同帧同 key 去重）</summary>
    public void PlayUiSfx(string key, float volumeDb = 0f)
    {
        if (string.IsNullOrEmpty(key) || IsDuplicatedThisFrame(key)) return;

        var stream = GetOrLoadSfx(key);
        if (stream == null) return;

        var player = NextPlayer(_uiPool, ref _uiRoundRobin);
        if (player == null) return;

        player.VolumeDb = volumeDb;
        player.Stream = stream;
        player.Play();
    }

    /// <summary>同帧同 key 只播一次：复合动作（RepeatAction 多段伤害/BranchAction 分支）同帧内
    /// 连续触发多个相同音效时合并为一声，避免堆叠爆音；跨帧连击每帧一声保持节奏</summary>
    private bool IsDuplicatedThisFrame(string key)
    {
        int frame = (int)Engine.GetProcessFrames();
        if (_lastSfxFrame.TryGetValue(key, out int last) && last == frame) return true;
        _lastSfxFrame[key] = frame;
        return false;
    }

    private AudioStream GetOrLoadSfx(string key)
    {
        if (_sfxCache.TryGetValue(key, out var cached)) return cached;

        AudioStream stream = null;
        foreach (var ext in new[] { "wav", "ogg", "mp3" })
        {
            var path = $"res://Resource/Audio/Sfx/{key}.{ext}";
            if (ResourceLoader.Exists(path))
            {
                stream = ResourceLoader.Load<AudioStream>(path);
                break;
            }
        }

        if (stream == null)
        {
            if (_warnedSfx.Add(key))
                GD.PushWarning($"[Audio] 音效缺失: {key}（请放入 res://Resource/Audio/Sfx/{key}.wav）");
            return null;
        }

        _sfxCache[key] = stream;
        return stream;
    }

    /// <summary>取一个空闲播放器；全忙返回 null（调用方跳过本次播放，不掐正在播的音）</summary>
    private AudioStreamPlayer NextPlayer(List<AudioStreamPlayer> pool, ref int roundRobin)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var p = pool[(roundRobin + i) % pool.Count];
            if (!p.Playing)
            {
                roundRobin = (roundRobin + i + 1) % pool.Count;
                return p;
            }
        }
        return null;
    }

    // ======================================================================
    // 音量（0~1 线性 → dB）
    // ======================================================================

    public void SetMusicVolume(float v)
    {
        SetBusLinearVolume(BusMusic, v);
        SaveVolumeSettings();
    }

    public void SetSfxVolume(float v)
    {
        SetBusLinearVolume(BusSfx, v);
        SaveVolumeSettings();
    }

    public void SetUiVolume(float v)
    {
        SetBusLinearVolume(BusUi, v);
        SaveVolumeSettings();
    }

    public float GetMusicVolume() => GetBusLinearVolume(BusMusic);
    public float GetSfxVolume() => GetBusLinearVolume(BusSfx);
    public float GetUiVolume() => GetBusLinearVolume(BusUi);

    /// <summary>
    /// 暂停时压低 BGM（不写盘）：duck=true 记录当前音量并降为 30%，false 恢复。
    /// 恢复时读 settings.cfg 的最新 music 值——暂停期间可能改过设置（SetMusicVolume 已写盘）。
    /// </summary>
    public void DuckBgm(bool duck)
    {
        if (duck)
        {
            if (_savedBgmVolume < 0f) _savedBgmVolume = GetBusLinearVolume(BusMusic);
            SetBusLinearVolume(BusMusic, GetBusLinearVolume(BusMusic) * 0.3f);
        }
        else
        {
            if (_savedBgmVolume >= 0f)
            {
                var cfg = new ConfigFile();
                cfg.Load(SettingsPath);
                float v = (float)cfg.GetValue("audio", "music", _savedBgmVolume);
                SetBusLinearVolume(BusMusic, v);
            }
            _savedBgmVolume = -1f;
        }
    }

    /// <summary>加载音量设置（user://settings.cfg）；不存在时写入默认值</summary>
    private void LoadVolumeSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SettingsPath) == Error.Ok)
        {
            SetBusLinearVolume(BusMusic, (float)cfg.GetValue("audio", "music", DefaultMusicVolume));
            SetBusLinearVolume(BusSfx, (float)cfg.GetValue("audio", "sfx", DefaultSfxVolume));
            SetBusLinearVolume(BusUi, (float)cfg.GetValue("audio", "ui", DefaultUiVolume));
            GD.Print($"[Audio] 音量设置已加载: music={GetBusLinearVolume(BusMusic):0.00} " +
                     $"sfx={GetBusLinearVolume(BusSfx):0.00} ui={GetBusLinearVolume(BusUi):0.00}");
        }
        else
        {
            SetBusLinearVolume(BusMusic, DefaultMusicVolume);
            SetBusLinearVolume(BusSfx, DefaultSfxVolume);
            SetBusLinearVolume(BusUi, DefaultUiVolume);
            SaveVolumeSettings();
        }
    }

    /// <summary>
    /// 保存音量设置到 user://settings.cfg。
    /// 先 Load 再改再 Save：settings.cfg 由多个模块共享（audio 段 + video 段），
    /// 直接 new ConfigFile 会丢其他段。
    /// </summary>
    private void SaveVolumeSettings()
    {
        var cfg = new ConfigFile();
        cfg.Load(SettingsPath);   // 文件不存在时忽略错误，保留已有段
        cfg.SetValue("audio", "music", GetBusLinearVolume(BusMusic));
        cfg.SetValue("audio", "sfx", GetBusLinearVolume(BusSfx));
        cfg.SetValue("audio", "ui", GetBusLinearVolume(BusUi));
        cfg.Save(SettingsPath);
    }

    private static void SetBusLinearVolume(string bus, float v)
    {
        int idx = AudioServer.GetBusIndex(bus);
        if (idx == -1) return;
        v = Mathf.Clamp(v, 0f, 1f);
        AudioServer.SetBusVolumeDb(idx, v <= 0f ? -80f : Mathf.LinearToDb(v));
    }

    private static float GetBusLinearVolume(string bus)
    {
        int idx = AudioServer.GetBusIndex(bus);
        if (idx == -1) return 0f;
        float db = AudioServer.GetBusVolumeDb(idx);
        return db <= -79f ? 0f : Mathf.DbToLinear(db);
    }

    // ======================================================================
    // 事件订阅（事件驱动：规则逻辑零音频调用）
    // ======================================================================

    private void SubscribeEvents()
    {
        // 静态事件（跨场景残留，UnsubscribeEvents 必须对称退订）
        GameAction.OnAnyExecuted += OnActionExecuted;

        _unitManager = UnitManager.Instance;
        if (_unitManager != null)
        {
            _unitManager.OnUnitSpawned += OnUnitSpawned;
            _unitManager.OnUnitRemoved += OnUnitRemoved;
            _unitManager.OnUnitTransformed += OnUnitTransformed;
            _unitManager.OnUnitMoved += OnUnitMoved;
        }

        _buffManager = BuffManager.Instance;
        if (_buffManager != null)
        {
            _buffManager.BuffApplied += OnBuffApplied;
            _buffManager.BuffRemoved += OnBuffRemoved;
        }

        _equipmentManager = EquipmentManager.Instance;
        if (_equipmentManager != null)
        {
            _equipmentManager.EquipmentApplied += OnEquipmentApplied;
            _equipmentManager.EquipmentRemoved += OnEquipmentRemoved;
        }

        _environmentManager = EnvironmentManager.Instance;
        if (_environmentManager != null)
        {
            _environmentManager.EnvironmentApplied += OnEnvironmentApplied;
            _environmentManager.EnvironmentRemoved += OnEnvironmentRemoved;
        }

        _cardManager = CardManager.Instance;
        if (_cardManager != null)
            _cardManager.OnCardDrawn += OnCardDrawn;

        _battleManager = BattleManager.Instance;
        if (_battleManager != null)
        {
            _battleManager.PhaseChanged += OnPhaseChanged;
            _battleManager.GameEnded += OnGameEnded;
        }

        _selectionManager = SelectionManager.Instance;
        if (_selectionManager != null)
            _selectionManager.CardPlayRequest += OnCardPlayRequest;
    }

    private void UnsubscribeEvents()
    {
        GameAction.OnAnyExecuted -= OnActionExecuted;

        if (_unitManager != null)
        {
            _unitManager.OnUnitSpawned -= OnUnitSpawned;
            _unitManager.OnUnitRemoved -= OnUnitRemoved;
            _unitManager.OnUnitTransformed -= OnUnitTransformed;
            _unitManager.OnUnitMoved -= OnUnitMoved;
        }
        if (_buffManager != null)
        {
            _buffManager.BuffApplied -= OnBuffApplied;
            _buffManager.BuffRemoved -= OnBuffRemoved;
        }
        if (_equipmentManager != null)
        {
            _equipmentManager.EquipmentApplied -= OnEquipmentApplied;
            _equipmentManager.EquipmentRemoved -= OnEquipmentRemoved;
        }
        if (_environmentManager != null)
        {
            _environmentManager.EnvironmentApplied -= OnEnvironmentApplied;
            _environmentManager.EnvironmentRemoved -= OnEnvironmentRemoved;
        }
        if (_cardManager != null)
            _cardManager.OnCardDrawn -= OnCardDrawn;
        if (_battleManager != null)
        {
            _battleManager.PhaseChanged -= OnPhaseChanged;
            _battleManager.GameEnded -= OnGameEnded;
        }
        if (_selectionManager != null)
            _selectionManager.CardPlayRequest -= OnCardPlayRequest;

        _unitManager = null;
        _buffManager = null;
        _equipmentManager = null;
        _environmentManager = null;
        _cardManager = null;
        _battleManager = null;
        _selectionManager = null;
    }

    // ======================================================================
    // 事件响应
    // ======================================================================

    /// <summary>ActionQueue 动作执行后：只挂队列独有/不与其他事件重复的声音
    /// （召唤/Buff/变身/抽牌等由对应 Manager 事件触发，避免双响）</summary>
    private void OnActionExecuted(GameAction action, Context ctx)
    {
        switch (action)
        {
            case DamageAction: PlaySfx("hit"); break;
            case HealAction: PlaySfx("heal"); break;
            case MoveUnitAction m: PlaySfx(m.Mode == MoveUnitAction.MoveMode.Teleport ? "teleport" : "move"); break;
            case ModifyStatAction ms:
            {
                // MaxHP 修改附带的当前血变化（Apply 里 CurrentHP += val）不是 Damage/Heal 动作，
                // 这里补上治疗/扣血反馈；其他属性修改按增减补 buff/debuff 反馈
                int v = ms.ValueSource?.GetValue(ctx) ?? ms.Value;
                if (v == 0) break;
                if (ms.TargetStat == ModifyStatType.MaxHP)
                    PlaySfx(v > 0 ? "heal" : "hit");
                else
                    PlaySfx(v > 0 ? "buff" : "debuff");
                break;
            }
        }
    }

    private void OnUnitSpawned(Unit unit) => PlaySfx("summon");
    private void OnUnitRemoved(Unit unit) => PlaySfx("death");
    private void OnUnitTransformed(Unit unit) => PlaySfx("transform");
    private void OnUnitMoved(Unit unit) => PlaySfx("move");
    private void OnBuffApplied(Unit target, Buff buff) => PlaySfx("buff");
    private void OnBuffRemoved(Unit target, Buff buff) => PlaySfx("debuff");
    private void OnEquipmentApplied(Unit target, Equipment equip) => PlaySfx("equip");
    private void OnEquipmentRemoved(Unit target, Equipment equip) => PlaySfx("unequip");
    private void OnEnvironmentApplied(Cell cell, Environment env) => PlaySfx("place_env");
    private void OnEnvironmentRemoved(Cell cell, Environment env) => PlaySfx("remove_env");
    private void OnCardDrawn(Card card) => PlaySfx("draw");
    private void OnCardPlayRequest(Card card, Context ctx) => PlaySfx("card_play");

    /// <summary>阶段切换：进入 GameStart（战斗场景初始化）即播放战斗 BGM</summary>
    private void OnPhaseChanged(BattlePhase phase, Team team, int round)
    {
        // 一进战斗场景（BattleManager 初始进入 GameStart 阶段）即播放战斗 BGM
        if (phase == BattlePhase.GameStart)
            PlayBgm("battle");
    }

    private void OnGameEnded(Team winner, int round)
    {
        // 胜负已分立即停 BGM——播完胜利/失败音效即安静（返回主界面时 MainMenu 再播 title）
        StopBgm();
        PlaySfx(winner == Team.Player ? "victory" : "defeat");
    }
}

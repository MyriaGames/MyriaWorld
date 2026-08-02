using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Maps;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.NPCs;
using MyriaLib.Entities.Skills;
using MyriaLib.Entities.Items;
using MyriaLib.Services;
using MyriaLib.Services.Manager;
using MyriaLib.Systems;
using MyriaLib.Systems.Enums;
using MyriaLib.Systems.Events;
using MyriaWorld.Services;
using MyriaWorld.UI;
using MyriaWorld.World;
using Sfx = MyriaWorld.Services.AudioService.Sfx;

namespace MyriaWorld.Screens;

public class WorldScreen : Screen
{
    private readonly Character _player;

    // ── 3-D ───────────────────────────────────────────────────────────────────
    private BasicEffect _effect = null!;

    // Navigation mesh + world decorations
    private NavMesh          _navMesh      = null!;
    private NavMeshRenderer  _navRenderer  = null!;
    private WorldDecorations _decorations  = null!;

    // Character mesh
    private VertexPositionColor[] _playerVerts = null!;
    private int[]                 _playerIdx   = null!;

    // ── Room tracking ─────────────────────────────────────────────────────────
    private int    _currentFace = -1;
    private Room?  _currentRoom;
    private string _roomName    = "";

    // ── Monster state ─────────────────────────────────────────────────────────
    private readonly Dictionary<int, List<WorldMonster>> _faceMonsters = new();
    private List<WorldMonster> _monsters = new();
    private WorldMonster?      _target;

    // ── NPC state ─────────────────────────────────────────────────────────────
    private readonly Dictionary<int, List<WorldNpc>> _faceNpcs = new();
    private List<WorldNpc> _npcs      = new();
    private WorldNpc?      _nearbyNpc;       // NPC within interact range
    private WorldNpc?      _npcDialog;       // NPC whose panel is currently open

    // ── Gather node state ─────────────────────────────────────────────────────
    private readonly Dictionary<int, List<WorldGatherNode>> _faceGatherNodes = new();
    private List<WorldGatherNode> _gatherNodes = new();
    private WorldGatherNode?      _nearbyNode;
    private string         _npcDialogFeedback = "";
    private float          _npcDialogFeedbackTimer;

    // ── NPC sub-panel state ───────────────────────────────────────────────────
    private string   _activeService = "";
    private int      _shopScroll    = 0;
    private int      _shopItemIdx   = -1;
    private int      _sellScroll    = 0;
    private int      _sellItemIdx   = -1;
    private int      _classScroll   = 0;
    private int      _classIdx      = -1;
    private string[] _classChoices  = [];

    // ── Combat / skills ───────────────────────────────────────────────────────
    private Skill?  _pendingSkill;     // skill queued while auto-moving toward target
    private bool    _autoMoving;
    private bool    _autoAttacking;
    private float   _autoAttackTimer;
    private const float AutoAttackInterval = 1.5f;  // seconds between auto-hits; replace with player stat later
    private readonly Dictionary<string, float> _cooldowns = new();
    private const float GlobalCooldown  = 1.5f;
    private const float AttackRange     = 2.4f;
    private const float AoeRadius       = 8f;

    // Double-click tracking for auto-attack start
    private double        _lastClickTime;
    private WorldMonster? _lastClickedMonster;

    // ── Loot drops ────────────────────────────────────────────────────────────
    private readonly List<WorldLootDrop> _lootDrops = new();
    private const float PickupRange = 3f;

    // ── Inventory overlay ─────────────────────────────────────────────────────
    private InventoryOverlay _inventoryOverlay = null!;
    private bool             _inventoryOpen;

    // Minimap
    private MinimapRenderer _minimapRenderer = null!;

    // NPCs, dialogue, shop & quest log
    private readonly List<WorldNpc>    _worldNpcs         = new();
    private readonly DialogueOverlay   _dialogueOverlay   = new();
    private readonly ShopOverlay       _shopOverlay       = new();
    private readonly QuestLogOverlay   _questLogOverlay   = new();
    private readonly ZoneTransitionOverlay _zoneOverlay   = new();
    private readonly PauseMenuOverlay  _pauseMenu         = new();
    private readonly DayNightCycle     _dayNight          = new();
    private readonly WeatherSystem    _weather           = new();
    private readonly WorldMapOverlay  _worldMap          = new();
    private readonly HashSet<int>     _visitedFaces      = new();

    // ── Static world decorations ──────────────────────────────────────────────
    private VertexPositionColor[] _decoVerts = [];
    private int[]                 _decoIdx   = [];

    // ── Buildings (NPC placement) ─────────────────────────────────────────────
    private IReadOnlyList<WorldBuilding> _buildings = [];

    // ── Waypoints ─────────────────────────────────────────────────────────────
    private IReadOnlyList<WorldWaypoint>  _waypoints         = [];
    private readonly HashSet<int>         _discoveredWpRooms = new();
    private readonly FastTravelOverlay    _fastTravel        = new();

    // ── Terrain height data ───────────────────────────────────────────────────
    private float[] _heights = [];

    // ── Zone / ambient tracking ───────────────────────────────────────────────
    private string _currentTerrain = "grass";

    // ── Footstep timer ────────────────────────────────────────────────────────
    private float _footstepTimer;
    private const float FootstepInterval = 0.38f;

    // ── Quest tracking ────────────────────────────────────────────────────────
    private Quest? _trackedQuest;   // the active quest shown in the HUD

    // ── Notifications ─────────────────────────────────────────────────────────
    private float  _roomNameTimer;
    private string _infoMessage  = "";
    private float  _infoMsgTimer;
    private Color  _infoMsgColor = Color.White;
    private const float RoomNameTime = 3f;
    private const float InfoMsgTime  = 2f;

    // ── Quest / save notifications ────────────────────────────────────────────
    private string _questMsg      = "";
    private float  _questMsgTimer;
    private const float QuestMsgTime = 3.5f;

    // ── Mana regeneration ─────────────────────────────────────────────────────
    private float _manaRegenTimer;
    private const float ManaRegenInterval = 3f;   // seconds between MP ticks
    private const int   ManaRegenAmount   = 1;    // MP restored per tick
    private const float ManaRegenPct      = 0.02f; // 2 % of max mana per tick

    // ── Level-up banner ───────────────────────────────────────────────────────
    private string _levelUpMsg   = "";
    private float  _levelUpTimer;
    private int    _levelUpTo;
    private const float LevelUpBannerTime = 3f;
    private const float LevelUpTime       = 4f;

    // ── Death / respawn ───────────────────────────────────────────────────────
    private bool  _isDead;
    private float _deathTimer;
    private const float DeathRespawnDelay = 5f;
    private static readonly Vector3 RespawnPoint = Vector3.Zero;

    // ── Character spatial ────────────────────────────────────────────────────────
    private Vector3 _pos = Vector3.Zero;
    private float   _yaw;

    // ── Camera ────────────────────────────────────────────────────────────────
    private float _camYaw   = 0f;
    private float _camPitch = -0.35f;
    private const float CamDist   = 7f;
    private const float MoveSpeed = 8f;
    private const float MouseSens = 0.004f;

    // ── Camera matrices (set in Draw, used in HUD for NPC name projection) ────
    private Matrix _lastView = Matrix.Identity;
    private Matrix _lastProj = Matrix.Identity;

    // ── Input ─────────────────────────────────────────────────────────────────
    private MouseState _prevMouse;
    private Point      _lastMousePos;

    // Floating text entries (combat numbers, status messages, loot)
    private record struct FloatText(string Msg, Color Color, float Timer, float X, float StartY);
    private readonly List<FloatText> _floatTexts = new();
    private const float FloatTextTime  = 2.2f;
    private const float FloatDriftPx   = 50f;   // total vertical drift over full lifetime

    // ── HUD layout ────────────────────────────────────────────────────────────
    private int       _sw, _sh;
    private Rectangle _skillBarBounds;   // used for right-click UI-hit test

    // UI4: Inventory overlay
    private bool _showInventory;
    private int  _inventoryPage;
    private int  _inventorySelected = -1; // absolute item index; -1 = none

    // Pause / ESC menu
    private bool  _showPause;
    private float _savedFlashTimer;
    private const float SavedFlashTime = 2.5f;

    // ─────────────────────────────────────────────────────────────────────────
    public WorldScreen(Character player) { _player = player; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public override void LoadContent()
    {
        var gd = ScreenManager.Instance.GraphicsDevice;
        _sw = gd.Viewport.Width;
        _sh = gd.Viewport.Height;

        _effect = new BasicEffect(gd) { VertexColorEnabled = true, LightingEnabled = false };

        _navMesh     = NavMeshLoader.Load(Path.Combine("Content", "world.json"));
        _heights     = TerrainHeights.Build(_navMesh);
        _navRenderer = new NavMeshRenderer();
        _navRenderer.Build(gd, _navMesh, _heights);
        _decorations = new WorldDecorations();
        _decorations.Build(gd);
        (_decoVerts, _decoIdx, _buildings, _waypoints) = WorldDecorationSpawner.Build(_navMesh, _heights);
        _minimapRenderer = new MinimapRenderer();
        _minimapRenderer.Build(gd, _navMesh, _effect);
        _worldMap.Build(gd, _navMesh, _effect, _sw, _sh);
        _worldNpcs.AddRange(WorldNpc.SpawnAll(_navMesh, _buildings));

        BuildCharacterMesh();
        ComputeSkillBarBounds();

        _player.LeveledUp += OnLevelUp;
        _player.LeveledUp += (_, e) =>
        {
            _levelUpTimer = LevelUpTime;
            _levelUpTo    = e.NewLevel;
        };

        // Resolve starting face without announcing the room name
        UpdateFace(_navMesh.FindFaceIndex(new Vector2(_pos.X, _pos.Z)), announce: false);
        _pos.Y = TerrainHeights.GetHeight(_heights, _navMesh, _currentFace, _pos.X, _pos.Z);
    }

    public override void OnEnter()
    {
        // Cursor is always visible — camera rotates only while RMB is held
        Game1.Instance.IsMouseVisible = true;
        var ms = Mouse.GetState();
        _prevMouse    = ms;
        _lastMousePos = new Point(ms.X, ms.Y);
    }

    public override void OnExit()
    {
        _player.LeveledUp -= OnLevelUp;
        Game1.Instance.IsMouseVisible = true;
        _navRenderer.Dispose();
        _minimapRenderer.Dispose();
        _worldMap.Dispose();
        _decorations.Dispose();
    }

    private void OnLevelUp(object? sender, LevelUpEventArgs e)
    {
        _levelUpMsg   = $"Level Up!  {e.NewLevel}";
        _levelUpTimer = LevelUpBannerTime;
        AudioService.Play(Sfx.LevelUp, 0.85f);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt)
    {
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        var   kb = Keyboard.GetState();
        var   ms = Mouse.GetState();

        // ESC — priority: close inventory → back from sub-panel → close NPC dialog → toggle pause
        if (WasKeyJustPressed(kb, Keys.Escape))
        {
            if (_showInventory)      { _showInventory = false; _inventorySelected = -1; _prevKb = kb; return; }
            if (_activeService != "") { _activeService = ""; _prevKb = kb; return; }
            if (_npcDialog != null)  { _npcDialog = null; _npcDialogFeedback = ""; _prevKb = kb; return; }
            _showPause = !_showPause;
            _prevKb = kb; return;
        }

        // F / E — interact with nearby NPC
        if ((WasKeyJustPressed(kb, Keys.F) || WasKeyJustPressed(kb, Keys.E))
            && _nearbyNpc != null && !_showPause && !_showInventory && _npcDialog == null)
        {
            _npcDialog = _nearbyNpc;
            _npcDialogFeedback = "";
            _prevKb = kb; return;
        }

        // G — gather from nearby resource node
        if (WasKeyJustPressed(kb, Keys.G) && _nearbyNode != null
            && !_showPause && !_showInventory && _npcDialog == null && !_isDead)
        {
            HandleGather();
        }

        // NPC dialog open — handle clicks and block gameplay
        if (_npcDialog != null)
        {
            HandleNpcDialogClick(ms);
            if (_activeService is "shop" or "sell" or "class")
            {
                if (WasKeyJustPressed(kb, Keys.PageUp))   ScrollSubPanel(-1);
                if (WasKeyJustPressed(kb, Keys.PageDown)) ScrollSubPanel(+1);
            }
            _prevMouse = ms; _prevKb = kb; return;
        }

        // F5 — quick-save
        if (WasKeyJustPressed(kb, Keys.F5))
            QuickSave();

        // Pause menu — handle mouse clicks then block everything else
        if (_showPause)
        {
            HandlePauseClick(ms);
            _prevMouse = ms;
            _prevKb    = kb;
            return;
        }

        // I / B — toggle inventory overlay
        if (WasKeyJustPressed(kb, Keys.I) || WasKeyJustPressed(kb, Keys.B))
        {
            _showInventory = !_showInventory;
            _inventoryPage = 0;
        }

        // Inventory page navigation
        if (_showInventory)
        {
            const int perPage = 12;
            int maxPage = Math.Max(0, (_player.Inventory.Items.Count - 1) / perPage);
            if (WasKeyJustPressed(kb, Keys.PageDown)) _inventoryPage = Math.Min(_inventoryPage + 1, maxPage);
            if (WasKeyJustPressed(kb, Keys.PageUp))   _inventoryPage = Math.Max(_inventoryPage - 1, 0);
            if (WasKeyJustPressed(kb, Keys.Enter))    TryEquip(_inventorySelected);
            HandleInventoryClick(ms);
            _prevKb = kb;
            return;   // block gameplay input while inventory is open
        }

        // ── Dead — only handle respawn input, skip all gameplay ───────────────
        if (_isDead)
        {
            _deathTimer -= dt;
            if (_deathTimer <= 0f || WasKeyJustPressed(kb, Keys.R))
                Respawn();
            _prevKb = kb;
            return;
        }

        // Number keys 1-9: activate skill
        for (int k = 0; k < Math.Min(_player.Skills.Count, 9); k++)
        {
            if (WasKeyJustPressed(kb, Keys.D1 + k))
            {
                TryActivateSkill(_player.Skills[k]);
                break;
            }
        }

        // Tab: select nearest alive monster that isn't the current target
        if (WasKeyJustPressed(kb, Keys.Tab))
            SelectNearestTarget();

        // ── Mouse ──────────────────────────────────────────────────────────────

        // Right-click drag on non-UI → rotate camera
        if (ms.RightButton == ButtonState.Pressed && !IsOverUI(ms.X, ms.Y))
        {
            int dx = ms.X - _lastMousePos.X;
            int dy = ms.Y - _lastMousePos.Y;
            _camYaw   += dx * MouseSens;
            _camPitch  = MathHelper.Clamp(_camPitch + dy * MouseSens, -1.35f, 0.1f);
        }
        _lastMousePos = new Point(ms.X, ms.Y);

        // Left-click on world (not UI) → ray-pick / double-click auto-attack
        bool leftClicked = ms.LeftButton == ButtonState.Released
                        && _prevMouse.LeftButton == ButtonState.Pressed
                        && !IsOverUI(ms.X, ms.Y);
        _prevMouse = ms;
        bool hasManualInput = kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up)
                           || kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down)
                           || kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left)
                           || kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right);
        if (hasManualInput) _autoAttacking = false;

        // ── Movement ──────────────────────────────────────────────────────────

        // Build camera-relative movement vectors
        Vector3 camForward = new Vector3(-MathF.Sin(_camYaw), 0f, -MathF.Cos(_camYaw));
        Vector3 camRight   = Vector3.Cross(camForward, Vector3.Up);

        // Priority: pending-skill auto-move > auto-attack approach > WASD
        if (_autoMoving && _target is { IsAlive: true } && _pendingSkill != null)
        {
            float dist = Vector3.Distance(
                new Vector3(_pos.X, 0f, _pos.Z),
                new Vector3(_target.Position.X, 0f, _target.Position.Z));

            if (dist <= AttackRange)
            {
                ExecuteSkill(_pendingSkill, _target);
                _autoMoving      = false;
                _pendingSkill    = null;
                _autoAttacking   = true;
                _autoAttackTimer = AutoAttackInterval;  // brief pause after skill before first auto-hit
            }
            else
            {
                Vector3 dir = new Vector3(
                    _target.Position.X - _pos.X, 0f,
                    _target.Position.Z - _pos.Z);
                dir.Normalize();
                MoveCharacter(dir, dt);
            }
        }
        else
        {
            _autoMoving   = false;
            _pendingSkill = null;

            if (hasManualInput)
            {
                Vector3 move = Vector3.Zero;
                if (kb.IsKeyDown(Keys.W) || kb.IsKeyDown(Keys.Up))    move += camForward;
                if (kb.IsKeyDown(Keys.S) || kb.IsKeyDown(Keys.Down))  move -= camForward;
                if (kb.IsKeyDown(Keys.A) || kb.IsKeyDown(Keys.Left))  move -= camRight;
                if (kb.IsKeyDown(Keys.D) || kb.IsKeyDown(Keys.Right)) move += camRight;
                if (move.LengthSquared() > 0.01f) { move.Normalize(); MoveCharacter(move, dt); }
            }
            else if (_autoAttacking && _target is { IsAlive: true })
            {
                float dist = Vector3.Distance(
                    new Vector3(_pos.X, 0f, _pos.Z),
                    new Vector3(_target.Position.X, 0f, _target.Position.Z));

                if (dist > AttackRange)
                {
                    Vector3 dir = new Vector3(
                        _target.Position.X - _pos.X, 0f,
                        _target.Position.Z - _pos.Z);
                    dir.Normalize();
                    MoveCharacter(dir, dt);
                }
                else
                {
                    _autoAttackTimer -= dt;
                    if (_autoAttackTimer <= 0f)
                    {
                        DoAutoAttack(_target);
                        _autoAttackTimer = AutoAttackInterval;
                    }
                }
            }
        }

        // ── Ray-pick after movement (needs up-to-date matrices) ───────────────
        if (leftClicked)
        {
            var picked = TryPickTarget(ms.X, ms.Y);
            bool isDoubleClick = picked != null
                              && picked == _lastClickedMonster
                              && gt.TotalGameTime.TotalSeconds - _lastClickTime < 0.4;
            if (isDoubleClick)
            {
                _autoAttacking   = true;
                _autoAttackTimer = 0f;   // fire on very next frame
            }
            else if (picked == null)
            {
                _autoAttacking = false;
            }
            _lastClickedMonster = picked;
            _lastClickTime      = gt.TotalGameTime.TotalSeconds;
        }

        // ── Monster AI + status ticks ─────────────────────────────────────────
        foreach (var m in _monsters)
        {
            m.Update(dt, _pos, _navMesh, rawDmg =>
            {
                int dmg = Math.Max(1, rawDmg - _player.DefandPhysical());
                _player.TakeDamage(dmg);
                AddFloatText($"-{dmg} HP", new Color(200, 80, 60));
                if (_player.CurrentHealth <= 0) TriggerDeath();
            });

            if (m.IsAlive)
                m.Position = new Vector3(m.Position.X,
                    TerrainHeights.GetHeight(_heights, _navMesh, m.SpawnFace, m.Position.X, m.Position.Z),
                    m.Position.Z);

            // Flash timer
            if (m.HitFlashTimer > 0f) m.HitFlashTimer = Math.Max(0f, m.HitFlashTimer - dt);

            // Stun timer
            if (m.StunTimer > 0f)     m.StunTimer     = Math.Max(0f, m.StunTimer - dt);

            // Poison DoT
            if (m.PoisonTimer > 0f && m.IsAlive)
            {
                m.PoisonTimer -= dt;
                m.PoisonTick  -= dt;
                if (m.PoisonTick <= 0f)
                {
                    m.PoisonTick = 1f;
                    m.Data.TakeDamage(m.PoisonDamage);
                    ShowInfo($"Poison -{m.PoisonDamage}", new Color(80, 200, 80));
                    if (!m.IsAlive)
                    {
                        _player.GainXp(m.Data.Exp);
                        OnMonsterKilled(m);
                        if (m == _target) { _target = null; _autoAttacking = false; }
                    }
                }
            }
        }
        // Clear target only once it is fully gone (not just respawning)
        if (_target is { IsAlive: false, IsRespawning: false }) { _target = null; _autoAttacking = false; }

        // UI1: viewport resize
        var vp = ScreenManager.Instance.GraphicsDevice.Viewport;
        if (vp.Width != _sw || vp.Height != _sh)
        {
            _sw = vp.Width;
            _sh = vp.Height;
            ComputeSkillBarBounds();
        }

        // ── Cooldowns & timers ────────────────────────────────────────────────
        foreach (var key in _cooldowns.Keys.ToList())
        {
            _cooldowns[key] -= dt;
            if (_cooldowns[key] <= 0f) _cooldowns.Remove(key);
        }
        if (_roomNameTimer         > 0f) _roomNameTimer         -= dt;
        if (_infoMsgTimer          > 0f) _infoMsgTimer          -= dt;
        if (_levelUpTimer          > 0f) _levelUpTimer          -= dt;
        if (_savedFlashTimer       > 0f) _savedFlashTimer       -= dt;
        if (_npcDialogFeedbackTimer> 0f) _npcDialogFeedbackTimer -= dt;

        // Nearby-NPC detection (disabled when dialogs/menus are open)
        _nearbyNpc = null;
        if (!_showPause && !_showInventory && _npcDialog == null && !_isDead)
        {
            WorldNpc? closest  = null;
            float     bestDist = WorldNpc.InteractRange;
            foreach (var npc in _npcs)
            {
                float d = Vector3.Distance(
                    new Vector3(_pos.X, 0f, _pos.Z),
                    new Vector3(npc.Position.X, 0f, npc.Position.Z));
                if (d < bestDist) { bestDist = d; closest = npc; }
            }
            _nearbyNpc = closest;
        }

        // Nearby gather node detection
        _nearbyNode = null;
        if (!_showPause && !_showInventory && _npcDialog == null && !_isDead)
        {
            WorldGatherNode? closestNode = null;
            float            bestDist    = WorldGatherNode.InteractRange;
            foreach (var node in _gatherNodes)
            {
                float d = Vector3.Distance(
                    new Vector3(_pos.X, 0f, _pos.Z),
                    new Vector3(node.Position.X, 0f, node.Position.Z));
                if (d < bestDist) { bestDist = d; closestNode = node; }
            }
            _nearbyNode = closestNode;
        }

        // Float text lifetime
        for (int i = _floatTexts.Count - 1; i >= 0; i--)
        {
            var ft = _floatTexts[i];
            _floatTexts[i] = ft with { Timer = ft.Timer - dt };
            if (_floatTexts[i].Timer <= 0f) _floatTexts.RemoveAt(i);
        }

        // GM4: mana regeneration (out of combat)
        if (!_isDead && _target == null)
        {
            _manaRegenTimer += dt;
            if (_manaRegenTimer >= ManaRegenInterval)
            {
                _manaRegenTimer -= ManaRegenInterval;
                int regen = Math.Max(1, (int)(_player.MaxMana * ManaRegenPct));
                _player.CurrentMana = Math.Min(_player.MaxMana, _player.CurrentMana + regen);
            }
        }
        else
        {
            _manaRegenTimer = 0f;
        }

        _prevKb = kb;
    }

    // ── Input helpers ─────────────────────────────────────────────────────────

    private KeyboardState _prevKb;
    private bool WasKeyJustPressed(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && !_prevKb.IsKeyDown(key);

    private bool IsOverUI(int x, int y) =>
        _inventoryOpen || _shopOverlay.IsOpen || _questLogOverlay.IsOpen || _pauseMenu.IsOpen || _skillBarBounds.Contains(x, y);

    // ── Movement ──────────────────────────────────────────────────────────────

    private void MoveCharacter(Vector3 dir, float dt)
    {
        Vector3 candidate    = _pos + dir * MoveSpeed * dt;
        int     candidateFace = _navMesh.FindFaceIndex(new Vector2(candidate.X, candidate.Z));
        if (candidateFace < 0) return;

        // Room-access check when crossing faces
        if (candidateFace != _currentFace)
        {
            var face = _navMesh.Faces[candidateFace];
            var room = face.RoomId.HasValue ? WorldDataService.GetRoom(face.RoomId.Value) : null;
            if (room != null && !WorldDataService.CanEnter(_player, room))
            {
                AddFloatText(WorldDataService.DeniedReason(_player, room), new Color(200, 80, 80));
                return;
            }
            UpdateFace(candidateFace, announce: true);
        }

        _pos   = candidate;
        _pos.Y = TerrainHeights.GetHeight(_heights, _navMesh, _currentFace, _pos.X, _pos.Z);
        _yaw   = MathF.Atan2(dir.X, dir.Z);

        if (_footstepTimer <= 0f)
        {
            float pitch = _currentTerrain is "dungeon" or "cave" ? -0.3f : 0f;
            AudioService.Play(Sfx.Footstep, 0.55f, pitch);
            _footstepTimer = FootstepInterval;
        }
    }

    // ── Room tracking ─────────────────────────────────────────────────────────

    private void UpdateFace(int faceIndex, bool announce)
    {
        _currentFace = faceIndex;
        if (faceIndex >= 0) _visitedFaces.Add(faceIndex);
        var face     = faceIndex >= 0 ? _navMesh.Faces[faceIndex] : null;

        _currentRoom = face?.RoomId.HasValue == true
            ? WorldDataService.GetRoom(face.RoomId.Value) : null;

        _roomName = _currentRoom != null
            ? WorldDataService.GetRoomName(_currentRoom)
            : face?.RoomName ?? "";

        // Zone / terrain transition
        string newTerrain = face?.Terrain ?? "grass";
        if (newTerrain != _currentTerrain)
        {
            bool enteringUnderground = newTerrain is "dungeon" or "cave";
            bool leavingUnderground  = _currentTerrain is "dungeon" or "cave";

            if (announce && (enteringUnderground || leavingUnderground))
            {
                string zoneLabel = enteringUnderground
                    ? $"Entering {_roomName}"
                    : "Returning to surface";
                _zoneOverlay.Trigger(WorldDataService.AsciiSafe(zoneLabel));
                AudioService.Play(Sfx.EnterDungeon, 0.7f);
            }

            _currentTerrain = newTerrain;
            AudioService.SetAmbientZone(_currentTerrain);
        }

        if (announce && _roomName.Length > 0)
        {
            _roomNameTimer = RoomNameTime;
            if (_currentRoom?.HasMonsters == true)
                AddFloatText("Hostile area", new Color(200, 120, 50));
        }

        // Waypoint discovery — auto-discover when entering a city face for the first time
        if (announce && face != null && face.RoomId.HasValue)
        {
            var wp = _waypoints.FirstOrDefault(w => w.RoomId == face.RoomId.Value);
            if (wp != null && _discoveredWpRooms.Add(wp.RoomId))
                ShowQuestMsg($"Waypoint discovered: {wp.Name}", new Color(100, 180, 255));
        }

        // Swap monster list to the current face's pool (spawn if first visit)
        _monsters = GetOrSpawnMonsters(faceIndex);

        // Swap NPC list for this face
        _npcs = GetOrSpawnNpcs(faceIndex);

        // Swap gather node list for this face
        _gatherNodes = GetOrSpawnGatherNodes(faceIndex);

        // Clear any target that no longer belongs to this face
        if (_target != null && !_monsters.Contains(_target))
            _target = null;
    }

    private List<WorldMonster> GetOrSpawnMonsters(int faceIndex)
    {
        if (faceIndex < 0) return new();
        if (_faceMonsters.TryGetValue(faceIndex, out var existing)) return existing;

        var face = _navMesh.Faces[faceIndex];

        // Open plains are safe during the day; monsters only appear at night
        if (face.Terrain is "grass" or "dirt" && !_dayNight.IsNight)
            return new();  // not cached — re-checked each time the player enters
        if (face.RoomId.HasValue && WorldDataService.GetRoom(face.RoomId.Value) is { HasMonsters: true } room)
        {
            var spawned = MonsterSpawner.Spawn(_navMesh, faceIndex, room);
            foreach (var m in spawned)
                m.Position = new Vector3(m.Position.X,
                    TerrainHeights.GetHeight(_heights, _navMesh, faceIndex, m.Position.X, m.Position.Z),
                    m.Position.Z);
            _faceMonsters[faceIndex] = spawned;
            return spawned;
        }

        _faceMonsters[faceIndex] = new();
        return _faceMonsters[faceIndex];
    }

    private List<WorldNpc> GetOrSpawnNpcs(int faceIndex)
    {
        if (faceIndex < 0) return new();
        if (_faceNpcs.TryGetValue(faceIndex, out var cached)) return cached;

        var list = _worldNpcs
            .Where(n => _navMesh.FindFaceIndex(new Vector2(n.Position.X, n.Position.Z)) == faceIndex)
            .ToList();
        _faceNpcs[faceIndex] = list;
        return list;
    }

    private List<WorldGatherNode> GetOrSpawnGatherNodes(int faceIndex)
    {
        if (faceIndex < 0) return new();
        if (_faceGatherNodes.TryGetValue(faceIndex, out var cached)) return cached;

        var face = _navMesh.Faces[faceIndex];
        var room = face.RoomId.HasValue ? WorldDataService.GetRoom(face.RoomId.Value) : null;

        var list = new List<WorldGatherNode>();
        foreach (var p in face.GatherNodes)
        {
            var spot = room?.GatheringSpots.FirstOrDefault(s => s.Type == p.Type);
            list.Add(new WorldGatherNode(p.Type, new Vector3(p.X, 0f, p.Z), p.Label, spot));
        }

        _faceGatherNodes[faceIndex] = list;
        return list;
    }

    private void HandleGather()
    {
        if (_nearbyNode == null) return;

        if (_nearbyNode.IsDepleted)
        {
            AddFloatText($"{_nearbyNode.Label} depleted today", new Color(160, 140, 100));
            return;
        }

        if (_nearbyNode.Spot == null)
        {
            AddFloatText("Nothing to gather here", Theme.ForegroundDim);
            return;
        }

        var result = GatherService.GatherFromSpot(_player, _nearbyNode.Spot);
        switch (result)
        {
            case GatherResult.Success:
                _nearbyNode.TryConsume();
                AddFloatText($"+ {_nearbyNode.Label}", new Color(120, 200, 120));
                break;
            case GatherResult.NoTool:
                string toolHint = _nearbyNode.Type switch
                {
                    GatheringType.Ore  => "Requires a pickaxe",
                    GatheringType.Tree => "Requires a woodcutting axe",
                    _                  => "Missing required tool",
                };
                AddFloatText(toolHint, new Color(200, 165, 60));
                break;
            case GatherResult.InventoryFull:
                AddFloatText("Inventory full", new Color(200, 80, 60));
                break;
        }
    }

    // ── Targeting ─────────────────────────────────────────────────────────────

    private void SelectNearestTarget()
    {
        WorldMonster? nearest  = null;
        float         bestDist = float.MaxValue;
        Vector3       flat     = new(_pos.X, 0f, _pos.Z);

        foreach (var m in _monsters)
        {
            if (!m.IsAlive || m == _target) continue;
            float d = Vector3.Distance(flat, new Vector3(m.Position.X, 0f, m.Position.Z));
            if (d < bestDist) { bestDist = d; nearest = m; }
        }

        if (nearest != null) _target = nearest;
    }

    private WorldMonster? TryPickTarget(int mouseX, int mouseY)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;

        // Rebuild matrices to match Draw
        float cosP      = MathF.Cos(_camPitch);
        Vector3 headPos = _pos + new Vector3(0f, 1.4f, 0f);
        Vector3 camOff  = new Vector3(
            MathF.Sin(_camYaw) * cosP * CamDist,
           -MathF.Sin(_camPitch) * CamDist,
            MathF.Cos(_camYaw) * cosP * CamDist);

        Matrix view = Matrix.CreateLookAt(headPos + camOff, headPos, Vector3.Up);
        Matrix proj = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f), (float)_sw / _sh, 0.05f, 600f);

        Vector3 nearPt = gd.Viewport.Unproject(new Vector3(mouseX, mouseY, 0f), proj, view, Matrix.Identity);
        Vector3 farPt  = gd.Viewport.Unproject(new Vector3(mouseX, mouseY, 1f), proj, view, Matrix.Identity);
        Vector3 ray    = Vector3.Normalize(farPt - nearPt);

        WorldMonster? best    = null;
        float         bestDist = float.MaxValue;
        foreach (var m in _monsters)
        {
            if (!m.IsAlive) continue;
            float t = RaySphere(nearPt, ray, m.Position + new Vector3(0f, 0.9f, 0f), 0.65f);
            if (t >= 0f && t < bestDist) { bestDist = t; best = m; }
        }
        _target = best;   // null clears the target if clicking empty space
        return best;
    }

    private static float RaySphere(Vector3 origin, Vector3 dir, Vector3 center, float r)
    {
        Vector3 oc = origin - center;
        float b = Vector3.Dot(oc, dir);
        float c = Vector3.Dot(oc, oc) - r * r;
        float d = b * b - c;
        return d < 0f ? -1f : -b - MathF.Sqrt(d);
    }

    // ── Skills ────────────────────────────────────────────────────────────────

    private void TryActivateSkill(Skill skill)
    {
        if (_cooldowns.ContainsKey(skill.Id))
        {
            AddFloatText("Not ready", Theme.ForegroundDim); return;
        }
        if (_player.CurrentMana < skill.ManaCost)
        {
            AddFloatText("Not enough mana", new Color(60, 120, 210)); return;
        }

        switch (skill.Target)
        {
            case SkillTarget.SingleEnemy:
                if (_target == null || !_target.IsAlive)
                {
                    AddFloatText("No target", new Color(200, 100, 60)); return;
                }
                float dist = Vector3.Distance(
                    new Vector3(_pos.X, 0f, _pos.Z),
                    new Vector3(_target.Position.X, 0f, _target.Position.Z));
                if (dist > AttackRange)
                {
                    // Queue skill and start auto-moving
                    _pendingSkill = skill;
                    _autoMoving   = true;
                    return;
                }
                ExecuteSkill(skill, _target);
                break;

            case SkillTarget.AllEnemies:
                ExecuteSkill(skill, null);
                break;

            case SkillTarget.Self:
            case SkillTarget.SingleAlly:
                ExecuteSkill(skill, null);
                break;
        }
    }

    private void ExecuteSkill(Skill skill, WorldMonster? singleTarget)
    {
        _player.CurrentMana = Math.Max(0, _player.CurrentMana - skill.ManaCost);
        _cooldowns[skill.Id] = GlobalCooldown;
        AudioService.Play(Sfx.SkillCast, 0.55f);

        switch (skill.Target)
        {
            case SkillTarget.SingleEnemy when singleTarget is { IsAlive: true }:
            {
                if (RollMiss(singleTarget)) { ShowInfo("Miss!", new Color(180, 180, 180)); break; }
                int dmg = CalcDamage(skill, singleTarget);
                singleTarget.Data.TakeDamage(dmg);
                AddFloatText($"-{dmg}", new Color(220, 80, 80));
                TryApplyStatus(skill, singleTarget);
                if (!singleTarget.IsAlive)
                {
                    OnMonsterKilled(singleTarget);
                    _target = null;
                }
                break;
            }

            case SkillTarget.AllEnemies:
            {
                int hits  = 0;
                int total = 0;
                foreach (var m in _monsters.Where(m => m.IsAlive))
                {
                    float d = Vector3.Distance(
                        new Vector3(_pos.X, 0f, _pos.Z),
                        new Vector3(m.Position.X, 0f, m.Position.Z));
                    if (d > AoeRadius) continue;
                    if (RollMiss(m)) continue;
                    int dmg = CalcDamage(skill, m);
                    m.Data.TakeDamage(dmg);
                    m.HitFlashTimer = 0.22f;
                    TryApplyStatus(skill, m);
                    total += dmg; hits++;
                    if (!m.IsAlive)
                    {
                        OnMonsterKilled(m);
                        if (m == _target) _target = null;
                    }
                }
                if (hits > 0) AddFloatText($"AoE -{total} ({hits} hit)", new Color(220, 160, 60));
                break;
            }

            case SkillTarget.Self:
            case SkillTarget.SingleAlly:
                if (skill.IsHealing)
                {
                    int heal = Math.Max(1, (int)(_player.TotalMagicAttack * skill.ScalingFactor));
                    _player.CurrentHealth = Math.Min(_player.MaxHealth, _player.CurrentHealth + heal);
                    AddFloatText($"+{heal} HP", new Color(80, 200, 80));
                }
                AudioService.Play(Sfx.SkillCast, 0.6f);
                break;
        }
    }

    // GM1/GM3/GM6: centralised kill handler
    private void OnMonsterKilled(WorldMonster m)
    {
        // GM1: XP
        _player.GainXp(m.Data.Exp);
        AddFloatText($"Defeated! +{m.Data.Exp} EXP", Theme.GoldSoft);

        // GM6: Quest kill tracking
        foreach (var quest in _player.ActiveQuests.Where(q =>
            q.Status == MyriaLib.Systems.Enums.QuestStatus.InProgress &&
            q.RequiredKills.ContainsKey(m.Data.Id)))
        {
            quest.KillProgress.TryGetValue(m.Data.Id, out int cur);
            quest.KillProgress[m.Data.Id] = cur + 1;

            bool killsDone = quest.RequiredKills.All(kv =>
                quest.KillProgress.TryGetValue(kv.Key, out int k) && k >= kv.Value);
            bool itemsDone = quest.RequiredItems.All(kv =>
                quest.ItemProgress.TryGetValue(kv.Key, out int i) && i >= kv.Value);
            if (killsDone && itemsDone)
            {
                quest.Status = MyriaLib.Systems.Enums.QuestStatus.Completed;
                AddFloatText($"Quest complete: {quest.Name}", Theme.GoldSoft);
            }
        }

        // GM3: Loot drops
        var loot = LootGenerator.GetLootFor(m.Data);
        if (loot.Count > 0)
        {
            foreach (var item in loot)
                _player.Inventory.AddItem(item, _player);

            // UI5: brief loot notification
            string lootLine = string.Join(", ", loot
                .GroupBy(i => i.Name)
                .Select(g => g.Sum(x => x.StackSize) > 1
                    ? $"+{g.Sum(x => x.StackSize)} {g.Key}" : $"+{g.Key}"));
            AddFloatText(lootLine, new Color(170, 210, 120));
        }
    }

    private const float CritChance     = 0.20f;
    private const float CritMultiplier = 1.8f;

    private void DoAutoAttack(WorldMonster target)
    {
        if (RollMiss(target)) { ShowInfo("Miss!", new Color(180, 180, 180)); return; }

        int atk = _player.TotalPhysicalAttack;
        int raw = Math.Max(atk / 5, atk - target.Data.DefandPhysical())
                + _player.Level * 5;

        bool isCrit = Random.Shared.NextSingle() < CritChance;
        int  dmg    = isCrit ? (int)(raw * CritMultiplier) : raw;

        target.Data.TakeDamage(dmg);
        AddFloatText($"-{dmg}", new Color(220, 120, 80));
        if (!target.IsAlive)
        {
            OnMonsterKilled(target);
            _target        = null;
            _autoAttacking = false;
        }
    }

    private void TryPickupNearbyLoot()
    {
        bool any = false;
        for (int i = _lootDrops.Count - 1; i >= 0; i--)
        {
            var drop = _lootDrops[i];
            float dist = Vector3.Distance(
                new Vector3(_pos.X, 0f, _pos.Z),
                new Vector3(drop.Position.X, 0f, drop.Position.Z));
            if (dist > PickupRange) continue;

            if (drop.Gold > 0)
            {
                _player.Money.TryAdd(drop.Gold);
                ShowInfo($"+{drop.Gold} Gold", new Color(220, 185, 60));
                drop.TakeGold();
            }

            foreach (var item in drop.Items.ToList())
            {
                if (_player.Inventory.AddItem(item, _player))
                {
                    AudioService.Play(Sfx.PickUp, 0.6f);
                    ShowInfo($"+ {item.Name}", new Color(180, 220, 120));
                    drop.Items.Remove(item);
                }
            }

            if (drop.IsEmpty) _lootDrops.RemoveAt(i);
            any = true;
        }

        if (!any) ShowInfo("Nothing nearby", Theme.ForegroundDim);
    }

    private int CalcDamage(Skill skill, WorldMonster target)
    {
        int attack  = skill.Type == SkillType.Physical
            ? _player.TotalPhysicalAttack : _player.TotalMagicAttack;
        int defense = skill.Type == SkillType.Physical
            ? target.Data.DefandPhysical() : target.Data.TotalMagicDefense;
        return Math.Max(1, (int)(attack * skill.ScalingFactor) - defense);
    }

    /// <summary>Returns true if the attack should miss.</summary>
    private bool RollMiss(WorldMonster target)
    {
        // Base 5% + 3% per level above player; max 40%
        int levelDiff = Math.Max(0, target.Data.Level - _player.Level);
        // Dex difference reduces miss chance
        int dexDiff   = Math.Max(0, target.Data.TotalDEX - _player.TotalDEX);
        float chance  = 0.05f + levelDiff * 0.03f + dexDiff * 0.002f;
        return Random.Shared.NextSingle() < Math.Min(chance, 0.40f);
    }

    /// <summary>Applies poison or stun based on skill type and a random roll.</summary>
    private void TryApplyStatus(Skill skill, WorldMonster target)
    {
        if (!target.IsAlive) return;

        if (skill.Type == SkillType.Magical && Random.Shared.NextSingle() < 0.22f && !target.IsPoisoned)
        {
            target.PoisonTimer  = 5f;
            target.PoisonTick   = 1f;
            target.PoisonDamage = Math.Max(1, _player.TotalMagicAttack / 8);
            ShowInfo("Poisoned!", new Color(60, 200, 60));
        }
        else if (skill.Type == SkillType.Physical && Random.Shared.NextSingle() < 0.12f && !target.IsStunned)
        {
            target.StunTimer = 1.8f;
            ShowInfo("Stunned!", new Color(140, 140, 220));
        }
    }

    // ── Death / respawn ───────────────────────────────────────────────────────

    private void TriggerDeath()
    {
        _isDead        = true;
        _deathTimer    = DeathRespawnDelay;
        _autoAttacking = false;
        _autoMoving    = false;
        _pendingSkill  = null;
        _target        = null;
        _cooldowns.Clear();
        _floatTexts.Clear();
        _player.ApplyDeathXpPenalty();  // GM5
    }

    private void Respawn()
    {
        _isDead = false;
        _player.CurrentHealth = _player.MaxHealth;
        _player.CurrentMana   = _player.MaxMana;
        _pos = RespawnPoint;
        _yaw = 0f;

        int startFace = _navMesh.FindFaceIndex(new Vector2(RespawnPoint.X, RespawnPoint.Z));
        UpdateFace(startFace, announce: false);
        _pos.Y = TerrainHeights.GetHeight(_heights, _navMesh, _currentFace, _pos.X, _pos.Z);

        // Deaggro all known monsters so they don't instantly re-engage
        foreach (var (_, list) in _faceMonsters)
            foreach (var m in list)
                if (m.State == MonsterAiState.Aggroed)
                    m.State = MonsterAiState.Idle;
    }

    // ── Draw ──────────────────────────────────────────────────────────────────

    public override void Draw(SpriteBatch sb)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;
        gd.Clear(_weather.SkyColor);

        gd.DepthStencilState = DepthStencilState.Default;
        gd.RasterizerState   = RasterizerState.CullCounterClockwise;
        gd.BlendState        = BlendState.Opaque;
        gd.SamplerStates[0]  = SamplerState.LinearClamp;

        float cosP     = MathF.Cos(_camPitch);
        Vector3 headPos = _pos + new Vector3(0f, 1.4f, 0f);
        Vector3 camOff  = new Vector3(
            MathF.Sin(_camYaw) * cosP * CamDist,
           -MathF.Sin(_camPitch) * CamDist,
            MathF.Cos(_camYaw) * cosP * CamDist);

        Matrix view = Matrix.CreateLookAt(headPos + camOff, headPos, Vector3.Up);
        Matrix proj = Matrix.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(70f), (float)_sw / _sh, 0.05f, 600f);

        _effect.View       = view;
        _effect.Projection = proj;
        _lastView = view;
        _lastProj = proj;

        // Navmesh floor
        _navRenderer.Draw(gd, _effect);

        gd.RasterizerState = RasterizerState.CullCounterClockwise;

        // World decorations (trees, rocks, pillars, etc.)
        _decorations.Draw(gd, _effect);

        // NPCs
        foreach (var npc in _npcs)
        {
            _effect.World = Matrix.CreateTranslation(npc.Position);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    npc.MeshVerts, 0, npc.MeshVerts.Length,
                    npc.MeshIdx,   0, npc.MeshIdx.Length / 3);
            }
        }

        // Gather nodes
        foreach (var node in _gatherNodes)
        {
            _effect.World = Matrix.CreateTranslation(node.Position);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    node.MeshVerts, 0, node.MeshVerts.Length,
                    node.MeshIdx,   0, node.MeshIdx.Length / 3);
            }
        }

        // Monsters
        foreach (var m in _monsters)
        {
            if (!m.IsAlive) continue;

            // Target ring
            if (m == _target)
                DrawTargetRing(gd, m.Position);

            _effect.World = Matrix.CreateRotationY(m.Yaw) * Matrix.CreateTranslation(m.Position);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    m.MeshVerts, 0, m.MeshVerts.Length,
                    m.MeshIdx,   0, m.MeshIdx.Length / 3);
            }

            // Status overlay (additive): white flash on hit, dim colour tint for poison/stun
            float flashAmt  = m.HitFlashTimer > 0f ? Math.Clamp(m.HitFlashTimer / 0.18f, 0f, 1f) : 0f;
            Color overlayCol = m.HitFlashTimer > 0f ? Color.White
                             : m.IsPoisoned         ? new Color(30, 130, 30)
                             : m.IsStunned           ? new Color(60, 60, 160)
                             : Color.Transparent;

            if (overlayCol != Color.Transparent)
            {
                float oa = m.HitFlashTimer > 0f ? flashAmt * 0.85f : 0.30f;
                DrawMonsterOverlay(gd, m, overlayCol * oa);
            }
        }

        // Character (hidden while dead)
        if (!_isDead)
        {
            _effect.World = Matrix.CreateRotationY(_yaw) * Matrix.CreateTranslation(_pos);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _playerVerts, 0, _playerVerts.Length,
                    _playerIdx,   0, _playerIdx.Length / 3);
            }
        }

        // Loot drops (flat quads on the floor)
        foreach (var drop in _lootDrops)
            DrawLootDrop(gd, drop);

        // Waypoint shrines (geometry baked into decoVerts, but WorldWaypoint also has its own mesh)
        _effect.World = Matrix.Identity;
        foreach (var wp in _waypoints)
        {
            _effect.World = Matrix.CreateTranslation(wp.Position);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    wp.MeshVerts, 0, wp.MeshVerts.Length,
                    wp.MeshIdx,   0, wp.MeshIdx.Length / 3);
            }
        }
        _effect.World = Matrix.Identity;

        // NPCs (static entities — no rotation needed)
        foreach (var npc in _worldNpcs)
        {
            _effect.World = Matrix.CreateTranslation(npc.Position);
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    npc.MeshVerts, 0, npc.MeshVerts.Length,
                    npc.MeshIdx,   0, npc.MeshIdx.Length / 3);
            }
        }

        // 2-D HUD
        gd.DepthStencilState = DepthStencilState.None;
        gd.BlendState        = BlendState.AlphaBlend;
        sb.Begin();
        // Day/night tint over the 3D world, under the HUD
        Color nightTint = _dayNight.GetOverlayColor();
        if (nightTint.A > 0)
            Gfx.Rect(sb, 0, 0, _sw, _sh, nightTint);
        // Weather overlay (atmospheric darkening + rain drops)
        _weather.DrawOverlay(sb, _sw, _sh);
        DrawHud(sb);
        if (_isDead)          DrawDeathOverlay(sb);
        if (_showInventory)   DrawInventoryOverlay(sb);
        if (_showPause)       DrawPauseOverlay(sb);
        if (_npcDialog != null) DrawNpcDialog(sb);
        DrawNpcLabels(sb);
        DrawNodeLabels(sb);
        DrawSavedFlash(sb);
        sb.End();
    }

    private void DrawTargetRing(GraphicsDevice gd, Vector3 center)
    {
        const int   segs   = 20;
        const float radius = 0.75f;
        var verts = new VertexPositionColor[segs * 2];
        var gold  = new Color(220, 185, 60);
        for (int i = 0; i < segs; i++)
        {
            float a0 = MathF.Tau * i / segs;
            float a1 = MathF.Tau * (i + 1) / segs;
            verts[i * 2]     = new VertexPositionColor(
                new Vector3(center.X + MathF.Cos(a0) * radius, 0.06f, center.Z + MathF.Sin(a0) * radius), gold);
            verts[i * 2 + 1] = new VertexPositionColor(
                new Vector3(center.X + MathF.Cos(a1) * radius, 0.06f, center.Z + MathF.Sin(a1) * radius), gold);
        }
        _effect.World = Matrix.Identity;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserPrimitives(PrimitiveType.LineList, verts, 0, segs);
        }
    }

    private void DrawMonsterOverlay(GraphicsDevice gd, WorldMonster m, Color col)
    {
        // Re-upload mesh with flat overlay colour in additive blend
        var verts = new VertexPositionColor[m.MeshVerts.Length];
        for (int i = 0; i < verts.Length; i++)
            verts[i] = new VertexPositionColor(m.MeshVerts[i].Position, col);

        var prevBlend = gd.BlendState;
        gd.BlendState = BlendState.Additive;
        _effect.World = Matrix.CreateRotationY(m.Yaw) * Matrix.CreateTranslation(m.Position);
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                verts, 0, verts.Length, m.MeshIdx, 0, m.MeshIdx.Length / 3);
        }
        gd.BlendState = prevBlend;
    }

    private void DrawLootDrop(GraphicsDevice gd, WorldLootDrop drop)
    {
        const float hs = 0.28f;
        const float y  = 0.07f;

        var gold  = new Color(220, 185, 60);
        var verts = new VertexPositionColor[]
        {
            new(new Vector3(drop.Position.X - hs, y, drop.Position.Z - hs), gold),
            new(new Vector3(drop.Position.X + hs, y, drop.Position.Z - hs), gold),
            new(new Vector3(drop.Position.X - hs, y, drop.Position.Z + hs), gold),
            new(new Vector3(drop.Position.X + hs, y, drop.Position.Z + hs), gold),
        };
        int[] idx = [0, 1, 2, 1, 3, 2];

        _effect.World = Matrix.Identity;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, verts, 0, 4, idx, 0, 2);
        }
    }

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void DrawHud(SpriteBatch sb)
    {
        // ── Character info (top-left) ────────────────────────────────────────────
        string info = $"{_player.Name}   Lv. {_player.Level}  {_player.Class}   {_player.Money.Balance.BronzeTotal} Gold";
        Gfx.Rect(sb, 0, 0, (int)Assets.FontNormal.MeasureString(info).X + 28, 40,
            new Color(0, 0, 0, 140));
        Gfx.Text(sb, Assets.FontNormal, info, new Vector2(14, 10), Theme.GoldSoft);

        DrawBar(sb, 14, 48, 180, 10, _player.CurrentHealth, _player.MaxHealth,
            new Color(80, 180, 80), "HP");
        DrawBar(sb, 14, 64, 180, 10, _player.CurrentMana, _player.MaxMana,
            new Color(60, 120, 210), "MP");
        DrawBar(sb, 14, 80, 180, 10, (int)_player.Experience, (int)_player.ExpForNextLvl,
            new Color(180, 140, 30), "XP");

        // UI2: XP bar
        long xpCur  = _player.Experience;
        long xpNext = Math.Max(1, _player.ExpForNextLvl);
        DrawBar(sb, 14, 80, 180, 8,
            (int)Math.Min(xpCur, int.MaxValue),
            (int)Math.Min(xpNext, int.MaxValue),
            new Color(170, 130, 40), "XP");

        // Money display
        string moneyStr = _player.Money.Balance.ToString("C", null);
        var mSz = Assets.FontSmall.MeasureString(moneyStr);
        Gfx.Rect(sb, 14, 96, (int)mSz.X + 12, 16, new Color(0, 0, 0, 110));
        Gfx.Text(sb, Assets.FontSmall, moneyStr, new Vector2(18, 97), new Color(200, 185, 115));

        if (_currentRoom?.HasMonsters == true)
        {
            int alive      = _monsters.Count(m => m.IsAlive);
            int respawning = _monsters.Count(m => m.IsRespawning);
            string danger  = $"! HOSTILE  {alive} alive  {respawning} respawning";
            var dSz = Assets.FontSmall.MeasureString(danger);
            Gfx.Rect(sb, 14, 116, (int)dSz.X + 16, 18, new Color(100, 30, 30, 180));
            Gfx.Text(sb, Assets.FontSmall, danger, new Vector2(22, 118), new Color(220, 100, 80));
        }

        // GM2: Level-up overlay
        if (_levelUpTimer > 0f)
        {
            float alpha = MathHelper.Clamp(_levelUpTimer, 0f, 1f);
            string lvMsg = $"Level Up!  Now Level {_levelUpTo}";
            var lvSz = Assets.FontMedium.MeasureString(lvMsg);
            int lvY = _sh / 4;
            Gfx.Rect(sb, (int)((_sw - lvSz.X) / 2f) - 28, lvY - 10,
                (int)lvSz.X + 56, (int)lvSz.Y + 20, new Color(0, 0, 0, (int)(160 * alpha)));
            Gfx.Text(sb, Assets.FontMedium, lvMsg,
                new Vector2((_sw - lvSz.X) / 2f, lvY), Theme.GoldSoft * alpha);
        }

        // ── Minimap + clock (top-right) ───────────────────────────────────────
        int mapX = _sw - MinimapRenderer.Size - 14;
        int mapY = 14;
        _minimapRenderer.Draw(sb, mapX, mapY, _pos, _worldNpcs);

        // ── Target panel (top-right) ──────────────────────────────────────────
        if (_target != null)
        {
            const int panelW = 220, panelH = 58;
            int px = _sw - panelW - 14, py = mapY + MinimapRenderer.Size + 8;
            Gfx.Rect(sb, px, py, panelW, panelH, Theme.PanelBg);
            Gfx.Border(sb, new Rectangle(px, py, panelW, panelH),
                _target.IsAlive ? Theme.Gold * 0.7f : Theme.Gold * 0.3f);
            Gfx.Text(sb, Assets.FontNormal, _target.Data.Name, new Vector2(px + 10, py + 8),
                _target.IsAlive ? Theme.Foreground : Theme.ForegroundDim);
            if (_target.IsAlive)
            {
                DrawBar(sb, px + 10, py + 36, panelW - 20, 10,
                    _target.Data.CurrentHealth, _target.Data.MaxHealth,
                    new Color(180, 60, 60), "HP");
            }
            else if (_target.IsRespawning)
            {
                string rt = $"Respawn: {(int)Math.Ceiling(_target.RespawnTimeRemaining)}s";
                var rtSz = Assets.FontSmall.MeasureString(rt);
                Gfx.Text(sb, Assets.FontSmall, rt,
                    new Vector2(px + (panelW - rtSz.X) / 2f, py + 34),
                    new Color(140, 140, 220));
            }
        }

        // ── Active quest tracker (below target panel) ─────────────────────────
        DrawQuestTracker(sb, mapY + MinimapRenderer.Size + 8 + (_target != null ? 66 : 0));

        // ── Room name announcement ────────────────────────────────────────────
        if (_roomNameTimer > 0f && _roomName.Length > 0)
        {
            float alpha = MathHelper.Clamp(_roomNameTimer, 0f, 1f);
            var   sz    = Assets.FontMedium.MeasureString(_roomName);
            int   ry    = _sh / 3;
            Gfx.Rect(sb, (int)((_sw - sz.X) / 2f) - 24, ry - 8,
                (int)sz.X + 48, (int)sz.Y + 16, new Color(0, 0, 0, (int)(130 * alpha)));
            Gfx.Text(sb, Assets.FontMedium, _roomName,
                new Vector2((_sw - sz.X) / 2f, ry), Theme.GoldSoft * alpha);
        }

        // UI3: Floating combat / status text
        foreach (var ft in _floatTexts)
        {
            float elapsed = FloatTextTime - ft.Timer;
            float dy      = elapsed / FloatTextTime * FloatDriftPx;
            float alpha   = MathHelper.Clamp(ft.Timer / (FloatTextTime * 0.4f), 0f, 1f);
            var   sz      = Assets.FontNormal.MeasureString(ft.Msg);
            Gfx.Text(sb, Assets.FontNormal, ft.Msg,
                new Vector2(ft.X - sz.X / 2f, ft.StartY - dy),
                ft.Color * alpha);
        }

        // ── Level-up banner (above room name) ────────────────────────────────
        if (_levelUpTimer > 0f && _levelUpMsg.Length > 0)
        {
            float alpha = MathHelper.Clamp(_levelUpTimer, 0f, 1f);
            var   sz    = Assets.FontMedium.MeasureString(_levelUpMsg);
            int   ry    = _sh / 3 - 60;
            Gfx.Rect(sb, (int)((_sw - sz.X) / 2f) - 36, ry - 12,
                (int)sz.X + 72, (int)sz.Y + 24, new Color(0, 0, 0, (int)(190 * alpha)));
            Gfx.Text(sb, Assets.FontMedium, _levelUpMsg,
                new Vector2((_sw - sz.X) / 2f, ry), new Color(220, 185, 60) * alpha);
        }

        // ── Nearby interaction prompt (NPC talk or loot pickup) ───────────────
        if (!_isDead && !_dialogueOverlay.IsOpen)
        {
            WorldNpc? nearNpc = _worldNpcs.FirstOrDefault(n =>
                Vector3.Distance(new Vector3(_pos.X, 0f, _pos.Z),
                                 new Vector3(n.Position.X, 0f, n.Position.Z)) <= WorldNpc.InteractRange);
            bool nearLoot = _lootDrops.Any(d => Vector3.Distance(
                new Vector3(_pos.X, 0f, _pos.Z),
                new Vector3(d.Position.X, 0f, d.Position.Z)) <= PickupRange);

            string? prompt     = null;
            Color   promptCol  = Color.White;
            if (nearNpc != null)   { prompt = $"F  Talk to {nearNpc.Name}"; promptCol = new Color(80, 200, 120); }
            else if (nearLoot)     { prompt = "F  Pick up";                 promptCol = Theme.GoldSoft; }

            if (prompt != null)
            {
                var phSz = Assets.FontNormal.MeasureString(prompt);
                int phY  = _sh / 2 + 50;
                Gfx.Rect(sb, (int)((_sw - phSz.X) / 2f) - 14, phY - 4,
                    (int)phSz.X + 28, (int)phSz.Y + 8, new Color(0, 0, 0, 150));
                Gfx.Text(sb, Assets.FontNormal, prompt,
                    new Vector2((_sw - phSz.X) / 2f, phY), promptCol);
            }
        }

        // ── Skill bar (bottom centre) — hidden while inventory is open ───────
        if (!_inventoryOpen) DrawSkillBar(sb);

        // ── Control hint (very bottom) ────────────────────────────────────────
        const string hint = "WASD  Move   Tab  Target   1-9  Skills   I  Inventory   F  Talk   G  Gather   F5  Save   ESC  Menu";
        var hSz = Assets.FontSmall.MeasureString(hint);
        Gfx.Rect(sb, (int)((_sw - hSz.X) / 2f) - 8, _sh - 28, (int)hSz.X + 16, 22,
            new Color(0, 0, 0, 120));
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2((_sw - hSz.X) / 2f, _sh - 24), Theme.ForegroundDim);
    }

    private void DrawQuestTracker(SpriteBatch sb, int py)
    {
        // Pick the first in-progress or just-completed quest if tracked one is gone
        if (_trackedQuest == null || !_player.ActiveQuests.Contains(_trackedQuest))
            _trackedQuest = _player.ActiveQuests.FirstOrDefault();
        if (_trackedQuest == null) return;

        var q = _trackedQuest;
        const int panelW = 230;
        int px = _sw - panelW - 14;

        bool ready = q.Status == QuestStatus.Completed;

        // Collect objective lines
        var lines = new List<(string text, bool done)>();
        foreach (var (id, req) in q.RequiredKills)
        {
            q.KillProgress.TryGetValue(id, out int got);
            string mobName = GetMonsterName(id);
            lines.Add(($"  {mobName}: {got}/{req}", got >= req));
        }
        foreach (var (itemId, req) in q.RequiredItems)
            lines.Add(($"  {itemId} (x{req})", false));

        if (lines.Count == 0)
            lines.Add(("  Return to quest giver", ready));

        if (ready && q.RequiredKills.Count > 0)
            lines.Add(("  >> Return to quest giver!", true));

        int panelH = 20 + lines.Count * 16 + 8;
        Gfx.Rect(sb, px, py, panelW, panelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, panelW, panelH),
            ready ? new Color(120, 220, 80) * 0.8f : Theme.Gold * 0.45f);

        // Title
        string title = WorldDataService.Localize(q.Name);
        if (title.Length > 25) title = title[..25] + "..";
        Gfx.Text(sb, Assets.FontSmall, title, new Vector2(px + 8, py + 4),
            ready ? new Color(120, 220, 80) : Theme.GoldSoft);

        for (int i = 0; i < lines.Count; i++)
        {
            Color col = lines[i].done ? new Color(120, 220, 80) : Theme.ForegroundDim;
            Gfx.Text(sb, Assets.FontSmall, lines[i].text,
                new Vector2(px + 8, py + 20 + i * 16), col);
        }
    }

    private static string GetMonsterName(int monsterId)
    {
        var m = MyriaLib.Services.MonsterService.GetMonsterById(monsterId);
        return m != null ? WorldDataService.Localize(m.Name) : $"#{monsterId}";
    }

    private void DrawSkillBar(SpriteBatch sb)
    {
        const int slotSz = 62, gap = 4;
        int count = Math.Min(_player.Skills.Count, 9);
        if (count == 0) return;

        int barX = (_sw - (count * (slotSz + gap) - gap)) / 2;
        int barY = _sh - slotSz - 38;

        for (int i = 0; i < count; i++)
        {
            var  skill    = _player.Skills[i];
            int  sx       = barX + i * (slotSz + gap);
            bool hasMana  = _player.CurrentMana >= skill.ManaCost;
            bool hasTarget = skill.Target != SkillTarget.SingleEnemy || (_target is { IsAlive: true });
            bool onCd     = _cooldowns.ContainsKey(skill.Id);
            bool usable   = hasMana && hasTarget && !onCd;

            // Slot background
            Color bg = usable ? Theme.PanelDark : new Color(18, 14, 22, 210);
            Gfx.Rect(sb, sx, barY, slotSz, slotSz, bg);
            Gfx.Border(sb, new Rectangle(sx, barY, slotSz, slotSz),
                usable ? Theme.Gold * 0.75f : Theme.Gold * 0.25f);

            // Hotkey number
            Gfx.Text(sb, Assets.FontSmall, $"{i + 1}",
                new Vector2(sx + 4, barY + 3), Theme.ForegroundDim);

            // Skill name (truncate to fit)
            string name = skill.Name.Length > 9 ? skill.Name[..9] : skill.Name;
            var    nSz  = Assets.FontSmall.MeasureString(name);
            Gfx.Text(sb, Assets.FontSmall, name,
                new Vector2(sx + (slotSz - nSz.X) / 2f, barY + 22),
                usable ? Theme.Foreground : Theme.ForegroundDim);

            // Mana cost (bottom-left)
            Gfx.Text(sb, Assets.FontSmall, $"{skill.ManaCost}mp",
                new Vector2(sx + 4, barY + slotSz - 16), new Color(60, 110, 200));

            // Cooldown overlay
            if (onCd && _cooldowns.TryGetValue(skill.Id, out float cd))
            {
                float frac = Math.Clamp(cd / GlobalCooldown, 0f, 1f);
                int   coverH = (int)(slotSz * frac);
                Gfx.Rect(sb, sx, barY, slotSz, coverH, new Color(0, 0, 0, 160));
                var cdSz = Assets.FontSmall.MeasureString($"{cd:F1}");
                Gfx.Text(sb, Assets.FontSmall, $"{cd:F1}",
                    new Vector2(sx + (slotSz - cdSz.X) / 2f, barY + slotSz / 2f - 7f),
                    Theme.Gold);
            }

            // "NO TARGET" label when single-enemy skill has no target
            if (!hasTarget)
            {
                var noSz = Assets.FontSmall.MeasureString("TARGET");
                Gfx.Text(sb, Assets.FontSmall, "TARGET",
                    new Vector2(sx + (slotSz - noSz.X) / 2f, barY + slotSz - 28),
                    new Color(200, 80, 50));
            }
        }
    }

    private void DrawDeathOverlay(SpriteBatch sb)
    {
        // Full-screen dark vignette
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(40, 0, 0, 200));

        float cx = _sw / 2f;
        float cy = _sh / 2f;

        // "You Died"
        var titleSz = Assets.FontMedium.MeasureString("You Died");
        Gfx.Text(sb, Assets.FontMedium, "You Died",
            new Vector2(cx - titleSz.X / 2f, cy - 60),
            new Color(210, 50, 50));

        // Countdown / prompt
        int secs   = Math.Max(0, (int)Math.Ceiling(_deathTimer));
        string sub = secs > 0
            ? $"Respawning in {secs}s   -   R to respawn now"
            : "Respawning...";
        var subSz = Assets.FontNormal.MeasureString(sub);
        Gfx.Text(sb, Assets.FontNormal, sub,
            new Vector2(cx - subSz.X / 2f, cy),
            new Color(180, 120, 120));
    }

    private static void DrawBar(SpriteBatch sb, int x, int y, int w, int h,
        int current, int max, Color fill, string label)
    {
        Gfx.Rect(sb, x, y, w, h, new Color(30, 30, 30, 180));
        if (max > 0)
            Gfx.Rect(sb, x, y, (int)(w * Math.Clamp((float)current / max, 0f, 1f)), h, fill);
        Gfx.Border(sb, new Rectangle(x, y, w, h), Theme.Gold * 0.5f);
        Gfx.Text(sb, Assets.FontSmall, $"{label} {current}/{max}",
            new Vector2(x + w + 6, y - 1), Theme.ForegroundDim);
    }

    // NPC labels + dialog ─────────────────────────────────────────────────────

    private void DrawNpcLabels(SpriteBatch sb)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;
        foreach (var npc in _npcs)
        {
            // Project a point 2.1 units above the NPC's feet to screen space
            Vector3 worldTop = npc.Position + new Vector3(0f, 2.15f, 0f);
            Vector3 screen   = gd.Viewport.Project(worldTop, _lastProj, _lastView, Matrix.Identity);
            if (screen.Z >= 1f) continue;   // behind camera

            bool isNearby = npc == _nearbyNpc;

            // Name label
            string name = npc.DisplayName;
            var    nSz  = Assets.FontSmall.MeasureString(name);
            float  nx   = screen.X - nSz.X / 2f;
            float  ny   = screen.Y - nSz.Y;

            // Shadow
            Gfx.Text(sb, Assets.FontSmall, name, new Vector2(nx + 1, ny + 1),
                Color.Black * 0.6f);
            Gfx.Text(sb, Assets.FontSmall, name, new Vector2(nx, ny),
                isNearby ? Theme.GoldSoft : Theme.Foreground * 0.85f);

            // "Press F" prompt when nearby
            if (isNearby)
            {
                const string prompt = "[F] Talk";
                var pSz = Assets.FontSmall.MeasureString(prompt);
                float px = screen.X - pSz.X / 2f;
                float py = ny - pSz.Y - 2f;
                Gfx.Text(sb, Assets.FontSmall, prompt, new Vector2(px + 1, py + 1),
                    Color.Black * 0.5f);
                Gfx.Text(sb, Assets.FontSmall, prompt, new Vector2(px, py), Theme.Gold);
            }
        }
    }

    private void DrawNodeLabels(SpriteBatch sb)
    {
        var gd = ScreenManager.Instance.GraphicsDevice;

        foreach (var node in _gatherNodes)
        {
            float topOffset = node.Type == GatheringType.Tree ? 3.2f : 1.0f;
            Vector3 worldTop = node.Position + new Vector3(0f, topOffset, 0f);
            Vector3 screen   = gd.Viewport.Project(worldTop, _lastProj, _lastView, Matrix.Identity);
            if (screen.Z >= 1f) continue;

            bool isNearby = node == _nearbyNode;
            bool depleted = node.IsDepleted;
            Color textColor = depleted
                ? Theme.ForegroundDim * 0.7f
                : (isNearby ? Theme.GoldSoft : new Color(180, 210, 170));

            var  lSz = Assets.FontSmall.MeasureString(node.Label);
            float lx = screen.X - lSz.X / 2f;
            float ly = screen.Y - lSz.Y;

            Gfx.Text(sb, Assets.FontSmall, node.Label, new Vector2(lx + 1, ly + 1), Color.Black * 0.55f);
            Gfx.Text(sb, Assets.FontSmall, node.Label, new Vector2(lx, ly), textColor);

            if (isNearby)
            {
                string prompt = depleted ? "(Depleted)" : "[G] Gather";
                Color  pc     = depleted ? new Color(160, 140, 100) : Theme.Gold;
                var    pSz    = Assets.FontSmall.MeasureString(prompt);
                float  px     = screen.X - pSz.X / 2f;
                float  py     = ly - pSz.Y - 2f;
                Gfx.Text(sb, Assets.FontSmall, prompt, new Vector2(px + 1, py + 1), Color.Black * 0.5f);
                Gfx.Text(sb, Assets.FontSmall, prompt, new Vector2(px, py), pc);
            }
        }
    }

    private void HandleNpcDialogClick(MouseState ms)
    {
        if (ms.LeftButton != ButtonState.Released ||
            _prevMouse.LeftButton != ButtonState.Pressed) return;

        var pos = ms.Position;
        if (_npcDialog == null) return;

        // Sub-panel is open — route to its own handler
        if (_activeService != "") { HandleSubPanelClick(pos); return; }

        int panW = 460, panH = 320;
        int px = (_sw - panW) / 2;
        int py = (_sh - panH) / 2;

        // Close button
        var closeR = new Rectangle(px + panW / 2 - 60, py + panH - 58, 120, 42);
        if (closeR.Contains(pos)) { _npcDialog = null; _npcDialogFeedback = ""; _activeService = ""; return; }

        // Service buttons
        var services = _npcDialog.Data.Services;
        int btnW = 130, btnH = 38, btnGap = 8;
        int rowStartX = px + (panW - (services.Count * (btnW + btnGap) - btnGap)) / 2;
        int btnY = py + 188;

        for (int si = 0; si < services.Count; si++)
        {
            if (!new Rectangle(rowStartX + si * (btnW + btnGap), btnY, btnW, btnH).Contains(pos)) continue;

            switch (services[si])
            {
                case "heal":
                    _npcDialog.Data.HealingAction(_player);
                    _npcDialogFeedback      = "You have been healed!";
                    _npcDialogFeedbackTimer = 2.5f;
                    break;
                case "shop_equipment":
                case "buy_items":
                case "shop_general":
                    _activeService = "shop"; _shopScroll = 0; _shopItemIdx = -1;
                    break;
                case "sell_items":
                    _activeService = "sell"; _sellScroll = 0; _sellItemIdx = -1;
                    break;
                case "learn_job":
                    HandleLearnJob();
                    break;
                case "change_class":
                    _activeService  = "class"; _classScroll = 0; _classIdx = -1;
                    _classChoices   = ClassManager.GetAllowedClasses(_player.Race).OrderBy(c => c).ToArray();
                    break;
                case "talk":
                    _npcDialogFeedback      = "...";
                    _npcDialogFeedbackTimer = 1.5f;
                    break;
                default:
                    _npcDialogFeedback      = $"{ServiceLabel(services[si])} — coming soon.";
                    _npcDialogFeedbackTimer = 2f;
                    break;
            }
            break;
        }
    }

    private void HandleLearnJob()
    {
        if (_npcDialog == null) return;
        string? jobId = _npcDialog.Data.MasterJobId;
        if (string.IsNullOrEmpty(jobId)) { _npcDialogFeedback = "No job to learn here."; _npcDialogFeedbackTimer = 2f; return; }
        if (_player.ActiveJobId == jobId) { _npcDialogFeedback = "Already your active job."; _npcDialogFeedbackTimer = 2f; return; }
        if (!JobManager.CanChangeJob(_player))
        {
            int days = (int)JobManager.GetCooldownRemaining(_player).TotalDays + 1;
            _npcDialogFeedback = $"Job change available in {days}d"; _npcDialogFeedbackTimer = 3f; return;
        }
        JobManager.SetActiveJob(_player, jobId);
        var job = JobManager.GetById(jobId);
        string name = job?.Name?.Length > 0 ? job.Name : (char.ToUpper(jobId[0]) + jobId[1..]);
        _npcDialogFeedback = $"Active job: {name}"; _npcDialogFeedbackTimer = 3f;
    }

    private void HandleSubPanelClick(Point pos)
    {
        switch (_activeService)
        {
            case "shop":  HandleShopClick(pos);  break;
            case "sell":  HandleSellClick(pos);  break;
            case "class": HandleClassClick(pos); break;
        }
    }

    private void ScrollSubPanel(int delta)
    {
        if (_activeService == "shop")
            _shopScroll  = Math.Clamp(_shopScroll  + delta, 0, Math.Max(0, (_npcDialog?.Data.ItemRefs.Count  ?? 0) - 12));
        else if (_activeService == "sell")
            _sellScroll  = Math.Clamp(_sellScroll  + delta, 0, Math.Max(0, _player.Inventory.Items.Count - 12));
        else if (_activeService == "class")
            _classScroll = Math.Clamp(_classScroll + delta, 0, Math.Max(0, _classChoices.Length - 12));
    }

    private void HandleShopClick(Point pos)
    {
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        const int listX = 10, listY = 52, listW = 248, rowH = 23, visRows = 12;
        int dx = px + listX + listW + 14;

        if (new Rectangle(px + W / 2 - 60, py + H - 46, 120, 36).Contains(pos)) { _activeService = ""; return; }

        var items = _npcDialog!.Data.ItemRefs;
        for (int i = 0; i < visRows; i++)
        {
            int idx = _shopScroll + i;
            if (idx >= items.Count) break;
            if (new Rectangle(px + listX, py + listY + i * rowH, listW, rowH - 1).Contains(pos)) { _shopItemIdx = idx; return; }
        }

        if (_shopItemIdx >= 0 && _shopItemIdx < items.Count
            && new Rectangle(dx, py + 300, 120, 36).Contains(pos))
        {
            var result = _npcDialog.Data.BuyItem(_player, items[_shopItemIdx]);
            _npcDialogFeedback = result.Success ? "Purchased!" : result.MessageKey switch
            {
                "npc.action.buy.notEnoughMoney" => "Not enough money.",
                "npc.action.buy.inventoryFull"  => "Inventory full.",
                _                               => "Cannot purchase."
            };
            _npcDialogFeedbackTimer = 2.5f;
        }
    }

    private void HandleSellClick(Point pos)
    {
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        const int listX = 10, listY = 52, listW = 248, rowH = 23, visRows = 12;
        int dx = px + listX + listW + 14;

        if (new Rectangle(px + W / 2 - 60, py + H - 46, 120, 36).Contains(pos)) { _activeService = ""; return; }

        var items = _player.Inventory.Items;
        for (int i = 0; i < visRows; i++)
        {
            int idx = _sellScroll + i;
            if (idx >= items.Count) break;
            if (new Rectangle(px + listX, py + listY + i * rowH, listW, rowH - 1).Contains(pos)) { _sellItemIdx = idx; return; }
        }

        if (_sellItemIdx >= 0 && _sellItemIdx < items.Count)
        {
            var item = items[_sellItemIdx];
            // Sell 1
            if (new Rectangle(dx, py + 296, 110, 34).Contains(pos))
            {
                int val = JobManager.GetSellValue(item, _player);
                var r   = _npcDialog!.Data.SellItem(_player, item, 1);
                _npcDialogFeedback = r.Success ? $"Sold! +{val} bronze" : "Cannot sell.";
                _npcDialogFeedbackTimer = 2.5f;
                _sellItemIdx = Math.Min(_sellItemIdx, Math.Max(0, _player.Inventory.Items.Count - 1));
            }
            // Sell All
            else if (item.StackSize > 1 && new Rectangle(dx + 118, py + 296, 110, 34).Contains(pos))
            {
                int total = JobManager.GetSellValue(item, _player) * item.StackSize;
                var r     = _npcDialog!.Data.SellItem(_player, item, item.StackSize);
                _npcDialogFeedback = r.Success ? $"Sold all! +{total} bronze" : "Cannot sell.";
                _npcDialogFeedbackTimer = 2.5f;
                _sellItemIdx = Math.Min(_sellItemIdx, Math.Max(0, _player.Inventory.Items.Count - 1));
            }
        }
    }

    private void HandleClassClick(Point pos)
    {
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        const int listX = 10, listY = 52, listW = 248, rowH = 23, visRows = 12;
        int dx = px + listX + listW + 14;

        if (new Rectangle(px + W / 2 - 60, py + H - 46, 120, 36).Contains(pos)) { _activeService = ""; return; }

        for (int i = 0; i < visRows; i++)
        {
            int idx = _classScroll + i;
            if (idx >= _classChoices.Length) break;
            if (new Rectangle(px + listX, py + listY + i * rowH, listW, rowH - 1).Contains(pos)) { _classIdx = idx; return; }
        }

        if (_classIdx >= 0 && _classIdx < _classChoices.Length)
        {
            string selCls = _classChoices[_classIdx];
            bool isSame   = selCls.Equals(_player.Class, StringComparison.OrdinalIgnoreCase);
            if (!isSame && new Rectangle(dx, py + H - 94, 160, 36).Contains(pos))
            {
                bool ok = ClassManager.SetClass(_player, selCls);
                if (ok)
                {
                    WorldDataService.PrepareCharacter(_player);
                    _npcDialogFeedback = $"Class changed to {selCls}!";
                }
                else
                {
                    var rem = ClassManager.GetClassChangeCooldownRemaining(_player);
                    _npcDialogFeedback = rem > TimeSpan.Zero
                        ? $"Cooldown: {(int)rem.TotalDays + 1} days"
                        : "Cannot change class.";
                }
                _npcDialogFeedbackTimer = 3f;
            }
        }
    }

    private void DrawNpcDialog(SpriteBatch sb)
    {
        if (_npcDialog == null) return;
        if (_activeService == "shop")  { DrawShopPanel(sb);  return; }
        if (_activeService == "sell")  { DrawSellPanel(sb);  return; }
        if (_activeService == "class") { DrawClassPanel(sb); return; }

        const int panW = 460, panH = 320;
        int px = (_sw - panW) / 2;
        int py = (_sh - panH) / 2;

        // Backdrop
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(0, 0, 0, 120));
        Gfx.Rect(sb, px, py, panW, panH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, panW, panH), Theme.Gold);

        // NPC name
        var  nSz = Assets.FontMedium.MeasureString(_npcDialog.DisplayName);
        Gfx.Text(sb, Assets.FontMedium, _npcDialog.DisplayName,
            new Vector2(px + (panW - nSz.X) / 2f, py + 12), Theme.GoldSoft);

        Gfx.Rect(sb, px + 20, py + 48, panW - 40, 1, Theme.Gold * 0.35f);

        // Description (word-wrap to ~panW-50 px)
        string desc = _npcDialog.Description.Length > 0
            ? _npcDialog.Description
            : "(Nothing to say)";
        DrawWrapped(sb, Assets.FontSmall, desc, px + 24, py + 56, panW - 48, Theme.Foreground);

        // Feedback message
        if (_npcDialogFeedbackTimer > 0f)
        {
            float alpha = MathHelper.Clamp(_npcDialogFeedbackTimer, 0f, 1f);
            var fSz = Assets.FontSmall.MeasureString(_npcDialogFeedback);
            Gfx.Text(sb, Assets.FontSmall, _npcDialogFeedback,
                new Vector2(px + (panW - fSz.X) / 2f, py + 150),
                new Color(100, 220, 130) * alpha);
        }

        Gfx.Rect(sb, px + 20, py + 178, panW - 40, 1, Theme.Gold * 0.25f);

        // Service buttons
        var services = _npcDialog.Data.Services;
        int btnW = 130, btnH = 38, btnGap = 8;
        int totalBtns = services.Count;
        int rowStartX = px + (panW - (totalBtns * (btnW + btnGap) - btnGap)) / 2;
        int btnY = py + 188;
        var hover = Mouse.GetState().Position;

        for (int si = 0; si < services.Count; si++)
        {
            var r    = new Rectangle(rowStartX + si * (btnW + btnGap), btnY, btnW, btnH);
            bool ov  = r.Contains(hover);
            Gfx.Rect(sb, r, ov ? Theme.NavHover : Theme.PanelDark);
            Gfx.Border(sb, r, Theme.Gold * (ov ? 0.85f : 0.35f));
            Gfx.TextCentered(sb, Assets.FontSmall, ServiceLabel(services[si]), r,
                ov ? Theme.GoldSoft : Theme.Foreground);
        }

        // Close button
        var closeR = new Rectangle(px + panW / 2 - 60, py + panH - 58, 120, 42);
        bool cov = closeR.Contains(hover);
        Gfx.Rect(sb, closeR, cov ? Theme.NavHover : Theme.PanelDark);
        Gfx.Border(sb, closeR, Theme.Gold * (cov ? 0.85f : 0.4f));
        Gfx.TextCentered(sb, Assets.FontNormal, "Close", closeR,
            cov ? Theme.GoldSoft : Theme.ForegroundDim);

        // ESC hint
        Gfx.Text(sb, Assets.FontSmall, "ESC to close",
            new Vector2(px + 10, py + panH - 18), Theme.ForegroundDim * 0.6f);
    }

    // ── NPC sub-panels ────────────────────────────────────────────────────────

    private void DrawSubPanelFrame(SpriteBatch sb, int px, int py,
        int W, int H, string title, string? rightHeader = null)
    {
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(0, 0, 0, 130));
        Gfx.Rect(sb, px, py, W, H, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, W, H), Theme.Gold);
        Gfx.Text(sb, Assets.FontNormal, title, new Vector2(px + 12, py + 10), Theme.GoldSoft);
        if (rightHeader != null)
        {
            var rSz = Assets.FontSmall.MeasureString(rightHeader);
            Gfx.Text(sb, Assets.FontSmall, rightHeader, new Vector2(px + W - rSz.X - 12, py + 14),
                new Color(200, 185, 115));
        }
        Gfx.Rect(sb, px + 10, py + 36, W - 20, 1, Theme.Gold * 0.35f);
    }

    private void DrawSubPanelList<T>(SpriteBatch sb, int px, int py,
        IReadOnlyList<T> items, int scroll, int selected,
        Func<T, (string label, string right, Color col)> rowData)
    {
        const int listX = 10, listY = 52, listW = 248, rowH = 23, visRows = 12;
        var hover = Mouse.GetState().Position;

        if (items.Count == 0)
        { Gfx.Text(sb, Assets.FontSmall, "Nothing here.", new Vector2(px + listX + 4, py + listY + 4), Theme.ForegroundDim); }

        for (int i = 0; i < visRows; i++)
        {
            int idx = scroll + i;
            if (idx >= items.Count) break;
            var (lbl, right, col) = rowData(items[idx]);
            bool sel = idx == selected;
            var rowR = new Rectangle(px + listX, py + listY + i * rowH, listW, rowH - 1);
            if (sel)                   Gfx.Rect(sb, rowR, Theme.NavHover);
            else if (rowR.Contains(hover)) Gfx.Rect(sb, rowR, new Color(60, 55, 50, 100));
            Gfx.Text(sb, Assets.FontSmall, (sel ? "> " : "  ") + lbl,
                new Vector2(px + listX + 4, py + listY + i * rowH + 4), col);
            if (right.Length > 0)
            {
                var rSz = Assets.FontSmall.MeasureString(right);
                Gfx.Text(sb, Assets.FontSmall, right,
                    new Vector2(px + listX + listW - rSz.X - 4, py + listY + i * rowH + 4), Theme.ForegroundDim);
            }
        }

        if (items.Count > visRows)
        {
            string pg = $"pg.dn/up  ({scroll + 1}-{Math.Min(scroll + visRows, items.Count)}/{items.Count})";
            Gfx.Text(sb, Assets.FontSmall, pg, new Vector2(px + listX, py + listY + visRows * rowH + 2), Theme.ForegroundDim * 0.7f);
        }

        Gfx.Rect(sb, px + listX + listW + 4, py + 40, 1, 300, Theme.Gold * 0.25f);
    }

    private void DrawSubPanelBack(SpriteBatch sb, int px, int py, int W, int H)
    {
        Gfx.Rect(sb, px + 10, py + H - 54, W - 20, 1, Theme.Gold * 0.3f);
        var backR  = new Rectangle(px + W / 2 - 60, py + H - 46, 120, 36);
        bool backOv = backR.Contains(Mouse.GetState().Position);
        Gfx.Rect(sb, backR, backOv ? Theme.NavHover : Theme.PanelDark);
        Gfx.Border(sb, backR, Theme.Gold * (backOv ? 0.85f : 0.35f));
        Gfx.TextCentered(sb, Assets.FontNormal, "Back", backR, backOv ? Theme.GoldSoft : Theme.ForegroundDim);
        Gfx.Text(sb, Assets.FontSmall, "ESC to close", new Vector2(px + 10, py + H - 14), Theme.ForegroundDim * 0.6f);
    }

    private void DrawShopPanel(SpriteBatch sb)
    {
        if (_npcDialog == null) return;
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        int dx = px + 272;

        DrawSubPanelFrame(sb, px, py, W, H,
            $"{_npcDialog.DisplayName}  -  Equipment",
            _player.Money.Balance.ToString("C", null));

        var items = _npcDialog.Data.ItemRefs;
        DrawSubPanelList(sb, px, py, items, _shopScroll, _shopItemIdx,
            item =>
            {
                string name = WorldDataService.GetItemName(item);
                if (name.Length > 21) name = name[..21];
                return (name, $"{item.BuyPrice}br", RarityColor(item.Rarity));
            });

        // Detail pane
        int dy = py + 52;
        if (_shopItemIdx >= 0 && _shopItemIdx < items.Count)
        {
            var sel = items[_shopItemIdx];
            string dname = WorldDataService.GetItemName(sel);
            Gfx.Text(sb, Assets.FontNormal, dname, new Vector2(dx, dy), RarityColor(sel.Rarity)); dy += 24;
            Gfx.Text(sb, Assets.FontSmall, sel.Rarity.ToString(), new Vector2(dx, dy), Theme.ForegroundDim); dy += 20;
            if (!string.IsNullOrEmpty(sel.Description))
            { DrawWrapped(sb, Assets.FontSmall, sel.Description, dx, dy, 270, Theme.Foreground * 0.85f); }

            dy = py + 210;
            Gfx.Text(sb, Assets.FontSmall, $"Price:   {sel.BuyPrice} bronze", new Vector2(dx, dy), Theme.Foreground); dy += 20;
            bool canAfford = _player.Money.CanAfford(sel.BuyPrice);
            Gfx.Text(sb, Assets.FontSmall, canAfford ? $"Balance: {_player.Money.Balance:C}" : "Not enough money",
                new Vector2(dx, dy), canAfford ? Theme.ForegroundDim : new Color(200, 80, 60));

            if (_npcDialogFeedbackTimer > 0f)
            {
                float a = MathHelper.Clamp(_npcDialogFeedbackTimer, 0f, 1f);
                Gfx.Text(sb, Assets.FontSmall, _npcDialogFeedback, new Vector2(dx, py + 258), new Color(100, 220, 130) * a);
            }

            var buyR  = new Rectangle(dx, py + 300, 120, 36);
            bool buyOv = buyR.Contains(Mouse.GetState().Position) && canAfford;
            Gfx.Rect(sb, buyR, canAfford ? (buyOv ? Theme.NavHover : Theme.PanelDark) : new Color(28, 24, 32, 200));
            Gfx.Border(sb, buyR, Theme.Gold * (canAfford ? (buyOv ? 0.9f : 0.5f) : 0.2f));
            Gfx.TextCentered(sb, Assets.FontNormal, "Buy", buyR,
                canAfford ? (buyOv ? Theme.GoldSoft : Theme.Foreground) : Theme.ForegroundDim);
        }
        else
        {
            Gfx.Text(sb, Assets.FontSmall, "Select an item.", new Vector2(dx, dy + 10), Theme.ForegroundDim);
        }

        DrawSubPanelBack(sb, px, py, W, H);
    }

    private void DrawSellPanel(SpriteBatch sb)
    {
        if (_npcDialog == null) return;
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        int dx = px + 272;

        DrawSubPanelFrame(sb, px, py, W, H,
            $"{_npcDialog.DisplayName}  -  Sell Items",
            _player.Money.Balance.ToString("C", null));

        var items = _player.Inventory.Items;
        DrawSubPanelList(sb, px, py, (IReadOnlyList<Item>)items, _sellScroll, _sellItemIdx,
            item =>
            {
                string name = WorldDataService.GetItemName(item);
                if (name.Length > 16) name = name[..16];
                int sv = JobManager.GetSellValue(item, _player);
                return (name, $"x{item.StackSize} {sv}br", RarityColor(item.Rarity));
            });

        // Detail pane
        int dy = py + 52;
        if (_sellItemIdx >= 0 && _sellItemIdx < items.Count)
        {
            var item  = items[_sellItemIdx];
            string dname = WorldDataService.GetItemName(item);
            Gfx.Text(sb, Assets.FontNormal, dname, new Vector2(dx, dy), RarityColor(item.Rarity)); dy += 24;
            Gfx.Text(sb, Assets.FontSmall, item.Rarity.ToString(), new Vector2(dx, dy), Theme.ForegroundDim);

            int sv = JobManager.GetSellValue(item, _player);
            dy = py + 210;
            Gfx.Text(sb, Assets.FontSmall, $"Sell value: {sv} bronze each", new Vector2(dx, dy), Theme.Foreground); dy += 20;
            Gfx.Text(sb, Assets.FontSmall, $"Stack: {item.StackSize}  Total: {sv * item.StackSize} br",
                new Vector2(dx, dy), Theme.ForegroundDim);

            if (_npcDialogFeedbackTimer > 0f)
            {
                float a = MathHelper.Clamp(_npcDialogFeedbackTimer, 0f, 1f);
                Gfx.Text(sb, Assets.FontSmall, _npcDialogFeedback, new Vector2(dx, py + 258), new Color(100, 220, 130) * a);
            }

            var hover = Mouse.GetState().Position;
            // Sell 1
            var s1R   = new Rectangle(dx, py + 296, 110, 34);
            bool s1ov = s1R.Contains(hover);
            Gfx.Rect(sb, s1R, s1ov ? Theme.NavHover : Theme.PanelDark);
            Gfx.Border(sb, s1R, Theme.Gold * (s1ov ? 0.85f : 0.4f));
            Gfx.TextCentered(sb, Assets.FontSmall, "Sell 1", s1R, s1ov ? Theme.GoldSoft : Theme.Foreground);
            // Sell All
            if (item.StackSize > 1)
            {
                var saR   = new Rectangle(dx + 118, py + 296, 110, 34);
                bool saov = saR.Contains(hover);
                Gfx.Rect(sb, saR, saov ? Theme.NavHover : Theme.PanelDark);
                Gfx.Border(sb, saR, Theme.Gold * (saov ? 0.85f : 0.4f));
                Gfx.TextCentered(sb, Assets.FontSmall, "Sell All", saR, saov ? Theme.GoldSoft : Theme.Foreground);
            }
        }
        else
        {
            Gfx.Text(sb, Assets.FontSmall, items.Count == 0 ? "Inventory is empty." : "Select an item to sell.",
                new Vector2(dx, dy + 10), Theme.ForegroundDim);
        }

        DrawSubPanelBack(sb, px, py, W, H);
    }

    private void DrawClassPanel(SpriteBatch sb)
    {
        if (_npcDialog == null) return;
        const int W = 560, H = 400;
        int px = (_sw - W) / 2, py = (_sh - H) / 2;
        int dx = px + 272;

        DrawSubPanelFrame(sb, px, py, W, H, $"{_npcDialog.DisplayName}  -  Change Class");

        DrawSubPanelList(sb, px, py, _classChoices, _classScroll, _classIdx,
            cls =>
            {
                bool cur = cls.Equals(_player.Class, StringComparison.OrdinalIgnoreCase);
                string lbl = cls + (cur ? " *" : "");
                if (lbl.Length > 22) lbl = lbl[..22];
                return (lbl, ClassManager.GetClassGroup(cls), cur ? Theme.GoldSoft : Theme.Foreground);
            });

        // Detail pane
        int dy = py + 52;
        Gfx.Text(sb, Assets.FontSmall, "Current class:", new Vector2(dx, dy), Theme.ForegroundDim); dy += 18;
        Gfx.Text(sb, Assets.FontNormal, _player.Class, new Vector2(dx, dy), Theme.GoldSoft); dy += 26;
        Gfx.Text(sb, Assets.FontSmall, $"Group: {ClassManager.GetClassGroup(_player.Class)}", new Vector2(dx, dy), Theme.ForegroundDim); dy += 18;
        Gfx.Text(sb, Assets.FontSmall, $"Level: {ClassManager.GetClassLevel(_player, _player.Class)}", new Vector2(dx, dy), Theme.ForegroundDim);

        if (_classIdx >= 0 && _classIdx < _classChoices.Length)
        {
            string selCls = _classChoices[_classIdx];
            bool   isSame = selCls.Equals(_player.Class, StringComparison.OrdinalIgnoreCase);
            dy = py + 165;
            Gfx.Rect(sb, dx - 4, dy, W - 272 - 8, 1, Theme.Gold * 0.25f); dy += 8;
            Gfx.Text(sb, Assets.FontSmall, "Selected:", new Vector2(dx, dy), Theme.ForegroundDim); dy += 18;
            Gfx.Text(sb, Assets.FontNormal, selCls, new Vector2(dx, dy), Theme.Foreground); dy += 24;
            Gfx.Text(sb, Assets.FontSmall, $"Group: {ClassManager.GetClassGroup(selCls)}", new Vector2(dx, dy), Theme.ForegroundDim); dy += 18;
            Gfx.Text(sb, Assets.FontSmall, $"Level in class: {ClassManager.GetClassLevel(_player, selCls)}", new Vector2(dx, dy), Theme.ForegroundDim);

            if (_npcDialogFeedbackTimer > 0f)
            {
                float a = MathHelper.Clamp(_npcDialogFeedbackTimer, 0f, 1f);
                Gfx.Text(sb, Assets.FontSmall, _npcDialogFeedback, new Vector2(dx, py + 280), new Color(100, 220, 130) * a);
            }

            bool canChange = !isSame && ClassManager.CanChangeClass(_player);
            string btnLbl = isSame ? "Current class" : (canChange ? "Change Class" : "On cooldown");
            var chR  = new Rectangle(dx, py + H - 94, 160, 36);
            bool chOv = chR.Contains(Mouse.GetState().Position) && canChange;
            Gfx.Rect(sb, chR, canChange ? (chOv ? Theme.NavHover : Theme.PanelDark) : new Color(28, 24, 32, 200));
            Gfx.Border(sb, chR, Theme.Gold * (canChange ? (chOv ? 0.9f : 0.5f) : 0.2f));
            Gfx.TextCentered(sb, Assets.FontSmall, btnLbl, chR,
                canChange ? (chOv ? Theme.GoldSoft : Theme.Foreground) : Theme.ForegroundDim);
            if (!canChange && !isSame)
            {
                var rem = ClassManager.GetClassChangeCooldownRemaining(_player);
                if (rem > TimeSpan.Zero)
                    Gfx.Text(sb, Assets.FontSmall, $"Ready in {(int)rem.TotalDays + 1}d",
                        new Vector2(dx, py + H - 52), Theme.ForegroundDim);
            }
        }
        else
        {
            Gfx.Text(sb, Assets.FontSmall, "Select a class.", new Vector2(dx, dy + 20), Theme.ForegroundDim);
        }

        DrawSubPanelBack(sb, px, py, W, H);
    }

    private static string ServiceLabel(string svc) => svc switch
    {
        "heal"            => "Heal",
        "shop_equipment"  => "Equipment",
        "shop_general"    => "Shop",
        "buy_items"       => "Buy",
        "sell_items"      => "Sell",
        "upgrade"         => "Upgrade",
        "craft"           => "Craft",
        "learn_job"       => "Learn Job",
        "talk"            => "Talk",
        _                 => svc,
    };

    private static void DrawWrapped(SpriteBatch sb, SpriteFont font, string text,
                                    int x, int y, int maxW, Color color)
    {
        var words  = text.Split(' ');
        var lineW  = 0f;
        var line   = new System.Text.StringBuilder();
        float lineH = font.MeasureString("A").Y;

        foreach (var word in words)
        {
            float ww = font.MeasureString(word + " ").X;
            if (lineW + ww > maxW && line.Length > 0)
            {
                Gfx.Text(sb, font, line.ToString().TrimEnd(), new Vector2(x, y), color);
                y    += (int)lineH + 2;
                line.Clear();
                lineW = 0f;
            }
            line.Append(word + " ");
            lineW += ww;
        }
        if (line.Length > 0)
            Gfx.Text(sb, font, line.ToString().TrimEnd(), new Vector2(x, y), color);
    }

    // Pause menu ──────────────────────────────────────────────────────────────

    private Rectangle PausePanel()
    {
        const int w = 320, h = 308;
        return new Rectangle((_sw - w) / 2, (_sh - h) / 2, w, h);
    }

    private Rectangle PauseBtn(Rectangle panel, int index)
    {
        const int bw = 260, bh = 50, gap = 10;
        int x = panel.X + (panel.Width - bw) / 2;
        int y = panel.Y + 60 + index * (bh + gap);
        return new Rectangle(x, y, bw, bh);
    }

    private void HandlePauseClick(MouseState ms)
    {
        if (ms.LeftButton != ButtonState.Released || _prevMouse.LeftButton != ButtonState.Pressed) return;
        var pos = ms.Position;
        var pan = PausePanel();
        if (PauseBtn(pan, 0).Contains(pos)) { _showPause = false; }               // Resume
        if (PauseBtn(pan, 1).Contains(pos)) QuickSave();                           // Save
        if (PauseBtn(pan, 2).Contains(pos)) { _showPause = false;                 // Settings
            ScreenManager.Instance.Navigate(new SettingsScreen()); }
        if (PauseBtn(pan, 3).Contains(pos)) { _showPause = false;                 // Main Menu
            ScreenManager.Instance.GoBack(); }
    }

    private void DrawPauseOverlay(SpriteBatch sb)
    {
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(0, 0, 0, 150));

        var pan = PausePanel();
        Gfx.Rect(sb, pan, Theme.PanelBg);
        Gfx.Border(sb, pan, Theme.Gold);

        // Title
        const string ttl = "Paused";
        var tSz = Assets.FontMedium.MeasureString(ttl);
        Gfx.Text(sb, Assets.FontMedium, ttl,
            new Vector2(pan.X + (pan.Width - tSz.X) / 2f, pan.Y + 10), Theme.GoldSoft);

        // Buttons
        (string Label, bool Enabled)[] items =
        {
            ("Resume",        true),
            ("Save Character",true),
            ("Settings",      true),
            ("Main Menu",     true),
        };

        var hovered = Mouse.GetState().Position;
        for (int i = 0; i < items.Length; i++)
        {
            var r = PauseBtn(pan, i);
            bool over = r.Contains(hovered);
            Gfx.Rect(sb, r, over ? Theme.NavHover : Theme.PanelDark);
            Gfx.Border(sb, r, Theme.Gold * (over ? 0.9f : 0.35f));
            if (over) Gfx.Rect(sb, r.X, r.Y, 4, r.Height, Theme.Gold);
            Gfx.TextCentered(sb, Assets.FontNormal, items[i].Label, r,
                over ? Theme.GoldSoft : Theme.Foreground);
        }
    }

    private void QuickSave()
    {
        _player.CurrentRoomId = _currentRoom?.Id ?? _player.CurrentRoomId;
        MyriaWorld.Services.LocalSaveService.Save(_player);
        _savedFlashTimer = SavedFlashTime;
        _showPause       = false;
    }

    private void DrawSavedFlash(SpriteBatch sb)
    {
        if (_savedFlashTimer <= 0f) return;
        float alpha = MathHelper.Clamp(_savedFlashTimer / (SavedFlashTime * 0.4f), 0f, 1f);
        const string msg = "Character Saved!";
        var sz = Assets.FontNormal.MeasureString(msg);
        Gfx.Text(sb, Assets.FontNormal, msg,
            new Vector2((_sw - sz.X) / 2f, _sh * 0.12f),
            new Color(100, 210, 120) * alpha);
    }

    // UI4: Inventory overlay ──────────────────────────────────────────────────

    private void HandleInventoryClick(MouseState ms)
    {
        if (ms.LeftButton != ButtonState.Released || _prevMouse.LeftButton != ButtonState.Pressed) return;

        var  pos    = ms.Position;
        const int panelW = 640;
        int  px     = (_sw - panelW) / 2;
        int  py     = (_sh - 496)    / 2;
        int  titleH = (int)Assets.FontMedium.MeasureString("Inventory").Y;

        // Equipment slot y starts after title + 2 divider increments
        int cySlots = py + 10 + titleH + 6 + 6;
        // Item rows start after slots + 2nd divider + column headers
        int cyRows  = cySlots + 72 + 8 + 8 + 18;

        // ── Equipment slot clicks (unequip) ───────────────────────────────────
        const int slotSz = 72;
        int eqX0 = px + (panelW - 3 * (slotSz + 10) + 10) / 2;
        var slotTypes = new[] { EquipmentType.Weapon, EquipmentType.Armor, EquipmentType.Accessory };
        for (int s = 0; s < 3; s++)
        {
            if (new Rectangle(eqX0 + s * (slotSz + 10), cySlots, slotSz, slotSz).Contains(pos))
            {
                TryUnequip(slotTypes[s]);
                return;
            }
        }

        // ── Item row clicks (select / equip button) ───────────────────────────
        const int perPage = 12, rowH = 26;
        var  items = _player.Inventory.Items;
        int  start = _inventoryPage * perPage;
        int  ix    = px + 20;

        for (int i = 0; i < perPage; i++)
        {
            int idx  = start + i;
            if (idx >= items.Count) break;

            int rowY = cyRows + i * rowH;
            if (!new Rectangle(px + 14, rowY, panelW - 28, rowH - 2).Contains(pos)) continue;

            // Check [Equip] button first (only on already-selected row)
            if (_inventorySelected == idx
                && items[idx] is EquipmentItem eq && eq.IsUsableBy(_player)
                && new Rectangle(ix + 490, rowY + 2, 110, rowH - 4).Contains(pos))
            {
                TryEquip(idx);
                return;
            }

            // Toggle selection (second click on same row also equips if equipment)
            if (_inventorySelected == idx)
            {
                if (items[idx] is EquipmentItem eq2 && eq2.IsUsableBy(_player))
                    TryEquip(idx);
                else
                    _inventorySelected = -1;
            }
            else
            {
                _inventorySelected = idx;
            }
            return;
        }

        _inventorySelected = -1;   // clicked outside list
    }

    private void TryEquip(int idx)
    {
        var items = _player.Inventory.Items;
        if (idx < 0 || idx >= items.Count) return;
        if (items[idx] is not EquipmentItem eq) return;
        if (!eq.IsUsableBy(_player))
        {
            AddFloatText("Wrong class for this item", new Color(200, 80, 60));
            return;
        }

        // What's currently in that slot?
        EquipmentItem? displaced = eq.SlotType switch
        {
            EquipmentType.Weapon    => _player.WeaponSlot,
            EquipmentType.Armor     => _player.ArmorSlot,
            EquipmentType.Accessory => _player.AccessorySlot,
            _                       => null
        };

        _player.Inventory.RemoveItem(eq);

        // Return displaced item to inventory (can't fail — we just freed a slot)
        if (displaced != null)
            _player.Inventory.AddItem(displaced, _player);

        switch (eq.SlotType)
        {
            case EquipmentType.Weapon:    _player.WeaponSlot    = eq; break;
            case EquipmentType.Armor:     _player.ArmorSlot     = eq; break;
            case EquipmentType.Accessory: _player.AccessorySlot = eq; break;
        }

        _inventorySelected = -1;
        AddFloatText($"Equipped {WorldDataService.GetItemName(eq)}", new Color(120, 200, 120));
    }

    private void TryUnequip(EquipmentType slot)
    {
        EquipmentItem? item = slot switch
        {
            EquipmentType.Weapon    => _player.WeaponSlot,
            EquipmentType.Armor     => _player.ArmorSlot,
            EquipmentType.Accessory => _player.AccessorySlot,
            _                       => null
        };
        if (item == null) return;

        if (!_player.Inventory.AddItem(item, _player))
        {
            AddFloatText("Inventory full - can't unequip", new Color(200, 80, 60));
            return;
        }

        switch (slot)
        {
            case EquipmentType.Weapon:    _player.WeaponSlot    = null; break;
            case EquipmentType.Armor:     _player.ArmorSlot     = null; break;
            case EquipmentType.Accessory: _player.AccessorySlot = null; break;
        }

        AddFloatText($"Unequipped {WorldDataService.GetItemName(item)}", Theme.ForegroundDim);
    }

    private void DrawInventoryOverlay(SpriteBatch sb)
    {
        const int panelW = 640, panelH = 496;
        int px = (_sw - panelW) / 2;
        int py = (_sh - panelH) / 2;
        var hover = Mouse.GetState().Position;

        // Backdrop
        Gfx.Rect(sb, 0, 0, _sw, _sh, new Color(0, 0, 0, 140));
        Gfx.Rect(sb, px, py, panelW, panelH, Theme.PanelBg);
        Gfx.Border(sb, new Rectangle(px, py, panelW, panelH), Theme.Gold);

        // Title
        string title = "Inventory";
        var titleSz = Assets.FontMedium.MeasureString(title);
        Gfx.Text(sb, Assets.FontMedium, title,
            new Vector2(px + (panelW - titleSz.X) / 2f, py + 10), Theme.GoldSoft);
        int cy = py + 10 + (int)titleSz.Y + 6;

        // ── Equipment slots ───────────────────────────────────────────────────
        Gfx.Rect(sb, px + 12, cy, panelW - 24, 1, Theme.Gold * 0.4f);
        cy += 6;

        const int slotSz = 72;
        (string Label, EquipmentItem? Item, EquipmentType SlotType)[] slots =
        {
            ("Weapon",    _player.WeaponSlot,    EquipmentType.Weapon),
            ("Armor",     _player.ArmorSlot,     EquipmentType.Armor),
            ("Accessory", _player.AccessorySlot, EquipmentType.Accessory),
        };

        int eqX = px + (panelW - slots.Length * (slotSz + 10) + 10) / 2;
        foreach (var (label, item, _) in slots)
        {
            bool filled    = item != null;
            bool slotHover = new Rectangle(eqX, cy, slotSz, slotSz).Contains(hover);
            Gfx.Rect(sb, eqX, cy, slotSz, slotSz,
                slotHover && filled ? new Color(80, 68, 42, 160) : Theme.PanelDark);
            Gfx.Border(sb, new Rectangle(eqX, cy, slotSz, slotSz),
                filled ? Theme.Gold * (slotHover ? 1f : 0.8f) : Theme.Gold * 0.25f);

            if (filled)
            {
                string dname  = WorldDataService.GetItemName(item!);
                string dname9 = dname.Length > 9 ? dname[..9] : dname;
                var nSz = Assets.FontSmall.MeasureString(dname9);
                Gfx.Text(sb, Assets.FontSmall, dname9,
                    new Vector2(eqX + (slotSz - nSz.X) / 2f, cy + 18),
                    RarityColor(item!.Rarity));
                // Unequip hint on hover
                string bottomLabel = slotHover ? "Unequip" : label;
                var lbSz = Assets.FontSmall.MeasureString(bottomLabel);
                Gfx.Text(sb, Assets.FontSmall, bottomLabel,
                    new Vector2(eqX + (slotSz - lbSz.X) / 2f, cy + slotSz - 18),
                    slotHover ? Theme.Gold : Theme.ForegroundDim);
            }
            else
            {
                var lSz = Assets.FontSmall.MeasureString(label);
                Gfx.Text(sb, Assets.FontSmall, label,
                    new Vector2(eqX + (slotSz - lSz.X) / 2f, cy + slotSz / 2 - 7),
                    Theme.ForegroundDim * 0.6f);
            }

            eqX += slotSz + 10;
        }

        cy += slotSz + 8;
        Gfx.Rect(sb, px + 12, cy, panelW - 24, 1, Theme.Gold * 0.4f);
        cy += 8;

        // ── Item list ─────────────────────────────────────────────────────────
        const int perPage = 12, rowH = 26;
        var items = _player.Inventory.Items;
        int totalPages = items.Count == 0 ? 1 : (int)Math.Ceiling(items.Count / (double)perPage);
        _inventoryPage = Math.Clamp(_inventoryPage, 0, totalPages - 1);

        int start = _inventoryPage * perPage;
        int end   = Math.Min(start + perPage, items.Count);
        int ix    = px + 20;

        if (items.Count == 0)
        {
            string empty = "Inventory is empty";
            var eSz = Assets.FontNormal.MeasureString(empty);
            Gfx.Text(sb, Assets.FontNormal, empty,
                new Vector2(px + (panelW - eSz.X) / 2f, cy + rowH * 3),
                Theme.ForegroundDim * 0.7f);
        }
        else
        {
            Gfx.Text(sb, Assets.FontSmall, "Item",   new Vector2(ix + 18, cy), Theme.ForegroundDim);
            Gfx.Text(sb, Assets.FontSmall, "Qty",    new Vector2(ix + 330, cy), Theme.ForegroundDim);
            Gfx.Text(sb, Assets.FontSmall, "Type",   new Vector2(ix + 390, cy), Theme.ForegroundDim);
            cy += 18;

            for (int idx = start; idx < end; idx++)
            {
                var  item     = items[idx];
                bool isSel    = idx == _inventorySelected;
                bool even     = (idx - start) % 2 == 0;

                if (isSel)
                    Gfx.Rect(sb, px + 14, cy, panelW - 28, rowH - 2, Theme.NavHover);
                else if (even)
                    Gfx.Rect(sb, px + 14, cy, panelW - 28, rowH - 2, new Color(30, 26, 38, 120));

                // Rarity dot
                Gfx.Rect(sb, ix, cy + 6, 10, 10, RarityColor(item.Rarity));

                // Display name (resolved through locale)
                string dname = WorldDataService.GetItemName(item);
                string trunc = dname.Length > 28 ? dname[..28] : dname;
                Gfx.Text(sb, Assets.FontSmall, trunc, new Vector2(ix + 18, cy + 4),
                    isSel ? Theme.GoldSoft : Theme.Foreground);

                if (item.StackSize > 1)
                    Gfx.Text(sb, Assets.FontSmall, $"x{item.StackSize}",
                        new Vector2(ix + 330, cy + 4), Theme.ForegroundDim);

                string tag = item.GetType().Name.Replace("Item", "");
                Gfx.Text(sb, Assets.FontSmall, tag,
                    new Vector2(ix + 390, cy + 4), Theme.ForegroundDim * 0.8f);

                // [Equip] button on selected equipment row
                if (isSel && item is EquipmentItem eq)
                {
                    bool canEquip = eq.IsUsableBy(_player);
                    var  btnR    = new Rectangle(ix + 490, cy + 2, 110, rowH - 4);
                    bool btnHov  = btnR.Contains(hover) && canEquip;
                    if (canEquip)
                    {
                        Gfx.Rect(sb, btnR,
                            btnHov ? new Color(55, 95, 55, 220) : new Color(35, 65, 35, 180));
                        Gfx.Border(sb, btnR, Theme.Gold * (btnHov ? 0.85f : 0.45f));
                        Gfx.TextCentered(sb, Assets.FontSmall, "Equip", btnR,
                            new Color(120, 225, 120));
                    }
                    else
                    {
                        Gfx.Text(sb, Assets.FontSmall, "Wrong class",
                            new Vector2(ix + 492, cy + 5), new Color(180, 80, 60));
                    }
                }

                cy += rowH;
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        int fy = py + panelH - 28;
        Gfx.Rect(sb, px + 12, fy - 4, panelW - 24, 1, Theme.Gold * 0.3f);

        string pageStr = $"Page {_inventoryPage + 1}/{totalPages}  ({items.Count} items / {_player.Inventory.Capacity} slots)";
        Gfx.Text(sb, Assets.FontSmall, pageStr, new Vector2(px + 16, fy + 4), Theme.ForegroundDim);

        string closeHint = "Enter  Equip   PageUp/Down  Scroll   I / ESC  Close";
        var    cSz       = Assets.FontSmall.MeasureString(closeHint);
        Gfx.Text(sb, Assets.FontSmall, closeHint,
            new Vector2(px + panelW - cSz.X - 16, fy + 4), Theme.ForegroundDim);
    }

    private static Color RarityColor(MyriaLib.Systems.Enums.ItemRarity rarity) => rarity switch
    {
        MyriaLib.Systems.Enums.ItemRarity.Common    => new Color(180, 180, 180),
        MyriaLib.Systems.Enums.ItemRarity.Uncommon  => new Color(60,  180, 70),
        MyriaLib.Systems.Enums.ItemRarity.Rare      => new Color(60,  120, 210),
        MyriaLib.Systems.Enums.ItemRarity.Epic      => new Color(155, 60,  210),
        MyriaLib.Systems.Enums.ItemRarity.Unique    => new Color(210, 130, 40),
        MyriaLib.Systems.Enums.ItemRarity.Legendary => new Color(210, 155, 30),
        MyriaLib.Systems.Enums.ItemRarity.Godly     => new Color(210, 210, 70),
        _                                           => Color.White,
    };

    private void AddFloatText(string msg, Color color, float x = -1f, float y = -1f)
    {
        float fx = x < 0f ? _sw * 0.5f : x;
        // Stagger new entries slightly so they don't all overlap at the same Y
        float fy = y < 0f ? _sh * 0.38f + _floatTexts.Count * 20f : y;
        _floatTexts.Add(new FloatText(msg, color, FloatTextTime, fx, fy));
    }

    private void ShowInfo(string msg, Color color)
    {
        _infoMessage  = msg;
        _infoMsgColor = color;
        _infoMsgTimer = InfoMsgTime;
    }

    private void ShowQuestMsg(string msg, Color color)
    {
        string safe    = WorldDataService.AsciiSafe(msg);
        _questMsg      = safe;
        _questMsgTimer = QuestMsgTime;
        ShowInfo(safe, color);
    }

    // ── Layout helpers ────────────────────────────────────────────────────────

    private void ComputeSkillBarBounds()
    {
        const int slotSz = 62, gap = 4;
        int count = Math.Min(_player.Skills.Count, 9);
        if (count == 0) { _skillBarBounds = Rectangle.Empty; return; }
        int barW = count * (slotSz + gap) - gap;
        int barX = (_sw - barW) / 2;
        int barY = _sh - slotSz - 38;
        _skillBarBounds = new Rectangle(barX - 8, barY - 4, barW + 16, slotSz + 8);
    }

    // ── Geometry ──────────────────────────────────────────────────────────────

    private void BuildCharacterMesh()
    {
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();

        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.3f, 0f,    -0.2f),
            new Vector3( 0.3f, 1.4f,   0.2f),
            new Color(60, 90, 150), new Color(35, 60, 110));

        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.2f, 1.4f,  -0.2f),
            new Vector3( 0.2f, 1.85f,  0.2f),
            new Color(220, 175, 135), new Color(180, 140, 105));

        _playerVerts = [.. verts];
        _playerIdx   = [.. idx];
    }
}

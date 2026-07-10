using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Maps;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.NPCs;
using MyriaLib.Entities.Skills;
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

    // Navigation mesh
    private NavMesh         _navMesh     = null!;
    private NavMeshRenderer _navRenderer = null!;

    // Character mesh
    private VertexPositionColor[] _playerVerts = null!;
    private int[]                 _playerIdx   = null!;

    // ── Room tracking ─────────────────────────────────────────────────────────
    private int    _currentFace = -1;
    private Room?  _currentRoom;
    private string _roomName    = "";

    // ── Monster state ─────────────────────────────────────────────────────────
    // Monsters are keyed by the navmesh face they spawned in so they persist
    // when the player walks away and returns within the same session.
    private readonly Dictionary<int, List<WorldMonster>> _faceMonsters = new();
    private List<WorldMonster> _monsters = new();  // alias for current face
    private WorldMonster?      _target;

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

    // ── Quest / save notifications ────────────────────────────────────────────
    private string _questMsg      = "";
    private float  _questMsgTimer;
    private const float QuestMsgTime = 3.5f;

    // ── Mana regeneration ─────────────────────────────────────────────────────
    private float _manaRegenTimer;
    private const float ManaRegenInterval = 3f;   // seconds between MP ticks
    private const int   ManaRegenAmount   = 1;    // MP restored per tick

    // ── Level-up banner ───────────────────────────────────────────────────────
    private string _levelUpMsg   = "";
    private float  _levelUpTimer;
    private const float LevelUpBannerTime = 3f;

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

    // ── Input ─────────────────────────────────────────────────────────────────
    private MouseState _prevMouse;
    private Point      _lastMousePos;

    // ── Notifications ─────────────────────────────────────────────────────────
    private float  _roomNameTimer;
    private string _infoMessage  = "";
    private float  _infoMsgTimer;
    private Color  _infoMsgColor = Color.White;
    private const float RoomNameTime = 3f;
    private const float InfoMsgTime  = 2f;

    // ── HUD layout ────────────────────────────────────────────────────────────
    private int       _sw, _sh;
    private Rectangle _skillBarBounds;   // used for right-click UI-hit test

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

        BuildCharacterMesh();
        ComputeSkillBarBounds();

        _inventoryOverlay = new InventoryOverlay(_player);
        _player.LeveledUp += OnLevelUp;

        _pauseMenu.Init(
            onResume: () => _pauseMenu.Close(),
            onSave:   () => { SaveService.Save(_player, _pos, _discoveredWpRooms); ShowQuestMsg("Game saved.", new Color(120, 200, 120)); _pauseMenu.Close(); },
            onLoad:   () => { if (SaveService.HasSave()) { var (p, wps) = SaveService.Load(_player); _pos = p; foreach (var r in wps) _discoveredWpRooms.Add(r); ShowQuestMsg("Game loaded.", new Color(120, 200, 180)); } _pauseMenu.Close(); },
            onQuit:   () => { ScreenManager.Instance.GoBack(); });

        _minimapRenderer = new MinimapRenderer();
        _minimapRenderer.Build(gd, _navMesh, _effect);
        _worldMap.Build(gd, _navMesh, _effect, _sw, _sh);
        (_decoVerts, _decoIdx, _buildings, _waypoints) = WorldDecorationSpawner.Build(_navMesh, _heights);

        _worldNpcs.AddRange(WorldNpc.SpawnAll(_navMesh, _buildings));
        foreach (var npc in _worldNpcs)
        {
            int nf = _navMesh.FindFaceIndex(new Vector2(npc.Position.X, npc.Position.Z));
            if (nf >= 0)
                npc.Position = new Vector3(npc.Position.X,
                    TerrainHeights.GetHeight(_heights, _navMesh, nf, npc.Position.X, npc.Position.Z),
                    npc.Position.Z);
        }

        AudioService.Init();
        AudioService.SetAmbientZone(_currentTerrain);

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
        AudioService.Dispose();
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

        // ESC / I: inventory toggle or world exit — use WasKeyJustPressed so holding
        // ESC to close inventory doesn't immediately exit the world on the next frame.
        bool escJust = WasKeyJustPressed(kb, Keys.Escape);
        bool iJust   = WasKeyJustPressed(kb, Keys.I);
        bool fJust   = WasKeyJustPressed(kb, Keys.F);
        bool yJust   = WasKeyJustPressed(kb, Keys.Y);
        bool qJust   = WasKeyJustPressed(kb, Keys.Q);
        bool mJust   = WasKeyJustPressed(kb, Keys.M);

        // Pause menu: block all other input
        if (_pauseMenu.IsOpen)
        {
            _pauseMenu.Update();
            if (escJust) _pauseMenu.Close();
            _prevKb    = kb;
            _prevMouse = ms;
            return;
        }

        // Quest log: block all other input
        if (_questLogOverlay.IsOpen)
        {
            _questLogOverlay.Update(ms, _prevMouse);
            var trackReq = _questLogOverlay.ConsumeTrackRequest();
            if (trackReq != null) _trackedQuest = trackReq;
            if (escJust || qJust) _questLogOverlay.Close();
            _prevKb    = kb;
            _prevMouse = ms;
            return;
        }

        // Shop overlay: block all other input
        if (_shopOverlay.IsOpen)
        {
            _shopOverlay.Update(ms, _prevMouse);
            _shopOverlay.UpdateDt(dt);
            if (escJust) _shopOverlay.Close();
            _prevKb    = kb;
            _prevMouse = ms;
            return;
        }

        // Dialogue: highest priority — close on F or ESC, accept quest on Y
        if (_dialogueOverlay.IsOpen)
        {
            if (_dialogueOverlay.Mode == DialogueMode.QuestAccept && yJust)
            {
                AcceptPendingQuest();
            }
            else if (_dialogueOverlay.Mode == DialogueMode.QuestReturn && (escJust || fJust))
            {
                CompletePendingQuest();
            }
            else if (escJust || fJust)
            {
                _dialogueOverlay.Close();
            }
            _prevKb    = kb;
            _prevMouse = ms;
            return;
        }

        if (_worldMap.IsOpen)
        {
            if (escJust || mJust) { _worldMap.Close(); _prevKb = kb; _prevMouse = ms; }
            return;
        }

        if (_fastTravel.IsOpen)
        {
            _fastTravel.Update(ms, _prevMouse);
            if (escJust || fJust) _fastTravel.Close();
            var dest = _fastTravel.ConsumeTravel();
            if (dest != null) DoFastTravel(dest);
            _prevKb    = kb;
            _prevMouse = ms;
            return;
        }

        if (_inventoryOpen)
        {
            if (escJust || iJust)
            {
                _inventoryOpen = false;
                _prevKb        = kb;
                _lastMousePos  = new Point(ms.X, ms.Y);
            }
            else
            {
                _prevKb       = kb;
                _prevMouse    = ms;
                _lastMousePos = new Point(ms.X, ms.Y);
            }
            return;
        }

        if (escJust) { _pauseMenu.Open(_sw, _sh); AudioService.Play(Sfx.OpenMenu, 0.4f); _prevKb = kb; _prevMouse = ms; return; }
        if (iJust)   { _inventoryOpen = true; AudioService.Play(Sfx.OpenMenu, 0.5f); _prevKb = kb; _prevMouse = ms; return; }
        if (qJust)   { _questLogOverlay.Open(_player); AudioService.Play(Sfx.OpenMenu, 0.5f); _prevKb = kb; _prevMouse = ms; return; }
        if (mJust)   { _worldMap.Toggle(); AudioService.Play(Sfx.OpenMenu, 0.5f); _prevKb = kb; _prevMouse = ms; return; }

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
                ShowInfo($"-{dmg} HP", new Color(200, 80, 60));
                AudioService.Play(Sfx.Hit, 0.4f, -0.2f);
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

        // F: talk to nearby NPC or pick up loot
        if (fJust) TryInteractOrPickup();

        // F5 / F9: save / load
        if (WasKeyJustPressed(kb, Keys.F5))
        {
            SaveService.Save(_player, _pos, _discoveredWpRooms);
            ShowQuestMsg("Game saved.", new Color(120, 200, 120));
        }
        if (WasKeyJustPressed(kb, Keys.F9) && SaveService.HasSave())
        {
            var (savedPos, savedWps) = SaveService.Load(_player);
            if (savedPos != Vector3.Zero)
            {
                _pos = savedPos;
                UpdateFace(_navMesh.FindFaceIndex(new Vector2(_pos.X, _pos.Z)), announce: false);
                _pos.Y = TerrainHeights.GetHeight(_heights, _navMesh, _currentFace, _pos.X, _pos.Z);
            }
            foreach (var r in savedWps) _discoveredWpRooms.Add(r);
            ShowQuestMsg("Game loaded.", new Color(120, 180, 220));
        }

        // ── Cooldowns & timers ────────────────────────────────────────────────
        foreach (var key in _cooldowns.Keys.ToList())
        {
            _cooldowns[key] -= dt;
            if (_cooldowns[key] <= 0f) _cooldowns.Remove(key);
        }
        if (_roomNameTimer  > 0f) _roomNameTimer  -= dt;
        if (_infoMsgTimer   > 0f) _infoMsgTimer   -= dt;
        if (_levelUpTimer   > 0f) _levelUpTimer   -= dt;
        if (_questMsgTimer  > 0f) _questMsgTimer  -= dt;
        if (_footstepTimer  > 0f) _footstepTimer  -= dt;
        _zoneOverlay.Update(dt);
        _dayNight.Update(dt);
        _weather.Update(dt, _sw, _sh);

        // Mana regen
        _manaRegenTimer += dt;
        if (_manaRegenTimer >= ManaRegenInterval)
        {
            _manaRegenTimer -= ManaRegenInterval;
            if (_player.CurrentMana < _player.MaxMana)
                _player.RestoreMana(ManaRegenAmount);
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
                ShowInfo(WorldDataService.DeniedReason(_player, room), new Color(200, 80, 80));
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
                ShowInfo("Hostile area", new Color(200, 120, 50));
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
            ShowInfo("Not ready", Theme.ForegroundDim); return;
        }
        if (_player.CurrentMana < skill.ManaCost)
        {
            ShowInfo("Not enough mana", new Color(60, 120, 210)); return;
        }

        switch (skill.Target)
        {
            case SkillTarget.SingleEnemy:
                if (_target == null || !_target.IsAlive)
                {
                    ShowInfo("No target", new Color(200, 100, 60)); return;
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
                singleTarget.HitFlashTimer = 0.22f;
                AudioService.Play(Sfx.Hit, 0.75f, skill.Type == SkillType.Magical ? 0.3f : 0f);
                ShowInfo($"-{dmg}", new Color(220, 80, 80));
                TryApplyStatus(skill, singleTarget);
                if (!singleTarget.IsAlive)
                {
                    AudioService.Play(Sfx.MonsterDeath, 0.65f);
                    _player.GainXp(singleTarget.Data.Exp);
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
                        AudioService.Play(Sfx.MonsterDeath, 0.5f);
                        _player.GainXp(m.Data.Exp);
                        OnMonsterKilled(m);
                        if (m == _target) _target = null;
                    }
                }
                if (hits > 0)
                {
                    AudioService.Play(Sfx.Hit, 0.8f, 0.1f);
                    ShowInfo($"AoE -{total} ({hits} hit)", new Color(220, 160, 60));
                }
                break;
            }

            case SkillTarget.Self:
            case SkillTarget.SingleAlly:
                if (skill.IsHealing)
                {
                    int heal = Math.Max(1, (int)(_player.TotalMagicAttack * skill.ScalingFactor));
                    _player.CurrentHealth = Math.Min(_player.MaxHealth, _player.CurrentHealth + heal);
                    ShowInfo($"+{heal} HP", new Color(80, 200, 80));
                }
                AudioService.Play(Sfx.SkillCast, 0.6f);
                break;
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
        target.HitFlashTimer = 0.18f;
        AudioService.Play(Sfx.Hit, isCrit ? 0.85f : 0.65f);

        if (isCrit)
            ShowInfo($"CRIT! -{dmg}", new Color(255, 215, 50));
        else
            ShowInfo($"-{dmg}", new Color(220, 120, 80));

        // Small stun chance on physical hit (10%)
        if (Random.Shared.NextSingle() < 0.10f && !target.IsStunned)
        {
            target.StunTimer = 1.2f;
            ShowInfo("Stunned!", new Color(140, 140, 220));
        }

        if (!target.IsAlive)
        {
            _player.GainXp(target.Data.Exp);
            OnMonsterKilled(target);
            _target        = null;
            _autoAttacking = false;
        }
    }

    private void OnMonsterKilled(WorldMonster monster)
    {
        ShowInfo($"Defeated!  +{monster.Data.Exp} EXP", Theme.GoldSoft);

        var items = LootGenerator.GetLootFor(monster.Data);
        int gold  = Random.Shared.Next(monster.Data.MinLoot, Math.Max(monster.Data.MinLoot + 1, monster.Data.MaxLoot + 1));

        if (items.Count > 0 || gold > 0)
            _lootDrops.Add(new WorldLootDrop(monster.Position, items, gold));

        // Update kill progress for active quests
        int monsterId = monster.Data.Id;
        foreach (var quest in _player.ActiveQuests)
        {
            if (quest.Status != QuestStatus.InProgress) continue;
            if (!quest.RequiredKills.TryGetValue(monsterId, out int required)) continue;

            quest.KillProgress.TryGetValue(monsterId, out int current);
            int newCount = current + 1;
            quest.KillProgress[monsterId] = newCount;

            // Show per-kill progress feedback
            ShowInfo($"Quest kill: {newCount}/{required} {GetMonsterName(monsterId)}",
                new Color(220, 185, 60));

            // Check all kill objectives are met
            bool allDone = quest.RequiredKills.All(kvp =>
                quest.KillProgress.TryGetValue(kvp.Key, out int got) && got >= kvp.Value);
            if (allDone && quest.RequiredItems.Count == 0)
            {
                quest.Status = QuestStatus.Completed;
                ShowQuestMsg($"Quest ready to return: {WorldDataService.Localize(quest.Name)}", new Color(220, 185, 60));
            }
        }
    }

    private void DoFastTravel(WorldWaypoint dest)
    {
        _pos = dest.Position;
        UpdateFace(_navMesh.FindFaceIndex(new Vector2(_pos.X, _pos.Z)), announce: true);
        _pos.Y = TerrainHeights.GetHeight(_heights, _navMesh, _currentFace, _pos.X, _pos.Z);
        _target        = null;
        _autoAttacking = false;
        _monsters      = new List<WorldMonster>();
        AudioService.Play(Sfx.EnterDungeon, 0.55f);
    }

    private void TryInteractOrPickup()
    {
        // Waypoint shrine — highest priority
        var wp = _waypoints.FirstOrDefault(w =>
            _discoveredWpRooms.Contains(w.RoomId) &&
            Vector3.Distance(new Vector3(_pos.X, 0f, _pos.Z),
                             new Vector3(w.Position.X, 0f, w.Position.Z)) <= WorldWaypoint.InteractRange);
        if (wp != null)
        {
            var discovered = _waypoints
                .Where(w => _discoveredWpRooms.Contains(w.RoomId))
                .ToList();
            _fastTravel.Open(_currentRoom?.Id ?? -1, discovered, _worldMap);
            return;
        }

        // NPC interaction has priority over loot pickup
        var npc = _worldNpcs.FirstOrDefault(n =>
            Vector3.Distance(new Vector3(_pos.X, 0f, _pos.Z),
                             new Vector3(n.Position.X, 0f, n.Position.Z)) <= WorldNpc.InteractRange);

        if (npc != null)
        {
            // Shop NPC → open the vendor panel
            if (npc.Source.Type == NpcType.Shop)
            {
                _shopOverlay.Open(npc.Source, _player);
                return;
            }

            // Quest return first (player must have a completable quest for this NPC)
            var returnable = QuestManager.GetReturnableForNpc(_player, npc.Source.Id);
            if (returnable.Count > 0)
            {
                _dialogueOverlay.OpenQuestReturn(npc.Name, returnable[0]);
                return;
            }

            // Quest accept (NPC has a quest the player can take)
            var acceptable = QuestManager.GetAcceptableForNpc(_player, npc.Source.Id);
            if (acceptable.Count > 0)
            {
                _dialogueOverlay.OpenQuestAccept(npc.Name, acceptable[0]);
                return;
            }

            // Regular dialogue fallback
            _dialogueOverlay.Open(npc.Name, npc.DialogueLines);
            return;
        }

        TryPickupNearbyLoot();
    }

    private void AcceptPendingQuest()
    {
        var quest = _dialogueOverlay.PendingQuest;
        if (quest == null) { _dialogueOverlay.Close(); return; }

        var clone  = quest.Clone();
        clone.Status = QuestStatus.InProgress;
        clone.GrantAcceptItems(_player);
        _player.ActiveQuests.Add(clone);
        _trackedQuest = clone;

        // Talk-only quests auto-complete on accept
        if (clone.IsTalkOnly)
            clone.Status = QuestStatus.Completed;

        AudioService.Play(Sfx.QuestAccept, 0.75f);
        ShowQuestMsg($"Quest accepted: {WorldDataService.Localize(clone.Name)}", new Color(220, 185, 60));
        _dialogueOverlay.Close();
    }

    private void CompletePendingQuest()
    {
        var quest = _dialogueOverlay.PendingQuest;
        if (quest == null) { _dialogueOverlay.Close(); return; }

        quest.GrantRewards(_player);
        quest.Status = QuestStatus.Returned;
        _player.ActiveQuests.Remove(quest);
        _player.CompletedQuests.Add(quest);

        if (_trackedQuest == quest) _trackedQuest = _player.ActiveQuests.FirstOrDefault();

        AudioService.Play(Sfx.QuestComplete, 0.8f);
        ShowQuestMsg($"Quest complete: {WorldDataService.Localize(quest.Name)}", new Color(120, 220, 120));
        _dialogueOverlay.Close();
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
        _weather.ApplyFog(_effect);

        // Navmesh floor
        _navRenderer.Draw(gd, _effect);

        gd.RasterizerState = RasterizerState.CullCounterClockwise;

        // Static decorations (trees, houses, rocks…) — one combined draw call
        if (_decoVerts.Length > 0)
        {
            gd.RasterizerState = RasterizerState.CullNone;
            _effect.World      = Matrix.Identity;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _decoVerts, 0, _decoVerts.Length,
                    _decoIdx,   0, _decoIdx.Length / 3);
            }
            gd.RasterizerState = RasterizerState.CullCounterClockwise;
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
        if (_isDead) DrawDeathOverlay(sb);
        if (_inventoryOpen)
        {
            var ms2 = Mouse.GetState();
            _inventoryOverlay.Draw(sb, ms2.X, ms2.Y);
        }
        if (_dialogueOverlay.IsOpen)
            _dialogueOverlay.Draw(sb, _sw, _sh);
        if (_shopOverlay.IsOpen)
            _shopOverlay.Draw(sb);
        if (_questLogOverlay.IsOpen)
            _questLogOverlay.Draw(sb);
        _zoneOverlay.Draw(sb, _sw, _sh);
        _worldMap.Draw(sb, _sw, _sh, _pos, _currentFace, _visitedFaces, _worldNpcs);
        _fastTravel.Draw(sb, _sw, _sh);
        _pauseMenu.Draw(sb, _sw, _sh);
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

        if (_currentRoom?.HasMonsters == true)
        {
            int alive      = _monsters.Count(m => m.IsAlive);
            int respawning = _monsters.Count(m => m.IsRespawning);
            string danger  = $"! HOSTILE  {alive} alive  {respawning} respawning";
            var dSz = Assets.FontSmall.MeasureString(danger);
            Gfx.Rect(sb, 14, 96, (int)dSz.X + 16, 18, new Color(100, 30, 30, 180));
            Gfx.Text(sb, Assets.FontSmall, danger, new Vector2(22, 98), new Color(220, 100, 80));
        }

        // ── Minimap + clock (top-right) ───────────────────────────────────────
        int mapX = _sw - MinimapRenderer.Size - 14;
        int mapY = 14;
        _minimapRenderer.Draw(sb, mapX, mapY, _pos, _worldNpcs);

        string weather  = _weather.DisplayName;
        string timeStr  = (_dayNight.IsNight ? "[Night] " : "")
                        + _dayNight.FormatTime()
                        + (weather.Length > 0 ? $"  {weather}" : "");
        var    timeSz   = Assets.FontSmall.MeasureString(timeStr);
        int    timeX    = _sw - (int)timeSz.X - 14;
        int    timeY    = mapY + MinimapRenderer.Size + 6;
        Color  timeCol  = _dayNight.IsNight ? new Color(140, 160, 220) : new Color(220, 200, 120);
        Gfx.Rect(sb, timeX - 6, timeY - 2, (int)timeSz.X + 12, (int)timeSz.Y + 4, new Color(0, 0, 0, 130));
        Gfx.Text(sb, Assets.FontSmall, timeStr, new Vector2(timeX, timeY), timeCol);

        // ── Target panel (below minimap) ──────────────────────────────────────
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

        // ── Info message (access denied, damage, etc.) ────────────────────────
        if (_infoMsgTimer > 0f && _infoMessage.Length > 0)
        {
            float alpha = MathHelper.Clamp(_infoMsgTimer, 0f, 1f);
            var   sz    = Assets.FontNormal.MeasureString(_infoMessage);
            int   my    = _sh / 2;
            Gfx.Rect(sb, (int)((_sw - sz.X) / 2f) - 16, my - 4,
                (int)sz.X + 32, (int)sz.Y + 8, new Color(0, 0, 0, (int)(150 * alpha)));
            Gfx.Text(sb, Assets.FontNormal, _infoMessage,
                new Vector2((_sw - sz.X) / 2f, my), _infoMsgColor * alpha);
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
        const string hint = "WASD  Move   Tab  Target   1-9  Skills   F  Talk/Pickup   I  Inventory   Q  Quests   F5  Save   F9  Load";
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

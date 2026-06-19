using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MyriaLib.Entities.Maps;
using MyriaLib.Entities.Characters;
using MyriaLib.Entities.Skills;
using MyriaWorld.Services;
using MyriaWorld.UI;
using MyriaWorld.World;

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
        _navRenderer = new NavMeshRenderer();
        _navRenderer.Build(gd, _navMesh);

        BuildCharacterMesh();
        ComputeSkillBarBounds();

        // Resolve starting face without announcing the room name
        UpdateFace(_navMesh.FindFaceIndex(new Vector2(_pos.X, _pos.Z)), announce: false);
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
        Game1.Instance.IsMouseVisible = true;
        _navRenderer.Dispose();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    public override void Update(GameTime gt)
    {
        float dt = (float)gt.ElapsedGameTime.TotalSeconds;
        var   kb = Keyboard.GetState();
        var   ms = Mouse.GetState();

        // ESC
        if (kb.IsKeyDown(Keys.Escape))
        {
            ScreenManager.Instance.GoBack();
            return;
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

        // ── Monster AI ────────────────────────────────────────────────────────
        foreach (var m in _monsters)
        {
            m.Update(dt, _pos, _navMesh, rawDmg =>
            {
                int dmg = Math.Max(1, rawDmg - _player.DefandPhysical());
                _player.TakeDamage(dmg);
                ShowInfo($"-{dmg} HP", new Color(200, 80, 60));
                if (_player.CurrentHealth <= 0) TriggerDeath();
            });
        }
        // Clear target only once it is fully gone (not just respawning)
        if (_target is { IsAlive: false, IsRespawning: false }) { _target = null; _autoAttacking = false; }

        // ── Cooldowns & timers ────────────────────────────────────────────────
        foreach (var key in _cooldowns.Keys.ToList())
        {
            _cooldowns[key] -= dt;
            if (_cooldowns[key] <= 0f) _cooldowns.Remove(key);
        }
        if (_roomNameTimer > 0f) _roomNameTimer -= dt;
        if (_infoMsgTimer  > 0f) _infoMsgTimer  -= dt;
        _prevKb = kb;
    }

    // ── Input helpers ─────────────────────────────────────────────────────────

    private KeyboardState _prevKb;
    private bool WasKeyJustPressed(KeyboardState current, Keys key)
        => current.IsKeyDown(key) && !_prevKb.IsKeyDown(key);

    private bool IsOverUI(int x, int y) => _skillBarBounds.Contains(x, y);

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

        _pos = candidate;
        _yaw = MathF.Atan2(dir.X, dir.Z);
    }

    // ── Room tracking ─────────────────────────────────────────────────────────

    private void UpdateFace(int faceIndex, bool announce)
    {
        _currentFace = faceIndex;
        var face     = faceIndex >= 0 ? _navMesh.Faces[faceIndex] : null;

        _currentRoom = face?.RoomId.HasValue == true
            ? WorldDataService.GetRoom(face.RoomId.Value) : null;

        _roomName = _currentRoom != null
            ? WorldDataService.GetRoomName(_currentRoom)
            : face?.RoomName ?? "";

        if (announce && _roomName.Length > 0)
        {
            _roomNameTimer = RoomNameTime;
            if (_currentRoom?.HasMonsters == true)
                ShowInfo("Hostile area", new Color(200, 120, 50));
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
        if (face.RoomId.HasValue && WorldDataService.GetRoom(face.RoomId.Value) is { HasMonsters: true } room)
        {
            var spawned = MonsterSpawner.Spawn(_navMesh, faceIndex, room);
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

        switch (skill.Target)
        {
            case SkillTarget.SingleEnemy when singleTarget is { IsAlive: true }:
            {
                int dmg = CalcDamage(skill, singleTarget);
                singleTarget.Data.TakeDamage(dmg);
                ShowInfo($"-{dmg}", new Color(220, 80, 80));
                if (!singleTarget.IsAlive)
                {
                    ShowInfo($"Defeated! +{singleTarget.Data.Exp} EXP", Theme.GoldSoft);
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
                    int dmg = CalcDamage(skill, m);
                    m.Data.TakeDamage(dmg);
                    total += dmg; hits++;
                    if (!m.IsAlive && m == _target) _target = null;
                }
                if (hits > 0) ShowInfo($"AoE -{total} ({hits} hit)", new Color(220, 160, 60));
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
                break;
        }
    }

    private void DoAutoAttack(WorldMonster target)
    {
        int dmg = Math.Max(1, _player.TotalPhysicalAttack - target.Data.DefandPhysical());
        target.Data.TakeDamage(dmg);
        ShowInfo($"-{dmg}", new Color(220, 120, 80));
        if (!target.IsAlive)
        {
            ShowInfo($"Defeated! +{target.Data.Exp} EXP", Theme.GoldSoft);
            _target        = null;
            _autoAttacking = false;
        }
    }

    private int CalcDamage(Skill skill, WorldMonster target)
    {
        int attack  = skill.Type == SkillType.Physical
            ? _player.TotalPhysicalAttack : _player.TotalMagicAttack;
        int defense = skill.Type == SkillType.Physical
            ? target.Data.DefandPhysical() : target.Data.TotalMagicDefense;
        return Math.Max(1, (int)(attack * skill.ScalingFactor) - defense);
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
        gd.Clear(new Color(85, 115, 145));

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

        // Navmesh floor
        _navRenderer.Draw(gd, _effect);

        gd.RasterizerState = RasterizerState.CullCounterClockwise;

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

        // 2-D HUD
        gd.DepthStencilState = DepthStencilState.None;
        gd.BlendState        = BlendState.AlphaBlend;
        sb.Begin();
        DrawHud(sb);
        if (_isDead) DrawDeathOverlay(sb);
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

    // ── HUD ───────────────────────────────────────────────────────────────────

    private void DrawHud(SpriteBatch sb)
    {
        // ── Character info (top-left) ────────────────────────────────────────────
        string info = $"{_player.Name}   Lv. {_player.Level}  {_player.Class}";
        Gfx.Rect(sb, 0, 0, (int)Assets.FontNormal.MeasureString(info).X + 28, 40,
            new Color(0, 0, 0, 140));
        Gfx.Text(sb, Assets.FontNormal, info, new Vector2(14, 10), Theme.GoldSoft);

        DrawBar(sb, 14, 48, 180, 10, _player.CurrentHealth, _player.MaxHealth,
            new Color(80, 180, 80), "HP");
        DrawBar(sb, 14, 64, 180, 10, _player.CurrentMana, _player.MaxMana,
            new Color(60, 120, 210), "MP");

        if (_currentRoom?.HasMonsters == true)
        {
            int alive      = _monsters.Count(m => m.IsAlive);
            int respawning = _monsters.Count(m => m.IsRespawning);
            string danger  = $"! HOSTILE  {alive} alive  {respawning} respawning";
            var dSz = Assets.FontSmall.MeasureString(danger);
            Gfx.Rect(sb, 14, 82, (int)dSz.X + 16, 18, new Color(100, 30, 30, 180));
            Gfx.Text(sb, Assets.FontSmall, danger, new Vector2(22, 84), new Color(220, 100, 80));
        }

        // ── Target panel (top-right) ──────────────────────────────────────────
        if (_target != null)
        {
            const int panelW = 220, panelH = 58;
            int px = _sw - panelW - 14, py = 14;
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

        // ── Skill bar (bottom centre) ─────────────────────────────────────────
        DrawSkillBar(sb);

        // ── Control hint (very bottom) ────────────────────────────────────────
        const string hint = "WASD  Move   Tab  Target   1-9  Skills   RMB  Look   2xClick  Auto-attack";
        var hSz = Assets.FontSmall.MeasureString(hint);
        Gfx.Rect(sb, (int)((_sw - hSz.X) / 2f) - 8, _sh - 28, (int)hSz.X + 16, 22,
            new Color(0, 0, 0, 120));
        Gfx.Text(sb, Assets.FontSmall, hint,
            new Vector2((_sw - hSz.X) / 2f, _sh - 24), Theme.ForegroundDim);
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
            ? $"Respawning in {secs}s   —   R to respawn now"
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

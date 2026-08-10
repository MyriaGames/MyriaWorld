using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myria.Lib.Core.Entities.Monsters;
using Myria.Lib.Core.Systems.Enums;

namespace Myria.Mono.World;

public enum MonsterAiState { Idle, Aggroed, Dead }

/// <summary>
/// A live monster entity in the 3D world.  Wraps a cloned Myria.Lib.Core Monster so the
/// shared template is never mutated.  Handles its own simple AI and pre-bakes its
/// local-space mesh so Draw needs only a matrix swap per monster.
/// </summary>
public sealed class WorldMonster
{
    // ── Identity ──────────────────────────────────────────────────────────────
    public Monster        Data          { get; }
    public MonsterAiState State         { get; set; } = MonsterAiState.Idle;

    // ── Spatial state ─────────────────────────────────────────────────────────
    public Vector3 Position      { get; set; }
    public Vector3 SpawnPosition { get; }           // world position at spawn time
    public float   Yaw           { get; set; }
    public int     SpawnFace     { get; }           // navmesh face the monster lives in

    // ── Convenience ───────────────────────────────────────────────────────────
    public bool IsAlive     => Data.CurrentHealth > 0;
    public bool IsRespawning => !IsAlive && _respawnTimer >= 0f;
    public float RespawnTimeRemaining => _respawnTimer;

    // ── Combat VFX & status ───────────────────────────────────────────────────
    /// <summary>Seconds remaining for white hit-flash overlay. Set externally on damage.</summary>
    public float HitFlashTimer { get; set; }
    /// <summary>Seconds of poison remaining.</summary>
    public float PoisonTimer   { get; set; }
    /// <summary>Poison DoT tick countdown (1 dmg per second).</summary>
    public float PoisonTick    { get; set; }
    /// <summary>Damage dealt per poison tick.</summary>
    public int   PoisonDamage  { get; set; }
    /// <summary>Seconds of stun remaining. AI is skipped while > 0.</summary>
    public float StunTimer     { get; set; }

    public bool IsPoisoned => PoisonTimer > 0f;
    public bool IsStunned  => StunTimer  > 0f;

    // ── Pre-baked local-space mesh ────────────────────────────────────────────
    public VertexPositionColor[] MeshVerts { get; }
    public int[]                 MeshIdx   { get; }

    // ── AI tuning ─────────────────────────────────────────────────────────────
    private const float AggroRadius    = 10f;
    private const float LeashRadius    = 20f;   // deaggro past this distance
    private const float AttackRadius   = 2.2f;
    private const float MoveSpeed      = 2.5f;
    private const float AttackInterval = 1.8f;
    private const float RespawnDelay   = 30f;

    private float _attackTimer  = AttackInterval;
    private float _respawnTimer = -1f;          // -1 = not yet started

    // ─────────────────────────────────────────────────────────────────────────

    public WorldMonster(Monster template, Vector3 position, int spawnFace)
    {
        Data          = template.Clone();
        Position      = position;
        SpawnPosition = position;
        SpawnFace     = spawnFace;

        (MeshVerts, MeshIdx) = BuildMesh(template.Type);
    }

    // ── AI ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Advances AI for one frame.
    /// <paramref name="dealDamage"/> is called with the raw hit amount when the
    /// monster lands a melee attack on the player.
    /// </summary>
    public void Update(float dt, Vector3 playerPos, NavMesh navMesh,
                       Action<int> dealDamage)
    {
        if (!IsAlive)
        {
            State = MonsterAiState.Dead;
            if (_respawnTimer < 0f) _respawnTimer = RespawnDelay;
            _respawnTimer -= dt;
            if (_respawnTimer <= 0f)
            {
                Data.CurrentHealth = Data.MaxHealth;
                Position      = SpawnPosition;
                Yaw           = 0f;
                State         = MonsterAiState.Idle;
                _attackTimer  = AttackInterval;
                _respawnTimer = -1f;
                HitFlashTimer = 0f;
                PoisonTimer   = 0f;
                StunTimer     = 0f;
            }
            return;
        }

        // Stunned: pause AI, let timer tick in WorldScreen
        if (StunTimer > 0f) return;

        float dist = Vector3.Distance(
            new Vector3(Position.X, 0f, Position.Z),
            new Vector3(playerPos.X, 0f, playerPos.Z));

        switch (State)
        {
            case MonsterAiState.Idle:
                if (dist <= AggroRadius)
                    State = MonsterAiState.Aggroed;
                break;

            case MonsterAiState.Aggroed:
                if (dist > LeashRadius)
                {
                    State = MonsterAiState.Idle;
                    break;
                }

                if (dist > AttackRadius)
                {
                    // Walk toward player, stay within spawn face
                    Vector3 dir = new Vector3(playerPos.X - Position.X, 0f, playerPos.Z - Position.Z);
                    if (dir.LengthSquared() > 0.001f) dir.Normalize();
                    Vector3 candidate = Position + dir * MoveSpeed * dt;
                    var     xz        = new Vector2(candidate.X, candidate.Z);
                    if (navMesh.FindFaceIndex(xz) == SpawnFace)
                        Position = candidate;
                    Yaw = MathF.Atan2(dir.X, dir.Z);
                }
                else
                {
                    // In melee range — attack on timer
                    _attackTimer -= dt;
                    if (_attackTimer <= 0f)
                    {
                        dealDamage(Data.DealPhysicalDamage());
                        _attackTimer = AttackInterval;
                    }
                }
                break;
        }
    }

    // ── Mesh building ─────────────────────────────────────────────────────────

    private static (VertexPositionColor[], int[]) BuildMesh(MonsterType type)
    {
        var (bright, dark) = ColorsFor(type);
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();

        // Body
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.35f, 0f,   -0.22f),
            new Vector3( 0.35f, 1.4f,  0.22f),
            bright, dark);
        // Head
        Color headBright = MeshBuilder.Lerp(bright, Color.White, 0.15f);
        Color headDark   = MeshBuilder.Lerp(dark,   Color.Black, 0.1f);
        MeshBuilder.AddBox(verts, idx,
            new Vector3(-0.22f, 1.4f,  -0.22f),
            new Vector3( 0.22f, 1.82f,  0.22f),
            headBright, headDark);

        return ([.. verts], [.. idx]);
    }

    private static (Color bright, Color dark) ColorsFor(MonsterType type) => type switch
    {
        MonsterType.Beast     => (new Color(150, 65,  45),  new Color(100, 40, 25)),
        MonsterType.Spirit    => (new Color(125, 155, 205), new Color(80, 105, 155)),
        MonsterType.Elemental => (new Color(205, 125, 45),  new Color(140, 80,  20)),
        MonsterType.Shadow    => (new Color(80,  45,  105), new Color(50,  28,  68)),
        MonsterType.Undead    => (new Color(88,  108, 72),  new Color(58,  72,  46)),
        MonsterType.Humanoid  => (new Color(155, 115, 80),  new Color(105, 75,  50)),
        _                     => (new Color(120, 80,  80),  new Color(80,  50,  50)),
    };
}

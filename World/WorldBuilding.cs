namespace Myria.Mono.World;

/// <summary>
/// Links a world building (placed by WorldDecorationSpawner) to the NPC that lives inside it.
/// The NPC is placed at the building center; the player walks in physically (no collision).
/// </summary>
public sealed record WorldBuilding(string Name, string NpcId, float CenterX, float CenterZ);

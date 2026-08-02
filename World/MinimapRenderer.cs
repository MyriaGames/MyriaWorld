using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MyriaWorld.UI;

namespace MyriaWorld.World;

/// <summary>
/// Pre-renders the navmesh to a small texture once, then draws it each frame
/// with a player dot and optional NPC markers.
/// Call <see cref="Build"/> once after navmesh + effect are ready, then
/// <see cref="Draw"/> every frame.  Dispose to release the render target.
/// </summary>
public sealed class MinimapRenderer : IDisposable
{
    public const int Size = 160;

    private RenderTarget2D? _bg;
    private float _cx, _cz, _span;

    public void Build(GraphicsDevice gd, NavMesh mesh, BasicEffect effect)
    {
        // World bounds from vertex data
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var v in mesh.Vertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.X > maxX) maxX = v.X;
            if (v.Y < minZ) minZ = v.Y;
            if (v.Y > maxZ) maxZ = v.Y;
        }
        _cx   = (minX + maxX) / 2f;
        _cz   = (minZ + maxZ) / 2f;
        _span = Math.Max(maxX - minX, maxZ - minZ) * 1.15f;  // 15% padding

        // Build flat face geometry (Y=0)
        var verts = new List<VertexPositionColor>();
        var idx   = new List<int>();
        foreach (var face in mesh.Faces)
        {
            Color c    = NavMeshRenderer.TerrainColor(face.Terrain);
            int   base_ = verts.Count;
            foreach (int vi in face.VertexIndices)
            {
                var xz = mesh.Vertices[vi];
                verts.Add(new VertexPositionColor(new Vector3(xz.X, 0f, xz.Y), c));
            }
            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                idx.Add(base_);
                idx.Add(base_ + i);
                idx.Add(base_ + i + 1);
            }
        }

        if (idx.Count == 0) return;

        var vb = new VertexBuffer(gd, typeof(VertexPositionColor), verts.Count, BufferUsage.WriteOnly);
        vb.SetData(verts.ToArray());
        var ib = new IndexBuffer(gd, IndexElementSize.ThirtyTwoBits, idx.Count, BufferUsage.WriteOnly);
        ib.SetData(idx.ToArray());

        var rt = new RenderTarget2D(gd, Size, Size, false, SurfaceFormat.Color, DepthFormat.None);

        // Save GPU state
        var savedDS     = gd.DepthStencilState;
        var savedBlend  = gd.BlendState;
        var savedRaster = gd.RasterizerState;
        var savedView   = effect.View;
        var savedProj   = effect.Projection;
        var savedWorld  = effect.World;

        gd.SetRenderTarget(rt);
        gd.Clear(new Color(15, 12, 20));

        gd.DepthStencilState = DepthStencilState.None;
        gd.BlendState        = BlendState.Opaque;
        gd.RasterizerState   = RasterizerState.CullNone;

        // Top-down orthographic: camera above world center, north (-Z) is up
        effect.View       = Matrix.CreateLookAt(
            new Vector3(_cx, 100f, _cz),
            new Vector3(_cx, 0f,   _cz),
            new Vector3(0f,  0f,  -1f));
        effect.Projection = Matrix.CreateOrthographic(_span, _span, 0f, 200f);
        effect.World      = Matrix.Identity;

        gd.SetVertexBuffer(vb);
        gd.Indices = ib;
        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, idx.Count / 3);
        }

        gd.SetRenderTarget(null);

        // Restore GPU state
        gd.DepthStencilState = savedDS;
        gd.BlendState        = savedBlend;
        gd.RasterizerState   = savedRaster;
        effect.View          = savedView;
        effect.Projection    = savedProj;
        effect.World         = savedWorld;

        vb.Dispose();
        ib.Dispose();

        _bg = rt;
    }

    /// <summary>
    /// Draws the minimap at screen position (mapX, mapY).
    /// Must be called inside a SpriteBatch Begin/End block.
    /// </summary>
    public void Draw(SpriteBatch sb, int mapX, int mapY,
                     Vector3 playerPos, IEnumerable<WorldNpc> npcs)
    {
        if (_bg == null) return;

        sb.Draw(_bg, new Rectangle(mapX, mapY, Size, Size), Color.White);
        Gfx.Border(sb, new Rectangle(mapX, mapY, Size, Size), new Color(180, 150, 60, 200));

        // NPC dots — small green squares
        foreach (var npc in npcs)
        {
            var (nx, nz) = WorldToMap(npc.Position.X, npc.Position.Z);
            if (nx >= 1 && nx < Size - 1 && nz >= 1 && nz < Size - 1)
                Gfx.Rect(sb, mapX + nx - 2, mapY + nz - 2, 5, 5, new Color(80, 200, 120));
        }

        // Player dot — gold ring + white center
        var (px, pz) = WorldToMap(playerPos.X, playerPos.Z);
        Gfx.Rect(sb, mapX + px - 4, mapY + pz - 4, 9, 9, new Color(220, 185, 60, 200));
        Gfx.Rect(sb, mapX + px - 2, mapY + pz - 2, 5, 5, Color.White);
    }

    /// <summary>Converts world XZ to pixel coordinates relative to minimap top-left.</summary>
    public (int x, int z) WorldToMap(float worldX, float worldZ)
    {
        int px = (int)(((worldX - _cx) / _span + 0.5f) * Size);
        int pz = (int)(((worldZ - _cz) / _span + 0.5f) * Size);
        return (px, pz);
    }

    public void Dispose()
    {
        _bg?.Dispose();
        _bg = null;
    }
}

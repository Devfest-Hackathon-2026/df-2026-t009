using System.Collections.Generic;
using UnityEngine;

namespace Onsite.Map
{
    /// <summary>光る線・輪・柱を作る小さな道具。</summary>
    public static class Shapes
    {
        static Shader _glow;
        static Shader Glow => _glow != null ? _glow : (_glow = Shader.Find("Onsite/Glow"));

        public static Material GlowMaterial(Color hdr, float fadeY = 0, float pulse = 0, float dash = 0)
        {
            var m = new Material(Glow);
            m.SetColor("_Color", hdr);
            m.SetFloat("_FadeY", fadeY);
            m.SetFloat("_Pulse", pulse);
            m.SetFloat("_Dash", dash);
            return m;
        }

        public static LineRenderer Line(Transform parent, string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.widthMultiplier = width;
            lr.useWorldSpace = true;
            lr.textureMode = LineTextureMode.Tile;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// <summary>地面に寝かせた円（範囲の輪）。六角形なら sides = 6。</summary>
        public static void Ring(LineRenderer lr, Vector3 center, float radius, int sides = 96, float yaw = 0)
        {
            lr.loop = true;
            lr.positionCount = sides;
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2 + yaw;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
            }
        }

        public static void Path(LineRenderer lr, IReadOnlyList<Vector3> pts)
        {
            lr.loop = false;
            lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
        }

        /// <summary>上に向かって細くなる光の柱（側面だけの筒）。</summary>
        public static GameObject Beam(Transform parent, Material mat, float height, float radius, int sides = 6)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "beam" };
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(dir * radius); uv.Add(new Vector2(i / (float)sides, 0));
                v.Add(dir * radius * 0.35f + Vector3.up * height); uv.Add(new Vector2(i / (float)sides, 1));
            }
            for (int i = 0; i < sides; i++)
            {
                int b = i * 2;
                tri.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
            }
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}

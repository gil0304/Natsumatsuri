using System.Collections.Generic;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 手続き生成した部品から GameObject を組み立てるための小道具。
    /// 全ファクトリが共通で使う。
    /// </summary>
    public static class ArtParts
    {
        static MaterialPropertyBlock s_Block;

        /// <summary>空の Transform だけのノード。QueuePoint などの位置マーカーにも使う。</summary>
        public static GameObject Empty(string name, Transform parent)
            => Empty(name, parent, Vector3.zero, Quaternion.identity);

        public static GameObject Empty(string name, Transform parent, Vector3 localPos)
            => Empty(name, parent, localPos, Quaternion.identity);

        public static GameObject Empty(string name, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>メッシュ1個ぶんの見える部品。</summary>
        public static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 localPos)
            => Part(name, parent, mesh, material, localPos, Quaternion.identity, Vector3.one);

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPos, Quaternion localRot)
            => Part(name, parent, mesh, material, localPos, localRot, Vector3.one);

        public static GameObject Part(string name, Transform parent, Mesh mesh, Material material,
            Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            var go = Empty(name, parent, localPos, localRot);
            go.transform.localScale = localScale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return go;
        }

        /// <summary>影を落とさない小物にする。細かい部品を大量に置くときの負荷対策 (§58)。</summary>
        public static GameObject NoShadow(GameObject go)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>マテリアルを増やさずに色だけ変える (§58 ドローコール対策)。</summary>
        public static void SetColor(GameObject go, Color color)
        {
            var mr = go.GetComponent<Renderer>();
            if (mr == null) return;
            SetColor(mr, color);
        }

        public static void SetColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            s_Block ??= new MaterialPropertyBlock();
            // 使い回しのブロックなので、他のレンダラーの値が残らないよう毎回洗う
            s_Block.Clear();
            renderer.GetPropertyBlock(s_Block);
            s_Block.SetColor("_BaseColor", color);
            s_Block.SetColor("_Color", color);
            s_Block.SetColor("_UnlitColor", color);
            renderer.SetPropertyBlock(s_Block);
        }

        /// <summary>色と発光をまとめて上書きする。電球・提灯の色違いに使う。</summary>
        public static void SetColorAndEmission(Renderer renderer, Color color, Color emissive, float intensity)
        {
            if (renderer == null) return;
            s_Block ??= new MaterialPropertyBlock();
            // 使い回しのブロックなので、他のレンダラーの値が残らないよう毎回洗う
            s_Block.Clear();
            renderer.GetPropertyBlock(s_Block);
            s_Block.SetColor("_BaseColor", color);
            s_Block.SetColor("_Color", color);
            Color hdr = emissive.linear * Mathf.Max(0f, intensity);
            s_Block.SetColor("_EmissiveColor", hdr);
            s_Block.SetColor("_EmissionColor", hdr);
            renderer.SetPropertyBlock(s_Block);
        }

        /// <summary>テクスチャの UV オフセットを差し替える。水面のスクロール (§62) 用。</summary>
        public static void SetTextureOffset(Renderer renderer, Vector2 tiling, Vector2 offset)
        {
            if (renderer == null) return;
            s_Block ??= new MaterialPropertyBlock();
            // 使い回しのブロックなので、他のレンダラーの値が残らないよう毎回洗う
            s_Block.Clear();
            renderer.GetPropertyBlock(s_Block);
            var st = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            s_Block.SetVector("_BaseColorMap_ST", st);
            s_Block.SetVector("_MainTex_ST", st);
            s_Block.SetVector("_NormalMap_ST", st);
            renderer.SetPropertyBlock(s_Block);
        }

        /// <summary>親以下のレンダラーをまとめて集める。</summary>
        public static void CollectRenderers(Transform root, List<Renderer> into)
        {
            root.GetComponentsInChildren(true, into);
        }
    }

    /// <summary>LOD の残し方。数字が小さいほど遠くまで残る。</summary>
    public enum LodTier
    {
        /// <summary>遠景でも残る骨格。これだけでシルエットが成立する。</summary>
        Silhouette = 0,
        /// <summary>中距離まで残す。屋台らしさを決める部品。</summary>
        Medium = 1,
        /// <summary>近景専用の小物。垂木・商品見本・背面の荷物など。</summary>
        Detail = 2
    }

    /// <summary>
    /// LOD の分類をオブジェクトに直接書いておくための印。
    /// 名前だけでは分類できない「Roof の下の垂木」のような細部を、確実に LOD1 で落とすために使う。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LodTag : MonoBehaviour
    {
        public LodTier Tier = LodTier.Detail;
    }

    /// <summary>
    /// §58 の LOD。子オブジェクトを「重要度」でグループ分けし、
    /// LOD0=全部 / LOD1=小物を外した中間 / LOD2=シルエットのみ、で LODGroup を張る。
    ///
    /// 重要度の決め方は2段階。
    ///   1. 自分か祖先に <see cref="LodTag"/> があれば、その指定に従う（最優先）
    ///   2. 無ければ root 直下のグループ名で判定する（既存のファクトリ向けの後方互換）
    /// </summary>
    public static class LodBuilder
    {
        /// <summary>遠くでも残す骨格。これだけでシルエットが成立する名前。</summary>
        static readonly HashSet<string> Silhouette = new HashSet<string>
        {
            "MainStructure", "Roof", "Counter", "Structure", "Base", "Pole", "Body",
            "Trunk", "Canopy", "Pillars", "Hall", "Frame", "Tub", "Board", "Seat", "Torii", "Head",
            "LegL", "LegR"
        };

        /// <summary>中距離まで残す物。屋台らしさを決める部品。</summary>
        static readonly HashSet<string> Medium = new HashSet<string>
        {
            "Noren", "Sign", "Lantern", "LightBulbs", "Cloth", "Roof2", "Stripes",
            "Leaves", "Steps", "Door", "Legs", "ArmL", "ArmR", "LegL", "LegR", "Obi", "Hair",
            "Staff", "Prop", "TakoyakiPlate", "Teppan", "IceShaver", "CandyAppleRack", "CottonCandyMachine",
            "Grill", "FishTank", "ShootingRack", "YoyoTub", "BallTub", "KatanukiDesk"
        };

        /// <summary>このオブジェクト以下の LOD 分類を指定する。</summary>
        public static GameObject Tag(GameObject go, LodTier tier)
        {
            if (go == null) return null;
            var tag = go.GetComponent<LodTag>();
            if (tag == null) tag = go.AddComponent<LodTag>();
            tag.Tier = tier;
            return go;
        }

        public static void AddLod(GameObject root) => AddLod(root, new[] { 0.30f, 0.10f, 0.020f });

        /// <summary>
        /// screenRelativeHeights は LOD0→LOD1→LOD2 の切り替え閾値（降順）。
        /// 3要素なら最後の値より小さくなった時点で Culled になる。
        /// </summary>
        public static void AddLod(GameObject root, float[] screenRelativeHeights)
        {
            if (root == null) return;
            if (screenRelativeHeights == null || screenRelativeHeights.Length == 0)
                screenRelativeHeights = new[] { 0.30f, 0.10f, 0.020f };

            var all = new List<Renderer>(64);
            root.GetComponentsInChildren(true, all);
            if (all.Count == 0) return;

            var mid = new List<Renderer>(all.Count);
            var low = new List<Renderer>(all.Count);

            foreach (var r in all)
            {
                LodTier tier;
                var tag = r.GetComponentInParent<LodTag>(true);
                if (tag != null)
                {
                    tier = tag.Tier;
                }
                else
                {
                    string group = TopLevelGroupName(root.transform, r.transform);
                    if (group == null) continue;
                    if (Silhouette.Contains(group)) tier = LodTier.Silhouette;
                    else if (Medium.Contains(group)) tier = LodTier.Medium;
                    else tier = LodTier.Detail;
                }

                if (tier == LodTier.Silhouette) { low.Add(r); mid.Add(r); }
                else if (tier == LodTier.Medium) mid.Add(r);
            }

            // 分類できるものが無かった場合は、全部を骨格扱いにして LOD の意味を保つ
            if (low.Count == 0) low.AddRange(all);
            if (mid.Count == 0) mid.AddRange(all);

            var group0 = root.GetComponent<LODGroup>();
            if (group0 == null) group0 = root.AddComponent<LODGroup>();

            int levels = Mathf.Clamp(screenRelativeHeights.Length, 1, 3);
            var lods = new LOD[levels];
            for (int i = 0; i < levels; i++)
            {
                Renderer[] set = i == 0 ? all.ToArray() : (i == 1 ? mid.ToArray() : low.ToArray());
                lods[i] = new LOD(Mathf.Clamp01(screenRelativeHeights[i]), set);
            }
            group0.SetLODs(lods);
            group0.fadeMode = LODFadeMode.CrossFade;
            group0.animateCrossFading = true;
            group0.RecalculateBounds();
        }

        /// <summary>root の直下の子のうち、この Renderer が属するものの名前を返す。</summary>
        static string TopLevelGroupName(Transform root, Transform target)
        {
            if (target == null || target == root) return null;
            Transform t = target;
            while (t.parent != null && t.parent != root) t = t.parent;
            return t.parent == root ? t.name : null;
        }
    }
}

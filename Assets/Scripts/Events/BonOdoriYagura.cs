using System.Collections.Generic;
using Matsuri.Art;
using Matsuri.Visitors;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Events
{
    /// <summary>
    /// 仕様書 §22。盆踊りのやぐら。
    /// 「見た目を組み立てること」と「踊りの輪の立ち位置を配れること」の2つだけを持つ (§66)。
    /// 輪は半径を段階的に増やした同心円で、時間とともにゆっくり回る。
    /// NPC 側は <see cref="TryReserveSlot"/> でスロットを取り、毎フレーム
    /// <see cref="GetDanceSlot"/> の位置へ歩けば自然に輪になって回る。
    /// 自前の Update は持たない。更新は EventManager から来る (§57)。
    /// </summary>
    public sealed class BonOdoriYagura : MonoBehaviour
    {
        /// <summary>一番内側の輪の半径 (m)。やぐらの外周より少し外。</summary>
        public float InnerRadius = 4.6f;

        /// <summary>輪と輪の間隔 (m)。</summary>
        public float RingSpacing = 1.75f;

        /// <summary>同じ輪の中の人と人の間隔 (m)。</summary>
        public float SlotSpacing = 1.55f;

        /// <summary>輪が1周するのにかかる秒数。反時計回りにゆっくり回る。</summary>
        public float RotationPeriod = 72f;

        /// <summary>作る輪の数。</summary>
        public int RingCount = 5;

        readonly List<int> _ringOfSlot = new List<int>(256);
        readonly List<int> _indexInRing = new List<int>(256);
        readonly List<int> _ringCapacity = new List<int>(8);
        readonly List<float> _ringRadius = new List<float>(8);

        readonly Dictionary<VisitorAgent, int> _reserved = new Dictionary<VisitorAgent, int>(128);
        bool[] _occupied = System.Array.Empty<bool>();

        float _phase;
        readonly List<SwayAnimator> _lanternSways = new List<SwayAnimator>(8);

        /// <summary>踊りの輪に入れる最大人数。</summary>
        public int SlotCount => _ringOfSlot.Count;

        /// <summary>いま輪に入っている人数。</summary>
        public int DancerCount => _reserved.Count;

        /// <summary>やぐらの中心（＝踊りの輪の中心）。</summary>
        public Vector3 Center => transform.position;

        /// <summary>一番外側の輪の半径。呼び寄せる範囲の目安に使う。</summary>
        public float OuterRadius => _ringRadius.Count > 0 ? _ringRadius[_ringRadius.Count - 1] : InnerRadius;

        // ────────────────────────────────────────────────────────
        // 生成
        // ────────────────────────────────────────────────────────

        /// <summary>やぐらを建てる。</summary>
        public static BonOdoriYagura Build(Vector3 center, Transform parent)
        {
            var go = new GameObject("BonOdoriYagura");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var yagura = go.AddComponent<BonOdoriYagura>();
            yagura.Construct();
            yagura.BuildRings();
            return yagura;
        }

        /// <summary>木組みのやぐら本体を組み立てる (§79 「箱1個で済ませない」)。</summary>
        void Construct()
        {
            Material wood      = MatsuriMaterials.Wood(new Color(0.40f, 0.27f, 0.17f));
            Material woodLight = MatsuriMaterials.Wood(new Color(0.55f, 0.40f, 0.26f));
            Material fabricRed = MatsuriMaterials.Fabric(new Color(0.74f, 0.13f, 0.12f));
            Material fabricWhite = MatsuriMaterials.Fabric(new Color(0.94f, 0.92f, 0.87f));
            Material paper     = MatsuriMaterials.Paper(new Color(0.95f, 0.86f, 0.60f));
            Material metal     = MatsuriMaterials.Metal(new Color(0.35f, 0.33f, 0.30f));

            const float baseSize = 5.0f;
            const float deckY = 3.0f;
            const float postR = 0.14f;

            // 基壇
            Mesh baseMesh = MatsuriMeshes.Box(new Vector3(baseSize, 0.55f, baseSize));
            GroundPart(Part(transform, "Base", baseMesh, wood, Vector3.zero, Quaternion.identity), baseMesh, 0f);

            // 4本の柱
            Mesh postMesh = MatsuriMeshes.Cylinder(postR, deckY, 10);
            float h = baseSize * 0.5f - 0.45f;
            var corners = new[]
            {
                new Vector3( h, 0f,  h), new Vector3(-h, 0f,  h),
                new Vector3( h, 0f, -h), new Vector3(-h, 0f, -h)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                GameObject post = Part(transform, $"Post{i + 1:00}", postMesh, wood, corners[i], Quaternion.identity);
                GroundPart(post, postMesh, 0.5f);
            }

            // 斜めの筋交い（木組みらしさ）
            Mesh braceMesh = MatsuriMeshes.Box(new Vector3(0.10f, 0.10f, 3.3f));
            for (int i = 0; i < 4; i++)
            {
                float ang = 90f * i;
                Quaternion yaw = Quaternion.Euler(0f, ang, 0f);
                Vector3 pos = yaw * new Vector3(0f, 1.55f, h);
                Part(transform, $"Brace{i + 1:00}", braceMesh, woodLight, pos, yaw * Quaternion.Euler(38f, 0f, 0f));
            }

            // 舞台の床
            Mesh deckMesh = MatsuriMeshes.Box(new Vector3(baseSize - 0.6f, 0.22f, baseSize - 0.6f));
            GroundPart(Part(transform, "Deck", deckMesh, woodLight, Vector3.zero, Quaternion.identity), deckMesh, deckY);

            // 手すり
            Mesh railMesh = MatsuriMeshes.Box(new Vector3(baseSize - 0.6f, 0.09f, 0.09f));
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                Part(transform, $"Rail{i + 1:00}", railMesh, wood,
                     yaw * new Vector3(0f, deckY + 0.85f, (baseSize - 0.6f) * 0.5f), yaw);
            }

            // 紅白幕（手すりの下に垂らす）
            Mesh curtain = MatsuriMeshes.ClothStrip(baseSize - 0.6f, 0.75f, 8, 3);
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                Material m = (i % 2 == 0) ? fabricRed : fabricWhite;
                GameObject cloth = Part(transform, $"Curtain{i + 1:00}", curtain, m,
                                        yaw * new Vector3(0f, deckY + 0.78f, (baseSize - 0.6f) * 0.5f + 0.02f), yaw);
                AttachSway(cloth, 0.05f, 1.1f);
            }

            // 屋根
            Mesh roof = MatsuriMeshes.GableRoof(baseSize + 1.1f, baseSize + 1.1f, 1.35f, 0.55f);
            GroundPart(Part(transform, "Roof", roof, fabricRed, Vector3.zero, Quaternion.identity), roof, deckY + 1.35f);

            // 舞台の上の大太鼓
            Mesh drum = MatsuriMeshes.Cylinder(0.62f, 0.95f, 20);
            Part(transform, "Taiko", drum, wood, new Vector3(0f, deckY + 0.85f, 0f), Quaternion.Euler(90f, 0f, 0f));
            Mesh head = MatsuriMeshes.Cylinder(0.64f, 0.06f, 20);
            Part(transform, "TaikoHeadA", head, paper, new Vector3(0f, deckY + 0.85f, 0.48f), Quaternion.Euler(90f, 0f, 0f));
            Part(transform, "TaikoHeadB", head, paper, new Vector3(0f, deckY + 0.85f, -0.48f), Quaternion.Euler(90f, 0f, 0f));

            // 四方に張った綱と提灯（やぐらの象徴 §21 / §59）
            BuildLanternLines(metal, deckY);
        }

        /// <summary>屋根の頂点から地面へ放射状に提灯を吊るす。</summary>
        void BuildLanternLines(Material ropeMat, float deckY)
        {
            const int lines = 6;
            const int perLine = 5;
            float topY = deckY + 2.5f;
            Mesh lanternMesh = MatsuriMeshes.Lantern(0.22f, 0.34f, 12);
            Mesh ropeMesh = MatsuriMeshes.Cylinder(0.015f, 1f, 5);

            Color[] palette =
            {
                new Color(0.93f, 0.25f, 0.17f),
                new Color(0.97f, 0.80f, 0.42f),
                new Color(0.94f, 0.93f, 0.88f)
            };

            for (int l = 0; l < lines; l++)
            {
                float ang = 360f / lines * l;
                Quaternion yaw = Quaternion.Euler(0f, ang, 0f);
                Vector3 top = new Vector3(0f, topY, 0f);
                Vector3 end = yaw * new Vector3(0f, 1.1f, 7.2f);

                // 綱
                Vector3 mid = (top + end) * 0.5f;
                float len = Vector3.Distance(top, end);
                GameObject rope = Part(transform, $"Rope{l + 1:00}", ropeMesh, ropeMat, mid,
                                       Quaternion.FromToRotation(Vector3.up, (end - top).normalized));
                rope.transform.localScale = new Vector3(1f, len, 1f);

                for (int k = 1; k <= perLine; k++)
                {
                    float t = k / (float)(perLine + 1);
                    Vector3 p = Vector3.Lerp(top, end, t) + Vector3.down * 0.18f;
                    Color c = palette[(l + k) % palette.Length];
                    Material lit = MatsuriMaterials.Emissive(c, 2.4f);
                    GameObject lantern = Part(transform, $"Lantern{l + 1:00}_{k:00}", lanternMesh, lit, p, Quaternion.identity);
                    AttachSway(lantern, 3.5f, 1.4f + k * 0.11f);

                    if (k % 2 == 1) AddWarmLight(lantern.transform, c, 260f, 7f);
                }
            }

            // やぐら全体を照らす主光源
            AddWarmLight(transform, new Color(1f, 0.84f, 0.58f), 5200f, 26f, new Vector3(0f, deckY + 1.2f, 0f));
        }

        void AttachSway(GameObject go, float amount, float speed)
        {
            var sway = go.AddComponent<SwayAnimator>();
            sway.Amount = amount;
            sway.Speed = speed;
            _lanternSways.Add(sway);
        }

        static GameObject Part(Transform parent, string partName, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        /// <summary>メッシュの原点規約に依存しないよう、bounds を見て底面を bottomY に合わせる。</summary>
        static void GroundPart(GameObject go, Mesh mesh, float bottomY)
        {
            if (go == null || mesh == null) return;
            Vector3 p = go.transform.localPosition;
            p.y = bottomY - mesh.bounds.min.y;
            go.transform.localPosition = p;
        }

        /// <summary>HDRP の点光源 (§59)。Light + HDAdditionalLightData で作る。</summary>
        static void AddWarmLight(Transform parent, Color color, float lumen, float range, Vector3 localPos = default)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = 1f;
            light.shadows = LightShadows.None;

            var hd = go.GetComponent<HDAdditionalLightData>();
            if (hd == null) hd = go.AddComponent<HDAdditionalLightData>();
            hd.SetIntensity(lumen, LightUnit.Lumen);
        }

        // ────────────────────────────────────────────────────────
        // 踊りの輪
        // ────────────────────────────────────────────────────────

        /// <summary>同心円のスロットを作る。外側の輪ほど人数が多い。</summary>
        void BuildRings()
        {
            _ringOfSlot.Clear();
            _indexInRing.Clear();
            _ringCapacity.Clear();
            _ringRadius.Clear();

            for (int ring = 0; ring < Mathf.Max(1, RingCount); ring++)
            {
                float radius = InnerRadius + RingSpacing * ring;
                int capacity = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / Mathf.Max(0.4f, SlotSpacing)));

                _ringRadius.Add(radius);
                _ringCapacity.Add(capacity);

                for (int i = 0; i < capacity; i++)
                {
                    _ringOfSlot.Add(ring);
                    _indexInRing.Add(i);
                }
            }

            _occupied = new bool[_ringOfSlot.Count];
        }

        /// <summary>index 番目のスロットのワールド位置。輪はゆっくり回る。</summary>
        public Vector3 GetDanceSlot(int index)
        {
            if (_ringOfSlot.Count == 0) return Center;
            index = Mathf.Clamp(index, 0, _ringOfSlot.Count - 1);

            int ring = _ringOfSlot[index];
            int slot = _indexInRing[index];
            int capacity = _ringCapacity[ring];
            float radius = _ringRadius[ring];

            // 輪ごとに少しずらして、放射方向に人が一直線に並ばないようにする。
            float offset = (ring % 2 == 0) ? 0f : Mathf.PI / capacity;
            float angle = (Mathf.PI * 2f * slot / capacity) + offset + _phase;

            return Center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>空いているスロットを予約する。満員なら false。</summary>
        public bool TryReserveSlot(VisitorAgent visitor, out int index)
        {
            index = -1;
            if (visitor == null || _occupied.Length == 0) return false;

            if (_reserved.TryGetValue(visitor, out int already))
            {
                index = already;
                return true;
            }

            // 内側の輪から埋める（中心に人が集まって見える）
            for (int i = 0; i < _occupied.Length; i++)
            {
                if (_occupied[i]) continue;
                _occupied[i] = true;
                _reserved[visitor] = i;
                index = i;
                return true;
            }
            return false;
        }

        /// <summary>予約済みの立ち位置。予約が無ければ false。</summary>
        public bool TryGetReservedPosition(VisitorAgent visitor, out Vector3 position)
        {
            position = Center;
            if (visitor == null) return false;
            if (!_reserved.TryGetValue(visitor, out int index)) return false;
            position = GetDanceSlot(index);
            return true;
        }

        public bool IsDancing(VisitorAgent visitor) => visitor != null && _reserved.ContainsKey(visitor);

        /// <summary>輪から抜ける。</summary>
        public void ReleaseSlot(VisitorAgent visitor)
        {
            if (visitor == null) return;
            if (!_reserved.TryGetValue(visitor, out int index)) return;
            _reserved.Remove(visitor);
            if (index >= 0 && index < _occupied.Length) _occupied[index] = false;
        }

        /// <summary>全員解散させる。</summary>
        public void ReleaseAll()
        {
            _reserved.Clear();
            for (int i = 0; i < _occupied.Length; i++) _occupied[i] = false;
        }

        /// <summary>EventManager から毎フレーム呼ばれる (§57)。輪を回し、消えた NPC を掃除する。</summary>
        public void Tick(float dt)
        {
            if (RotationPeriod > 0.01f)
                _phase += (Mathf.PI * 2f / RotationPeriod) * dt;

            if (_phase > Mathf.PI * 2f) _phase -= Mathf.PI * 2f;

            PurgeGoneDancers();
        }

        void PurgeGoneDancers()
        {
            List<VisitorAgent> gone = null;
            foreach (KeyValuePair<VisitorAgent, int> pair in _reserved)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                {
                    gone ??= new List<VisitorAgent>(4);
                    gone.Add(pair.Key);
                }
            }
            if (gone == null) return;

            for (int i = 0; i < gone.Count; i++)
            {
                if (_reserved.TryGetValue(gone[i], out int idx) && idx >= 0 && idx < _occupied.Length)
                    _occupied[idx] = false;
                _reserved.Remove(gone[i]);
            }
        }
    }
}

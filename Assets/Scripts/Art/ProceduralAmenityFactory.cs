using Matsuri.Data;
using Matsuri.Festival;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 「居場所」になる設備の手続き生成 (§20 拡張 / §34 / §79)。
    /// 盆踊り場・休憩所・神社・手水舎。屋台と同じ作法で、既製の 3D モデルを使わずに組み立てる。
    ///
    /// 立ち位置は必ず子オブジェクト "Slot01", "Slot02", ... として作る。
    /// <see cref="Matsuri.Festival.Facility"/> がこれを名前で探し、NPC に配る。
    ///
    /// 神社と手水舎は ProceduralAmenityFactory.Shrine.cs にある (§66 1ファイル1責務)。
    /// </summary>
    public static partial class ProceduralAmenityFactory
    {
        // ---- 共通の色 ----
        static readonly Color Wood       = new Color(0.44f, 0.31f, 0.19f);
        static readonly Color DarkWood   = new Color(0.25f, 0.18f, 0.12f);
        static readonly Color LightWood  = new Color(0.58f, 0.43f, 0.28f);
        static readonly Color Vermilion  = new Color(0.78f, 0.20f, 0.13f);
        static readonly Color Stone      = new Color(0.58f, 0.57f, 0.54f);
        static readonly Color RoofSlate  = new Color(0.21f, 0.22f, 0.25f);
        static readonly Color LanternIvory = new Color(0.97f, 0.86f, 0.58f);

        /// <summary>この設備を専用の見た目で作れるか。</summary>
        public static bool Handles(FacilityData data)
        {
            if (data == null) return false;
            switch (data.Effect)
            {
                case FacilityEffect.Dance:
                case FacilityEffect.Worship:
                case FacilityEffect.Purify:
                    return true;
                case FacilityEffect.Rest:
                    // ベンチは既存の ProceduralFacilityFactory に任せる。休憩所だけこちらで作る。
                    return data.Id == AmenityIds.RestArea;
                default:
                    return false;
            }
        }

        /// <summary>親の下に施設1つぶんの GameObject を作って返す。</summary>
        public static GameObject Build(FacilityData data, Transform parent)
        {
            string name = data != null && !string.IsNullOrEmpty(data.DisplayName)
                ? data.DisplayName : "Amenity";
            var root = ArtParts.Empty(name, parent);
            BuildInto(data, root.transform);
            return root;
        }

        /// <summary>すでにある GameObject の中身として組み立てる。</summary>
        public static void BuildInto(FacilityData data, Transform root)
        {
            if (root == null) return;

            var effect = data != null ? data.Effect : FacilityEffect.Rest;
            int capacity = data != null ? Mathf.Max(1, data.Capacity) : 4;

            switch (effect)
            {
                case FacilityEffect.Dance:   BuildBonOdoriGround(root, capacity); break;
                case FacilityEffect.Worship: BuildShrineGround(root, capacity); break;
                case FacilityEffect.Purify:  BuildTemizuya(root, capacity); break;
                default:                     BuildRestArea(root, capacity); break;
            }

            CreateSlots(root, effect, capacity);
            LodBuilder.AddLod(root.gameObject, new[] { 0.30f, 0.08f, 0.012f });
        }

        /// <summary>NPC の立ち位置を "Slot01".. として作る。Facility が名前で探す。</summary>
        static void CreateSlots(Transform root, FacilityEffect effect, int capacity)
        {
            var group = ArtParts.Empty("Slots", root);
            for (int i = 0; i < capacity; i++)
            {
                var slot = ArtParts.Empty($"Slot{i + 1:00}", group.transform,
                    AmenitySlotLayout.Local(effect, i, capacity));
                // 中心（やぐら・社殿・水盤）を向かせておく。NPC はここに立って中心を見る。
                Vector3 toCenter = -slot.transform.localPosition;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.0001f)
                    slot.transform.localRotation = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 盆踊り場 — やぐら + 提灯の綱 + 円形の縁取り
        // ══════════════════════════════════════════════════════════════════

        /// <summary>やぐらを中心に、踊りの輪が回れる広場を作る (§22 の盆踊りを「建てられる場所」にした物)。</summary>
        static void BuildBonOdoriGround(Transform root, int capacity)
        {
            // 一番外の輪より少し外に縁取りを置く。
            float outer = OuterDanceRadius(capacity) + 1.2f;

            BuildDanceGroundRing(root, outer);
            BuildYagura(root);
            BuildLanternRopes(root, outer);
        }

        /// <summary>capacity 人が入る輪のうち、一番外側の半径。</summary>
        static float OuterDanceRadius(int capacity)
        {
            float max = AmenitySlotLayout.DanceInnerRadius;
            for (int i = 0; i < capacity; i++)
            {
                Vector3 p = AmenitySlotLayout.DanceRing(i);
                float r = new Vector2(p.x, p.z).magnitude;
                if (r > max) max = r;
            }
            return max;
        }

        /// <summary>踏み固められた円形の広場と、その縁取り。</summary>
        static void BuildDanceGroundRing(Transform root, float outer)
        {
            var node = ArtParts.Empty("Base", root);

            // 踏み固められた土。周りの地面よりわずかに明るく、少しだけ浮かせて Z ファイトを避ける。
            ArtParts.NoShadow(ArtParts.Part("Yard", node.transform, MatsuriMeshes.Disc(outer, 40),
                MatsuriMaterials.Ground(new Color(0.36f, 0.30f, 0.23f)), new Vector3(0f, 0.015f, 0f)));

            // 縁取りの縄。
            ArtParts.NoShadow(ArtParts.Part("Edge", node.transform, MatsuriMeshes.Torus(outer, 0.075f, 44, 6),
                MatsuriMaterials.Fabric(new Color(0.86f, 0.82f, 0.70f)), new Vector3(0f, 0.075f, 0f)));

            // 縄を受ける杭。8本。
            var stake = MatsuriMeshes.Cylinder(0.055f, 0.42f, 6);
            var stakeMat = MatsuriMaterials.Wood(DarkWood);
            for (int i = 0; i < 8; i++)
            {
                float a = Mathf.PI * 2f * i / 8f;
                ArtParts.NoShadow(ArtParts.Part($"Stake{i:00}", node.transform, stake, stakeMat,
                    new Vector3(Mathf.Cos(a) * outer, 0.21f, Mathf.Sin(a) * outer)));
            }
        }

        /// <summary>やぐら本体。土台・4本柱・筋交い・舞台・欄干・屋根・太鼓。</summary>
        static void BuildYagura(Transform root)
        {
            var wood = MatsuriMaterials.Wood(Wood);
            var light = MatsuriMaterials.Wood(LightWood);
            var dark = MatsuriMaterials.Wood(DarkWood);

            const float baseSize = 4.4f;
            const float deckY = 3.1f;
            float half = baseSize * 0.5f - 0.40f;

            // ---- 土台 ----
            var baseNode = ArtParts.Empty("Structure", root);
            ArtParts.Part("Platform", baseNode.transform, MatsuriMeshes.Box(new Vector3(baseSize, 0.50f, baseSize)),
                dark, new Vector3(0f, 0.25f, 0f));

            // ---- 4本柱 ----
            var frame = ArtParts.Empty("Frame", root);
            var post = MatsuriMeshes.Cylinder(0.145f, deckY, 10);
            var corners = new[]
            {
                new Vector3( half, 0f,  half), new Vector3(-half, 0f,  half),
                new Vector3( half, 0f, -half), new Vector3(-half, 0f, -half)
            };
            for (int i = 0; i < corners.Length; i++)
                ArtParts.Part($"Post{i + 1:00}", frame.transform, post, wood,
                    corners[i] + new Vector3(0f, 0.50f + deckY * 0.5f, 0f));

            // 筋交い。木組みらしさが出る (§79 「箱1個で済ませない」)。
            var brace = MatsuriMeshes.Box(new Vector3(0.09f, 0.09f, 3.1f));
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                ArtParts.Part($"Brace{i + 1:00}", frame.transform, brace, light,
                    yaw * new Vector3(0f, 1.95f, half), yaw * Quaternion.Euler(40f, 0f, 0f));
            }

            // ---- 舞台の床 ----
            ArtParts.Part("Deck", baseNode.transform, MatsuriMeshes.Box(new Vector3(baseSize - 0.5f, 0.20f, baseSize - 0.5f)),
                light, new Vector3(0f, 0.50f + deckY + 0.10f, 0f));

            // ---- 上段の欄干 ----
            float railY = 0.50f + deckY + 0.20f;
            float railHalf = (baseSize - 0.5f) * 0.5f;
            var railNode = ArtParts.Empty("Rail", root);
            var balusterMesh = MatsuriMeshes.Box(new Vector3(0.06f, 0.62f, 0.06f));
            for (int side = 0; side < 4; side++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * side, 0f);
                // 手すり（上下2本）
                for (int k = 0; k < 2; k++)
                    ArtParts.Part($"Rail{side}{k}", railNode.transform,
                        MatsuriMeshes.Box(new Vector3(baseSize - 0.5f, 0.07f, 0.07f)), dark,
                        yaw * new Vector3(0f, railY + 0.30f + k * 0.30f, railHalf), yaw);
                // 束
                for (int k = 0; k < 5; k++)
                {
                    float t = -railHalf + 0.2f + k * (railHalf * 2f - 0.4f) / 4f;
                    ArtParts.NoShadow(ArtParts.Part($"Baluster{side}{k}", railNode.transform, balusterMesh, dark,
                        yaw * new Vector3(t, railY + 0.31f, railHalf), yaw));
                }
            }

            // ---- 屋根 ----
            float roofY = railY + 0.92f;
            var roof = ArtParts.Empty("Roof", root, new Vector3(0f, roofY, 0f));
            var roofMat = MatsuriMaterials.Painted(RoofSlate, 0.28f);
            // 屋根を支える柱
            var upperPost = MatsuriMeshes.Cylinder(0.085f, 0.92f, 8);
            for (int i = 0; i < corners.Length; i++)
                ArtParts.Part($"UpperPost{i + 1:00}", roof.transform, upperPost, wood,
                    corners[i] * 0.92f + new Vector3(0f, -0.46f, 0f));
            ArtParts.Part("Gable", roof.transform, MatsuriMeshes.GableRoof(baseSize + 0.9f, baseSize + 0.9f, 0.95f, 0.45f),
                roofMat, Vector3.zero);
            ArtParts.Part("Finial", roof.transform, MatsuriMeshes.Sphere(0.16f, 10, 7),
                MatsuriMaterials.Metal(new Color(0.72f, 0.60f, 0.28f)), new Vector3(0f, 1.05f, 0f));

            // 紅白幕。やぐらの一番の目印 (§78 祭りらしさ)。
            var cloth = ArtParts.Empty("Cloth", root, new Vector3(0f, railY + 0.60f, 0f));
            var red = MatsuriMaterials.Fabric(new Color(0.76f, 0.14f, 0.13f));
            var white = MatsuriMaterials.Fabric(new Color(0.95f, 0.93f, 0.89f));
            var stripMesh = MatsuriMeshes.ClothStrip(0.52f, 0.60f, 3, 4);
            for (int side = 0; side < 4; side++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * side, 0f);
                for (int k = 0; k < 8; k++)
                {
                    float t = -railHalf + 0.30f + k * (railHalf * 2f - 0.6f) / 7f;
                    var go = ArtParts.Part($"Curtain{side}{k}", cloth.transform, stripMesh,
                        (k % 2 == 0) ? red : white, yaw * new Vector3(t, 0f, railHalf + 0.05f), yaw);
                    ArtParts.NoShadow(go);
                }
            }

            // ---- 太鼓 ----
            var drum = ArtParts.Empty("Prop", root, new Vector3(0f, 0.50f + deckY + 0.20f, 0f));
            var drumBody = MatsuriMaterials.Wood(new Color(0.36f, 0.16f, 0.10f));
            ArtParts.Part("DrumStand", drum.transform, MatsuriMeshes.Box(new Vector3(0.9f, 0.10f, 0.5f)), dark, new Vector3(0f, 0.05f, 0f));
            ArtParts.Part("DrumBody", drum.transform, MatsuriMeshes.Cylinder(0.52f, 0.62f, 18), drumBody,
                new Vector3(0f, 0.62f, 0f), Quaternion.Euler(0f, 0f, 90f));
            var skin = MatsuriMaterials.Painted(new Color(0.90f, 0.84f, 0.68f), 0.2f);
            ArtParts.Part("DrumHeadL", drum.transform, MatsuriMeshes.Disc(0.52f, 18), skin,
                new Vector3(-0.32f, 0.62f, 0f), Quaternion.Euler(0f, 0f, 90f));
            ArtParts.Part("DrumHeadR", drum.transform, MatsuriMeshes.Disc(0.52f, 18), skin,
                new Vector3(0.32f, 0.62f, 0f), Quaternion.Euler(0f, 0f, -90f));
            var stick = MatsuriMeshes.Cylinder(0.028f, 0.44f, 6);
            ArtParts.NoShadow(ArtParts.Part("StickL", drum.transform, stick, light, new Vector3(-0.10f, 1.02f, 0.30f), Quaternion.Euler(72f, 0f, 12f)));
            ArtParts.NoShadow(ArtParts.Part("StickR", drum.transform, stick, light, new Vector3(0.10f, 1.02f, 0.30f), Quaternion.Euler(72f, 0f, -12f)));

            // 実光源はやぐらの上に1つだけ (§58)。提灯の光は Emissive で見せる。
            ProceduralDecorationFactory.AttachLight(root, new Color(1f, 0.74f, 0.44f), 2600f, 22f, roofY - 0.4f);
        }

        /// <summary>柱の頭から四方へ綱を張り、提灯を吊るす。</summary>
        static void BuildLanternRopes(Transform root, float outer)
        {
            var group = ArtParts.Empty("Lantern", root);
            var ropeMat = MatsuriMaterials.Fabric(new Color(0.24f, 0.20f, 0.16f));
            var poleMat = MatsuriMaterials.Wood(DarkWood);
            var colors = new[]
            {
                LanternIvory,
                new Color(0.95f, 0.55f, 0.30f),
                new Color(0.94f, 0.80f, 0.42f)
            };

            const float topY = 4.55f;   // やぐらの屋根の下あたり
            const float poleH = 4.0f;
            float poleR = outer + 0.6f;

            for (int side = 0; side < 4; side++)
            {
                float a = Mathf.PI * 0.5f * side + Mathf.PI * 0.25f;
                Vector3 outPos = new Vector3(Mathf.Cos(a) * poleR, 0f, Mathf.Sin(a) * poleR);

                // 綱を受ける柱
                ArtParts.Part($"Pole{side:00}", group.transform, MatsuriMeshes.Cylinder(0.075f, poleH, 8), poleMat,
                    outPos + new Vector3(0f, poleH * 0.5f, 0f));

                Vector3 from = new Vector3(Mathf.Cos(a) * 1.6f, topY, Mathf.Sin(a) * 1.6f);
                Vector3 to = outPos + new Vector3(0f, poleH - 0.15f, 0f);
                Rope(group.transform, $"Rope{side:00}", from, to, ropeMat, 0.018f);

                // 綱に沿って提灯を吊るす。垂れ下がりを sin で作る。
                const int perRope = 5;
                for (int i = 1; i <= perRope; i++)
                {
                    float t = i / (float)(perRope + 1);
                    Vector3 p = Vector3.Lerp(from, to, t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * 0.42f;   // 綱のたるみ
                    ProceduralDecorationFactory.BuildLantern(group.transform, p,
                        colors[(side + i) % colors.Length], 0.20f, 0.50f, 0.55f + i * 0.09f);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 休憩所 — 切妻の屋根 + 柱4本 + 縁台2つ + 赤い毛氈 + 提灯 + うちわ立て
        // ══════════════════════════════════════════════════════════════════

        static void BuildRestArea(Transform root, int capacity)
        {
            int perRow = Mathf.Max(1, Mathf.CeilToInt(capacity / (capacity <= 4 ? 1f : 2f)));
            float span = Mathf.Max(1.2f, perRow * 0.82f);
            float w = span + 1.6f;
            const float d = 3.4f;
            const float postH = 2.5f;

            var wood = MatsuriMaterials.Wood(Wood);
            var dark = MatsuriMaterials.Wood(DarkWood);
            var light = MatsuriMaterials.Wood(LightWood);

            // ---- 柱4本と桁 ----
            var frame = ArtParts.Empty("Frame", root);
            var post = MatsuriMeshes.Box(new Vector3(0.14f, postH, 0.14f));
            float px = w * 0.5f - 0.2f, pz = d * 0.5f - 0.2f;
            var feet = new[]
            {
                new Vector3( px, 0f,  pz), new Vector3(-px, 0f,  pz),
                new Vector3( px, 0f, -pz), new Vector3(-px, 0f, -pz)
            };
            for (int i = 0; i < feet.Length; i++)
                ArtParts.Part($"Post{i + 1:00}", frame.transform, post, wood, feet[i] + new Vector3(0f, postH * 0.5f, 0f));

            ArtParts.Part("BeamL", frame.transform, MatsuriMeshes.Box(new Vector3(0.10f, 0.12f, d - 0.3f)), dark, new Vector3(-px, postH - 0.06f, 0f));
            ArtParts.Part("BeamR", frame.transform, MatsuriMeshes.Box(new Vector3(0.10f, 0.12f, d - 0.3f)), dark, new Vector3(px, postH - 0.06f, 0f));
            ArtParts.Part("BeamF", frame.transform, MatsuriMeshes.Box(new Vector3(w - 0.3f, 0.12f, 0.10f)), dark, new Vector3(0f, postH - 0.06f, pz));
            ArtParts.Part("BeamB", frame.transform, MatsuriMeshes.Box(new Vector3(w - 0.3f, 0.12f, 0.10f)), dark, new Vector3(0f, postH - 0.06f, -pz));

            // ---- 切妻の屋根 ----
            ArtParts.Part("Roof", root, MatsuriMeshes.GableRoof(w, d, 0.62f, 0.35f),
                MatsuriMaterials.Painted(RoofSlate, 0.28f), new Vector3(0f, postH, 0f));

            // ---- 縁台2つ（座る場所） ----
            var seats = ArtParts.Empty("Seat", root);
            int rows = capacity <= 4 ? 1 : 2;
            for (int r = 0; r < rows; r++)
            {
                float z = rows == 1 ? 0f : (r == 0 ? -1.15f : 1.15f);
                var bench = ArtParts.Empty($"Bench{r + 1:00}", seats.transform, new Vector3(0f, 0f, z));

                ArtParts.Part("Top", bench.transform, MatsuriMeshes.Box(new Vector3(span + 0.7f, 0.09f, 0.62f)),
                    light, new Vector3(0f, AmenitySlotLayout.BenchSeatHeight - 0.045f, 0f));
                var leg = MatsuriMeshes.Box(new Vector3(0.10f, AmenitySlotLayout.BenchSeatHeight - 0.09f, 0.52f));
                ArtParts.Part("LegL", bench.transform, leg, dark,
                    new Vector3(-(span + 0.7f) * 0.5f + 0.2f, (AmenitySlotLayout.BenchSeatHeight - 0.09f) * 0.5f, 0f));
                ArtParts.Part("LegR", bench.transform, leg, dark,
                    new Vector3((span + 0.7f) * 0.5f - 0.2f, (AmenitySlotLayout.BenchSeatHeight - 0.09f) * 0.5f, 0f));

                // 赤い毛氈。縁台の上に敷く。
                ArtParts.NoShadow(ArtParts.Part("Cloth", bench.transform, MatsuriMeshes.Plane(span + 0.76f, 0.68f),
                    MatsuriMaterials.Fabric(new Color(0.72f, 0.13f, 0.14f)),
                    new Vector3(0f, AmenitySlotLayout.BenchSeatHeight + 0.005f, 0f)));
                // 前に垂れる縁
                var skirt = ArtParts.Part("ClothSkirt", bench.transform, MatsuriMeshes.ClothStrip(span + 0.76f, 0.22f, 6, 2),
                    MatsuriMaterials.Fabric(new Color(0.72f, 0.13f, 0.14f)),
                    new Vector3(0f, AmenitySlotLayout.BenchSeatHeight, 0.34f));
                ArtParts.NoShadow(skirt);
                SwayAnimator.Attach(skirt, SwayMode.Cloth, 0.018f, 0.7f);
            }

            // ---- うちわ立てと湯呑みの盆（生活感を足す §79） ----
            var prop = ArtParts.Empty("Prop", root, new Vector3(px - 0.45f, 0f, -pz + 0.35f));
            ArtParts.Part("FanTub", prop.transform, MatsuriMeshes.Cylinder(0.16f, 0.34f, 10),
                MatsuriMaterials.Wood(new Color(0.62f, 0.50f, 0.32f)), new Vector3(0f, 0.17f, 0f));
            var fanMesh = MatsuriMeshes.Quad(0.22f, 0.26f);
            var fanMat = MatsuriMaterials.Paper(new Color(0.94f, 0.92f, 0.86f));
            for (int i = 0; i < 5; i++)
            {
                float a = 20f * i - 40f;
                ArtParts.NoShadow(ArtParts.Part($"Fan{i:00}", prop.transform, fanMesh, fanMat,
                    new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 0.05f, 0.52f, Mathf.Cos(a * Mathf.Deg2Rad) * 0.02f),
                    Quaternion.Euler(0f, a, 8f - i * 3f)));
            }

            // ---- 提灯1つ ----
            var lantern = ArtParts.Empty("Lantern", root);
            ProceduralDecorationFactory.BuildLantern(lantern.transform, new Vector3(0f, postH - 0.10f, pz - 0.05f),
                LanternIvory, 0.22f, 0.55f, 0.6f);
            ProceduralDecorationFactory.AttachLight(root, new Color(1f, 0.78f, 0.52f), 900f, 9f, postH - 0.35f);
        }

        // ══════════════════════════════════════════════════════════════════
        // 共通の小道具
        // ══════════════════════════════════════════════════════════════════

        /// <summary>2点を結ぶ綱・紐。細い円柱を向きを合わせて置く。</summary>
        static GameObject Rope(Transform parent, string name, Vector3 a, Vector3 b, Material mat, float radius)
        {
            Vector3 delta = b - a;
            float len = delta.magnitude;
            if (len < 0.01f) return null;

            var rot = Quaternion.FromToRotation(Vector3.up, delta / len);
            return ArtParts.NoShadow(ArtParts.Part(name, parent, MatsuriMeshes.Cylinder(radius, len, 6),
                mat, a + delta * 0.5f, rot));
        }
    }
}

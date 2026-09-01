using Matsuri.Data;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Art
{
    /// <summary>
    /// §21 の装飾を手続き生成する。提灯・のぼり・神社・鳥居・木・電飾・大看板。
    /// GroundBuilder からも木と神社を借りられるよう、個別のビルダーを公開している。
    /// </summary>
    public static class ProceduralDecorationFactory
    {
        static readonly Color Vermilion = new Color(0.78f, 0.20f, 0.13f);
        static readonly Color DarkWood = new Color(0.26f, 0.19f, 0.13f);
        static readonly Color Stone = new Color(0.52f, 0.51f, 0.48f);

        public static GameObject Build(DecorationData data, Transform parent)
        {
            var kind = data != null ? data.Visual : DecorationVisualKind.Lantern;
            string name = data != null && !string.IsNullOrEmpty(data.DisplayName) ? data.DisplayName : kind.ToString();
            Color main = data != null ? data.MainColor : Vermilion;

            var root = ArtParts.Empty(name, parent);

            switch (kind)
            {
                case DecorationVisualKind.Nobori: BuildNobori(root.transform, main, name); break;
                case DecorationVisualKind.Shrine: BuildShrine(root.transform, main); break;
                case DecorationVisualKind.Torii: BuildTorii(root.transform, main, 4.2f, 5.4f); break;
                case DecorationVisualKind.Tree: BuildTree(root.transform, main, 1f, 0); break;
                case DecorationVisualKind.StallLight: BuildStallLight(root.transform, main); break;
                case DecorationVisualKind.FestivalSign: BuildFestivalSign(root.transform, main); break;
                default: BuildLanternPost(root.transform, main); break;
            }

            if (data != null && data.EmitsLight)
                AttachLight(root.transform, data.LightColor, data.LightIntensity, data.LightRange, LightHeight(kind));

            if (data != null && data.SwaysInWind && kind == DecorationVisualKind.Tree)
            {
                var crown = root.transform.Find("Canopy");
                if (crown != null) SwayAnimator.Attach(crown.gameObject, SwayMode.Rotate, 2.2f, 0.42f).Axis = Vector3.forward;
            }

            LodBuilder.AddLod(root, new[] { 0.30f, 0.08f, 0.010f });
            return root;
        }

        static float LightHeight(DecorationVisualKind kind)
        {
            switch (kind)
            {
                case DecorationVisualKind.Shrine: return 3.4f;
                case DecorationVisualKind.Torii: return 4.6f;
                case DecorationVisualKind.FestivalSign: return 3.2f;
                case DecorationVisualKind.StallLight: return 2.9f;
                case DecorationVisualKind.Tree: return 3.0f;
                case DecorationVisualKind.Nobori: return 2.6f;
                default: return 2.5f;
            }
        }

        /// <summary>実光源を1個だけ足す (§58)。影は落とさない。</summary>
        public static Light AttachLight(Transform parent, Color color, float intensity, float range, float height)
        {
            var go = ArtParts.Empty("PointLight", parent, new Vector3(0f, height, 0f));
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = Mathf.Max(1f, range);
            l.shadows = LightShadows.None;
            var hd = go.AddComponent<HDAdditionalLightData>();
            hd.intensity = Mathf.Max(1f, intensity);
            return l;
        }

        // ------------------------------------------------------------------ 提灯

        /// <summary>提灯を1灯ぶん作る。紐で吊られ、揺れ、光る。</summary>
        public static GameObject BuildLantern(Transform parent, Vector3 localPos, Color color,
            float radius, float height, float swaySpeed)
        {
            var pivot = ArtParts.Empty("Lantern", parent, localPos);
            SwayAnimator.Attach(pivot, SwayMode.Rotate, 5f, swaySpeed).Axis = Vector3.forward;
            ArtParts.NoShadow(ArtParts.Part("Cord", pivot.transform, MatsuriMeshes.Cylinder(0.006f, 0.24f, 5),
                MatsuriMaterials.Wood(DarkWood), new Vector3(0f, -0.12f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Body", pivot.transform, MatsuriMeshes.Lantern(radius, height, 16),
                MatsuriMaterials.GlowingPaper(color, 5.5f), new Vector3(0f, -0.24f - height * 0.5f, 0f)));
            return pivot;
        }

        static void BuildLanternPost(Transform root, Color color)
        {
            var wood = MatsuriMaterials.Wood(DarkWood);
            var pole = ArtParts.Empty("Pole", root);
            ArtParts.Part("Base", pole.transform, MatsuriMeshes.Box(new Vector3(0.44f, 0.12f, 0.44f)), MatsuriMaterials.Painted(Stone, 0.2f), new Vector3(0f, 0.06f, 0f));
            ArtParts.Part("Shaft", pole.transform, MatsuriMeshes.Cylinder(0.055f, 2.7f, 10), wood, new Vector3(0f, 1.35f, 0f));
            ArtParts.Part("Arm", pole.transform, MatsuriMeshes.Cylinder(0.035f, 1.05f, 8), wood,
                new Vector3(0f, 2.66f, 0f), Quaternion.Euler(0f, 0f, 90f));
            ArtParts.Part("Cap", pole.transform, MatsuriMeshes.Cone(0.09f, 0.12f, 8), wood, new Vector3(0f, 2.76f, 0f));

            var group = ArtParts.Empty("Lantern", root);
            BuildLantern(group.transform, new Vector3(-0.44f, 2.62f, 0f), color, 0.19f, 0.50f, 0.55f);
            BuildLantern(group.transform, new Vector3(0.44f, 2.62f, 0f), color, 0.19f, 0.50f, 0.71f);
        }

        // ------------------------------------------------------------------ のぼり

        static void BuildNobori(Transform root, Color color, string text)
        {
            var wood = MatsuriMaterials.Wood(new Color(0.72f, 0.62f, 0.44f));
            var pole = ArtParts.Empty("Pole", root);
            ArtParts.Part("FootA", pole.transform, MatsuriMeshes.Box(new Vector3(0.70f, 0.09f, 0.09f)), wood, new Vector3(0f, 0.045f, 0f));
            ArtParts.Part("FootB", pole.transform, MatsuriMeshes.Box(new Vector3(0.09f, 0.09f, 0.70f)), wood, new Vector3(0f, 0.045f, 0f));
            ArtParts.Part("Shaft", pole.transform, MatsuriMeshes.Cylinder(0.038f, 3.5f, 8), wood, new Vector3(0f, 1.75f, 0f));
            ArtParts.Part("Arm", pole.transform, MatsuriMeshes.Cylinder(0.026f, 0.66f, 6), wood,
                new Vector3(0.33f, 3.42f, 0f), Quaternion.Euler(0f, 0f, 90f));
            ArtParts.NoShadow(ArtParts.Part("Finial", pole.transform, MatsuriMeshes.Sphere(0.055f, 10, 7), wood, new Vector3(0f, 3.52f, 0f)));

            var cloth = ArtParts.Empty("Cloth", root, new Vector3(0.33f, 3.40f, 0f));
            var tex = ProceduralTextures.KanjiSign(text, 256, 768, color, PickInk(color), true);
            var flag = ArtParts.Part("Flag", cloth.transform, MatsuriMeshes.ClothStrip(0.62f, 2.5f, 5, 10),
                MatsuriMaterials.Printed(tex, Color.white), Vector3.zero);
            SwayAnimator.Attach(flag, SwayMode.Cloth, 0.10f, 1.15f);
        }

        static Color PickInk(Color background)
        {
            float lum = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return lum < 0.5f ? new Color(0.97f, 0.95f, 0.90f) : new Color(0.09f, 0.08f, 0.07f);
        }

        // ------------------------------------------------------------------ 神社

        /// <summary>社殿。土台・階段・柱・入母屋屋根・賽銭箱。</summary>
        public static void BuildShrine(Transform root, Color color)
        {
            var stone = MatsuriMaterials.Painted(Stone, 0.18f);
            var wood = MatsuriMaterials.Wood(new Color(0.34f, 0.22f, 0.15f));
            var paint = MatsuriMaterials.Painted(color, 0.30f);
            var roofMat = MatsuriMaterials.Painted(new Color(0.20f, 0.22f, 0.24f), 0.28f);

            const float w = 5.6f, d = 4.6f, wallH = 2.6f;

            var baseNode = ArtParts.Empty("Base", root);
            ArtParts.Part("Platform", baseNode.transform, MatsuriMeshes.Box(new Vector3(w + 1.4f, 0.55f, d + 1.2f)), stone, new Vector3(0f, 0.275f, 0f));
            ArtParts.Part("Deck", baseNode.transform, MatsuriMeshes.Box(new Vector3(w + 0.8f, 0.22f, d + 0.7f)), wood, new Vector3(0f, 0.66f, 0f));

            var steps = ArtParts.Empty("Steps", root);
            for (int i = 0; i < 4; i++)
                ArtParts.Part("Step" + i, steps.transform, MatsuriMeshes.Box(new Vector3(2.6f, 0.16f, 0.34f)), stone,
                    new Vector3(0f, 0.08f + i * 0.16f, d * 0.5f + 1.30f - i * 0.34f));

            var hall = ArtParts.Empty("Hall", root);
            var pillar = MatsuriMeshes.Cylinder(0.16f, wallH, 10);
            for (int i = 0; i < 6; i++)
            {
                float x = -w * 0.5f + (i % 3) * (w * 0.5f);
                float z = (i < 3 ? -1f : 1f) * d * 0.5f;
                ArtParts.Part("Pillar" + i, hall.transform, pillar, paint, new Vector3(x, 0.77f + wallH * 0.5f, z));
            }
            ArtParts.Part("BackWall", hall.transform, MatsuriMeshes.Box(new Vector3(w, wallH, 0.16f)), wood, new Vector3(0f, 0.77f + wallH * 0.5f, -d * 0.5f));
            ArtParts.Part("SideWallL", hall.transform, MatsuriMeshes.Box(new Vector3(0.16f, wallH, d)), wood, new Vector3(-w * 0.5f, 0.77f + wallH * 0.5f, 0f));
            ArtParts.Part("SideWallR", hall.transform, MatsuriMeshes.Box(new Vector3(0.16f, wallH, d)), wood, new Vector3(w * 0.5f, 0.77f + wallH * 0.5f, 0f));
            // 正面は開口。上に欄間を入れる
            ArtParts.Part("Lintel", hall.transform, MatsuriMeshes.Box(new Vector3(w, 0.45f, 0.18f)), paint, new Vector3(0f, 0.77f + wallH - 0.22f, d * 0.5f));

            // 入母屋屋根：裾の四方流れ（台形）＋上の切妻
            var roof = ArtParts.Empty("Roof", root, new Vector3(0f, 0.77f + wallH, 0f));
            ArtParts.Part("Skirt", roof.transform, MatsuriMeshes.TaperedBox(w + 3.0f, d + 2.6f, w * 0.6f, d * 0.55f, 1.05f), roofMat, new Vector3(0f, 0.52f, 0f));
            ArtParts.Part("Gable", roof.transform, MatsuriMeshes.GableRoof(w * 0.62f, d * 0.58f, 1.05f, 0.55f), roofMat, new Vector3(0f, 1.02f, 0f));
            ArtParts.Part("RidgeOrnament", roof.transform, MatsuriMeshes.Box(new Vector3(w * 0.66f, 0.16f, 0.28f)), paint, new Vector3(0f, 2.12f, 0f));

            // 賽銭箱
            var box = ArtParts.Empty("OfferingBox", root, new Vector3(0f, 0.88f, d * 0.5f + 0.10f));
            ArtParts.Part("Body", box.transform, MatsuriMeshes.Box(new Vector3(1.5f, 0.62f, 0.7f)), wood, new Vector3(0f, 0.31f, 0f));
            var slat = MatsuriMeshes.Box(new Vector3(1.42f, 0.05f, 0.05f));
            for (int i = 0; i < 7; i++)
                ArtParts.NoShadow(ArtParts.Part("Slat" + i, box.transform, slat, MatsuriMaterials.Wood(DarkWood), new Vector3(0f, 0.64f, -0.26f + i * 0.087f)));

            // 軒下の提灯
            var lanterns = ArtParts.Empty("Lantern", root);
            BuildLantern(lanterns.transform, new Vector3(-w * 0.42f, 0.77f + wallH - 0.10f, d * 0.5f + 0.45f), Vermilion, 0.24f, 0.62f, 0.5f);
            BuildLantern(lanterns.transform, new Vector3(w * 0.42f, 0.77f + wallH - 0.10f, d * 0.5f + 0.45f), Vermilion, 0.24f, 0.62f, 0.66f);
        }

        // ------------------------------------------------------------------ 鳥居

        /// <summary>鳥居。朱塗り、足元に沓石。</summary>
        public static void BuildTorii(Transform root, Color color, float width, float height)
        {
            var paint = MatsuriMaterials.Painted(color.maxColorComponent < 0.05f ? Vermilion : color, 0.32f);
            var node = ArtParts.Empty("Torii", root);
            ArtParts.Part("Frame", node.transform, MatsuriMeshes.Torii(width, height), paint, Vector3.zero);

            var stone = MatsuriMaterials.Painted(Stone, 0.18f);
            var footMesh = MatsuriMeshes.Cylinder(width * 0.085f, 0.30f, 12);
            var baseNode = ArtParts.Empty("Base", root);
            ArtParts.Part("FootL", baseNode.transform, footMesh, stone, new Vector3(-width * 0.5f, 0.15f, 0f));
            ArtParts.Part("FootR", baseNode.transform, footMesh, stone, new Vector3(width * 0.5f, 0.15f, 0f));
        }

        // ------------------------------------------------------------------ 木

        /// <summary>木。幹＋枝分かれ＋葉の塊。scale で大きさを変えられる。</summary>
        public static GameObject BuildTree(Transform root, Color leafColor, float scale, int seed)
        {
            var barkMat = MatsuriMaterials.Wood(new Color(0.28f, 0.21f, 0.15f));
            var leafMat = MatsuriMaterials.Foliage(leafColor.maxColorComponent < 0.05f ? new Color(0.20f, 0.34f, 0.19f) : leafColor);

            float h = 5.2f * scale;
            var trunk = ArtParts.Empty("Trunk", root);
            ArtParts.Part("Bole", trunk.transform, MatsuriMeshes.Cone(0.30f * scale, h, 10), barkMat, new Vector3(0f, h * 0.5f, 0f));

            var branch = MatsuriMeshes.Cone(0.10f * scale, 1.8f * scale, 6);
            for (int i = 0; i < 4; i++)
            {
                float a = (seed * 37 + i * 90) % 360;
                var pivot = ArtParts.Empty("Branch" + i, trunk.transform,
                    new Vector3(0f, h * (0.48f + (i % 2) * 0.14f), 0f), Quaternion.Euler(0f, a, 34f + (i % 2) * 10f));
                ArtParts.Part("Limb", pivot.transform, branch, barkMat, new Vector3(0f, 0.9f * scale, 0f));
            }

            // 葉の塊
            var canopy = ArtParts.Empty("Canopy", root, new Vector3(0f, h * 0.86f, 0f));
            var leaves = ArtParts.Empty("Leaves", canopy.transform);
            var blob = MatsuriMeshes.Sphere(1.0f, 12, 8);
            for (int i = 0; i < 6; i++)
            {
                float a = (seed * 53 + i * 61) % 360 * Mathf.Deg2Rad;
                float r = (i == 0 ? 0f : 1.05f) * scale;
                var pos = new Vector3(Mathf.Cos(a) * r, (i == 0 ? 0.55f : (i % 3) * 0.34f - 0.1f) * scale, Mathf.Sin(a) * r);
                float s = (i == 0 ? 1.55f : 1.05f + (i % 3) * 0.16f) * scale;
                ArtParts.Part("Leaf" + i, leaves.transform, blob, leafMat, pos, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f),
                    new Vector3(s, s * 0.78f, s));
            }
            return canopy;
        }

        // ------------------------------------------------------------------ 電飾ライン

        static void BuildStallLight(Transform root, Color color)
        {
            var wood = MatsuriMaterials.Wood(DarkWood);
            const float span = 7.0f, top = 3.1f;

            var pole = ArtParts.Empty("Pole", root);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * span * 0.5f;
                ArtParts.Part("Base" + i, pole.transform, MatsuriMeshes.Box(new Vector3(0.5f, 0.12f, 0.5f)), MatsuriMaterials.Painted(Stone, 0.2f), new Vector3(x, 0.06f, 0f));
                ArtParts.Part("Shaft" + i, pole.transform, MatsuriMeshes.Cylinder(0.06f, top, 10), wood, new Vector3(x, top * 0.5f, 0f));
            }

            // たるんだコード（折れ線で近似）
            var cord = ArtParts.Empty("Cord", root);
            const int segs = 8;
            var cordMat = MatsuriMaterials.Painted(new Color(0.10f, 0.10f, 0.10f), 0.25f);
            float Sag(float t) => top - 0.55f * Mathf.Sin(t * Mathf.PI);
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                Vector3 p0 = new Vector3(Mathf.Lerp(-span * 0.5f, span * 0.5f, t0), Sag(t0), 0f);
                Vector3 p1 = new Vector3(Mathf.Lerp(-span * 0.5f, span * 0.5f, t1), Sag(t1), 0f);
                Vector3 dir = p1 - p0;
                ArtParts.NoShadow(ArtParts.Part("Seg" + i, cord.transform, MatsuriMeshes.Cylinder(0.010f, dir.magnitude, 5), cordMat,
                    (p0 + p1) * 0.5f, Quaternion.FromToRotation(Vector3.up, dir.normalized)));
            }

            // 連なる提灯
            var lanterns = ArtParts.Empty("Lantern", root);
            for (int i = 0; i < segs; i++)
            {
                float t = (i + 0.5f) / segs;
                var pos = new Vector3(Mathf.Lerp(-span * 0.5f, span * 0.5f, t), Sag(t) - 0.04f, 0f);
                BuildLantern(lanterns.transform, pos, color, 0.115f, 0.30f, 0.5f + i * 0.08f);
            }
        }

        // ------------------------------------------------------------------ 大看板

        static void BuildFestivalSign(Transform root, Color color)
        {
            var wood = MatsuriMaterials.Wood(new Color(0.36f, 0.24f, 0.16f));
            const float bw = 5.4f, bh = 1.6f, postH = 3.6f;

            var frame = ArtParts.Empty("Frame", root);
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * (bw * 0.5f - 0.18f);
                ArtParts.Part("Post" + i, frame.transform, MatsuriMeshes.Box(new Vector3(0.24f, postH, 0.24f)), wood, new Vector3(x, postH * 0.5f, 0f));
                ArtParts.Part("Foot" + i, frame.transform, MatsuriMeshes.Box(new Vector3(0.6f, 0.16f, 0.8f)), MatsuriMaterials.Painted(Stone, 0.2f), new Vector3(x, 0.08f, 0f));
            }

            var board = ArtParts.Empty("Board", root, new Vector3(0f, postH - bh * 0.5f - 0.25f, 0f));
            ArtParts.Part("Panel", board.transform, MatsuriMeshes.Box(new Vector3(bw, bh, 0.14f)),
                MatsuriMaterials.Wood(new Color(0.90f, 0.84f, 0.66f)), Vector3.zero);
            var tex = ProceduralTextures.KanjiSign("夏祭り", 1024, 320, new Color(0.92f, 0.86f, 0.68f), new Color(0.10f, 0.09f, 0.08f));
            ArtParts.NoShadow(ArtParts.Part("Face", board.transform, MatsuriMeshes.Quad(bw - 0.18f, bh - 0.18f),
                MatsuriMaterials.Printed(tex, Color.white), new Vector3(0f, 0f, 0.075f)));

            // 上の小屋根
            ArtParts.Part("Roof", root, MatsuriMeshes.GableRoof(bw + 0.4f, 0.7f, 0.34f, 0.26f),
                MatsuriMaterials.Painted(new Color(0.24f, 0.24f, 0.26f), 0.3f), new Vector3(0f, postH + 0.02f, 0f));

            var lanterns = ArtParts.Empty("Lantern", root);
            BuildLantern(lanterns.transform, new Vector3(-bw * 0.5f - 0.20f, postH - 0.10f, 0f), color, 0.20f, 0.52f, 0.52f);
            BuildLantern(lanterns.transform, new Vector3(bw * 0.5f + 0.20f, postH - 0.10f, 0f), color, 0.20f, 0.52f, 0.68f);
        }
    }
}

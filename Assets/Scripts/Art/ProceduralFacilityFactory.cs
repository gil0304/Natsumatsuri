using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// §20 の設備を手続き生成する。ベンチ・ゴミ箱・仮設トイレ・門・案内板。
    /// 門は入り口／出口で暖簾の色と文字を変える。
    /// </summary>
    public static class ProceduralFacilityFactory
    {
        static readonly Color Wood = new Color(0.44f, 0.31f, 0.19f);
        static readonly Color DarkWood = new Color(0.25f, 0.18f, 0.12f);
        static readonly Color Steel = new Color(0.55f, 0.57f, 0.60f);
        static readonly Color Vermilion = new Color(0.78f, 0.20f, 0.13f);

        public static GameObject Build(FacilityData data, Transform parent)
        {
            var kind = data != null ? data.Visual : FacilityVisualKind.Bench;
            string name = data != null && !string.IsNullOrEmpty(data.DisplayName) ? data.DisplayName : kind.ToString();
            var root = ArtParts.Empty(name, parent);

            switch (kind)
            {
                case FacilityVisualKind.TrashCan: BuildTrashCan(root.transform); break;
                case FacilityVisualKind.Toilet: BuildToilet(root.transform); break;
                case FacilityVisualKind.Gate: BuildGate(root.transform, data); break;
                case FacilityVisualKind.SignBoard: BuildSignBoard(root.transform); break;
                default: BuildBench(root.transform); break;
            }

            LodBuilder.AddLod(root, new[] { 0.28f, 0.07f, 0.010f });
            return root;
        }

        // ------------------------------------------------------------------ ベンチ

        static void BuildBench(Transform root)
        {
            var wood = MatsuriMaterials.Wood(Wood);
            var dark = MatsuriMaterials.Wood(DarkWood);
            const float w = 1.9f, d = 0.52f, seatH = 0.44f;

            var legs = ArtParts.Empty("Legs", root);
            var leg = MatsuriMeshes.Box(new Vector3(0.08f, seatH, 0.42f));
            ArtParts.Part("LegL", legs.transform, leg, dark, new Vector3(-w * 0.5f + 0.14f, seatH * 0.5f, 0f));
            ArtParts.Part("LegR", legs.transform, leg, dark, new Vector3(w * 0.5f - 0.14f, seatH * 0.5f, 0f));
            ArtParts.Part("Brace", legs.transform, MatsuriMeshes.Box(new Vector3(w - 0.4f, 0.06f, 0.06f)), dark, new Vector3(0f, 0.16f, 0f));

            var seat = ArtParts.Empty("Seat", root);
            var slat = MatsuriMeshes.Box(new Vector3(w, 0.055f, 0.145f));
            for (int i = 0; i < 3; i++)
                ArtParts.Part("Slat" + i, seat.transform, slat, wood, new Vector3(0f, seatH, -d * 0.5f + 0.09f + i * 0.17f));

            var back = ArtParts.Empty("Back", root);
            var post = MatsuriMeshes.Box(new Vector3(0.07f, 0.52f, 0.07f));
            ArtParts.Part("PostL", back.transform, post, dark, new Vector3(-w * 0.5f + 0.14f, seatH + 0.26f, -d * 0.5f + 0.06f), Quaternion.Euler(-8f, 0f, 0f));
            ArtParts.Part("PostR", back.transform, post, dark, new Vector3(w * 0.5f - 0.14f, seatH + 0.26f, -d * 0.5f + 0.06f), Quaternion.Euler(-8f, 0f, 0f));
            for (int i = 0; i < 2; i++)
                ArtParts.Part("BackSlat" + i, back.transform, MatsuriMeshes.Box(new Vector3(w, 0.11f, 0.05f)), wood,
                    new Vector3(0f, seatH + 0.26f + i * 0.17f, -d * 0.5f + 0.03f - i * 0.024f), Quaternion.Euler(-8f, 0f, 0f));
        }

        // ------------------------------------------------------------------ ゴミ箱

        static void BuildTrashCan(Transform root)
        {
            var body = MatsuriMaterials.Painted(new Color(0.20f, 0.36f, 0.28f), 0.35f);
            var metal = MatsuriMaterials.Metal(Steel);

            // 燃える／燃えないの2口
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -0.34f : 0.34f);
                var node = ArtParts.Empty("Can" + i, root, new Vector3(x, 0f, 0f));
                ArtParts.Part("Base", node.transform, MatsuriMeshes.Cylinder(0.30f, 0.78f, 16), body, new Vector3(0f, 0.39f, 0f));
                ArtParts.Part("Rim", node.transform, MatsuriMeshes.Torus(0.30f, 0.022f, 16, 6), metal, new Vector3(0f, 0.78f, 0f));
                ArtParts.Part("Lid", node.transform, MatsuriMeshes.Cone(0.31f, 0.14f, 16), metal, new Vector3(0f, 0.85f, 0f));
                // 投入口
                ArtParts.NoShadow(ArtParts.Part("Slot", node.transform, MatsuriMeshes.Box(new Vector3(0.20f, 0.02f, 0.13f)),
                    MatsuriMaterials.Painted(new Color(0.05f, 0.05f, 0.05f), 0.1f), new Vector3(0f, 0.90f, 0f)));
                // 色分けのラベル
                ArtParts.NoShadow(ArtParts.Part("Label", node.transform, MatsuriMeshes.Quad(0.34f, 0.20f),
                    MatsuriMaterials.Painted(i == 0 ? new Color(0.90f, 0.72f, 0.20f) : new Color(0.30f, 0.55f, 0.85f), 0.3f),
                    new Vector3(0f, 0.50f, 0.302f)));
            }
            var frame = ArtParts.Empty("Frame", root);
            ArtParts.Part("Rail", frame.transform, MatsuriMeshes.Cylinder(0.022f, 0.95f, 8), metal, new Vector3(0f, 0.72f, -0.30f), Quaternion.Euler(0f, 0f, 90f));
        }

        // ------------------------------------------------------------------ 仮設トイレ

        static void BuildToilet(Transform root)
        {
            var shell = MatsuriMaterials.Painted(new Color(0.82f, 0.84f, 0.86f), 0.4f);
            var accent = MatsuriMaterials.Painted(new Color(0.22f, 0.44f, 0.66f), 0.4f);
            const float w = 1.15f, d = 1.25f, h = 2.30f;

            var structure = ArtParts.Empty("Structure", root);
            ArtParts.Part("Body", structure.transform, MatsuriMeshes.Box(new Vector3(w, h, d)), shell, new Vector3(0f, h * 0.5f, 0f));
            ArtParts.Part("Base", structure.transform, MatsuriMeshes.Box(new Vector3(w + 0.10f, 0.10f, d + 0.10f)), accent, new Vector3(0f, 0.05f, 0f));
            ArtParts.Part("Roof", structure.transform, MatsuriMeshes.Box(new Vector3(w + 0.12f, 0.09f, d + 0.12f)), accent, new Vector3(0f, h + 0.04f, 0f));

            var door = ArtParts.Empty("Door", root, new Vector3(0f, 0f, d * 0.5f));
            ArtParts.Part("Panel", door.transform, MatsuriMeshes.Box(new Vector3(w * 0.72f, h * 0.82f, 0.06f)), accent, new Vector3(0f, h * 0.46f, 0.02f));
            ArtParts.NoShadow(ArtParts.Part("Handle", door.transform, MatsuriMeshes.Cylinder(0.022f, 0.14f, 6), MatsuriMaterials.Metal(Steel),
                new Vector3(w * 0.26f, h * 0.45f, 0.06f), Quaternion.Euler(90f, 0f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Vent", door.transform, MatsuriMeshes.Quad(0.26f, 0.10f),
                MatsuriMaterials.Painted(new Color(0.10f, 0.11f, 0.13f), 0.2f), new Vector3(0f, h * 0.74f, 0.052f)));
            // 使用中／空きの表示
            ArtParts.NoShadow(ArtParts.Part("Indicator", door.transform, MatsuriMeshes.Quad(0.10f, 0.06f),
                MatsuriMaterials.Painted(new Color(0.30f, 0.75f, 0.40f), 0.3f), new Vector3(w * 0.26f, h * 0.55f, 0.052f)));

            var col = structure.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);
        }

        // ------------------------------------------------------------------ 門

        static void BuildGate(Transform root, FacilityData data)
        {
            bool isExit = data != null && data.Effect == FacilityEffect.Exit;
            string label = isExit ? "出口" : "入口";
            Color clothColor = isExit ? new Color(0.16f, 0.28f, 0.52f) : Vermilion;

            var wood = MatsuriMaterials.Wood(DarkWood);
            const float span = 4.6f, h = 3.5f;

            var frame = ArtParts.Empty("Frame", root);
            var post = MatsuriMeshes.Box(new Vector3(0.22f, h, 0.22f));
            ArtParts.Part("PostL", frame.transform, post, wood, new Vector3(-span * 0.5f, h * 0.5f, 0f));
            ArtParts.Part("PostR", frame.transform, post, wood, new Vector3(span * 0.5f, h * 0.5f, 0f));
            ArtParts.Part("Lintel", frame.transform, MatsuriMeshes.Box(new Vector3(span + 0.8f, 0.26f, 0.30f)), wood, new Vector3(0f, h - 0.02f, 0f));
            ArtParts.Part("Brace", frame.transform, MatsuriMeshes.Box(new Vector3(span + 0.2f, 0.14f, 0.18f)), wood, new Vector3(0f, h - 0.42f, 0f));
            var foot = MatsuriMeshes.Box(new Vector3(0.6f, 0.14f, 0.6f));
            ArtParts.Part("FootL", frame.transform, foot, MatsuriMaterials.Painted(new Color(0.5f, 0.49f, 0.46f), 0.2f), new Vector3(-span * 0.5f, 0.07f, 0f));
            ArtParts.Part("FootR", frame.transform, foot, MatsuriMaterials.Painted(new Color(0.5f, 0.49f, 0.46f), 0.2f), new Vector3(span * 0.5f, 0.07f, 0f));

            // 小屋根
            ArtParts.Part("Roof", root, MatsuriMeshes.GableRoof(span + 0.9f, 0.8f, 0.42f, 0.34f),
                MatsuriMaterials.Painted(new Color(0.22f, 0.22f, 0.25f), 0.3f), new Vector3(0f, h + 0.12f, 0f));

            // 暖簾（切れ込み4本・揺れる）
            var noren = ArtParts.Empty("Noren", root, new Vector3(0f, h - 0.60f, 0.16f));
            var tex = ProceduralTextures.KanjiSign(label, 512, 256, clothColor, new Color(0.97f, 0.95f, 0.90f));
            var mat = MatsuriMaterials.Printed(tex, Color.white);
            const int strips = 5;
            float total = span - 0.30f;
            float stripW = (total - 0.02f * (strips - 1)) / strips;
            var mesh = MatsuriMeshes.ClothStrip(stripW, 0.70f, 4, 6);
            for (int i = 0; i < strips; i++)
            {
                float x = -total * 0.5f + stripW * 0.5f + i * (stripW + 0.02f);
                var go = ArtParts.Part("Strip" + i.ToString("00"), noren.transform, mesh, mat, new Vector3(x, 0f, 0f));
                ArtParts.NoShadow(go);
                ArtParts.SetTextureOffset(go.GetComponent<MeshRenderer>(), new Vector2(1f / strips, 1f), new Vector2(i / (float)strips, 0f));
                SwayAnimator.Attach(go, SwayMode.Cloth, 0.04f, 0.85f + i * 0.06f);
            }

            // 提灯
            var lanterns = ArtParts.Empty("Lantern", root);
            ProceduralDecorationFactory.BuildLantern(lanterns.transform, new Vector3(-span * 0.5f + 0.05f, h - 0.20f, 0.30f), Vermilion, 0.22f, 0.56f, 0.55f);
            ProceduralDecorationFactory.BuildLantern(lanterns.transform, new Vector3(span * 0.5f - 0.05f, h - 0.20f, 0.30f), Vermilion, 0.22f, 0.56f, 0.70f);
            ProceduralDecorationFactory.AttachLight(root, new Color(1f, 0.72f, 0.45f), 700f, 12f, h - 0.4f);
        }

        // ------------------------------------------------------------------ 案内板

        static void BuildSignBoard(Transform root)
        {
            var wood = MatsuriMaterials.Wood(Wood);
            const float bw = 1.9f, bh = 1.25f, postH = 2.2f;

            var frame = ArtParts.Empty("Frame", root);
            var post = MatsuriMeshes.Box(new Vector3(0.12f, postH, 0.12f));
            ArtParts.Part("PostL", frame.transform, post, MatsuriMaterials.Wood(DarkWood), new Vector3(-bw * 0.5f + 0.16f, postH * 0.5f, 0f));
            ArtParts.Part("PostR", frame.transform, post, MatsuriMaterials.Wood(DarkWood), new Vector3(bw * 0.5f - 0.16f, postH * 0.5f, 0f));

            var board = ArtParts.Empty("Board", root, new Vector3(0f, postH - bh * 0.5f - 0.12f, 0f), Quaternion.Euler(-9f, 0f, 0f));
            ArtParts.Part("Panel", board.transform, MatsuriMeshes.Box(new Vector3(bw, bh, 0.09f)), wood, Vector3.zero);
            var map = ProceduralTextures.MapBoard(512, 340, new Color(0.93f, 0.89f, 0.78f), new Color(0.28f, 0.22f, 0.16f), 7);
            ArtParts.NoShadow(ArtParts.Part("Face", board.transform, MatsuriMeshes.Quad(bw - 0.12f, bh - 0.12f),
                MatsuriMaterials.Printed(map, Color.white), new Vector3(0f, 0f, 0.05f)));

            // 小屋根と見出し
            ArtParts.Part("Roof", root, MatsuriMeshes.GableRoof(bw + 0.24f, 0.5f, 0.24f, 0.20f),
                MatsuriMaterials.Painted(new Color(0.24f, 0.24f, 0.26f), 0.3f), new Vector3(0f, postH + 0.04f, 0f));
            var title = ProceduralTextures.KanjiSign("案内", 256, 128, new Color(0.30f, 0.24f, 0.18f), new Color(0.96f, 0.92f, 0.80f));
            ArtParts.NoShadow(ArtParts.Part("Title", root, MatsuriMeshes.Quad(0.62f, 0.24f),
                MatsuriMaterials.Printed(title, Color.white), new Vector3(0f, postH - 0.06f, 0.14f)));
        }
    }
}

using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// <see cref="ProceduralAmenityFactory"/> のうち、参拝まわりの見た目 (§79)。
    /// 神社（社殿・賽銭箱・鈴・狛犬・灯籠）と手水舎（水盤・柄杓・水面）。
    ///
    /// どちらも +Z を正面とする。NPC の立ち位置 (AmenitySlotLayout) も +Z 側に並ぶので、
    /// 参拝者は社殿を正面から見上げる形になる。
    /// </summary>
    public static partial class ProceduralAmenityFactory
    {
        // ══════════════════════════════════════════════════════════════════
        // 神社 — 石段 + 社殿 + 賽銭箱 + 鈴と鈴緒 + 狛犬2体 + 灯籠2基
        // ══════════════════════════════════════════════════════════════════

        const float HallWidth = 5.0f;
        const float HallDepth = 4.0f;
        const float HallWallHeight = 2.7f;
        const float DeckY = 0.86f;

        static void BuildShrineGround(Transform root, int capacity)
        {
            BuildShrineApproach(root, capacity);
            BuildShrineHall(root);
            BuildOfferingBox(root);
            BuildSuzu(root);
            BuildKomainu(root);
            BuildStoneLanterns(root);

            // 実光源は軒下に1つだけ (§58)。
            ProceduralDecorationFactory.AttachLight(root, new Color(1f, 0.80f, 0.55f), 1500f, 14f, DeckY + HallWallHeight - 0.3f);
        }

        /// <summary>参道の砂利と石段。人が並ぶ側の地面を作る。</summary>
        static void BuildShrineApproach(Transform root, int capacity)
        {
            var node = ArtParts.Empty("Base", root);
            var stone = MatsuriMaterials.Painted(Stone, 0.16f);

            // 参道。奥行きは待機列の長さに合わせて伸ばす。
            float pathLength = 5.0f + Mathf.Max(0, capacity - 4) * 0.45f;
            ArtParts.NoShadow(ArtParts.Part("Path", node.transform, MatsuriMeshes.Plane(4.2f, pathLength),
                MatsuriMaterials.Ground(new Color(0.52f, 0.50f, 0.46f)),
                new Vector3(0f, 0.015f, HallDepth * 0.5f + 1.2f + pathLength * 0.5f)));

            // 土台（basement）と縁の石。
            ArtParts.Part("Platform", node.transform,
                MatsuriMeshes.Box(new Vector3(HallWidth + 1.5f, 0.60f, HallDepth + 1.3f)), stone, new Vector3(0f, 0.30f, 0f));
            ArtParts.Part("Deck", node.transform,
                MatsuriMeshes.Box(new Vector3(HallWidth + 0.9f, 0.26f, HallDepth + 0.8f)),
                MatsuriMaterials.Wood(DarkWood), new Vector3(0f, DeckY - 0.13f, 0f));

            // 石段4段。
            var steps = ArtParts.Empty("Steps", root);
            for (int i = 0; i < 4; i++)
                ArtParts.Part($"Step{i + 1:00}", steps.transform,
                    MatsuriMeshes.Box(new Vector3(2.8f, 0.20f, 0.36f)), stone,
                    new Vector3(0f, 0.10f + i * 0.19f, HallDepth * 0.5f + 1.62f - i * 0.36f));
        }

        /// <summary>社殿。柱・板壁・扉・入母屋屋根。</summary>
        static void BuildShrineHall(Transform root)
        {
            var hall = ArtParts.Empty("Hall", root);
            var paint = MatsuriMaterials.Painted(Vermilion, 0.30f);
            var plank = MatsuriMaterials.Wood(new Color(0.36f, 0.24f, 0.16f));
            float midY = DeckY + HallWallHeight * 0.5f;

            // 柱6本（正面3・背面3）。
            var pillar = MatsuriMeshes.Cylinder(0.17f, HallWallHeight, 10);
            for (int i = 0; i < 6; i++)
            {
                float x = -HallWidth * 0.5f + (i % 3) * (HallWidth * 0.5f);
                float z = (i < 3 ? -1f : 1f) * HallDepth * 0.5f;
                ArtParts.Part($"Pillar{i + 1:00}", hall.transform, pillar, paint, new Vector3(x, midY, z));
            }

            ArtParts.Part("BackWall", hall.transform, MatsuriMeshes.Box(new Vector3(HallWidth, HallWallHeight, 0.16f)),
                plank, new Vector3(0f, midY, -HallDepth * 0.5f));
            ArtParts.Part("SideWallL", hall.transform, MatsuriMeshes.Box(new Vector3(0.16f, HallWallHeight, HallDepth)),
                plank, new Vector3(-HallWidth * 0.5f, midY, 0f));
            ArtParts.Part("SideWallR", hall.transform, MatsuriMeshes.Box(new Vector3(0.16f, HallWallHeight, HallDepth)),
                plank, new Vector3(HallWidth * 0.5f, midY, 0f));
            ArtParts.Part("Lintel", hall.transform, MatsuriMeshes.Box(new Vector3(HallWidth, 0.46f, 0.20f)),
                paint, new Vector3(0f, DeckY + HallWallHeight - 0.23f, HallDepth * 0.5f));

            // 正面の扉（格子戸）。閉まっている。
            var door = ArtParts.Empty("Door", root, new Vector3(0f, DeckY, HallDepth * 0.5f - 0.02f));
            var doorMat = MatsuriMaterials.Wood(new Color(0.30f, 0.20f, 0.14f));
            for (int i = 0; i < 2; i++)
            {
                float x = (i == 0 ? -1f : 1f) * 0.78f;
                ArtParts.Part($"Panel{i + 1:00}", door.transform,
                    MatsuriMeshes.Box(new Vector3(1.52f, HallWallHeight - 0.6f, 0.07f)), doorMat,
                    new Vector3(x, (HallWallHeight - 0.6f) * 0.5f, 0f));
                // 格子
                for (int k = 0; k < 5; k++)
                    ArtParts.NoShadow(ArtParts.Part($"Lattice{i}{k}", door.transform,
                        MatsuriMeshes.Box(new Vector3(0.05f, HallWallHeight - 0.7f, 0.05f)), paint,
                        new Vector3(x - 0.6f + k * 0.3f, (HallWallHeight - 0.6f) * 0.5f, 0.05f)));
            }

            // 入母屋屋根：裾の四方流れ（台形）＋上の切妻。
            var roof = ArtParts.Empty("Roof", root, new Vector3(0f, DeckY + HallWallHeight, 0f));
            var roofMat = MatsuriMaterials.Painted(RoofSlate, 0.26f);
            ArtParts.Part("Skirt", roof.transform,
                MatsuriMeshes.TaperedBox(HallWidth + 2.8f, HallDepth + 2.4f, HallWidth * 0.62f, HallDepth * 0.56f, 1.05f),
                roofMat, new Vector3(0f, 0.52f, 0f));
            ArtParts.Part("Gable", roof.transform,
                MatsuriMeshes.GableRoof(HallWidth * 0.64f, HallDepth * 0.58f, 1.0f, 0.55f), roofMat, new Vector3(0f, 1.02f, 0f));
            ArtParts.Part("Ridge", roof.transform, MatsuriMeshes.Box(new Vector3(HallWidth * 0.68f, 0.18f, 0.30f)),
                paint, new Vector3(0f, 2.08f, 0f));

            // 千木（屋根の上のＸ）。神社らしさはここで決まる。
            var gold = MatsuriMaterials.Metal(new Color(0.70f, 0.58f, 0.26f));
            var chigi = MatsuriMeshes.Box(new Vector3(0.11f, 1.25f, 0.11f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * HallWidth * 0.30f;
                float z = (i < 2 ? -1f : 1f) * HallDepth * 0.24f;
                ArtParts.Part($"Chigi{i + 1:00}", roof.transform, chigi, gold,
                    new Vector3(x, 2.45f, z), Quaternion.Euler(0f, 0f, x < 0f ? 22f : -22f));
            }
            var katsuogi = MatsuriMeshes.Cylinder(0.10f, 0.95f, 8);
            for (int i = 0; i < 3; i++)
                ArtParts.NoShadow(ArtParts.Part($"Katsuogi{i + 1:00}", roof.transform, katsuogi, gold,
                    new Vector3(0f, 2.24f, -0.7f + i * 0.7f), Quaternion.Euler(0f, 0f, 90f)));
        }

        /// <summary>賽銭箱。石段の上、扉の手前。</summary>
        static void BuildOfferingBox(Transform root)
        {
            var box = ArtParts.Empty("Prop", root, new Vector3(0f, DeckY, HallDepth * 0.5f + 0.30f));
            var wood = MatsuriMaterials.Wood(new Color(0.30f, 0.21f, 0.14f));

            ArtParts.Part("Body", box.transform, MatsuriMeshes.Box(new Vector3(1.7f, 0.66f, 0.78f)), wood, new Vector3(0f, 0.33f, 0f));
            ArtParts.Part("Foot", box.transform, MatsuriMeshes.Box(new Vector3(1.8f, 0.09f, 0.88f)),
                MatsuriMaterials.Wood(DarkWood), new Vector3(0f, 0.045f, 0f));

            // 上面の格子。小銭を落とす隙間。
            var slat = MatsuriMeshes.Box(new Vector3(1.60f, 0.055f, 0.055f));
            for (int i = 0; i < 8; i++)
                ArtParts.NoShadow(ArtParts.Part($"Slat{i:00}", box.transform, slat, MatsuriMaterials.Wood(DarkWood),
                    new Vector3(0f, 0.69f, -0.32f + i * 0.09f)));
        }

        /// <summary>鈴と鈴緒。軒から吊るす。参拝の合図。</summary>
        static void BuildSuzu(Transform root)
        {
            float eaveY = DeckY + HallWallHeight - 0.10f;
            var node = ArtParts.Empty("Prop2", root, new Vector3(0f, eaveY, HallDepth * 0.5f + 0.34f));

            // 鈴本体
            ArtParts.Part("Bell", node.transform, MatsuriMeshes.Sphere(0.28f, 14, 9),
                MatsuriMaterials.Metal(new Color(0.62f, 0.55f, 0.32f)), new Vector3(0f, -0.30f, 0f));
            ArtParts.NoShadow(ArtParts.Part("BellMouth", node.transform, MatsuriMeshes.Torus(0.24f, 0.045f, 14, 6),
                MatsuriMaterials.Metal(new Color(0.48f, 0.42f, 0.24f)), new Vector3(0f, -0.50f, 0f)));

            // 鈴緒（紅白のより紐）。揺れる。
            var rope = ArtParts.Empty("Suzuo", node.transform, new Vector3(0f, -0.55f, 0f));
            SwayAnimator.Attach(rope, SwayMode.Rotate, 3.2f, 0.55f).Axis = Vector3.forward;
            var red = MatsuriMaterials.Fabric(new Color(0.78f, 0.16f, 0.15f));
            var white = MatsuriMaterials.Fabric(new Color(0.95f, 0.93f, 0.88f));
            var seg = MatsuriMeshes.Cylinder(0.075f, 0.24f, 8);
            for (int i = 0; i < 7; i++)
                ArtParts.NoShadow(ArtParts.Part($"Seg{i:00}", rope.transform, seg, (i % 2 == 0) ? red : white,
                    new Vector3(0f, -0.12f - i * 0.24f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Tassel", rope.transform, MatsuriMeshes.Cone(0.11f, 0.26f, 8), red,
                new Vector3(0f, -1.86f, 0f), Quaternion.Euler(180f, 0f, 0f)));
        }

        /// <summary>狛犬2体。台座の上に、参道を挟んで向かい合う。</summary>
        static void BuildKomainu(Transform root)
        {
            var node = ArtParts.Empty("Prop3", root);
            var stone = MatsuriMaterials.Painted(new Color(0.62f, 0.61f, 0.57f), 0.14f);
            var dark = MatsuriMaterials.Painted(new Color(0.50f, 0.49f, 0.46f), 0.14f);

            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var one = ArtParts.Empty($"Komainu{side + 1:00}", node.transform,
                    new Vector3(sx * 3.1f, 0f, HallDepth * 0.5f + 2.6f),
                    Quaternion.Euler(0f, sx > 0f ? -105f : 105f, 0f));

                // 台座
                ArtParts.Part("Pedestal", one.transform, MatsuriMeshes.Box(new Vector3(0.78f, 0.90f, 0.78f)), dark, new Vector3(0f, 0.45f, 0f));
                ArtParts.Part("PedestalCap", one.transform, MatsuriMeshes.Box(new Vector3(0.92f, 0.10f, 0.92f)), stone, new Vector3(0f, 0.95f, 0f));

                // 体・胸・頭・耳・尾・前脚。獅子らしいシルエットを作る。
                ArtParts.Part("Body", one.transform, MatsuriMeshes.Box(new Vector3(0.42f, 0.46f, 0.80f)), stone, new Vector3(0f, 1.30f, -0.06f));
                ArtParts.Part("Chest", one.transform, MatsuriMeshes.Sphere(0.27f, 12, 8), stone, new Vector3(0f, 1.42f, 0.30f));
                ArtParts.Part("Head", one.transform, MatsuriMeshes.Sphere(0.26f, 12, 8), stone, new Vector3(0f, 1.82f, 0.30f));
                ArtParts.NoShadow(ArtParts.Part("Muzzle", one.transform, MatsuriMeshes.Box(new Vector3(0.20f, 0.16f, 0.20f)), stone, new Vector3(0f, 1.76f, 0.50f)));
                for (int e = 0; e < 2; e++)
                    ArtParts.NoShadow(ArtParts.Part($"Ear{e}", one.transform, MatsuriMeshes.Cone(0.09f, 0.16f, 6), stone,
                        new Vector3((e == 0 ? -1f : 1f) * 0.17f, 2.02f, 0.26f)));
                for (int l = 0; l < 2; l++)
                    ArtParts.Part($"Leg{l}", one.transform, MatsuriMeshes.Box(new Vector3(0.14f, 0.55f, 0.16f)), stone,
                        new Vector3((l == 0 ? -1f : 1f) * 0.17f, 1.23f, 0.36f));
                ArtParts.NoShadow(ArtParts.Part("Tail", one.transform, MatsuriMeshes.Cone(0.16f, 0.52f, 8), stone,
                    new Vector3(0f, 1.62f, -0.40f), Quaternion.Euler(-28f, 0f, 0f)));

                // 阿形は口に玉、吽形は足元に子。左右で違えて「2体並べただけ」に見せない (§79)。
                if (side == 0)
                    ArtParts.NoShadow(ArtParts.Part("Tama", one.transform, MatsuriMeshes.Sphere(0.13f, 10, 7), dark, new Vector3(0f, 1.12f, 0.44f)));
                else
                    ArtParts.NoShadow(ArtParts.Part("Kohaku", one.transform, MatsuriMeshes.Sphere(0.15f, 10, 7), dark, new Vector3(0.16f, 1.10f, 0.30f)));
            }
        }

        /// <summary>石灯籠2基。参道の入口側に置く。</summary>
        static void BuildStoneLanterns(Transform root)
        {
            var node = ArtParts.Empty("Lantern", root);
            var stone = MatsuriMaterials.Painted(new Color(0.60f, 0.59f, 0.55f), 0.14f);
            var fire = MatsuriMaterials.GlowingPaper(new Color(1f, 0.80f, 0.48f), 4.5f);

            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                var one = ArtParts.Empty($"Toro{side + 1:00}", node.transform,
                    new Vector3(sx * 4.2f, 0f, HallDepth * 0.5f + 4.4f));

                ArtParts.Part("Base", one.transform, MatsuriMeshes.Cylinder(0.36f, 0.24f, 10), stone, new Vector3(0f, 0.12f, 0f));
                ArtParts.Part("Shaft", one.transform, MatsuriMeshes.Cylinder(0.16f, 1.15f, 10), stone, new Vector3(0f, 0.82f, 0f));
                ArtParts.Part("Platform", one.transform, MatsuriMeshes.Cylinder(0.34f, 0.14f, 10), stone, new Vector3(0f, 1.46f, 0f));
                // 火袋
                ArtParts.Part("FireBox", one.transform, MatsuriMeshes.Box(new Vector3(0.46f, 0.44f, 0.46f)), stone, new Vector3(0f, 1.75f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Fire", one.transform, MatsuriMeshes.Sphere(0.15f, 10, 7), fire, new Vector3(0f, 1.75f, 0f)));
                // 笠と宝珠
                ArtParts.Part("Cap", one.transform, MatsuriMeshes.Cone(0.44f, 0.30f, 8), stone, new Vector3(0f, 2.12f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Hoju", one.transform, MatsuriMeshes.Sphere(0.09f, 8, 6), stone, new Vector3(0f, 2.32f, 0f)));
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // 手水舎 — 四本柱 + 小さい屋根 + 石の水盤 + 柄杓 + 水面
        // ══════════════════════════════════════════════════════════════════

        static void BuildTemizuya(Transform root, int capacity)
        {
            const float half = 1.55f;
            const float postH = 2.25f;
            const float basinHalf = 0.80f;
            const float basinTop = 0.86f;

            var wood = MatsuriMaterials.Wood(Wood);
            var dark = MatsuriMaterials.Wood(DarkWood);
            var stone = MatsuriMaterials.Painted(new Color(0.50f, 0.51f, 0.49f), 0.20f);

            // ---- 四本柱と貫 ----
            var frame = ArtParts.Empty("Frame", root);
            var post = MatsuriMeshes.Box(new Vector3(0.15f, postH, 0.15f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0) ? -1f : 1f;
                float sz = (i < 2) ? -1f : 1f;
                ArtParts.Part($"Post{i + 1:00}", frame.transform, post, wood, new Vector3(sx * half, postH * 0.5f, sz * half));
                ArtParts.NoShadow(ArtParts.Part($"Footing{i + 1:00}", frame.transform,
                    MatsuriMeshes.Cylinder(0.16f, 0.12f, 8), stone, new Vector3(sx * half, 0.06f, sz * half)));
            }
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                ArtParts.Part($"Beam{i + 1:00}", frame.transform,
                    MatsuriMeshes.Box(new Vector3(half * 2f, 0.12f, 0.10f)), dark,
                    yaw * new Vector3(0f, postH - 0.10f, half), yaw);
            }

            // ---- 小さい屋根 ----
            ArtParts.Part("Roof", root, MatsuriMeshes.GableRoof(half * 2.4f, half * 2.4f, 0.62f, 0.40f),
                MatsuriMaterials.Painted(RoofSlate, 0.26f), new Vector3(0f, postH, 0f));

            // ---- 石の水盤 ----
            var tub = ArtParts.Empty("Tub", root);
            ArtParts.Part("Pedestal", tub.transform, MatsuriMeshes.Box(new Vector3(basinHalf * 1.3f, 0.40f, basinHalf * 1.3f)),
                MatsuriMaterials.Painted(new Color(0.44f, 0.45f, 0.43f), 0.16f), new Vector3(0f, 0.20f, 0f));
            ArtParts.Part("Basin", tub.transform, MatsuriMeshes.Box(new Vector3(basinHalf * 2f, 0.48f, basinHalf * 2f)),
                stone, new Vector3(0f, 0.40f + 0.24f, 0f));
            // 縁を4枚の板で作り、内側をくぼませて見せる。
            var rim = MatsuriMeshes.Box(new Vector3(basinHalf * 2f, 0.14f, 0.14f));
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                ArtParts.Part($"Rim{i + 1:00}", tub.transform, rim, stone,
                    yaw * new Vector3(0f, basinTop - 0.03f, basinHalf - 0.07f), yaw);
            }

            // ---- 水面 (§62 と同じ水のマテリアル) ----
            var water = ArtParts.NoShadow(ArtParts.Part("Water", tub.transform,
                MatsuriMeshes.Plane(basinHalf * 1.86f, basinHalf * 1.86f), MatsuriMaterials.Water(),
                new Vector3(0f, basinTop - 0.09f, 0f)));
            SwayAnimator.Attach(water, SwayMode.WaterScroll, 0.05f, 0.35f);

            // ---- 竹の樋（水の出口） ----
            var prop = ArtParts.Empty("Prop", root);
            var bamboo = MatsuriMaterials.Painted(new Color(0.52f, 0.58f, 0.32f), 0.34f);
            ArtParts.Part("SpoutPost", prop.transform, MatsuriMeshes.Cylinder(0.055f, 1.05f, 8), bamboo,
                new Vector3(0f, 0.52f, -basinHalf - 0.16f));
            ArtParts.Part("Spout", prop.transform, MatsuriMeshes.Cylinder(0.048f, 0.62f, 8), bamboo,
                new Vector3(0f, 1.02f, -basinHalf + 0.16f), Quaternion.Euler(78f, 0f, 0f));

            // ---- 柄杓。水盤を渡した竹の上に並べる ----
            ArtParts.Part("LadleRack", prop.transform, MatsuriMeshes.Cylinder(0.035f, basinHalf * 2f, 6), bamboo,
                new Vector3(0f, basinTop + 0.06f, 0f), Quaternion.Euler(0f, 0f, 90f));

            int ladles = Mathf.Clamp(capacity, 2, 4);
            var handle = MatsuriMeshes.Cylinder(0.022f, 0.52f, 6);
            var cup = MatsuriMeshes.Cylinder(0.075f, 0.09f, 10);
            for (int i = 0; i < ladles; i++)
            {
                float z = ladles <= 1 ? 0f : -basinHalf * 0.6f + basinHalf * 1.2f * i / (ladles - 1);
                var one = ArtParts.Empty($"Ladle{i + 1:00}", prop.transform, new Vector3(0f, basinTop + 0.11f, z));
                ArtParts.NoShadow(ArtParts.Part("Handle", one.transform, handle, bamboo, new Vector3(0.18f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f)));
                ArtParts.NoShadow(ArtParts.Part("Cup", one.transform, cup, bamboo, new Vector3(-0.16f, -0.02f, 0f)));
            }

            // 提灯を1つだけ。夜に手元が見える (§59)。
            var lantern = ArtParts.Empty("Lantern", root);
            ProceduralDecorationFactory.BuildLantern(lantern.transform, new Vector3(half - 0.05f, postH - 0.18f, half - 0.05f),
                LanternIvory, 0.17f, 0.42f, 0.72f);
            ProceduralDecorationFactory.AttachLight(root, new Color(0.92f, 0.86f, 0.70f), 500f, 6.5f, postH - 0.4f);
        }
    }
}

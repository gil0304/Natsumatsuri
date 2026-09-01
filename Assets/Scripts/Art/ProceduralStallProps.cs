using System.Collections.Generic;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// §23「屋台の中身」。StallPropKind ごとに、作業台の上に置く道具一式を組み立てる。
    /// 原点は作業面の中央、Y=0 が作業面の高さ、+Z がお客側。
    /// </summary>
    public static class ProceduralStallProps
    {
        static readonly Dictionary<string, Mesh> s_Combined = new Dictionary<string, Mesh>(32);

        /// <summary>同じ形の繰り返し（たこ焼きの窪み・網など）は1メッシュにまとめて共有する。</summary>
        static Mesh Cluster(string key, System.Func<List<CombineInstance>> build)
        {
            if (s_Combined.TryGetValue(key, out var m) && m != null) return m;
            m = MatsuriMeshes.Combine(key, build());
            s_Combined[key] = m;
            return m;
        }

        public static void ClearCache()
        {
            foreach (var kv in s_Combined)
            {
                if (kv.Value == null) continue;
                if (Application.isPlaying) Object.Destroy(kv.Value);
                else Object.DestroyImmediate(kv.Value);
            }
            s_Combined.Clear();
        }

        static CombineInstance CI(Mesh mesh, Vector3 pos) => new CombineInstance { mesh = mesh, transform = Matrix4x4.Translate(pos) };
        static CombineInstance CI(Mesh mesh, Vector3 pos, Quaternion rot) => new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, Vector3.one) };
        static CombineInstance CI(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale) => new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) };

        static readonly Color MetalDark = new Color(0.18f, 0.18f, 0.20f);
        static readonly Color MetalBright = new Color(0.72f, 0.74f, 0.78f);
        static readonly Color WoodLight = new Color(0.62f, 0.47f, 0.30f);

        /// <summary>屋台の中身を組み立てる。GameObject 名は StallPropKind の名前になる。</summary>
        public static GameObject Build(StallPropKind kind, StallVisualRecipe recipe, Transform parent)
            => Build(kind, recipe, parent, recipe != null ? recipe.CounterHeight : 1.0f);

        /// <summary>
        /// surfaceY は作業面（カウンター天板）の高さ。
        /// 調理器具はその上に、水槽や景品棚は地面に置く。
        /// </summary>
        public static GameObject Build(StallPropKind kind, StallVisualRecipe recipe, Transform parent, float surfaceY)
        {
            var root = ArtParts.Empty(kind == StallPropKind.None ? "Prop" : kind.ToString(), parent);
            var surface = ArtParts.Empty("Surface", root.transform, new Vector3(0f, surfaceY, 0f)).transform;
            var ground = root.transform;

            Color product = recipe != null ? recipe.ProductColor : new Color(0.78f, 0.46f, 0.20f);
            float width = recipe != null ? recipe.Width : 3.2f;

            switch (kind)
            {
                case StallPropKind.TakoyakiPlate: BuildTakoyaki(surface, product); break;
                case StallPropKind.Teppan: BuildTeppan(surface, product); break;
                case StallPropKind.IceShaver: BuildIceShaver(surface, product); break;
                case StallPropKind.CandyAppleRack: BuildCandyApple(surface, product); break;
                case StallPropKind.CottonCandyMachine: BuildCottonCandy(surface, product); break;
                case StallPropKind.Grill: BuildGrill(surface, product); break;
                case StallPropKind.FishTank: BuildFishTank(ground, surface, product, width); break;
                case StallPropKind.ShootingRack: BuildShootingRack(ground, surface, product, width); break;
                case StallPropKind.YoyoTub: BuildYoyoTub(ground, surface, product, width); break;
                case StallPropKind.BallTub: BuildBallTub(ground, surface, product, width); break;
                case StallPropKind.KatanukiDesk: BuildKatanuki(surface, product); break;
                default: BuildGenericBoxes(surface, product); break;
            }
            return root;
        }

        // ------------------------------------------------------------------ たこ焼き

        static void BuildTakoyaki(Transform root, Color product)
        {
            var metal = MatsuriMaterials.Metal(MetalDark);
            var plate = MatsuriMeshes.Box(new Vector3(0.86f, 0.055f, 0.54f));
            ArtParts.Part("Plate", root, plate, metal, new Vector3(0f, 0.028f, 0f));

            // 窪みの縁をまとめて1メッシュに
            var dimples = Cluster("takoyaki_dimples", () =>
            {
                var list = new List<CombineInstance>(24);
                var ring = MatsuriMeshes.Torus(0.034f, 0.008f, 12, 6);
                for (int x = 0; x < 5; x++)
                    for (int z = 0; z < 3; z++)
                        list.Add(CI(ring, new Vector3(-0.32f + x * 0.16f, 0f, -0.16f + z * 0.16f)));
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Dimples", root, dimples, MatsuriMaterials.Metal(MetalDark), new Vector3(0f, 0.056f, 0f)));

            // 焼けているたこ焼き玉
            var ball = MatsuriMeshes.Sphere(0.031f, 10, 6);
            var ballMat = MatsuriMaterials.Painted(product, 0.35f);
            int index = 0;
            for (int x = 0; x < 5; x++)
                for (int z = 0; z < 3; z++)
                {
                    index++;
                    if (index % 4 == 0) continue;   // 焼き途中の空きを作る
                    var go = ArtParts.Part("Ball" + index, root, ball, ballMat,
                        new Vector3(-0.32f + x * 0.16f, 0.072f, -0.16f + z * 0.16f));
                    ArtParts.NoShadow(go);
                }

            // 千枚通し2本
            var pick = MatsuriMeshes.Cylinder(0.006f, 0.26f, 6);
            var steel = MatsuriMaterials.Metal(MetalBright);
            ArtParts.NoShadow(ArtParts.Part("Pick1", root, pick, steel, new Vector3(0.40f, 0.10f, 0.16f), Quaternion.Euler(62f, 20f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Pick2", root, pick, steel, new Vector3(0.44f, 0.10f, 0.10f), Quaternion.Euler(58f, 34f, 0f)));
        }

        // ------------------------------------------------------------------ 鉄板（焼きそば）

        static void BuildTeppan(Transform root, Color product)
        {
            var metal = MatsuriMaterials.Metal(MetalDark);
            ArtParts.Part("Plate", root, MatsuriMeshes.Box(new Vector3(1.15f, 0.05f, 0.62f)), metal, new Vector3(0f, 0.025f, 0f));
            // 縁の立ち上がり
            var rim = MatsuriMeshes.Box(new Vector3(1.15f, 0.05f, 0.03f));
            ArtParts.Part("RimBack", root, rim, metal, new Vector3(0f, 0.07f, -0.30f));
            ArtParts.Part("RimFront", root, rim, metal, new Vector3(0f, 0.07f, 0.30f));

            // 麺の山（つぶしたカプセルを重ねる）
            var noodle = MatsuriMeshes.Capsule(0.09f, 0.30f, 10);
            var noodleMat = MatsuriMaterials.Painted(product, 0.30f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 41f;
                var go = ArtParts.Part("Noodle" + i, root, noodle, noodleMat,
                    new Vector3(-0.28f + i * 0.13f, 0.075f + (i % 2) * 0.03f, (i % 3 - 1) * 0.07f),
                    Quaternion.Euler(90f, a, 0f), new Vector3(1f, 1f, 0.55f));
                ArtParts.NoShadow(go);
            }
            // キャベツの緑を少し
            var chip = MatsuriMeshes.Box(new Vector3(0.035f, 0.006f, 0.035f));
            var green = MatsuriMaterials.Painted(new Color(0.55f, 0.72f, 0.38f), 0.25f);
            for (int i = 0; i < 8; i++)
                ArtParts.NoShadow(ArtParts.Part("Cabbage" + i, root, chip, green,
                    new Vector3(-0.30f + i * 0.085f, 0.11f, ((i * 37) % 5 - 2) * 0.04f),
                    Quaternion.Euler(0f, i * 33f, 12f)));

            // ヘラ2枚
            var blade = MatsuriMeshes.Box(new Vector3(0.10f, 0.006f, 0.13f));
            var handle = MatsuriMeshes.Cylinder(0.010f, 0.16f, 6);
            var steel = MatsuriMaterials.Metal(MetalBright);
            var wood = MatsuriMaterials.Wood(WoodLight);
            for (int i = 0; i < 2; i++)
            {
                var pivot = ArtParts.Empty("Spatula" + i, root,
                    new Vector3(0.34f + i * 0.10f, 0.09f, 0.14f - i * 0.12f), Quaternion.Euler(0f, 24f + i * 30f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Blade", pivot.transform, blade, steel, Vector3.zero));
                ArtParts.NoShadow(ArtParts.Part("Handle", pivot.transform, handle, wood, new Vector3(0f, 0.02f, -0.14f), Quaternion.Euler(74f, 0f, 0f)));
            }
        }

        // ------------------------------------------------------------------ かき氷

        static void BuildIceShaver(Transform root, Color product)
        {
            var painted = MatsuriMaterials.Painted(new Color(0.85f, 0.87f, 0.90f), 0.55f);
            var steel = MatsuriMaterials.Metal(MetalBright);

            ArtParts.Part("Base", root, MatsuriMeshes.Box(new Vector3(0.42f, 0.06f, 0.36f)), painted, new Vector3(-0.22f, 0.03f, 0f));
            // 削り機の本体
            ArtParts.Part("Column", root, MatsuriMeshes.Box(new Vector3(0.13f, 0.46f, 0.13f)), painted, new Vector3(-0.34f, 0.29f, -0.06f));
            ArtParts.Part("Housing", root, MatsuriMeshes.Cylinder(0.115f, 0.20f, 14), steel, new Vector3(-0.22f, 0.46f, 0f));
            ArtParts.Part("Cap", root, MatsuriMeshes.Cone(0.12f, 0.10f, 14), painted, new Vector3(-0.22f, 0.61f, 0f));
            // ハンドル
            var handlePivot = ArtParts.Empty("Handle", root, new Vector3(-0.22f, 0.60f, 0f));
            SwayAnimator.Attach(handlePivot, SwayMode.Rotate, 6f, 2.4f).Axis = Vector3.up;
            ArtParts.NoShadow(ArtParts.Part("Arm", handlePivot.transform, MatsuriMeshes.Cylinder(0.010f, 0.20f, 6), steel, new Vector3(0.10f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 90f)));
            ArtParts.NoShadow(ArtParts.Part("Knob", handlePivot.transform, MatsuriMeshes.Cylinder(0.018f, 0.05f, 8), MatsuriMaterials.Wood(WoodLight), new Vector3(0.20f, 0.05f, 0f)));

            // 器と削られた氷
            ArtParts.Part("Bowl", root, MatsuriMeshes.Cylinder(0.085f, 0.06f, 14), MatsuriMaterials.Painted(Color.white, 0.5f), new Vector3(-0.22f, 0.09f, 0.02f));
            ArtParts.NoShadow(ArtParts.Part("Ice", root, MatsuriMeshes.Cone(0.082f, 0.12f, 12), MatsuriMaterials.Painted(product, 0.6f), new Vector3(-0.22f, 0.17f, 0.02f)));

            // シロップ瓶5本（色違い）
            var bottleColors = new[]
            {
                new Color(0.86f, 0.16f, 0.18f),   // いちご
                new Color(0.95f, 0.72f, 0.16f),   // レモン
                new Color(0.30f, 0.68f, 0.36f),   // メロン
                new Color(0.28f, 0.48f, 0.85f),   // ブルーハワイ
                new Color(0.62f, 0.35f, 0.72f)    // ぶどう
            };
            var bottle = MatsuriMeshes.Cylinder(0.032f, 0.20f, 10);
            var neck = MatsuriMeshes.Cylinder(0.013f, 0.06f, 8);
            for (int i = 0; i < bottleColors.Length; i++)
            {
                float x = 0.06f + i * 0.085f;
                var mat = MatsuriMaterials.Translucent(bottleColors[i], 0.82f, 0.75f);
                ArtParts.NoShadow(ArtParts.Part("Syrup" + i, root, bottle, mat, new Vector3(x, 0.10f, -0.04f)));
                ArtParts.NoShadow(ArtParts.Part("SyrupNeck" + i, root, neck, MatsuriMaterials.Painted(Color.white, 0.6f), new Vector3(x, 0.22f, -0.04f)));
            }
        }

        // ------------------------------------------------------------------ りんご飴

        static void BuildCandyApple(Transform root, Color product)
        {
            var wood = MatsuriMaterials.Wood(WoodLight);
            // 斜めのラック
            var board = MatsuriMeshes.Box(new Vector3(0.86f, 0.035f, 0.44f));
            ArtParts.Part("Rack", root, board, wood, new Vector3(0f, 0.16f, 0f), Quaternion.Euler(-18f, 0f, 0f));
            ArtParts.Part("RackLegBack", root, MatsuriMeshes.Box(new Vector3(0.80f, 0.24f, 0.03f)), wood, new Vector3(0f, 0.12f, -0.20f));
            ArtParts.Part("RackLegFront", root, MatsuriMeshes.Box(new Vector3(0.80f, 0.08f, 0.03f)), wood, new Vector3(0f, 0.04f, 0.20f));

            var apple = MatsuriMeshes.Sphere(0.055f, 12, 8);
            var stick = MatsuriMeshes.Cylinder(0.006f, 0.16f, 6);
            var candy = MatsuriMaterials.Painted(product, 0.85f);
            var stickMat = MatsuriMaterials.Wood(new Color(0.80f, 0.70f, 0.52f));
            for (int i = 0; i < 6; i++)
            {
                float x = -0.34f + i * 0.135f;
                float z = (i % 2 == 0) ? -0.06f : 0.06f;
                float y = 0.24f - z * 0.32f;
                var pivot = ArtParts.Empty("Apple" + i, root, new Vector3(x, y, z), Quaternion.Euler(-18f, i * 23f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Candy", pivot.transform, apple, candy, new Vector3(0f, 0.055f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Stick", pivot.transform, stick, stickMat, new Vector3(0f, 0.16f, 0f)));
            }
        }

        // ------------------------------------------------------------------ 綿あめ

        static void BuildCottonCandy(Transform root, Color product)
        {
            var steel = MatsuriMaterials.Metal(MetalBright);
            var painted = MatsuriMaterials.Painted(new Color(0.90f, 0.92f, 0.95f), 0.5f);

            ArtParts.Part("Base", root, MatsuriMeshes.Cylinder(0.30f, 0.10f, 18), painted, new Vector3(-0.15f, 0.05f, 0f));
            ArtParts.Part("Bowl", root, MatsuriMeshes.Cylinder(0.28f, 0.16f, 18), steel, new Vector3(-0.15f, 0.18f, 0f));
            var spinner = ArtParts.Empty("Spinner", root, new Vector3(-0.15f, 0.27f, 0f));
            SwayAnimator.Attach(spinner, SwayMode.Rotate, 20f, 6f).Axis = Vector3.up;
            ArtParts.NoShadow(ArtParts.Part("Head", spinner.transform, MatsuriMeshes.Cylinder(0.07f, 0.07f, 10), steel, Vector3.zero));

            // 出来上がりを袋に入れて吊るす
            var bar = MatsuriMeshes.Cylinder(0.010f, 0.72f, 6);
            ArtParts.Part("HangBar", root, bar, steel, new Vector3(0.28f, 0.62f, -0.10f), Quaternion.Euler(0f, 0f, 90f));
            var puff = MatsuriMeshes.Sphere(0.085f, 10, 7);
            var bagMat = MatsuriMaterials.Translucent(Color.white, 0.35f, 0.85f);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.06f + i * 0.22f;
                var hang = ArtParts.Empty("CandyBag" + i, root, new Vector3(x, 0.60f, -0.10f));
                SwayAnimator.Attach(hang, SwayMode.Rotate, 4f, 0.8f + i * 0.13f).Axis = Vector3.forward;
                ArtParts.NoShadow(ArtParts.Part("String", hang.transform, MatsuriMeshes.Cylinder(0.003f, 0.10f, 4), steel, new Vector3(0f, -0.05f, 0f)));
                var col = Color.Lerp(Color.white, product, i * 0.35f);
                ArtParts.NoShadow(ArtParts.Part("Puff", hang.transform, puff, MatsuriMaterials.Painted(col, 0.15f), new Vector3(0f, -0.19f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Bag", hang.transform, puff, bagMat, new Vector3(0f, -0.19f, 0f), Quaternion.identity, Vector3.one * 1.18f));
            }
        }

        // ------------------------------------------------------------------ フランクフルト

        static void BuildGrill(Transform root, Color product)
        {
            var metal = MatsuriMaterials.Metal(MetalDark);
            ArtParts.Part("Body", root, MatsuriMeshes.Box(new Vector3(0.92f, 0.14f, 0.46f)), metal, new Vector3(0f, 0.07f, 0f));

            // 網
            var grid = Cluster("grill_net", () =>
            {
                var list = new List<CombineInstance>(20);
                var barX = MatsuriMeshes.Cylinder(0.005f, 0.88f, 5);
                var barZ = MatsuriMeshes.Cylinder(0.005f, 0.42f, 5);
                for (int i = 0; i < 9; i++)
                    list.Add(CI(barX, new Vector3(0f, 0f, -0.19f + i * 0.0475f), Quaternion.Euler(0f, 0f, 90f)));
                for (int i = 0; i < 5; i++)
                    list.Add(CI(barZ, new Vector3(-0.40f + i * 0.20f, 0f, 0f), Quaternion.Euler(90f, 0f, 0f)));
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Net", root, grid, MatsuriMaterials.Metal(MetalBright), new Vector3(0f, 0.15f, 0f)));

            var frank = MatsuriMeshes.Capsule(0.028f, 0.22f, 10);
            var frankMat = MatsuriMaterials.Painted(product, 0.42f);
            var stickMat = MatsuriMaterials.Wood(new Color(0.80f, 0.70f, 0.52f));
            var stick = MatsuriMeshes.Cylinder(0.005f, 0.14f, 5);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.15f + (i % 3) * 0.15f;
                float x = -0.28f + (i / 3) * 0.30f;
                var pivot = ArtParts.Empty("Frank" + i, root, new Vector3(x, 0.18f, z), Quaternion.Euler(0f, 90f + i * 6f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Sausage", pivot.transform, frank, frankMat, Vector3.zero, Quaternion.Euler(90f, 0f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Stick", pivot.transform, stick, stickMat, new Vector3(0f, 0f, -0.17f), Quaternion.Euler(90f, 0f, 0f)));
            }
        }

        // ------------------------------------------------------------------ 金魚すくい (§62)

        static void BuildFishTank(Transform root, Transform surface, Color product, float stallWidth)
        {
            float w = Mathf.Clamp(stallWidth * 0.72f, 1.2f, 2.4f);
            float d = 1.05f;
            float wallH = 0.28f;
            var frame = MatsuriMaterials.Painted(new Color(0.20f, 0.42f, 0.36f), 0.4f);

            // 水槽は床置き
            var tank = ArtParts.Empty("Tub", root, new Vector3(0f, 0f, 0.12f));

            ArtParts.Part("Bottom", tank.transform, MatsuriMeshes.Box(new Vector3(w, 0.05f, d)), frame, new Vector3(0f, 0.025f, 0f));
            var sideX = MatsuriMeshes.Box(new Vector3(0.05f, wallH, d));
            var sideZ = MatsuriMeshes.Box(new Vector3(w, wallH, 0.05f));
            ArtParts.Part("WallL", tank.transform, sideX, frame, new Vector3(-w * 0.5f + 0.025f, wallH * 0.5f, 0f));
            ArtParts.Part("WallR", tank.transform, sideX, frame, new Vector3(w * 0.5f - 0.025f, wallH * 0.5f, 0f));
            ArtParts.Part("WallB", tank.transform, sideZ, frame, new Vector3(0f, wallH * 0.5f, -d * 0.5f + 0.025f));
            ArtParts.Part("WallF", tank.transform, sideZ, frame, new Vector3(0f, wallH * 0.5f, d * 0.5f - 0.025f));

            // 水面
            var water = ArtParts.Part("WaterSurface", tank.transform,
                MatsuriMeshes.Plane(w - 0.10f, d - 0.10f), MatsuriMaterials.Water(), new Vector3(0f, wallH * 0.78f, 0f));
            ArtParts.NoShadow(water);
            var scroll = SwayAnimator.Attach(water, SwayMode.WaterScroll, 0.06f, 1f);
            scroll.SetWaterTiling(new Vector2(3f, 3f));

            // 金魚
            var fishMesh = Cluster("goldfish", () =>
            {
                var list = new List<CombineInstance>(3);
                list.Add(CI(MatsuriMeshes.Sphere(0.030f, 10, 6), Vector3.zero, Quaternion.identity, new Vector3(0.62f, 0.72f, 1.5f)));
                list.Add(CI(MatsuriMeshes.TrianglePrism(new Vector2(0f, 0f), new Vector2(-0.055f, 0.035f), new Vector2(-0.055f, -0.035f), 0.004f),
                    new Vector3(0f, 0f, -0.042f), Quaternion.Euler(0f, 90f, 0f)));
                return list;
            });
            var fishColors = new[]
            {
                new Color(0.90f, 0.30f, 0.12f),
                new Color(0.95f, 0.55f, 0.18f),
                new Color(0.96f, 0.92f, 0.88f),
                new Color(0.86f, 0.22f, 0.24f)
            };
            var fishRoot = ArtParts.Empty("Goldfish", tank.transform, new Vector3(0f, wallH * 0.78f, 0f));
            for (int i = 0; i < 7; i++)
            {
                var go = ArtParts.Empty("Fish" + i.ToString("00"), fishRoot.transform);
                var mesh = ArtParts.Part("Mesh", go.transform, fishMesh,
                    MatsuriMaterials.Painted(fishColors[i % fishColors.Length], 0.68f), Vector3.zero);
                ArtParts.NoShadow(mesh);
                var swim = go.AddComponent<GoldfishSwimmer>();
                swim.Configure(Vector3.zero, new Vector2(w * 0.42f, d * 0.40f), 0.11f, (uint)(i * 977 + 13));
            }

            // ポイ（すくう網）
            var poiHandle = MatsuriMeshes.Cylinder(0.008f, 0.18f, 6);
            var poiRing = MatsuriMeshes.Torus(0.055f, 0.006f, 14, 5);
            var poiPaper = MatsuriMeshes.Disc(0.052f, 14);
            var paperMat = MatsuriMaterials.Translucent(new Color(0.96f, 0.93f, 0.86f), 0.55f, 0.3f);
            for (int i = 0; i < 4; i++)
            {
                var pivot = ArtParts.Empty("Poi" + i, surface, new Vector3(-0.42f + i * 0.10f, 0.02f, -0.22f), Quaternion.Euler(0f, i * 18f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Handle", pivot.transform, poiHandle, MatsuriMaterials.Painted(new Color(0.95f, 0.35f, 0.22f), 0.5f), new Vector3(0f, 0.02f, -0.11f), Quaternion.Euler(90f, 0f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Ring", pivot.transform, poiRing, MatsuriMaterials.Painted(new Color(0.95f, 0.35f, 0.22f), 0.5f), new Vector3(0f, 0.02f, 0.03f)));
                ArtParts.NoShadow(ArtParts.Part("Paper", pivot.transform, poiPaper, paperMat, new Vector3(0f, 0.021f, 0.03f)));
            }
        }

        // ------------------------------------------------------------------ 射的

        static void BuildShootingRack(Transform root, Transform surface, Color product, float stallWidth)
        {
            float w = Mathf.Clamp(stallWidth * 0.78f, 1.2f, 2.6f);
            var wood = MatsuriMaterials.Wood(WoodLight);
            var rack = ArtParts.Empty("Rack", root, new Vector3(0f, 0.42f, -0.35f));

            var shelf = MatsuriMeshes.Box(new Vector3(w, 0.035f, 0.20f));
            for (int i = 0; i < 3; i++)
            {
                float y = 0.10f + i * 0.28f;
                float z = -i * 0.10f;
                ArtParts.Part("Shelf" + i, rack.transform, shelf, wood, new Vector3(0f, y, z));
                ArtParts.Part("Riser" + i, rack.transform, MatsuriMeshes.Box(new Vector3(w, 0.26f, 0.03f)), wood, new Vector3(0f, y + 0.13f, z - 0.10f));
            }

            // 景品
            var prizeBox = MatsuriMeshes.Box(new Vector3(0.10f, 0.13f, 0.07f));
            var prizeBall = MatsuriMeshes.Sphere(0.055f, 10, 7);
            var colors = new[]
            {
                new Color(0.92f, 0.28f, 0.30f), new Color(0.28f, 0.55f, 0.90f),
                new Color(0.98f, 0.80f, 0.25f), new Color(0.42f, 0.78f, 0.45f),
                new Color(0.85f, 0.50f, 0.85f), product
            };
            int n = Mathf.Max(4, Mathf.FloorToInt(w / 0.24f));
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < n; j++)
                {
                    float y = 0.10f + i * 0.28f;
                    float z = -i * 0.10f;
                    float x = -w * 0.5f + 0.14f + j * ((w - 0.28f) / Mathf.Max(1, n - 1));
                    bool isBall = ((i + j) % 3) == 0;
                    var mat = MatsuriMaterials.Painted(colors[(i * 3 + j) % colors.Length], 0.5f);
                    var go = ArtParts.Part("Prize" + i + "_" + j, rack.transform,
                        isBall ? prizeBall : prizeBox, mat,
                        new Vector3(x, y + (isBall ? 0.073f : 0.083f), z), Quaternion.Euler(0f, (i * 31 + j * 17) % 40 - 20f, 0f));
                    ArtParts.NoShadow(go);
                }

            // コルク銃2丁
            var stock = MatsuriMeshes.Box(new Vector3(0.05f, 0.07f, 0.30f));
            var barrel = MatsuriMeshes.Cylinder(0.015f, 0.34f, 8);
            var gunWood = MatsuriMaterials.Wood(new Color(0.35f, 0.22f, 0.14f));
            var steel = MatsuriMaterials.Metal(MetalDark);
            for (int i = 0; i < 2; i++)
            {
                var pivot = ArtParts.Empty("CorkGun" + i, surface, new Vector3(-0.32f + i * 0.60f, 0.05f, 0.16f), Quaternion.Euler(0f, 14f - i * 28f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Stock", pivot.transform, stock, gunWood, Vector3.zero));
                ArtParts.NoShadow(ArtParts.Part("Barrel", pivot.transform, barrel, steel, new Vector3(0f, 0.02f, 0.28f), Quaternion.Euler(90f, 0f, 0f)));
            }
        }

        // ------------------------------------------------------------------ ヨーヨー釣り

        static void BuildYoyoTub(Transform root, Transform surface, Color product, float stallWidth)
        {
            float r = Mathf.Clamp(stallWidth * 0.30f, 0.55f, 0.95f);
            var tub = ArtParts.Empty("Tub", root, new Vector3(0f, 0f, 0.14f));
            var vinyl = MatsuriMaterials.Painted(new Color(0.20f, 0.32f, 0.62f), 0.55f);

            ArtParts.Part("Wall", tub.transform, MatsuriMeshes.Cylinder(r, 0.34f, 22), vinyl, new Vector3(0f, 0.17f, 0f));
            ArtParts.Part("Rim", tub.transform, MatsuriMeshes.Torus(r, 0.035f, 22, 7), vinyl, new Vector3(0f, 0.34f, 0f));

            var water = ArtParts.Part("WaterSurface", tub.transform, MatsuriMeshes.Disc(r - 0.05f, 24), MatsuriMaterials.Water(), new Vector3(0f, 0.27f, 0f));
            ArtParts.NoShadow(water);
            SwayAnimator.Attach(water, SwayMode.WaterScroll, 0.05f, 1f).SetWaterTiling(new Vector2(2f, 2f));

            var ball = MatsuriMeshes.Sphere(0.055f, 12, 8);
            var colors = new[]
            {
                new Color(0.95f, 0.32f, 0.30f), new Color(0.30f, 0.62f, 0.92f),
                new Color(0.98f, 0.82f, 0.28f), new Color(0.45f, 0.82f, 0.50f),
                new Color(0.88f, 0.52f, 0.86f), product
            };
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.399f;                       // 黄金角で散らす
                float rad = r * 0.72f * Mathf.Sqrt((i + 0.5f) / 9f);
                var pivot = ArtParts.Empty("Yoyo" + i.ToString("00"), tub.transform,
                    new Vector3(Mathf.Cos(a) * rad, 0.28f, Mathf.Sin(a) * rad));
                SwayAnimator.Attach(pivot, SwayMode.Rotate, 3.5f, 0.5f + i * 0.07f).Axis = Vector3.forward;
                var mat = MatsuriMaterials.Translucent(colors[i % colors.Length], 0.85f, 0.85f);
                ArtParts.NoShadow(ArtParts.Part("Ball", pivot.transform, ball, mat, new Vector3(0f, -0.012f, 0f), Quaternion.identity, new Vector3(1f, 0.85f, 1f)));
                ArtParts.NoShadow(ArtParts.Part("Loop", pivot.transform, MatsuriMeshes.Torus(0.012f, 0.003f, 8, 4),
                    MatsuriMaterials.Painted(Color.white, 0.4f), new Vector3(0f, 0.05f, 0f), Quaternion.Euler(90f, 0f, 0f)));
            }

            // こより（釣り糸）
            var koyori = MatsuriMeshes.Cylinder(0.0035f, 0.30f, 5);
            var paper = MatsuriMaterials.Painted(new Color(0.94f, 0.90f, 0.80f), 0.2f);
            for (int i = 0; i < 4; i++)
                ArtParts.NoShadow(ArtParts.Part("Koyori" + i, surface, koyori, paper,
                    new Vector3(-0.40f + i * 0.14f, 0.03f, -0.24f), Quaternion.Euler(72f, i * 15f, 0f)));
        }

        // ------------------------------------------------------------------ スーパーボールすくい

        static void BuildBallTub(Transform root, Transform surface, Color product, float stallWidth)
        {
            float w = Mathf.Clamp(stallWidth * 0.70f, 1.1f, 2.2f);
            float d = 0.95f;
            var tub = ArtParts.Empty("Tub", root, new Vector3(0f, 0f, 0.14f));
            var vinyl = MatsuriMaterials.Painted(new Color(0.22f, 0.48f, 0.55f), 0.5f);

            ArtParts.Part("Bottom", tub.transform, MatsuriMeshes.Box(new Vector3(w, 0.05f, d)), vinyl, new Vector3(0f, 0.025f, 0f));
            var sideX = MatsuriMeshes.Box(new Vector3(0.05f, 0.30f, d));
            var sideZ = MatsuriMeshes.Box(new Vector3(w, 0.30f, 0.05f));
            ArtParts.Part("WallL", tub.transform, sideX, vinyl, new Vector3(-w * 0.5f, 0.15f, 0f));
            ArtParts.Part("WallR", tub.transform, sideX, vinyl, new Vector3(w * 0.5f, 0.15f, 0f));
            ArtParts.Part("WallB", tub.transform, sideZ, vinyl, new Vector3(0f, 0.15f, -d * 0.5f));
            ArtParts.Part("WallF", tub.transform, sideZ, vinyl, new Vector3(0f, 0.15f, d * 0.5f));

            var water = ArtParts.Part("WaterSurface", tub.transform, MatsuriMeshes.Plane(w - 0.08f, d - 0.08f), MatsuriMaterials.Water(), new Vector3(0f, 0.245f, 0f));
            ArtParts.NoShadow(water);
            SwayAnimator.Attach(water, SwayMode.WaterScroll, 0.07f, 1f).SetWaterTiling(new Vector2(3f, 3f));

            var ball = MatsuriMeshes.Sphere(0.028f, 8, 6);
            var colors = new[]
            {
                new Color(0.95f, 0.25f, 0.28f), new Color(0.25f, 0.60f, 0.95f),
                new Color(0.98f, 0.85f, 0.22f), new Color(0.35f, 0.85f, 0.45f),
                new Color(0.90f, 0.45f, 0.85f), new Color(0.98f, 0.60f, 0.25f),
                new Color(0.98f, 0.98f, 0.98f), product
            };
            var mats = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++) mats[i] = MatsuriMaterials.Painted(colors[i], 0.85f);

            for (int i = 0; i < 34; i++)
            {
                float a = i * 2.399f;
                float rad = Mathf.Sqrt((i + 0.5f) / 34f);
                float x = Mathf.Cos(a) * rad * (w * 0.42f);
                float z = Mathf.Sin(a) * rad * (d * 0.40f);
                var go = ArtParts.Part("Ball" + i.ToString("00"), tub.transform, ball, mats[i % mats.Length],
                    new Vector3(x, 0.238f + ((i % 3) * 0.004f), z));
                ArtParts.NoShadow(go);
            }

            // すくうポイ
            var scoop = MatsuriMeshes.Torus(0.05f, 0.006f, 12, 5);
            for (int i = 0; i < 3; i++)
            {
                var pivot = ArtParts.Empty("Scoop" + i, surface, new Vector3(-0.38f + i * 0.13f, 0.02f, -0.24f), Quaternion.Euler(0f, i * 21f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Handle", pivot.transform, MatsuriMeshes.Cylinder(0.007f, 0.17f, 6), MatsuriMaterials.Painted(new Color(0.3f, 0.7f, 0.9f), 0.5f), new Vector3(0f, 0.02f, -0.10f), Quaternion.Euler(90f, 0f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Ring", pivot.transform, scoop, MatsuriMaterials.Painted(new Color(0.3f, 0.7f, 0.9f), 0.5f), new Vector3(0f, 0.02f, 0.03f)));
            }
        }

        // ------------------------------------------------------------------ 型抜き

        static void BuildKatanuki(Transform root, Color product)
        {
            var wood = MatsuriMaterials.Wood(WoodLight);
            ArtParts.Part("Desk", root, MatsuriMeshes.Box(new Vector3(0.90f, 0.04f, 0.50f)), wood, new Vector3(0f, 0.02f, 0f));
            ArtParts.Part("Ledge", root, MatsuriMeshes.Box(new Vector3(0.90f, 0.05f, 0.03f)), wood, new Vector3(0f, 0.06f, -0.24f));

            // 型抜き板（薄い板に星や桜の抜き跡）
            var plateMesh = MatsuriMeshes.Box(new Vector3(0.14f, 0.008f, 0.14f));
            var plateMat = MatsuriMaterials.Painted(Color.Lerp(product, new Color(0.95f, 0.90f, 0.72f), 0.4f), 0.25f);
            var markMat = MatsuriMaterials.Painted(new Color(0.55f, 0.40f, 0.24f), 0.2f);
            for (int i = 0; i < 6; i++)
            {
                float x = -0.34f + (i % 3) * 0.24f;
                float z = (i / 3) * 0.17f - 0.09f;
                var pivot = ArtParts.Empty("Plate" + i, root, new Vector3(x, 0.045f, z), Quaternion.Euler(0f, i * 17f, 0f));
                ArtParts.NoShadow(ArtParts.Part("Sheet", pivot.transform, plateMesh, plateMat, Vector3.zero));
                // 抜く形（星・丸・三角）を溝として表現
                Mesh mark = i % 3 == 0
                    ? MatsuriMeshes.Torus(0.035f, 0.004f, 12, 4)
                    : (i % 3 == 1
                        ? MatsuriMeshes.Torus(0.030f, 0.004f, 5, 4)
                        : MatsuriMeshes.Torus(0.028f, 0.004f, 3, 4));
                ArtParts.NoShadow(ArtParts.Part("Mark", pivot.transform, mark, markMat, new Vector3(0f, 0.005f, 0f)));
            }

            // 針
            var needle = MatsuriMeshes.Cylinder(0.0035f, 0.14f, 5);
            var steel = MatsuriMaterials.Metal(MetalBright);
            for (int i = 0; i < 3; i++)
                ArtParts.NoShadow(ArtParts.Part("Needle" + i, root, needle, steel,
                    new Vector3(0.34f + i * 0.03f, 0.06f, 0.14f - i * 0.05f), Quaternion.Euler(70f, i * 25f, 0f)));
        }

        // ------------------------------------------------------------------ 予備

        static void BuildGenericBoxes(Transform root, Color product)
        {
            var wood = MatsuriMaterials.Wood(WoodLight);
            ArtParts.Part("Crate", root, MatsuriMeshes.Box(new Vector3(0.42f, 0.26f, 0.32f)), wood, new Vector3(-0.24f, 0.13f, 0f));
            ArtParts.Part("Crate2", root, MatsuriMeshes.Box(new Vector3(0.34f, 0.20f, 0.28f)), wood, new Vector3(0.24f, 0.10f, -0.04f), Quaternion.Euler(0f, 12f, 0f));
            ArtParts.NoShadow(ArtParts.Part("Goods", root, MatsuriMeshes.Sphere(0.07f, 10, 7), MatsuriMaterials.Painted(product, 0.4f), new Vector3(0.24f, 0.27f, -0.04f)));
        }

        // ------------------------------------------------------------------ 背面の荷物

        /// <summary>
        /// 屋台の裏に積んである荷物。木箱・ビールケース・クーラーボックス・
        /// ポリタンク・のぼり竿を、背板のすぐ後ろに寄せて並べる。
        ///
        /// 背面がのっぺりした一枚板に見えるのを防ぐのが目的なので、
        /// 屋台ごとに並びを変える（幅と奥行きから決まるので毎回同じ屋台は同じ並びになる）。
        /// 置き場は MainStructure の当たり判定の中に収まる範囲に限る。
        /// </summary>
        public static GameObject BuildBackYard(Transform parent, StallVisualRecipe recipe, string displayName,
            float width, float depth)
        {
            var node = ArtParts.Empty("BackYard", parent, new Vector3(0f, 0f, -depth * 0.5f - 0.24f));
            if (recipe == null) return node;

            Color woodColor = new Color(recipe.WoodColor.r * 0.86f, recipe.WoodColor.g * 0.86f, recipe.WoodColor.b * 0.86f);
            var crateMat = MatsuriMaterials.Planks(woodColor);
            var caseMatA = MatsuriMaterials.Painted(new Color(0.62f, 0.13f, 0.13f), 0.42f);
            var caseMatB = MatsuriMaterials.Painted(new Color(0.16f, 0.30f, 0.52f), 0.42f);
            var coolerMat = MatsuriMaterials.Painted(new Color(0.86f, 0.87f, 0.88f), 0.55f);
            var lidMat = MatsuriMaterials.Painted(new Color(0.20f, 0.36f, 0.58f), 0.55f);

            // 左：ビールケースを2段
            float xL = -width * 0.30f;
            var beer = MatsuriMeshes.BeerCase(0.36f, 0.24f, 0.28f);
            ArtParts.Part("BeerCase0", node.transform, beer, caseMatA, new Vector3(xL, 0f, 0f), Quaternion.Euler(0f, 6f, 0f));
            ArtParts.Part("BeerCase1", node.transform, beer, caseMatB, new Vector3(xL + 0.02f, 0.24f, -0.01f), Quaternion.Euler(0f, -9f, 0f));

            // 中央：木箱の上に一斗缶
            float xC = -width * 0.02f;
            ArtParts.Part("Crate", node.transform, MatsuriMeshes.WoodCrate(0.44f, 0.30f, 0.32f), crateMat,
                new Vector3(xC, 0f, -0.01f), Quaternion.Euler(0f, -5f, 0f));
            ArtParts.NoShadow(ArtParts.Part("Can", node.transform,
                MatsuriMeshes.Box(new Vector3(0.22f, 0.30f, 0.22f)),
                MatsuriMaterials.Metal(new Color(0.55f, 0.53f, 0.48f)),
                new Vector3(xC - 0.05f, 0.45f, -0.01f), Quaternion.Euler(0f, 14f, 0f)));

            // 右：クーラーボックスと水のポリタンク
            float xR = width * 0.28f;
            ArtParts.Part("Cooler", node.transform, MatsuriMeshes.CoolerBox(0.46f, 0.32f, 0.30f),
                coolerMat, new Vector3(xR, 0f, 0f), Quaternion.Euler(0f, -8f, 0f));
            ArtParts.NoShadow(ArtParts.Part("CoolerLid", node.transform,
                MatsuriMeshes.Box(new Vector3(0.48f, 0.045f, 0.32f)), lidMat,
                new Vector3(xR, 0.325f, 0f), Quaternion.Euler(0f, -8f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("WaterTank", node.transform,
                MatsuriMeshes.TaperedBox(0.24f, 0.24f, 0.20f, 0.20f, 0.30f),
                MatsuriMaterials.Translucent(new Color(0.72f, 0.80f, 0.74f), 0.72f, 0.55f),
                new Vector3(xR - 0.34f, 0.15f, -0.06f), Quaternion.Euler(0f, 22f, 0f)));

            // のぼり竿。屋号を縦に入れた細長い布を背面に立てる
            BuildNobori(node.transform, recipe, displayName, width);
            return node;
        }

        /// <summary>
        /// 背面ののぼり。竿と、縦書きの布1枚。
        /// 竿は軒より低くする（屋根を突き抜けないように）。
        /// </summary>
        static void BuildNobori(Transform parent, StallVisualRecipe recipe, string displayName, float width)
        {
            string text = string.IsNullOrEmpty(recipe.NorenText) ? displayName : recipe.NorenText;
            if (string.IsNullOrEmpty(text)) text = "祭";

            // のぼりは背が高くて遠くからも見えるので、荷物より一段長く残す
            var node = LodBuilder.Tag(ArtParts.Empty("Nobori", parent), LodTier.Medium).transform;

            const float poleH = 2.10f;
            float x = -width * 0.5f + 0.10f;
            var pole = ArtParts.Part("NoboriPole", node,
                MatsuriMeshes.Cylinder(0.022f, poleH, 8),
                MatsuriMaterials.Wood(new Color(0.30f, 0.24f, 0.17f)),
                new Vector3(x, poleH * 0.5f, 0f));
            ArtParts.NoShadow(pole);

            Color cloth = recipe.NorenColor;
            Color ink = cloth.r * 0.299f + cloth.g * 0.587f + cloth.b * 0.114f < 0.5f
                ? new Color(0.96f, 0.94f, 0.90f)
                : new Color(0.10f, 0.09f, 0.08f);
            var tex = ProceduralTextures.KanjiSign(text, 192, 768, cloth, ink, true);
            var flag = ArtParts.Part("NoboriCloth", node,
                MatsuriMeshes.ClothStrip(0.34f, 1.46f, 3, 7),
                MatsuriMaterials.PrintedFabric(tex, Color.white),
                new Vector3(x + 0.19f, poleH - 0.16f, 0f));
            ArtParts.NoShadow(flag);
            // 布は表裏の両面が同じ UV なので、表（+Z 側）が鏡文字になる。
            // MaterialPropertyBlock で U を反転して、表から読める向きに直す
            ArtParts.SetTextureOffset(flag.GetComponent<MeshRenderer>(), new Vector2(-1f, 1f), new Vector2(1f, 0f));
            SwayAnimator.Attach(flag, SwayMode.Cloth, 0.045f, 0.75f);

            // 竿の先の横木。のぼりの上辺を張る
            ArtParts.NoShadow(ArtParts.Part("NoboriArm", node,
                MatsuriMeshes.Cylinder(0.014f, 0.40f, 6),
                MatsuriMaterials.Wood(new Color(0.30f, 0.24f, 0.17f)),
                new Vector3(x + 0.19f, poleH - 0.14f, 0f), Quaternion.Euler(0f, 0f, 90f)));
        }
    }
}

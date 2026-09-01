using System.Collections.Generic;
using Matsuri.Data;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Art
{
    /// <summary>
    /// §23 の屋台階層をコードでそのまま組み立てる。
    /// 屋台は +Z を「お客側」として作る。行列も +Z 方向に伸びる。
    /// StallData.Prefab に完成モデルが入ったら、このファクトリは使われなくなる (§69)。
    ///
    /// 見た目の勘所は3つ。
    ///   ・屋根は「面 + 骨組み + 垂木 + 縞」の4層。板1枚に見せない
    ///   ・正面は「軒下の裸電球 → カウンター → 前掛け」で明るさと文字を段に重ねる
    ///   ・側面と背面は板張りの目地と荷物で、のっぺりした面を作らない
    /// </summary>
    public static class ProceduralStallFactory
    {
        static StallVisualRecipe s_Fallback;

        /// <summary>レシピが割り当てられていない屋台でも「箱1個」にならないようにする既定値。</summary>
        static StallVisualRecipe Fallback
        {
            get
            {
                if (s_Fallback == null) s_Fallback = ScriptableObject.CreateInstance<StallVisualRecipe>();
                return s_Fallback;
            }
        }

        /// <summary>布や看板の面はメッシュ側で +Z を向いているので、追加の回転は要らない。</summary>
        static readonly Quaternion FaceFront = Quaternion.identity;

        /// <summary>軒の出。屋根が本体より張り出す量。</summary>
        const float Overhang = 0.42f;

        /// <summary>アルファを保ったまま暗くする。</summary>
        static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        public static GameObject Build(StallData data, StallVisualRecipe recipe, Transform parent)
        {
            if (recipe == null) recipe = data != null && data.VisualRecipe != null ? data.VisualRecipe : Fallback;
            string displayName = data != null ? data.DisplayName : "屋台";

            var root = ArtParts.Empty(string.IsNullOrEmpty(displayName) ? "Stall" : displayName, parent);

            float w = Mathf.Max(1.2f, recipe.Width);
            float d = Mathf.Max(1.0f, recipe.Depth);
            float h = Mathf.Max(1.6f, recipe.Height);
            float ch = Mathf.Clamp(recipe.CounterHeight, 0.6f, h - 0.5f);

            BuildMainStructure(root.transform, recipe, w, d, h);
            BuildRoof(root.transform, recipe, w, d, h);
            BuildNoren(root.transform, recipe, displayName, w, d, h);
            BuildSign(root.transform, recipe, displayName, w, d, h);
            BuildCounter(root.transform, recipe, displayName, w, d, ch);

            ProceduralStallProps.Build(recipe.Prop, recipe, root.transform, ch)
                .transform.localPosition = new Vector3(0f, 0f, -0.10f);

            BuildFoodProps(root.transform, recipe, w, d, ch);
            BuildSauceBottle(root.transform, recipe, w, d, ch);
            BuildLightBulbs(root.transform, recipe, w, d, h, ch);
            BuildLanterns(root.transform, recipe, w, d, h);
            BuildSideMenu(root.transform, recipe, w, d, h, ch);
            ProceduralStallProps.BuildBackYard(root.transform, recipe, displayName, w, d);
            BuildSteam(root.transform, data, recipe, ch);

            // 店員をカウンターの内側に1体。正面から「人がいる」と分かるようにする
            StallStaff.Build(root.transform, recipe, displayName,
                new Vector3(-w * 0.17f, 0f, -d * 0.5f + 0.30f));

            ArtParts.Empty("StaffPosition", root.transform, new Vector3(0f, 0f, -d * 0.5f + 0.45f));
            ArtParts.Empty("CustomerPosition", root.transform, new Vector3(0f, 0f, d * 0.5f + 0.62f));
            BuildQueuePoints(root.transform, recipe, d);
            BuildAudioSource(root.transform, h);

            LodBuilder.AddLod(root, new[] { 0.32f, 0.09f, 0.015f });
            return root;
        }

        // ------------------------------------------------------------------ 骨組み

        static void BuildMainStructure(Transform root, StallVisualRecipe recipe, float w, float d, float h)
        {
            var node = ArtParts.Empty("MainStructure", root);
            var wood = MatsuriMaterials.Wood(recipe.WoodColor);
            var plank = MatsuriMaterials.Planks(Darken(recipe.WoodColor, 0.78f));

            // 柱4本。上に向かってわずかに細くして、角材の一本調子を消す
            var post = MatsuriMeshes.TaperedBox(0.125f, 0.125f, 0.098f, 0.098f, h);
            float px = w * 0.5f - 0.07f, pz = d * 0.5f - 0.07f;
            ArtParts.Part("PostFL", node.transform, post, wood, new Vector3(-px, h * 0.5f, pz));
            ArtParts.Part("PostFR", node.transform, post, wood, new Vector3(px, h * 0.5f, pz));
            ArtParts.Part("PostBL", node.transform, post, wood, new Vector3(-px, h * 0.5f, -pz));
            ArtParts.Part("PostBR", node.transform, post, wood, new Vector3(px, h * 0.5f, -pz));

            // 背板・側板は板張り。板と板の間の目地が実際にへこんでいるので影が出る
            ArtParts.Part("BackPanel", node.transform,
                MatsuriMeshes.SlatPanel(w - 0.14f, h * 0.78f, 0.07f, 6), plank,
                new Vector3(0f, h * 0.5f, -d * 0.5f + 0.06f));
            var side = MatsuriMeshes.SlatPanel(d - 0.16f, h * 0.60f, 0.065f, 5);
            ArtParts.Part("SidePanelL", node.transform, side, plank,
                new Vector3(-w * 0.5f + 0.055f, h * 0.62f, 0f), Quaternion.Euler(0f, 90f, 0f));
            ArtParts.Part("SidePanelR", node.transform, side, plank,
                new Vector3(w * 0.5f - 0.055f, h * 0.62f, 0f), Quaternion.Euler(0f, 90f, 0f));

            // 梁
            ArtParts.Part("BeamFront", node.transform, MatsuriMeshes.Box(new Vector3(w, 0.11f, 0.10f)), wood,
                new Vector3(0f, h - 0.06f, d * 0.5f - 0.05f));
            ArtParts.Part("BeamBack", node.transform, MatsuriMeshes.Box(new Vector3(w, 0.11f, 0.10f)), wood,
                new Vector3(0f, h - 0.06f, -d * 0.5f + 0.05f));
            // 足元の桟
            ArtParts.Part("SillFront", node.transform, MatsuriMeshes.Box(new Vector3(w, 0.09f, 0.08f)), wood,
                new Vector3(0f, 0.05f, d * 0.5f - 0.05f));

            // 方杖（柱と梁の隅を留める斜材）。4隅ぶんを1メッシュにまとめる
            float braceLen = 0.34f;
            var braces = MatsuriMeshes.CombineCached(
                "StallBrace_" + px.ToString("0.##") + "_" + pz.ToString("0.##"), () =>
            {
                var bar = MatsuriMeshes.Box(new Vector3(0.055f, braceLen, 0.055f));
                var list = new List<CombineInstance>(4);
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        var pos = new Vector3(sx * (px - 0.10f), -0.13f, sz * (pz - 0.10f));
                        var rot = Quaternion.Euler(sz * 42f, 0f, -sx * 42f);
                        list.Add(new CombineInstance { mesh = bar, transform = Matrix4x4.TRS(pos, rot, Vector3.one) });
                    }
                return list;
            });
            LodBuilder.Tag(ArtParts.Part("Braces", node.transform, braces, wood, new Vector3(0f, h - 0.06f, 0f)),
                LodTier.Medium);

            // NavMesh が屋台を避けられるように当たりを付ける。背面の荷物ぶんも含める
            const float backYard = 0.55f;
            var col = node.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, -0.05f - backYard * 0.5f);
            col.size = new Vector3(w, h, d + backYard);
        }

        // ------------------------------------------------------------------ 屋根

        static void BuildRoof(Transform root, StallVisualRecipe recipe, float w, float d, float h)
        {
            var node = ArtParts.Empty("Roof", root, new Vector3(0f, h, 0f));
            float rise = Mathf.Clamp(w * 0.16f, 0.34f, 0.62f);
            float rw = w + 0.16f, rd = d + 0.16f;
            float halfD = rd * 0.5f + Overhang;
            const float stripeWidth = 0.40f;

            Color baseColor = recipe.StripedRoof ? recipe.RoofStripeColor : recipe.RoofColor;
            var frameMat = MatsuriMaterials.Wood(Darken(recipe.WoodColor, 0.60f));
            var rafterMat = MatsuriMaterials.Wood(Darken(recipe.WoodColor, 0.88f));

            switch (recipe.Roof)
            {
                case StallRoofKind.Shed:
                {
                    ArtParts.Part("Surface", node.transform,
                        MatsuriMeshes.ShedRoof(rw, rd, rise, Overhang),
                        MatsuriMaterials.Wood(baseColor), Vector3.zero);
                    AddRoofLayer(node.transform, "Frame", MatsuriMeshes.ShedRoofFrame(rw, rd, rise, Overhang),
                        frameMat, LodTier.Medium);
                    AddRoofLayer(node.transform, "Rafters", MatsuriMeshes.ShedRafters(rw, rd, rise, Overhang),
                        rafterMat, LodTier.Detail);
                    if (recipe.StripedRoof)
                        AddRoofLayer(node.transform, "Stripes",
                            MatsuriMeshes.ShedRoofStripes(rw, rd, rise, Overhang, stripeWidth),
                            MatsuriMaterials.Wood(recipe.RoofColor), LodTier.Medium);
                    break;
                }
                case StallRoofKind.Awning:
                {
                    int bays = Mathf.Clamp(Mathf.RoundToInt(rw / 0.85f), 2, 6);
                    ArtParts.Part("Surface", node.transform,
                        MatsuriMeshes.AwningRoof(rw, rd, rise, Overhang, bays),
                        MatsuriMaterials.Fabric(baseColor), Vector3.zero);
                    AddRoofLayer(node.transform, "Frame", MatsuriMeshes.AwningFrame(rw, rd, rise, Overhang, bays),
                        MatsuriMaterials.Metal(new Color(0.60f, 0.62f, 0.66f)), LodTier.Medium);
                    if (recipe.StripedRoof)
                        AddRoofLayer(node.transform, "Stripes",
                            MatsuriMeshes.AwningStripes(rw, rd, rise, Overhang, bays, stripeWidth),
                            MatsuriMaterials.Fabric(recipe.RoofColor), LodTier.Medium);

                    // 幕板の垂れ。軒先から下がる帯で、裾が波形に切ってある
                    float eaveY = MatsuriMeshes.AwningEaveY(rise);
                    float valanceW = rw + Overhang * 2f - 0.06f;
                    var valance = ArtParts.Part("Valance", node.transform,
                        MatsuriMeshes.AwningValance(valanceW, 0.26f,
                            Mathf.Clamp(Mathf.RoundToInt(valanceW / 0.34f), 4, 16)),
                        MatsuriMaterials.Fabric(recipe.RoofColor),
                        new Vector3(0f, eaveY - 0.012f, halfD - 0.02f), FaceFront);
                    ArtParts.NoShadow(valance);
                    LodBuilder.Tag(valance, LodTier.Medium);
                    break;
                }
                default:
                {
                    ArtParts.Part("Surface", node.transform,
                        MatsuriMeshes.GableRoof(rw, rd, rise, Overhang),
                        MatsuriMaterials.Wood(baseColor), Vector3.zero);
                    // 棟木・破風板・鼻隠し。ここが無いと稜線が立たず「板2枚」に見える
                    AddRoofLayer(node.transform, "Frame", MatsuriMeshes.GableRoofFrame(rw, rd, rise, Overhang),
                        frameMat, LodTier.Medium);
                    // 垂木。軒下を見上げたときの情報量
                    AddRoofLayer(node.transform, "Rafters", MatsuriMeshes.GableRafters(rw, rd, rise, Overhang),
                        rafterMat, LodTier.Detail);
                    if (recipe.StripedRoof)
                        AddRoofLayer(node.transform, "Stripes",
                            MatsuriMeshes.GableRoofStripes(rw, rd, rise, Overhang, stripeWidth),
                            MatsuriMaterials.Wood(recipe.RoofColor), LodTier.Medium);
                    break;
                }
            }
        }

        /// <summary>屋根の重ね層を1枚足す。屋根面と同じ原点に置くだけ。</summary>
        static void AddRoofLayer(Transform roofNode, string name, Mesh mesh, Material mat, LodTier tier)
        {
            var go = ArtParts.Part(name, roofNode, mesh, mat, Vector3.zero);
            if (tier == LodTier.Detail) ArtParts.NoShadow(go);
            LodBuilder.Tag(go, tier);
        }

        // ------------------------------------------------------------------ 暖簾

        static void BuildNoren(Transform root, StallVisualRecipe recipe, string displayName, float w, float d, float h)
        {
            var node = ArtParts.Empty("Noren", root, new Vector3(0f, h - 0.08f, d * 0.5f + 0.10f));

            string text = string.IsNullOrEmpty(recipe.NorenText) ? displayName : recipe.NorenText;
            int strips = Mathf.Clamp(recipe.NorenSlits + 1, 1, 8);
            float total = w * 0.92f;
            float gap = 0.018f;
            float stripW = (total - gap * (strips - 1)) / strips;
            float stripH = Mathf.Clamp(h * 0.28f, 0.42f, 0.85f);

            // 暖簾全体に1枚の染め抜きテクスチャを貼り、切れ込みごとに UV をずらす
            var tex = ProceduralTextures.KanjiSign(text, 512, 256, recipe.NorenColor, PickInkColor(recipe.NorenColor));
            var mat = MatsuriMaterials.PrintedFabric(tex, Color.white);
            var mesh = MatsuriMeshes.ClothStrip(stripW, stripH, 4, 5);

            // 上の竿
            ArtParts.Part("Rod", node.transform, MatsuriMeshes.Cylinder(0.024f, total + 0.14f, 8),
                MatsuriMaterials.Wood(Darken(recipe.WoodColor, 0.80f)), new Vector3(0f, 0.02f, 0f), Quaternion.Euler(0f, 0f, 90f));

            for (int i = 0; i < strips; i++)
            {
                float x = -total * 0.5f + stripW * 0.5f + i * (stripW + gap);
                var go = ArtParts.Part("Strip" + i.ToString("00"), node.transform, mesh, mat, new Vector3(x, 0f, 0f), FaceFront);
                ArtParts.NoShadow(go);
                var mr = go.GetComponent<MeshRenderer>();
                // U を反転して割り当てる（SignQuad と同じ理由。素の UV だと鏡文字になる）。
                // 一番左の切れ込み（ローカル +X 側）が屋号の1文字目になるよう、右端から順に割る
                ArtParts.SetTextureOffset(mr, new Vector2(-1f / strips, 1f), new Vector2(1f - i / (float)strips, 0f));
                SwayAnimator.Attach(go, SwayMode.Cloth, 0.035f, 0.9f + i * 0.07f);
            }
        }

        /// <summary>下地が暗ければ白、明るければ墨色を返す。</summary>
        static Color PickInkColor(Color background)
        {
            float lum = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return lum < 0.5f ? new Color(0.96f, 0.94f, 0.90f) : new Color(0.10f, 0.09f, 0.08f);
        }

        // ------------------------------------------------------------------ 看板

        static void BuildSign(Transform root, StallVisualRecipe recipe, string displayName, float w, float d, float h)
        {
            var node = ArtParts.Empty("Sign", root, new Vector3(0f, h + 0.40f, d * 0.5f + 0.06f));
            float bw = Mathf.Clamp(w * 0.74f, 1.0f, 2.6f);
            const float bh = 0.46f;

            var frame = MatsuriMaterials.Wood(Darken(recipe.WoodColor, 0.85f));
            ArtParts.Part("Board", node.transform, MatsuriMeshes.Box(new Vector3(bw, bh, 0.07f)), frame, Vector3.zero);
            ArtParts.Part("BraceL", node.transform, MatsuriMeshes.Box(new Vector3(0.05f, 0.42f, 0.05f)), frame,
                new Vector3(-bw * 0.5f + 0.06f, -bh * 0.5f - 0.20f, 0f), Quaternion.Euler(18f, 0f, 0f));
            ArtParts.Part("BraceR", node.transform, MatsuriMeshes.Box(new Vector3(0.05f, 0.42f, 0.05f)), frame,
                new Vector3(bw * 0.5f - 0.06f, -bh * 0.5f - 0.20f, 0f), Quaternion.Euler(18f, 0f, 0f));

            string text = string.IsNullOrEmpty(recipe.NorenText) ? displayName : recipe.NorenText;
            // 文字は大きめのテクスチャに焼く。近づいたときに輪郭がぼやけないように
            var tex = ProceduralTextures.KanjiSign(text, 1024, 400, recipe.SignBoardColor, recipe.SignTextColor);
            // 看板は屋根より上にあって裸電球の光が届かない。わずかに自発光させて夜でも読めるようにする
            var face = ArtParts.Part("Face", node.transform, MatsuriMeshes.SignQuad(bw - 0.06f, bh - 0.06f),
                MatsuriMaterials.PrintedGlow(tex, Color.white, 1.6f), new Vector3(0f, 0f, 0.037f), FaceFront);
            ArtParts.NoShadow(face);
        }

        // ------------------------------------------------------------------ カウンター

        static void BuildCounter(Transform root, StallVisualRecipe recipe, string displayName, float w, float d, float ch)
        {
            var node = ArtParts.Empty("Counter", root);
            var top = MatsuriMaterials.Wood(recipe.CounterColor);
            var apron = MatsuriMaterials.Planks(Darken(recipe.CounterColor, 0.66f));

            ArtParts.Part("Top", node.transform, MatsuriMeshes.Box(new Vector3(w, 0.07f, 0.55f)), top,
                new Vector3(0f, ch, d * 0.5f - 0.24f));
            // 幕板：カウンター前面を地面近くまで隠す板。板張りにして目地を出す
            float apronH = Mathf.Max(0.2f, ch - 0.12f);
            ArtParts.Part("Apron", node.transform,
                MatsuriMeshes.SlatPanel(w, apronH, 0.055f, 4), apron,
                new Vector3(0f, apronH * 0.5f + 0.06f, d * 0.5f + 0.005f));
            ArtParts.Part("ApronTrim", node.transform, MatsuriMeshes.Box(new Vector3(w, 0.05f, 0.07f)), top,
                new Vector3(0f, ch - 0.06f, d * 0.5f + 0.015f));

            // 前掛け：幕板に屋号を刷った布。正面から一番読みやすい高さに来る
            string text = string.IsNullOrEmpty(recipe.NorenText) ? displayName : recipe.NorenText;
            var tex = ProceduralTextures.KanjiSign(text, 1024, 384, Darken(recipe.NorenColor, 0.92f),
                PickInkColor(recipe.NorenColor));
            float signW = Mathf.Min(w * 0.86f, apronH * 2.6f);
            var sign = ArtParts.Part("ApronSign", node.transform,
                MatsuriMeshes.SignQuad(signW, apronH * 0.66f),
                MatsuriMaterials.PrintedFabric(tex, Color.white),
                new Vector3(0f, apronH * 0.52f, d * 0.5f + 0.038f), FaceFront);
            ArtParts.NoShadow(sign);
            LodBuilder.Tag(sign, LodTier.Medium);

            // 作業台。調理器具はこの上に載る（無いと道具が宙に浮いて見える）。
            // 水槽や景品棚を地面に置く屋台では、置き場が二重になるので作らない
            if (UsesCounterSurface(recipe.Prop))
            {
                // 屋台の内側なので、遠くからは見えない。LOD1 までで十分
                LodBuilder.Tag(ArtParts.Part("WorkTop", node.transform,
                    MatsuriMeshes.Box(new Vector3(w - 0.26f, 0.06f, d * 0.50f)),
                    MatsuriMaterials.Wood(Darken(recipe.CounterColor, 0.88f)),
                    new Vector3(0f, ch - 0.035f, -0.10f)), LodTier.Medium);
                LodBuilder.Tag(ArtParts.Part("WorkSkirt", node.transform,
                    MatsuriMeshes.SlatPanel(w - 0.26f, ch - 0.14f, 0.045f, 3),
                    MatsuriMaterials.Planks(Darken(recipe.CounterColor, 0.58f)),
                    new Vector3(0f, (ch - 0.14f) * 0.5f + 0.02f, -0.10f + d * 0.25f - 0.02f)), LodTier.Medium);
            }
        }

        /// <summary>調理器具をカウンターの作業面に置く種類か。水槽・景品棚は地面置き。</summary>
        static bool UsesCounterSurface(StallPropKind kind)
        {
            switch (kind)
            {
                case StallPropKind.FishTank:
                case StallPropKind.ShootingRack:
                case StallPropKind.YoyoTub:
                case StallPropKind.BallTub:
                    return false;
                default:
                    return true;
            }
        }

        // ------------------------------------------------------------------ 商品見本と調味料

        static void BuildFoodProps(Transform root, StallVisualRecipe recipe, float w, float d, float ch)
        {
            var node = ArtParts.Empty("FoodProps", root, new Vector3(0f, ch + 0.035f, d * 0.5f - 0.26f));
            var product = MatsuriMaterials.Painted(recipe.ProductColor, 0.35f);
            var trayMat = MatsuriMaterials.Painted(new Color(0.94f, 0.92f, 0.86f), 0.3f);
            var woodMat = MatsuriMaterials.Wood(Darken(recipe.CounterColor, 0.82f));

            float xTray = -w * 0.30f, xMid = w * 0.04f, xCase = w * 0.26f;

            // 舟皿3枚。皿と中身をそれぞれ1メッシュにまとめてドローコールを抑える
            var trays = MatsuriMeshes.CombineCached("StallTrays", () =>
            {
                var tray = MatsuriMeshes.TaperedBox(0.17f, 0.12f, 0.22f, 0.16f, 0.05f);
                var list = new List<CombineInstance>(3);
                for (int i = 0; i < 3; i++)
                    list.Add(new CombineInstance
                    {
                        mesh = tray,
                        transform = Matrix4x4.TRS(new Vector3(-0.25f + i * 0.25f, 0.025f, 0f),
                            Quaternion.Euler(0f, i * 9f - 9f, 0f), Vector3.one)
                    });
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Trays", node.transform, trays, trayMat, new Vector3(xTray, 0f, 0f)));

            var foods = MatsuriMeshes.CombineCached("StallTrayFood", () =>
            {
                var food = MatsuriMeshes.Sphere(0.030f, 8, 6);
                var list = new List<CombineInstance>(9);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 3; j++)
                        list.Add(new CombineInstance
                        {
                            mesh = food,
                            transform = Matrix4x4.Translate(new Vector3(
                                -0.25f + i * 0.25f - 0.05f + j * 0.05f, 0.074f, (j % 2) * 0.028f - 0.014f))
                        });
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Food", node.transform, foods, product, new Vector3(xTray, 0f, 0f)));

            // 紙カップと袋詰めの見本
            var cups = MatsuriMeshes.CombineCached("StallCups", () =>
            {
                var cup = MatsuriMeshes.TaperedBox(0.07f, 0.07f, 0.09f, 0.09f, 0.12f);
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = cup, transform = Matrix4x4.Translate(new Vector3(-0.065f, 0.06f, -0.02f)) },
                    new CombineInstance { mesh = cup, transform = Matrix4x4.Translate(new Vector3(0.065f, 0.06f, 0.01f)) }
                };
            });
            ArtParts.NoShadow(ArtParts.Part("Cups", node.transform, cups, trayMat, new Vector3(xMid, 0f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Bag", node.transform, MatsuriMeshes.Sphere(0.075f, 10, 7),
                MatsuriMaterials.Translucent(Color.white, 0.4f, 0.8f), new Vector3(xMid + 0.20f, 0.07f, -0.03f)));

            // 割り箸立て
            ArtParts.NoShadow(ArtParts.Part("ChopstickCup", node.transform,
                MatsuriMeshes.Cylinder(0.045f, 0.13f, 10), woodMat, new Vector3(xMid - 0.20f, 0.065f, -0.06f)));
            var sticks = MatsuriMeshes.CombineCached("StallChopsticks", () =>
            {
                var stick = MatsuriMeshes.Box(new Vector3(0.008f, 0.16f, 0.008f));
                var list = new List<CombineInstance>(7);
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 0.9f;
                    var pos = new Vector3(Mathf.Cos(a) * 0.022f, 0.10f, Mathf.Sin(a) * 0.022f);
                    var rot = Quaternion.Euler(Mathf.Sin(a) * 7f, 0f, -Mathf.Cos(a) * 7f);
                    list.Add(new CombineInstance { mesh = stick, transform = Matrix4x4.TRS(pos, rot, Vector3.one) });
                }
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Chopsticks", node.transform, sticks,
                MatsuriMaterials.Wood(new Color(0.78f, 0.66f, 0.46f)), new Vector3(xMid - 0.20f, 0f, -0.06f)));

            // 見本のケース。浅い木枠に商品を山盛りにして、串を立てる
            var caseMesh = MatsuriMeshes.CombineCached("StallShowcase", () =>
            {
                var rail = MatsuriMeshes.Box(new Vector3(0.42f, 0.055f, 0.022f));
                var railZ = MatsuriMeshes.Box(new Vector3(0.022f, 0.055f, 0.30f));
                var floor = MatsuriMeshes.Box(new Vector3(0.42f, 0.018f, 0.30f));
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = floor, transform = Matrix4x4.Translate(new Vector3(0f, 0.009f, 0f)) },
                    new CombineInstance { mesh = rail, transform = Matrix4x4.Translate(new Vector3(0f, 0.030f, 0.140f)) },
                    new CombineInstance { mesh = rail, transform = Matrix4x4.Translate(new Vector3(0f, 0.030f, -0.140f)) },
                    new CombineInstance { mesh = railZ, transform = Matrix4x4.Translate(new Vector3(0.200f, 0.030f, 0f)) },
                    new CombineInstance { mesh = railZ, transform = Matrix4x4.Translate(new Vector3(-0.200f, 0.030f, 0f)) }
                };
            });
            ArtParts.NoShadow(ArtParts.Part("Showcase", node.transform, caseMesh, woodMat, new Vector3(xCase, 0f, 0f)));
            ArtParts.NoShadow(ArtParts.Part("Mound", node.transform,
                MatsuriMeshes.Hemisphere(0.16f, 14, 6), product,
                new Vector3(xCase, 0.018f, 0f), Quaternion.identity, new Vector3(1.15f, 0.55f, 0.82f)));

            var skewers = MatsuriMeshes.CombineCached("StallSkewers", () =>
            {
                var stick = MatsuriMeshes.Box(new Vector3(0.007f, 0.18f, 0.007f));
                var list = new List<CombineInstance>(5);
                for (int i = 0; i < 5; i++)
                    list.Add(new CombineInstance
                    {
                        mesh = stick,
                        transform = Matrix4x4.TRS(new Vector3(-0.13f + i * 0.065f, 0.115f, -0.02f),
                            Quaternion.Euler(-16f + i * 4f, i * 12f, 6f - i * 3f), Vector3.one)
                    });
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Skewers", node.transform, skewers,
                MatsuriMaterials.Wood(new Color(0.74f, 0.62f, 0.42f)), new Vector3(xCase, 0f, 0f)));
        }

        static void BuildSauceBottle(Transform root, StallVisualRecipe recipe, float w, float d, float ch)
        {
            var node = ArtParts.Empty("SauceBottle", root, new Vector3(w * 0.5f - 0.22f, ch + 0.035f, d * 0.5f - 0.46f));
            var body = MatsuriMeshes.Cylinder(0.035f, 0.19f, 10);
            var colors = new[]
            {
                new Color(0.16f, 0.10f, 0.07f),   // ソース
                new Color(0.92f, 0.90f, 0.84f),   // マヨネーズ
                recipe.ProductColor
            };
            for (int i = 0; i < 3; i++)
            {
                var mat = MatsuriMaterials.Translucent(colors[i], 0.9f, 0.72f);
                ArtParts.NoShadow(ArtParts.Part("Bottle" + i, node.transform, body, mat,
                    new Vector3(-i * 0.10f, 0.095f, i * 0.04f)));
            }
            // 注ぎ口3個は1メッシュにまとめる
            var nozzles = MatsuriMeshes.CombineCached("StallNozzles", () =>
            {
                var cone = MatsuriMeshes.Cone(0.020f, 0.07f, 8);
                var list = new List<CombineInstance>(3);
                for (int i = 0; i < 3; i++)
                    list.Add(new CombineInstance { mesh = cone, transform = Matrix4x4.Translate(new Vector3(-i * 0.10f, 0.222f, i * 0.04f)) });
                return list;
            });
            ArtParts.NoShadow(ArtParts.Part("Nozzles", node.transform, nozzles,
                MatsuriMaterials.Painted(new Color(0.9f, 0.5f, 0.2f), 0.5f), Vector3.zero));
        }

        // ------------------------------------------------------------------ 照明

        static void BuildLightBulbs(Transform root, StallVisualRecipe recipe, float w, float d, float h, float ch)
        {
            // 暖簾の裾より下に電球を吊る。ここが正面の明るさを決める
            float stripH = Mathf.Clamp(h * 0.28f, 0.42f, 0.85f);
            float cordY = Mathf.Max(ch + 0.40f, Mathf.Min(h - 0.22f, h - 0.32f - stripH));
            var node = ArtParts.Empty("LightBulbs", root, new Vector3(0f, cordY, d * 0.5f - 0.10f));

            int count = Mathf.Clamp(recipe.BulbCount, 0, 12);
            float span = Mathf.Max(0.4f, w - 0.44f);
            float sag = Mathf.Clamp(span * 0.035f, 0.03f, 0.10f);
            var cordMat = MatsuriMaterials.Painted(new Color(0.10f, 0.09f, 0.08f), 0.3f);

            // 梁から電線を吊る2本の縦線
            float drop = (h - 0.12f) - cordY;
            var hangers = MatsuriMeshes.CombineCached(
                "StallHanger_" + span.ToString("0.##") + "_" + drop.ToString("0.##"), () =>
            {
                var wire = MatsuriMeshes.Box(new Vector3(0.010f, Mathf.Max(0.05f, drop), 0.010f));
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = wire, transform = Matrix4x4.Translate(new Vector3(-span * 0.5f, drop * 0.5f, 0f)) },
                    new CombineInstance { mesh = wire, transform = Matrix4x4.Translate(new Vector3(span * 0.5f, drop * 0.5f, 0f)) }
                };
            });
            ArtParts.NoShadow(ArtParts.Part("Hangers", node.transform, hangers, cordMat, Vector3.zero));
            ArtParts.NoShadow(ArtParts.Part("Cord", node.transform,
                MatsuriMeshes.SaggingCord(span, sag, 0.008f, 12), cordMat, Vector3.zero));

            if (count > 0)
            {
                string key = count.ToString("00") + "_" + span.ToString("0.##") + "_" + sag.ToString("0.###");
                // ソケットと笠。まとめて1メッシュにする
                var fixtures = MatsuriMeshes.CombineCached("StallBulbRig_" + key, () =>
                {
                    var socket = MatsuriMeshes.Cylinder(0.024f, 0.055f, 8);
                    var shade = MatsuriMeshes.Cone(0.085f, 0.075f, 12);
                    var list = new List<CombineInstance>(count * 2);
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 p = BulbAnchor(i, count, span, sag);
                        list.Add(new CombineInstance { mesh = socket, transform = Matrix4x4.Translate(p + new Vector3(0f, -0.030f, 0f)) });
                        // 笠は円錐を伏せる。下へ光を返して手元を明るく見せる
                        list.Add(new CombineInstance
                        {
                            mesh = shade,
                            transform = Matrix4x4.TRS(p + new Vector3(0f, -0.020f, 0f), Quaternion.identity, Vector3.one)
                        });
                    }
                    return list;
                });
                ArtParts.NoShadow(ArtParts.Part("Fixtures", node.transform, fixtures,
                    MatsuriMaterials.Metal(new Color(0.30f, 0.26f, 0.21f)), Vector3.zero));

                // 電球。実光源は増やせないので、Emission を強めて「光っている」と読ませる
                var bulbs = MatsuriMeshes.CombineCached("StallBulbs_" + key, () =>
                {
                    var bulb = MatsuriMeshes.Sphere(0.052f, 10, 7);
                    var list = new List<CombineInstance>(count);
                    for (int i = 0; i < count; i++)
                        list.Add(new CombineInstance
                        {
                            mesh = bulb,
                            transform = Matrix4x4.TRS(BulbAnchor(i, count, span, sag) + new Vector3(0f, -0.098f, 0f),
                                Quaternion.identity, new Vector3(1f, 1.18f, 1f))
                        });
                    return list;
                });
                ArtParts.NoShadow(ArtParts.Part("Bulbs", node.transform, bulbs,
                    MatsuriMaterials.Emissive(recipe.BulbColor, 22f), Vector3.zero));
            }

            // 実光源は屋台1軒につき1個だけ (§58)。
            // カウンターの真上に置いて、商品・店員・並んでいる客の顔をまとめて照らす
            var lightGo = ArtParts.Empty("StallPointLight", node.transform, new Vector3(0f, -0.14f, -0.18f));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = recipe.BulbColor;
            light.range = Mathf.Max(w, d) * 2.8f + 3.5f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            // HDRP は実光量を HDAdditionalLightData 側で持つ（既定の単位はルーメン）
            var hd = lightGo.AddComponent<HDAdditionalLightData>();
            hd.intensity = Mathf.Max(1f, recipe.LightIntensity);
        }

        /// <summary>i 番目の電球を吊る、電線上の位置。</summary>
        static Vector3 BulbAnchor(int i, int count, float span, float sag)
        {
            float t = count == 1 ? 0.5f : (i + 0.5f) / count;
            return new Vector3(-span * 0.5f + span * t, MatsuriMeshes.CordSagY(t, sag), 0f);
        }

        static void BuildLanterns(Transform root, StallVisualRecipe recipe, float w, float d, float h)
        {
            var node = ArtParts.Empty("Lantern", root);
            int count = Mathf.Clamp(recipe.LanternCount, 0, 8);
            if (count == 0) return;

            var paper = MatsuriMaterials.GlowingPaper(recipe.LanternColor, 6.5f);
            var wood = MatsuriMaterials.Wood(new Color(0.22f, 0.18f, 0.14f));
            var mesh = MatsuriMeshes.Lantern(0.16f, 0.42f, 16);
            var cord = MatsuriMeshes.Cylinder(0.005f, 0.20f, 5);

            for (int i = 0; i < count; i++)
            {
                float t = count == 1 ? 0.5f : i / (float)(count - 1);
                float x = Mathf.Lerp(-w * 0.5f + 0.10f, w * 0.5f - 0.10f, t);
                var pivot = ArtParts.Empty("Lantern" + i.ToString("00"), node.transform,
                    new Vector3(x, h + 0.34f, d * 0.5f + 0.30f));
                var sway = SwayAnimator.Attach(pivot, SwayMode.Rotate, 5.5f, 0.55f + i * 0.09f);
                sway.Axis = Vector3.forward;
                ArtParts.NoShadow(ArtParts.Part("Cord", pivot.transform, cord, wood, new Vector3(0f, -0.10f, 0f)));
                ArtParts.NoShadow(ArtParts.Part("Body", pivot.transform, mesh, paper, new Vector3(0f, -0.41f, 0f)));
            }
        }

        // ------------------------------------------------------------------ 品書き

        /// <summary>側面の板張りに品書きの紙を貼る。のっぺりした側面に情報を作る。</summary>
        static void BuildSideMenu(Transform root, StallVisualRecipe recipe, float w, float d, float h, float ch)
        {
            var node = ArtParts.Empty("SideMenu", root);
            var items = MenuItems(recipe.Prop);
            var paper = new Color(0.93f, 0.89f, 0.78f);
            var ink = new Color(0.12f, 0.10f, 0.09f);
            var tex = ProceduralTextures.MenuPaper("品書", items, 384, 512, paper, ink);
            var mat = MatsuriMaterials.Printed(tex, Color.white);

            float pw = Mathf.Min(d * 0.42f, 0.62f);
            float ph = pw * 1.34f;
            float y = Mathf.Clamp(ch + 0.55f, 0.9f, h - 0.45f);
            float x = w * 0.5f - 0.015f;

            // 側板の外側に、左右へ向けて貼る
            var l = ArtParts.Part("MenuL", node.transform, MatsuriMeshes.SignQuad(pw, ph), mat,
                new Vector3(-x, y, -d * 0.06f), Quaternion.Euler(0f, -90f, 0f));
            var r = ArtParts.Part("MenuR", node.transform, MatsuriMeshes.SignQuad(pw, ph), mat,
                new Vector3(x, y, -d * 0.06f), Quaternion.Euler(0f, 90f, 0f));
            ArtParts.NoShadow(l);
            ArtParts.NoShadow(r);

            // 右側にはもう1枚。左右で枚数を変えると「貼り足した」感じが出る
            var r2 = ArtParts.Part("MenuR2", node.transform, MatsuriMeshes.SignQuad(pw * 0.86f, ph * 0.86f), mat,
                new Vector3(x, y - ph * 0.50f, d * 0.24f), Quaternion.Euler(0f, 90f, 0f));
            ArtParts.NoShadow(r2);
        }

        /// <summary>屋台の中身から品書きの内容を決める。</summary>
        static string[] MenuItems(StallPropKind kind)
        {
            switch (kind)
            {
                case StallPropKind.TakoyakiPlate: return new[] { "たこ焼 六個", "たこ焼 八個", "青のり増" };
                case StallPropKind.Teppan: return new[] { "焼そば 並", "焼そば 大", "目玉焼付" };
                case StallPropKind.IceShaver: return new[] { "いちご", "れもん", "宇治金時" };
                case StallPropKind.CandyAppleRack: return new[] { "りんご飴", "姫りんご", "いちご飴" };
                case StallPropKind.CottonCandyMachine: return new[] { "わた飴", "特大わた飴", "袋入り" };
                case StallPropKind.Grill: return new[] { "フランク", "チーズ入", "からし付" };
                case StallPropKind.FishTank: return new[] { "金魚すくい", "ポイ一枚", "持ち帰り袋" };
                case StallPropKind.ShootingRack: return new[] { "射的 五発", "景品交換", "特賞あり" };
                case StallPropKind.YoyoTub: return new[] { "ヨーヨー", "こより付", "一回一つ" };
                case StallPropKind.BallTub: return new[] { "すくい", "三個まで", "袋つき" };
                case StallPropKind.KatanukiDesk: return new[] { "型抜き", "成功で賞金", "針は貸出" };
                default: return new[] { "各種あり", "できたて", "おひとつ" };
            }
        }

        // ------------------------------------------------------------------ 湯気・煙

        static void BuildSteam(Transform root, StallData data, StallVisualRecipe recipe, float ch)
        {
            var node = ArtParts.Empty("SteamVFX", root, new Vector3(0f, ch + 0.22f, -0.10f));
            var ps = node.AddComponent<ParticleSystem>();
            var renderer = node.GetComponent<ParticleSystemRenderer>();

            bool enabled = data == null || data.HasSteam;
            Color color = new Color(0.90f, 0.90f, 0.92f, 0.30f);
            float rate = 6f, life = 2.4f, size = 0.34f, speed = 0.42f, gravity = -0.02f;

            switch (recipe.Prop)
            {
                case StallPropKind.TakoyakiPlate:
                case StallPropKind.Teppan:
                    color = new Color(0.94f, 0.92f, 0.88f, 0.34f); rate = 9f; speed = 0.55f; break;
                case StallPropKind.Grill:
                    color = new Color(0.72f, 0.70f, 0.68f, 0.34f); rate = 7f; speed = 0.60f; size = 0.30f; break;
                case StallPropKind.IceShaver:
                    // 冷気は下に垂れる
                    color = new Color(0.82f, 0.92f, 1.00f, 0.28f); rate = 5f; speed = 0.10f; gravity = 0.05f; size = 0.22f; life = 1.6f; break;
                case StallPropKind.CottonCandyMachine:
                    color = new Color(1.00f, 0.95f, 0.98f, 0.22f); rate = 4f; speed = 0.30f; size = 0.20f; break;
                case StallPropKind.FishTank:
                case StallPropKind.YoyoTub:
                case StallPropKind.BallTub:
                    // 水物は湯気ではなく、水面のきらめきを少しだけ
                    color = new Color(0.75f, 0.95f, 1.00f, 0.22f); rate = 2.5f; speed = 0.08f; size = 0.07f; life = 1.2f;
                    node.transform.localPosition = new Vector3(0f, 0.42f, 0.10f);
                    break;
                case StallPropKind.CandyAppleRack:
                case StallPropKind.ShootingRack:
                case StallPropKind.KatanukiDesk:
                case StallPropKind.None:
                    enabled = false; break;
            }

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 48;

            var emission = ps.emission;
            emission.rateOverTime = enabled ? rate : 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.7f, 0.05f, 0.4f);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.9f));

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.22f;
            noise.frequency = 0.4f;

            renderer.sharedMaterial = MatsuriMaterials.Particle(Color.white);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = -2f;

            if (enabled) ps.Play();
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ------------------------------------------------------------------ 行列と音

        static void BuildQueuePoints(Transform root, StallVisualRecipe recipe, float d)
        {
            int count = Mathf.Clamp(recipe.QueuePointCount, 1, 24);
            float spacing = Mathf.Max(0.4f, recipe.QueueSpacing);
            float z0 = d * 0.5f + 0.95f;
            for (int i = 0; i < count; i++)
            {
                // 一直線だと機械的なので、わずかに左右へずらす
                float offsetX = ((i % 3) - 1) * 0.10f;
                ArtParts.Empty("QueuePoint" + (i + 1).ToString("00"), root,
                    new Vector3(offsetX, 0f, z0 + i * spacing));
            }
        }

        static void BuildAudioSource(Transform root, float h)
        {
            var node = ArtParts.Empty("AudioSource", root, new Vector3(0f, h * 0.6f, 0f));
            var src = node.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 2.5f;
            src.maxDistance = 24f;
            src.dopplerLevel = 0f;
            src.volume = 0.6f;
        }
    }
}

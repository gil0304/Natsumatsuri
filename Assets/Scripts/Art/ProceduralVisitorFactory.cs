using Matsuri.Data;
using UnityEngine;
using MRandom = Unity.Mathematics.Random;

namespace Matsuri.Art
{
    /// <summary>
    /// §79「無表情NPCが直立している」を避けるための来場者ビルダー。
    /// 体は身長1.0mを基準に組み立て、root のスケールで体格を変える。
    /// これによりメッシュもマテリアルも全員で共有でき、色だけ
    /// MaterialPropertyBlock で差し替える（ドローコール対策 §58）。
    /// 子オブジェクト名 Head / Body / ArmL / ArmR / LegL / LegR は
    /// ProceduralWalkAnimator が名前で探すので変えないこと。
    /// </summary>
    public static class ProceduralVisitorFactory
    {
        // 身長1.0mを基準にした各部の高さ
        const float HipY = 0.46f;
        const float ShoulderY = 0.80f;
        const float HeadY = 0.905f;
        const float HeadR = 0.072f;
        const float ShoulderW = 0.113f;
        const float HipW = 0.055f;

        static readonly Color[] FallbackOutfit =
        {
            new Color(0.24f, 0.36f, 0.60f), new Color(0.70f, 0.26f, 0.30f),
            new Color(0.28f, 0.48f, 0.44f), new Color(0.86f, 0.80f, 0.66f),
            new Color(0.44f, 0.30f, 0.52f), new Color(0.90f, 0.62f, 0.35f)
        };
        static readonly Color[] FallbackSkin =
        {
            new Color(0.96f, 0.83f, 0.72f), new Color(0.90f, 0.74f, 0.60f),
            new Color(0.80f, 0.62f, 0.47f), new Color(0.68f, 0.50f, 0.38f)
        };
        static readonly Color[] FallbackHair =
        {
            new Color(0.10f, 0.09f, 0.09f), new Color(0.19f, 0.13f, 0.10f),
            new Color(0.33f, 0.22f, 0.14f), new Color(0.46f, 0.36f, 0.26f)
        };

        static Color Pick(Color[] table, Color[] fallback, ref MRandom rng)
        {
            var src = (table != null && table.Length > 0) ? table : fallback;
            return src[rng.NextInt(0, src.Length)];
        }

        public static GameObject Build(VisitorArchetype archetype, ref MRandom rng, Transform parent)
        {
            float height = archetype != null
                ? Mathf.Clamp(archetype.BodyHeight.Sample(ref rng), 0.9f, 2.1f)
                : 1.65f;

            Color outfit = Pick(archetype != null ? archetype.OutfitColors : null, FallbackOutfit, ref rng);
            Color skin = Pick(archetype != null ? archetype.SkinColors : null, FallbackSkin, ref rng);
            Color hair = Pick(archetype != null ? archetype.HairColors : null, FallbackHair, ref rng);
            Color obi = ObiColor(outfit, ref rng);

            string name = archetype != null && !string.IsNullOrEmpty(archetype.DisplayName)
                ? "Visitor_" + archetype.DisplayName
                : "Visitor";

            var root = ArtParts.Empty(name, parent);
            root.transform.localScale = Vector3.one * height;

            // 全員で共有するマテリアル。色は MaterialPropertyBlock で変える
            var clothMat = MatsuriMaterials.Fabric(Color.white);
            var skinMat = MatsuriMaterials.Skin(Color.white);
            var hairMat = MatsuriMaterials.Painted(Color.white, 0.25f);

            var body = BuildBody(root.transform, clothMat, skinMat, hairMat, outfit, obi, skin, hair, height, ref rng);
            BuildLegs(root.transform, clothMat, skinMat, outfit, skin, ref rng);
            BuildAccessory(body, root.transform, clothMat, outfit, ref rng);

            var anim = root.AddComponent<ProceduralWalkAnimator>();
            anim.StrideLength = 0.58f * height;
            anim.LegSwing = 28f + rng.NextFloat(0f, 9f);
            anim.ArmSwing = 20f + rng.NextFloat(0f, 9f);
            anim.Bounce = 0.030f + rng.NextFloat(0f, 0.012f);
            anim.Roll = 2.6f + rng.NextFloat(0f, 2.2f);
            anim.SetIdle(true);

            LodBuilder.AddLod(root, new[] { 0.16f, 0.055f, 0.008f });
            return root;
        }

        static Color ObiColor(Color outfit, ref MRandom rng)
        {
            Color.RGBToHSV(outfit, out float h, out float s, out float v);
            h = Mathf.Repeat(h + rng.NextFloat(0.35f, 0.65f), 1f);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * 0.9f + 0.15f), Mathf.Clamp01(v * 0.55f + 0.35f));
        }

        // ------------------------------------------------------------------ 胴・頭・腕

        static Transform BuildBody(Transform root, Material cloth, Material skinMat, Material hairMat,
            Color outfit, Color obi, Color skin, Color hair, float height, ref MRandom rng)
        {
            var body = ArtParts.Empty("Body", root, new Vector3(0f, HipY, 0f));

            // 浴衣・甚平の胴。裾が広がる台形
            var torso = ArtParts.Part("Torso", body.transform,
                MatsuriMeshes.TaperedBox(0.300f, 0.196f, 0.236f, 0.150f, 0.520f), cloth, new Vector3(0f, 0.100f, 0f));
            ArtParts.SetColor(torso, outfit);

            // 帯を1本
            var obiGo = ArtParts.Part("Obi", body.transform, MatsuriMeshes.Box(new Vector3(0.268f, 0.058f, 0.178f)), cloth, new Vector3(0f, 0.162f, 0f));
            ArtParts.SetColor(obiGo, obi);
            var knot = ArtParts.Part("ObiKnot", body.transform, MatsuriMeshes.Box(new Vector3(0.10f, 0.070f, 0.055f)), cloth, new Vector3(0f, 0.170f, -0.105f));
            ArtParts.SetColor(knot, obi);

            // 襟
            var collar = ArtParts.Part("Collar", body.transform, MatsuriMeshes.TaperedBox(0.22f, 0.148f, 0.16f, 0.112f, 0.06f), cloth, new Vector3(0f, 0.352f, 0f));
            ArtParts.SetColor(collar, Color.Lerp(outfit, Color.white, 0.55f));

            // 首
            var neck = ArtParts.Part("Neck", body.transform, MatsuriMeshes.Cylinder(0.030f, 0.055f, 8), skinMat, new Vector3(0f, 0.392f, 0f));
            ArtParts.SetColor(neck, skin);

            BuildHead(body.transform, skinMat, hairMat, skin, hair, height, ref rng);
            BuildArm(body.transform, "ArmL", -1f, cloth, skinMat, outfit, skin);
            BuildArm(body.transform, "ArmR", 1f, cloth, skinMat, outfit, skin);
            return body.transform;
        }

        static void BuildHead(Transform body, Material skinMat, Material hairMat, Color skin, Color hair,
            float height, ref MRandom rng)
        {
            // 背が低いほど頭を大きく見せる（子どもらしさ）
            float headScale = Mathf.Clamp(1f + (1.62f - height) * 0.22f, 0.92f, 1.22f);
            var head = ArtParts.Empty("Head", body, new Vector3(0f, ShoulderY - HipY + 0.045f, 0f));
            head.transform.localScale = Vector3.one * headScale;

            float cy = HeadY - ShoulderY - 0.045f + 0.012f;
            var skull = ArtParts.Part("Skull", head.transform, MatsuriMeshes.Sphere(HeadR, 14, 9), skinMat,
                new Vector3(0f, cy, 0f), Quaternion.identity, new Vector3(1f, 1.14f, 0.96f));
            ArtParts.SetColor(skull, skin);

            // 目（点2つでも「直立の無表情」からは抜け出せる）
            var eye = MatsuriMeshes.Sphere(0.0105f, 6, 4);
            var eyeMat = MatsuriMaterials.Painted(Color.white, 0.2f);
            for (int i = 0; i < 2; i++)
            {
                var go = ArtParts.NoShadow(ArtParts.Part("Eye" + i, head.transform, eye, eyeMat,
                    new Vector3((i == 0 ? -1f : 1f) * 0.027f, cy + 0.008f, HeadR * 0.90f)));
                ArtParts.SetColor(go, new Color(0.09f, 0.08f, 0.08f));
            }

            // 髪型3種
            int style = rng.NextInt(0, 3);
            var cap = ArtParts.Part("Hair", head.transform, MatsuriMeshes.Hemisphere(HeadR * 1.06f, 14, 6), hairMat,
                new Vector3(0f, cy + 0.004f, 0f), Quaternion.identity, new Vector3(1f, 1.02f, 0.99f));
            ArtParts.SetColor(cap, hair);
            var fringe = ArtParts.Part("HairFringe", head.transform, MatsuriMeshes.Box(new Vector3(HeadR * 1.9f, 0.030f, 0.030f)), hairMat,
                new Vector3(0f, cy + HeadR * 0.52f, HeadR * 0.80f));
            ArtParts.SetColor(fringe, hair);

            if (style == 1)
            {
                var tail = ArtParts.Part("HairTail", head.transform, MatsuriMeshes.Capsule(0.030f, 0.17f, 8), hairMat,
                    new Vector3(0f, cy - 0.045f, -HeadR * 0.95f), Quaternion.Euler(-16f, 0f, 0f));
                ArtParts.SetColor(tail, hair);
            }
            else if (style == 2)
            {
                var bun = ArtParts.Part("HairBun", head.transform, MatsuriMeshes.Sphere(0.042f, 10, 7), hairMat,
                    new Vector3(0f, cy + HeadR * 1.05f, -0.020f));
                ArtParts.SetColor(bun, hair);
            }
        }

        static void BuildArm(Transform body, string name, float side, Material cloth, Material skinMat,
            Color outfit, Color skin)
        {
            var arm = ArtParts.Empty(name, body, new Vector3(side * ShoulderW, ShoulderY - HipY - 0.02f, 0f));
            // 袖（浴衣らしく袂を作る）
            var sleeve = ArtParts.Part("Sleeve", arm.transform,
                MatsuriMeshes.TaperedBox(0.086f, 0.086f, 0.104f, 0.104f, 0.185f), cloth, new Vector3(0f, -0.092f, 0f));
            ArtParts.SetColor(sleeve, outfit);
            // 前腕
            var fore = ArtParts.Part("Forearm", arm.transform, MatsuriMeshes.Capsule(0.026f, 0.175f, 8), skinMat, new Vector3(0f, -0.272f, 0f));
            ArtParts.SetColor(fore, skin);
            var hand = ArtParts.NoShadow(ArtParts.Part("Hand", arm.transform, MatsuriMeshes.Sphere(0.029f, 8, 6), skinMat, new Vector3(0f, -0.352f, 0f)));
            ArtParts.SetColor(hand, skin);
        }

        // ------------------------------------------------------------------ 脚

        static void BuildLegs(Transform root, Material cloth, Material skinMat, Color outfit, Color skin, ref MRandom rng)
        {
            bool wearsHakama = rng.NextFloat() < 0.35f;   // 裾の長い浴衣
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var leg = ArtParts.Empty(i == 0 ? "LegL" : "LegR", root, new Vector3(side * HipW, HipY, 0f));

                if (wearsHakama)
                {
                    // 裾の長い浴衣。脚は脛から下だけ見える
                    var lower = ArtParts.Part("Hakama", leg.transform, MatsuriMeshes.Capsule(0.054f, 0.30f, 8), cloth, new Vector3(0f, -0.155f, 0f));
                    ArtParts.SetColor(lower, outfit);
                }
                var shin = ArtParts.Part("Shin", leg.transform,
                    wearsHakama ? MatsuriMeshes.Capsule(0.034f, 0.20f, 8) : MatsuriMeshes.Capsule(0.036f, 0.42f, 8),
                    skinMat, new Vector3(0f, wearsHakama ? -0.335f : -0.215f, 0f));
                ArtParts.SetColor(shin, skin);

                // 下駄
                var geta = ArtParts.NoShadow(ArtParts.Part("Geta", leg.transform, MatsuriMeshes.Box(new Vector3(0.072f, 0.028f, 0.135f)),
                    cloth, new Vector3(0f, -0.446f, 0.016f)));
                ArtParts.SetColor(geta, new Color(0.62f, 0.50f, 0.36f));
            }
        }

        // ------------------------------------------------------------------ 小物

        static void BuildAccessory(Transform body, Transform root, Material cloth, Color outfit, ref MRandom rng)
        {
            var props = ArtParts.Empty("Props", root);
            int kind = rng.NextInt(0, 5);   // 0=なし 1=うちわ 2=お面 3=風船 4=巾着
            var handR = body.Find("ArmR");
            var head = body.Find("Head");
            Color accent = Color.HSVToRGB(rng.NextFloat(0f, 1f), 0.72f, 0.92f);

            switch (kind)
            {
                case 1:
                {
                    // うちわ（右手）
                    var pivot = ArtParts.Empty("Uchiwa", handR != null ? handR : props.transform,
                        new Vector3(0.02f, -0.38f, 0.03f), Quaternion.Euler(72f, 0f, 12f));
                    var handle = ArtParts.NoShadow(ArtParts.Part("Handle", pivot.transform, MatsuriMeshes.Cylinder(0.006f, 0.10f, 5), cloth, new Vector3(0f, 0.05f, 0f)));
                    ArtParts.SetColor(handle, new Color(0.75f, 0.66f, 0.48f));
                    var fan = ArtParts.NoShadow(ArtParts.Part("Fan", pivot.transform, MatsuriMeshes.Cylinder(0.078f, 0.006f, 14), cloth, new Vector3(0f, 0.155f, 0f)));
                    ArtParts.SetColor(fan, accent);
                    break;
                }
                case 2:
                {
                    // お面（頭の横にずらして掛ける）
                    var pivot = ArtParts.Empty("Mask", head != null ? head : props.transform,
                        new Vector3(0.070f, 0.055f, 0.010f), Quaternion.Euler(0f, 78f, 0f));
                    var face = ArtParts.NoShadow(ArtParts.Part("Face", pivot.transform, MatsuriMeshes.Hemisphere(0.062f, 12, 5), cloth,
                        Vector3.zero, Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 0.55f, 1.05f)));
                    ArtParts.SetColor(face, accent);
                    break;
                }
                case 3:
                {
                    // 風船（右手から上へ）
                    var pivot = ArtParts.Empty("Balloon", handR != null ? handR : props.transform, new Vector3(0.01f, -0.37f, 0.02f));
                    SwayAnimator.Attach(pivot, SwayMode.Rotate, 7f, 0.75f).Axis = Vector3.forward;
                    var str = ArtParts.NoShadow(ArtParts.Part("String", pivot.transform, MatsuriMeshes.Cylinder(0.0022f, 0.34f, 4), cloth, new Vector3(0f, 0.17f, 0f)));
                    ArtParts.SetColor(str, new Color(0.92f, 0.92f, 0.92f));
                    var ball = ArtParts.NoShadow(ArtParts.Part("Ball", pivot.transform, MatsuriMeshes.Sphere(0.082f, 12, 8), cloth,
                        new Vector3(0f, 0.415f, 0f), Quaternion.identity, new Vector3(1f, 1.15f, 1f)));
                    ArtParts.SetColor(ball, accent);
                    break;
                }
                case 4:
                {
                    // 巾着（左手）
                    var handL = body.Find("ArmL");
                    var pivot = ArtParts.Empty("Kinchaku", handL != null ? handL : props.transform, new Vector3(0f, -0.40f, 0.02f));
                    SwayAnimator.Attach(pivot, SwayMode.Rotate, 4.5f, 1.1f).Axis = Vector3.right;
                    var bag = ArtParts.NoShadow(ArtParts.Part("Bag", pivot.transform, MatsuriMeshes.Sphere(0.055f, 10, 7), cloth,
                        new Vector3(0f, -0.055f, 0f), Quaternion.identity, new Vector3(1f, 0.85f, 0.75f)));
                    ArtParts.SetColor(bag, Color.Lerp(outfit, accent, 0.5f));
                    break;
                }
            }
        }
    }
}

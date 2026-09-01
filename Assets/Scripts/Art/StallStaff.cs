using System.Collections.Generic;
using Matsuri.Data;
using UnityEngine;

namespace Matsuri.Art
{
    /// <summary>
    /// 屋台の店員 (§23 StaffPosition)。カウンターの内側に1体だけ立たせる。
    ///
    /// 「屋台の正面を見たときに中身が読み取れない」対策として、
    /// カウンターの向こうに人がいることをはっきり見せるのが役目。
    /// 動かないので Animator も更新処理も持たない。
    ///
    /// 来場者 (<see cref="ProceduralVisitorFactory"/>) とは担当が別なので、
    /// 人型はこのファイルで独自に組み立てる。骨格は
    /// 胴 / 頭 / 腕 / 前掛け / 鉢巻き の5点セット。
    /// 足元が y=0、正面が +Z（お客側）。
    /// </summary>
    public static class StallStaff
    {
        // 身長1.0mを基準にした各部の高さ。root のスケールで身長を変える
        const float FootTop = 0.038f;
        const float ShortsY = 0.420f;
        const float WaistY = 0.560f;
        const float ShoulderY = 0.830f;
        const float NeckY = 0.858f;
        const float HeadY = 0.925f;
        const float HeadR = 0.068f;

        /// <summary>
        /// 店員を1体組み立てる。localPos は足元の位置。
        /// </summary>
        public static GameObject Build(Transform parent, StallVisualRecipe recipe, string displayName, Vector3 localPos)
        {
            float height = 1.66f;
            var root = ArtParts.Empty("Staff", parent, localPos);
            root.transform.localScale = Vector3.one * height;

            Color happi = recipe != null ? recipe.NorenColor : new Color(0.70f, 0.12f, 0.12f);
            Color ink = PickInk(happi);
            Color skin = new Color(0.93f, 0.78f, 0.65f);
            Color hair = new Color(0.11f, 0.10f, 0.10f);
            Color obi = Mix(happi, new Color(0.14f, 0.12f, 0.10f), 0.55f);
            Color band = Mix(happi, Color.white, 0.72f);

            // 全身で共有するマテリアル。色は MaterialPropertyBlock で振り分ける (§58)
            var cloth = MatsuriMaterials.Fabric(Color.white);
            var skinMat = MatsuriMaterials.Skin(Color.white);
            var hairMat = MatsuriMaterials.Painted(Color.white, 0.22f);

            BuildLegs(root.transform, cloth, skinMat, happi, skin);
            BuildTorso(root.transform, cloth, happi, obi);
            BuildApron(root.transform, recipe, displayName, happi, ink);
            BuildArms(root.transform, cloth, skinMat, happi, skin);
            BuildHead(root.transform, skinMat, hairMat, cloth, skin, hair, band);

            // 近づいたときの見え方を作る部品。遠景では落とす
            LodBuilder.Tag(root, LodTier.Medium);
            return root;
        }

        static Color PickInk(Color background)
        {
            float lum = background.r * 0.299f + background.g * 0.587f + background.b * 0.114f;
            return lum < 0.5f ? new Color(0.96f, 0.94f, 0.90f) : new Color(0.10f, 0.09f, 0.08f);
        }

        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, t);

        // ------------------------------------------------------------------ 脚

        static void BuildLegs(Transform root, Material cloth, Material skinMat, Color happi, Color skin)
        {
            // 甚平の短パン。裾がわずかに広がる
            var shorts = ArtParts.Part("Shorts", root,
                MatsuriMeshes.TaperedBox(0.150f, 0.118f, 0.166f, 0.128f, 0.190f), cloth,
                new Vector3(0f, ShortsY - 0.010f, 0f));
            ArtParts.SetColor(shorts, Mix(happi, new Color(0.10f, 0.10f, 0.12f), 0.62f));

            // 素足（膝から下）
            var shin = MatsuriMeshes.Cylinder(0.032f, ShortsY - FootTop, 8);
            float legY = (ShortsY + FootTop) * 0.5f;
            var l = ArtParts.Part("LegL", root, shin, skinMat, new Vector3(-0.040f, legY, 0f));
            var r = ArtParts.Part("LegR", root, shin, skinMat, new Vector3(0.040f, legY, 0f));
            ArtParts.SetColor(l, skin);
            ArtParts.SetColor(r, skin);

            // 地下足袋。左右を1メッシュにまとめる
            var boots = MatsuriMeshes.CombineCached("StaffTabi", () =>
            {
                var foot = MatsuriMeshes.Box(new Vector3(0.070f, FootTop * 2f, 0.145f));
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = foot, transform = Matrix4x4.TRS(new Vector3(-0.040f, FootTop, 0.018f), Quaternion.Euler(0f, -5f, 0f), Vector3.one) },
                    new CombineInstance { mesh = foot, transform = Matrix4x4.TRS(new Vector3(0.040f, FootTop, 0.018f), Quaternion.Euler(0f, 5f, 0f), Vector3.one) }
                };
            });
            var tabi = ArtParts.Part("Tabi", root, boots, cloth, Vector3.zero);
            ArtParts.SetColor(tabi, new Color(0.14f, 0.14f, 0.16f));
        }

        // ------------------------------------------------------------------ 胴

        static void BuildTorso(Transform root, Material cloth, Color happi, Color obi)
        {
            // 法被。肩から裾に向かってわずかに広がる
            var torso = ArtParts.Part("Torso", root,
                MatsuriMeshes.TaperedBox(0.206f, 0.150f, 0.188f, 0.136f, ShoulderY - WaistY + 0.135f), cloth,
                new Vector3(0f, (WaistY + ShoulderY) * 0.5f - 0.020f, 0f));
            ArtParts.SetColor(torso, happi);

            // 襟。白く抜くと胴の輪郭が立つ
            var collar = MatsuriMeshes.CombineCached("StaffCollar", () =>
            {
                var strip = MatsuriMeshes.Box(new Vector3(0.030f, 0.230f, 0.022f));
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = strip, transform = Matrix4x4.TRS(new Vector3(-0.042f, 0f, 0.070f), Quaternion.Euler(0f, 0f, 9f), Vector3.one) },
                    new CombineInstance { mesh = strip, transform = Matrix4x4.TRS(new Vector3(0.042f, 0f, 0.070f), Quaternion.Euler(0f, 0f, -9f), Vector3.one) }
                };
            });
            var collarGo = ArtParts.Part("Collar", root, collar, cloth, new Vector3(0f, ShoulderY - 0.090f, 0f));
            ArtParts.SetColor(collarGo, Mix(happi, Color.white, 0.80f));

            // 帯
            var obiGo = ArtParts.Part("Obi", root, MatsuriMeshes.Box(new Vector3(0.200f, 0.052f, 0.148f)), cloth,
                new Vector3(0f, WaistY + 0.010f, 0f));
            ArtParts.SetColor(obiGo, obi);
        }

        // ------------------------------------------------------------------ 前掛け

        static void BuildApron(Transform root, StallVisualRecipe recipe, string displayName, Color happi, Color ink)
        {
            string text = recipe != null && !string.IsNullOrEmpty(recipe.NorenText) ? recipe.NorenText : displayName;
            if (string.IsNullOrEmpty(text)) text = "祭";

            // 屋号を縦に染め抜いた前掛け。正面から一番読みやすい位置に来る
            var tex = ProceduralTextures.KanjiSign(text, 192, 320, Mix(happi, Color.black, 0.30f), ink, true);
            var mat = MatsuriMaterials.PrintedFabric(tex, Color.white);
            var apron = ArtParts.Part("Apron", root, MatsuriMeshes.SignQuad(0.190f, 0.245f), mat,
                new Vector3(0f, WaistY - 0.095f, 0.082f));
            ArtParts.NoShadow(apron);

            // 前掛けの紐
            var cordMat = MatsuriMaterials.Fabric(Color.white);
            var cord = ArtParts.Part("ApronCord", root, MatsuriMeshes.Box(new Vector3(0.210f, 0.014f, 0.150f)), cordMat,
                new Vector3(0f, WaistY + 0.032f, 0f));
            ArtParts.SetColor(cord, Mix(happi, Color.black, 0.45f));
            ArtParts.NoShadow(cord);
        }

        // ------------------------------------------------------------------ 腕

        static void BuildArms(Transform root, Material cloth, Material skinMat, Color happi, Color skin)
        {
            // 両腕とも軽く前に出す。「仕事中」に見せるため左右で角度を変える
            BuildArm(root, "ArmL", -1f, -26f, cloth, skinMat, happi, skin);
            BuildArm(root, "ArmR", 1f, -44f, cloth, skinMat, happi, skin);
        }

        static void BuildArm(Transform root, string name, float side, float pitch,
            Material cloth, Material skinMat, Color happi, Color skin)
        {
            var pivot = ArtParts.Empty(name, root,
                new Vector3(side * 0.108f, ShoulderY - 0.012f, 0f),
                Quaternion.Euler(pitch, 0f, side * 7f));

            // 袖（法被）
            var sleeve = ArtParts.Part("Sleeve", pivot.transform,
                MatsuriMeshes.TaperedBox(0.072f, 0.076f, 0.086f, 0.090f, 0.150f), cloth,
                new Vector3(0f, -0.070f, 0f));
            ArtParts.SetColor(sleeve, happi);

            // 前腕と手を1メッシュにまとめる
            var limb = MatsuriMeshes.CombineCached("StaffForearm", () => new List<CombineInstance>
            {
                new CombineInstance
                {
                    mesh = MatsuriMeshes.Cylinder(0.026f, 0.155f, 8),
                    transform = Matrix4x4.Translate(new Vector3(0f, -0.078f, 0f))
                },
                new CombineInstance
                {
                    mesh = MatsuriMeshes.Sphere(0.034f, 8, 6),
                    transform = Matrix4x4.TRS(new Vector3(0f, -0.166f, 0.006f), Quaternion.identity, new Vector3(1f, 0.82f, 1.1f))
                }
            });
            var arm = ArtParts.Part("Forearm", pivot.transform, limb, skinMat, new Vector3(0f, -0.140f, 0f));
            ArtParts.SetColor(arm, skin);
        }

        // ------------------------------------------------------------------ 頭

        static void BuildHead(Transform root, Material skinMat, Material hairMat, Material cloth,
            Color skin, Color hair, Color band)
        {
            var head = ArtParts.Empty("Head", root, new Vector3(0f, NeckY, 0f));

            var neck = ArtParts.Part("Neck", head.transform, MatsuriMeshes.Cylinder(0.028f, 0.050f, 8), skinMat,
                new Vector3(0f, 0.010f, 0f));
            ArtParts.SetColor(neck, skin);

            float cy = HeadY - NeckY;
            var skull = ArtParts.Part("Skull", head.transform, MatsuriMeshes.Sphere(HeadR, 14, 9), skinMat,
                new Vector3(0f, cy, 0f), Quaternion.identity, new Vector3(1f, 1.12f, 0.96f));
            ArtParts.SetColor(skull, skin);

            var cap = ArtParts.Part("Hair", head.transform, MatsuriMeshes.Hemisphere(HeadR * 1.03f, 14, 6), hairMat,
                new Vector3(0f, cy + 0.004f, -0.004f), Quaternion.identity, new Vector3(1f, 0.92f, 1.02f));
            ArtParts.SetColor(cap, hair);

            // 目。点2つでも「のっぺらぼう」からは抜け出せる
            var eyes = MatsuriMeshes.CombineCached("StaffEyes", () =>
            {
                var e = MatsuriMeshes.Sphere(0.0098f, 6, 4);
                return new List<CombineInstance>
                {
                    new CombineInstance { mesh = e, transform = Matrix4x4.Translate(new Vector3(-0.026f, 0f, HeadR * 0.90f)) },
                    new CombineInstance { mesh = e, transform = Matrix4x4.Translate(new Vector3(0.026f, 0f, HeadR * 0.90f)) }
                };
            });
            var eyeGo = ArtParts.Part("Eyes", head.transform, eyes, hairMat, new Vector3(0f, cy + 0.004f, 0f));
            ArtParts.SetColor(eyeGo, new Color(0.09f, 0.08f, 0.08f));
            ArtParts.NoShadow(eyeGo);

            // 鉢巻き。輪と、後ろで結んだ玉と垂れ
            var ring = ArtParts.Part("Hachimaki", head.transform,
                MatsuriMeshes.Torus(HeadR * 1.02f, 0.0155f, 16, 6), cloth,
                new Vector3(0f, cy + HeadR * 0.44f, 0f), Quaternion.identity, new Vector3(1f, 1f, 0.98f));
            ArtParts.SetColor(ring, band);

            var knot = MatsuriMeshes.CombineCached("StaffHachimakiKnot", () => new List<CombineInstance>
            {
                new CombineInstance
                {
                    mesh = MatsuriMeshes.Box(new Vector3(0.036f, 0.030f, 0.030f)),
                    transform = Matrix4x4.identity
                },
                new CombineInstance
                {
                    mesh = MatsuriMeshes.Box(new Vector3(0.018f, 0.088f, 0.010f)),
                    transform = Matrix4x4.TRS(new Vector3(-0.017f, -0.052f, -0.012f), Quaternion.Euler(14f, 0f, 12f), Vector3.one)
                },
                new CombineInstance
                {
                    mesh = MatsuriMeshes.Box(new Vector3(0.018f, 0.076f, 0.010f)),
                    transform = Matrix4x4.TRS(new Vector3(0.017f, -0.046f, -0.012f), Quaternion.Euler(10f, 0f, -16f), Vector3.one)
                }
            });
            var knotGo = ArtParts.Part("HachimakiKnot", head.transform, knot, cloth,
                new Vector3(0f, cy + HeadR * 0.44f, -HeadR * 1.00f));
            ArtParts.SetColor(knotGo, band);
            ArtParts.NoShadow(knotGo);
        }
    }
}

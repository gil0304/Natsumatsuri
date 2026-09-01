using System;
using Matsuri.Art;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Matsuri.Events
{
    /// <summary>
    /// 仕様書 §22。太鼓演奏の舞台。
    /// 「太鼓台を組み立てること」と「一定のリズムで拍を刻むこと」だけを持つ (§66)。
    /// 音を鳴らすのと満足度を配るのは EventManager の仕事。
    /// 自前の Update は持たない。更新は EventManager から来る (§57)。
    /// </summary>
    public sealed class TaikoStage : MonoBehaviour
    {
        /// <summary>
        /// 打つ間隔（秒）のパターン。ドン・ドン・ドドン・ドン、を繰り返す。
        /// 一定の等間隔にすると機械的に聞こえるので、あえて崩している。
        /// </summary>
        static readonly float[] Pattern = { 0.58f, 0.58f, 0.29f, 0.29f, 0.86f, 0.58f, 0.29f, 0.29f };

        /// <summary>拍が鳴った瞬間。引数は打面のワールド位置。</summary>
        public event Action<Vector3> Beat;

        /// <summary>打面（音の発生源）のワールド位置。</summary>
        public Vector3 DrumHeadPosition => _drumHead != null ? _drumHead.position : transform.position;

        /// <summary>いままでに打った回数。</summary>
        public int BeatCount { get; private set; }

        Transform _drum;
        Transform _drumHead;
        Transform _bachiLeft;
        Transform _bachiRight;

        Vector3 _drumBaseScale = Vector3.one;
        float _timer;
        int _patternIndex;
        float _punch;          // 打面の残響（見た目の縮み）
        float _swingLeft;      // 撥の振り上がり 0〜1
        float _swingRight;
        bool _nextIsLeft = true;

        /// <summary>太鼓台を建てる。</summary>
        public static TaikoStage Build(Vector3 center, Transform parent)
        {
            var go = new GameObject("TaikoStage");
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            var stage = go.AddComponent<TaikoStage>();
            stage.Construct();
            return stage;
        }

        void Construct()
        {
            Material wood     = MatsuriMaterials.Wood(new Color(0.33f, 0.18f, 0.11f));
            Material woodDeck  = MatsuriMaterials.Wood(new Color(0.50f, 0.36f, 0.23f));
            Material skin      = MatsuriMaterials.Paper(new Color(0.93f, 0.87f, 0.72f));
            Material metal     = MatsuriMaterials.Metal(new Color(0.45f, 0.36f, 0.16f));
            Material fabricRed = MatsuriMaterials.Fabric(new Color(0.72f, 0.14f, 0.13f));

            // 舞台
            Mesh deck = MatsuriMeshes.Box(new Vector3(3.4f, 0.42f, 3.0f));
            GroundPart(Part(transform, "Deck", deck, woodDeck, Vector3.zero, Quaternion.identity), deck, 0f);

            // 舞台の縁（紅の幕）
            Mesh skirt = MatsuriMeshes.ClothStrip(3.4f, 0.42f, 6, 2);
            for (int i = 0; i < 4; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, 90f * i, 0f);
                float half = (i % 2 == 0) ? 1.5f : 1.7f;
                Part(transform, $"Skirt{i + 1:00}", skirt, fabricRed, yaw * new Vector3(0f, 0.21f, half + 0.02f), yaw);
            }

            // 太鼓の台座（X字の脚）
            Mesh leg = MatsuriMeshes.Box(new Vector3(0.14f, 1.35f, 0.14f));
            for (int i = 0; i < 4; i++)
            {
                float sx = (i < 2) ? -0.55f : 0.55f;
                float sz = (i % 2 == 0) ? -0.34f : 0.34f;
                float tilt = (i % 2 == 0) ? 16f : -16f;
                Part(transform, $"StandLeg{i + 1:00}", leg, wood,
                     new Vector3(sx, 1.05f, sz), Quaternion.Euler(tilt, 0f, 0f));
            }
            Mesh crossBar = MatsuriMeshes.Box(new Vector3(1.5f, 0.11f, 0.11f));
            Part(transform, "StandBar", crossBar, wood, new Vector3(0f, 0.95f, 0f), Quaternion.identity);

            // 太鼓の胴（打面が +Z を向くように寝かせる）
            Mesh body = MatsuriMeshes.Cylinder(0.70f, 0.98f, 24);
            GameObject drumGo = Part(transform, "Drum", body, wood,
                                     new Vector3(0f, 1.72f, 0f), Quaternion.Euler(90f, 0f, 0f));
            _drum = drumGo.transform;
            _drumBaseScale = _drum.localScale;

            // 打面（表・裏）
            Mesh head = MatsuriMeshes.Cylinder(0.735f, 0.05f, 24);
            GameObject headGo = Part(transform, "DrumHeadFront", head, skin,
                                     new Vector3(0f, 1.72f, 0.50f), Quaternion.Euler(90f, 0f, 0f));
            _drumHead = headGo.transform;
            Part(transform, "DrumHeadBack", head, skin,
                 new Vector3(0f, 1.72f, -0.50f), Quaternion.Euler(90f, 0f, 0f));

            // 鋲（打面の縁に打つ金物）
            Mesh stud = MatsuriMeshes.Cylinder(0.035f, 0.04f, 6);
            for (int i = 0; i < 16; i++)
            {
                float a = Mathf.PI * 2f * i / 16f;
                Part(transform, $"Stud{i + 1:00}", stud, metal,
                     new Vector3(Mathf.Cos(a) * 0.66f, 1.72f + Mathf.Sin(a) * 0.66f, 0.53f),
                     Quaternion.Euler(90f, 0f, 0f));
            }

            // 撥（ばち）2本
            Mesh bachi = MatsuriMeshes.Cylinder(0.035f, 0.62f, 8);
            _bachiLeft = Part(transform, "BachiLeft", bachi, woodDeck,
                              new Vector3(-0.42f, 1.78f, 0.95f), Quaternion.identity).transform;
            _bachiRight = Part(transform, "BachiRight", bachi, woodDeck,
                               new Vector3(0.42f, 1.78f, 0.95f), Quaternion.identity).transform;

            // 提灯と灯り (§59)
            Mesh lantern = MatsuriMeshes.Lantern(0.26f, 0.40f, 12);
            Color lanternColor = new Color(0.95f, 0.30f, 0.20f);
            Material lit = MatsuriMaterials.Emissive(lanternColor, 2.6f);
            Part(transform, "LanternL", lantern, lit, new Vector3(-1.55f, 2.55f, 0.9f), Quaternion.identity);
            Part(transform, "LanternR", lantern, lit, new Vector3(1.55f, 2.55f, 0.9f), Quaternion.identity);

            Mesh pole = MatsuriMeshes.Cylinder(0.05f, 2.8f, 8);
            Part(transform, "PoleL", pole, wood, new Vector3(-1.55f, 1.4f, 0.9f), Quaternion.identity);
            Part(transform, "PoleR", pole, wood, new Vector3(1.55f, 1.4f, 0.9f), Quaternion.identity);

            AddWarmLight(transform, new Color(1f, 0.80f, 0.55f), 2600f, 16f, new Vector3(0f, 2.4f, 0.6f));

            ApplyBachiPose();
        }

        // ────────────────────────────────────────────────────────
        // リズム
        // ────────────────────────────────────────────────────────

        /// <summary>EventManager から毎フレーム呼ばれる (§57)。</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            _timer += dt;
            float interval = Pattern[_patternIndex % Pattern.Length];
            if (_timer >= interval)
            {
                _timer -= interval;
                _patternIndex++;
                Strike();
            }

            // 打面の残響と撥の戻り
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 5.5f);
            _swingLeft = Mathf.MoveTowards(_swingLeft, _nextIsLeft ? 1f : 0.15f, dt * 6f);
            _swingRight = Mathf.MoveTowards(_swingRight, _nextIsLeft ? 0.15f : 1f, dt * 6f);

            ApplyDrumPose();
            ApplyBachiPose();
        }

        void Strike()
        {
            BeatCount++;
            _punch = 1f;

            if (_nextIsLeft) _swingLeft = 0f;
            else _swingRight = 0f;
            _nextIsLeft = !_nextIsLeft;

            Beat?.Invoke(DrumHeadPosition);
        }

        void ApplyDrumPose()
        {
            if (_drum == null) return;
            float squash = 1f + _punch * 0.055f;
            _drum.localScale = new Vector3(
                _drumBaseScale.x * squash,
                _drumBaseScale.y * (1f - _punch * 0.05f),
                _drumBaseScale.z * squash);
        }

        void ApplyBachiPose()
        {
            PoseBachi(_bachiLeft, _swingLeft, -0.42f);
            PoseBachi(_bachiRight, _swingRight, 0.42f);
        }

        /// <summary>swing 0 = 打面に当たっている、1 = 振り上げた状態。</summary>
        static void PoseBachi(Transform bachi, float swing, float sideX)
        {
            if (bachi == null) return;
            float lift = Mathf.Lerp(0f, 0.55f, swing);
            float back = Mathf.Lerp(0f, 0.35f, swing);
            bachi.localPosition = new Vector3(sideX, 1.72f + lift, 0.86f + back);
            bachi.localRotation = Quaternion.Euler(Mathf.Lerp(78f, 25f, swing), 0f, sideX < 0f ? 12f : -12f);
        }

        /// <summary>演奏をやめる。撥を下ろす。</summary>
        public void Rest()
        {
            _punch = 0f;
            _swingLeft = 0.15f;
            _swingRight = 0.15f;
            ApplyDrumPose();
            ApplyBachiPose();
        }

        // ────────────────────────────────────────────────────────
        // 組み立てヘルパ
        // ────────────────────────────────────────────────────────

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

        /// <summary>HDRP の点光源 (§59)。</summary>
        static void AddWarmLight(Transform parent, Color color, float lumen, float range, Vector3 localPos)
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
    }
}

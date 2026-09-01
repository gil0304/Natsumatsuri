using System;
using System.Collections;
using System.Collections.Generic;
using Matsuri.Audio;
using Matsuri.Core;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §39。建設演出。
    /// 「コードを書いた結果、世界が生まれた」と感じさせるための一連の見せ場。
    ///
    ///   1. 地面に光るリングが現れて広がる
    ///   2. 光の柱が立ち上がる
    ///   3. 屋台が地面の下からせり上がる（EaseOutBack で少し行き過ぎて据わる）
    ///   4. 着地の瞬間に砂ぼこりが舞う
    ///   5. 提灯・裸電球が下から順に1つずつ点灯する
    ///   6. 光の柱とリングが消えて完成
    ///
    /// 見た目の部品作りは <see cref="BuildEffectVisuals"/> に分けている (§66)。
    /// </summary>
    public static class BuildAnimation
    {
        /// <summary>リングが広がる時間（秒）。</summary>
        const float RingDuration = 0.34f;

        /// <summary>光の柱が立ち上がる時間（秒）。</summary>
        const float PillarDuration = 0.26f;

        /// <summary>電球が1つ点くごとの間隔（秒）。</summary>
        const float BulbInterval = 0.07f;

        /// <summary>リングと柱が消えるまでの余韻（秒）。</summary>
        const float FadeDuration = 0.45f;

        static readonly Color RingColor = new Color(1f, 0.78f, 0.42f);
        static readonly Color PillarColor = new Color(1f, 0.86f, 0.55f);
        static readonly Color DustColor = new Color(0.72f, 0.63f, 0.50f);

        /// <summary>
        /// 建設演出を再生する。呼び出し側（FestivalManager）が StartCoroutine する。
        /// </summary>
        /// <param name="target">建てる物のルート。呼ぶ時点で最終位置に置かれていること。</param>
        /// <param name="duration">せり上がりにかける時間（秒）。</param>
        /// <param name="delay">開始を遅らせる時間（秒）。複数同時建設をずらすのに使う。</param>
        /// <param name="onComplete">演出完了時に必ず呼ばれる。</param>
        public static IEnumerator Play(GameObject target, float duration, float delay, Action onComplete)
        {
            if (target == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            duration = Mathf.Max(0.15f, duration);

            Transform tr = target.transform;
            Vector3 finalPosition = tr.position;
            Vector3 finalScale = tr.localScale;
            float height = Mathf.Max(1.2f, MeasureHeight(target));
            float footprint = Mathf.Max(1.4f, MeasureFootprint(target));

            // 演出中は当たり判定を切る。未完成の屋台にNPCが引っかからないように。
            var colliders = target.GetComponentsInChildren<Collider>(true);
            var colliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
                colliders[i].enabled = false;
            }

            // 灯りは全部消しておく。あとで1つずつ点ける (§39-5)。
            List<BuildEffectVisuals.Lamp> lamps = BuildEffectVisuals.CollectLamps(target);
            for (int i = 0; i < lamps.Count; i++) lamps[i].TurnOff();

            target.SetActive(false);

            if (delay > 0f) yield return new WaitForSeconds(delay);

            if (target == null) { onComplete?.Invoke(); yield break; }

            // ── 1. 地面のリング ───────────────────────────────
            GameObject ring = BuildEffectVisuals.CreateRing(finalPosition, footprint, RingColor);
            Material ringMaterial = BuildEffectVisuals.GetInstanceMaterial(ring);

            float t = 0f;
            while (t < RingDuration)
            {
                t += UnityEngine.Time.deltaTime;
                float eased = EaseOutCubic(Mathf.Clamp01(t / RingDuration));
                if (ring != null) ring.transform.localScale = new Vector3(eased, 1f, eased);
                BuildEffectVisuals.SetGlow(ringMaterial, RingColor, Mathf.Lerp(0.4f, 3.4f, eased));
                yield return null;
            }

            // ── 2. 光の柱 ─────────────────────────────────────
            GameObject pillar = BuildEffectVisuals.CreatePillar(finalPosition, footprint * 0.72f, height * 1.9f, PillarColor);
            Material pillarMaterial = BuildEffectVisuals.GetInstanceMaterial(pillar);

            t = 0f;
            while (t < PillarDuration)
            {
                t += UnityEngine.Time.deltaTime;
                float eased = EaseOutCubic(Mathf.Clamp01(t / PillarDuration));
                if (pillar != null) pillar.transform.localScale = new Vector3(1f, eased, 1f);
                BuildEffectVisuals.SetGlow(pillarMaterial, PillarColor, Mathf.Lerp(0f, 2.6f, eased));
                yield return null;
            }

            // ── 3. せり上がり ────────────────────────────────
            if (target == null)
            {
                Dispose(ring, pillar);
                onComplete?.Invoke();
                yield break;
            }

            target.SetActive(true);

            var audio = GameManager.Instance != null ? GameManager.Instance.Audio : null;
            if (audio != null) audio.PlaySfx(MatsuriSfx.Build, finalPosition);

            Vector3 buried = finalPosition + Vector3.down * (height + 0.4f);

            t = 0f;
            while (t < duration)
            {
                t += UnityEngine.Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);

                tr.position = Vector3.LerpUnclamped(buried, finalPosition, EaseOutBack(k));
                tr.localScale = finalScale * Mathf.LerpUnclamped(0.86f, 1f, EaseOutCubic(k));

                BuildEffectVisuals.SetGlow(pillarMaterial, PillarColor, Mathf.Lerp(2.6f, 0.9f, k));
                yield return null;
            }

            tr.position = finalPosition;
            tr.localScale = finalScale;

            // ── 4. 着地の砂ぼこり ────────────────────────────
            BuildEffectVisuals.SpawnDust(finalPosition, footprint, DustColor);

            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) colliders[i].enabled = colliderStates[i];

            // ── 5. 電球・提灯が順に点灯 ──────────────────────
            for (int i = 0; i < lamps.Count; i++)
            {
                lamps[i].TurnOn();
                if (audio != null && i % 2 == 0) audio.PlaySfx(MatsuriSfx.Bulb, finalPosition, 0.55f);
                yield return new WaitForSeconds(BulbInterval);
            }

            // ── 6. 余韻。リングと柱が広がりながら消える ──────
            t = 0f;
            while (t < FadeDuration)
            {
                t += UnityEngine.Time.deltaTime;
                float k = Mathf.Clamp01(t / FadeDuration);

                if (ring != null)
                {
                    ring.transform.localScale = new Vector3(1f + k * 0.55f, 1f, 1f + k * 0.55f);
                    BuildEffectVisuals.SetGlow(ringMaterial, RingColor, Mathf.Lerp(3.4f, 0f, k));
                }
                if (pillar != null)
                {
                    float widen = Mathf.Lerp(1f, 1.35f, k);
                    pillar.transform.localScale = new Vector3(widen, 1f, widen);
                    BuildEffectVisuals.SetGlow(pillarMaterial, PillarColor, Mathf.Lerp(0.9f, 0f, k));
                }
                yield return null;
            }

            Dispose(ring, pillar);
            onComplete?.Invoke();
        }

        static void Dispose(GameObject ring, GameObject pillar)
        {
            if (ring != null) UnityEngine.Object.Destroy(ring);
            if (pillar != null) UnityEngine.Object.Destroy(pillar);
        }

        // ────────────────────────────────────────────────────
        // 大きさの計測とイージング
        // ────────────────────────────────────────────────────

        static bool TryMeasure(GameObject target, out Bounds bounds)
        {
            bounds = default;
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!any) { bounds = renderers[i].bounds; any = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return any;
        }

        static float MeasureHeight(GameObject target)
            => TryMeasure(target, out Bounds b) ? b.size.y : 2.5f;

        static float MeasureFootprint(GameObject target)
            => TryMeasure(target, out Bounds b) ? Mathf.Max(b.extents.x, b.extents.z) : 2f;

        static float EaseOutCubic(float k) => 1f - Mathf.Pow(1f - k, 3f);

        /// <summary>行き過ぎてから戻る。物が「ドンと据わる」感じを作る。</summary>
        static float EaseOutBack(float k)
        {
            const float C1 = 1.24f;
            const float C3 = C1 + 1f;
            float x = k - 1f;
            return 1f + C3 * x * x * x + C1 * x * x;
        }
    }
}

using System.Collections;
using Matsuri.Art;
using Matsuri.Audio;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Script;
using UnityEngine;

namespace Matsuri.Events
{
    /// <summary>
    /// 仕様書 §22 / §61。花火の「打ち上げ進行」だけを担当する (§66)。
    /// どこから何発を何秒おきに上げ、どの色で光らせるか。
    /// 実際の粒子は Matsuri.Art.FireworksController、会場のフラッシュは LightingRig が受け持つ。
    /// MonoBehaviour ではない。コルーチンは EventManager が回す (§57)。
    /// </summary>
    public sealed class FireworksDirector
    {
        readonly EventManager _owner;
        readonly Transform _root;

        FireworksController _fireworks;
        LightingRig _lighting;
        Unity.Mathematics.Random _rng = new Unity.Mathematics.Random(0x5EED2024u);

        /// <summary>まだ空中にある玉の数。0 になるまでが「花火開催中」。</summary>
        public int LiveShots { get; private set; }

        public FireworksDirector(EventManager owner, Transform root)
        {
            _owner = owner;
            _root = root;
        }

        /// <summary>打ち上げ器材を用意する。祭り前に一度呼んでおくと初弾が遅れない。</summary>
        public void Prepare()
        {
            if (_fireworks == null)
            {
                _fireworks = UnityEngine.Object.FindFirstObjectByType<FireworksController>();
                if (_fireworks == null)
                {
                    var go = new GameObject("FireworksController");
                    if (_root != null) go.transform.SetParent(_root, false);
                    _fireworks = go.AddComponent<FireworksController>();
                }
            }

            if (_lighting == null) _lighting = UnityEngine.Object.FindFirstObjectByType<LightingRig>();
        }

        public void Reset() => LiveShots = 0;

        /// <summary>複数発を少しずつ時間差で打ち上げる。EventManager が StartCoroutine する。</summary>
        public IEnumerator Sequence(string kind, FestivalEventData data, float baseStagger, float riseSeconds)
        {
            int shots = ShotCountFor(kind);
            float stagger = Mathf.Max(0.12f, baseStagger * StaggerScaleFor(kind));

            for (int i = 0; i < shots; i++)
            {
                _owner.StartCoroutine(SingleShot(kind, data, i, shots, riseSeconds));
                yield return new WaitForSeconds(stagger);
            }
        }

        /// <summary>1発ぶん。打ち上げ音 → 上昇 → 開花 → 破裂音 → 会場フラッシュ → NPCへ感動。</summary>
        IEnumerator SingleShot(string kind, FestivalEventData data, int shotIndex, int shotCount, float riseSeconds)
        {
            LiveShots++;

            Vector3 pad = PickLaunchPad();
            float apexHeight = _rng.NextFloat(36f, 58f) + (kind == MatsuriIds.FireworkOodama ? 14f : 0f);
            Vector3 apex = pad + new Vector3(_rng.NextFloat(-7f, 7f), apexHeight, _rng.NextFloat(-7f, 7f));

            AudioManager audioManager = GameManager.Instance != null ? GameManager.Instance.Audio : null;
            audioManager?.PlaySfx(MatsuriSfx.FireworkLaunch, pad, 0.6f);

            float rise = riseSeconds * Mathf.Clamp(apexHeight / 46f, 0.75f, 1.55f);
            yield return new WaitForSeconds(rise);

            float power = PowerFor(kind);
            Color color = ColorFor(kind, shotIndex);

            _fireworks?.Launch(kind, apex);
            audioManager?.PlaySfx(MatsuriSfx.FireworkBurst, apex, Mathf.Min(1f, 0.75f * power));

            // §61 打ち上がるたびに会場を一瞬照らす
            _lighting?.FlashFromFireworks(color, 2.6f * power, 0.45f);

            // 1発目が一番効く。連発するほど1発あたりの感動は薄れる。
            float falloff = shotCount <= 1 ? 1f : Mathf.Lerp(1f, 0.3f, shotIndex / (float)(shotCount - 1));
            _owner.NotifyFireworksBurst(apex, data, falloff * power);

            LiveShots = Mathf.Max(0, LiveShots - 1);
        }

        /// <summary>会場の奥（参道の先）を打ち上げ場所にする。</summary>
        Vector3 PickLaunchPad()
        {
            GroundBounds b = _owner.VenueBounds;
            float x = _rng.NextFloat(b.MinX * 0.45f, b.MaxX * 0.45f);
            float z = Mathf.Lerp(b.MaxZ * 0.55f, b.MaxZ * 0.95f, _rng.NextFloat());
            return new Vector3(x, 0f, z);
        }

        /// <summary>種類ごとの発数 (§61)。</summary>
        public static int ShotCountFor(string kind)
        {
            switch (kind)
            {
                case MatsuriIds.FireworkOodama:  return 3;    // 大玉は少なく、重く
                case MatsuriIds.FireworkSpecial: return 14;   // スターマイン
                case MatsuriIds.FireworkHeart:   return 4;
                case MatsuriIds.FireworkYanagi:  return 6;
                case MatsuriIds.FireworkBotan:   return 8;
                default:                         return 7;    // 菊
            }
        }

        /// <summary>間隔の倍率。大玉はゆったり、スペシャルは連発。</summary>
        public static float StaggerScaleFor(string kind)
        {
            switch (kind)
            {
                case MatsuriIds.FireworkOodama:  return 2.4f;
                case MatsuriIds.FireworkSpecial: return 0.45f;
                case MatsuriIds.FireworkHeart:   return 1.6f;
                default:                         return 1f;
            }
        }

        /// <summary>音と光の強さの倍率。</summary>
        public static float PowerFor(string kind)
        {
            switch (kind)
            {
                case MatsuriIds.FireworkOodama:  return 1.6f;
                case MatsuriIds.FireworkSpecial: return 1.25f;
                default:                         return 1f;
            }
        }

        /// <summary>種類ごとの色 (§61)。会場を照らすフラッシュの色にもなる。</summary>
        public static Color ColorFor(string kind, int shotIndex)
        {
            switch (kind)
            {
                case MatsuriIds.FireworkKiku:   return new Color(1.00f, 0.85f, 0.52f);   // 金
                case MatsuriIds.FireworkBotan:  return new Color(1.00f, 0.38f, 0.30f);   // 紅
                case MatsuriIds.FireworkYanagi: return new Color(0.78f, 0.95f, 0.52f);   // 柳
                case MatsuriIds.FireworkHeart:  return new Color(1.00f, 0.52f, 0.72f);   // 桃
                case MatsuriIds.FireworkOodama: return new Color(1.00f, 0.96f, 0.88f);   // 白金
                case MatsuriIds.FireworkSpecial:
                {
                    Color[] palette =
                    {
                        new Color(1.00f, 0.85f, 0.52f), new Color(1.00f, 0.38f, 0.30f),
                        new Color(0.52f, 0.78f, 1.00f), new Color(0.78f, 0.95f, 0.52f),
                        new Color(1.00f, 0.52f, 0.72f)
                    };
                    return palette[Mathf.Abs(shotIndex) % palette.Length];
                }
                default: return new Color(1.00f, 0.88f, 0.62f);
            }
        }
    }
}

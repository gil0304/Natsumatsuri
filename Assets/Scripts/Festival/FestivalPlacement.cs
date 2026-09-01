using System.Collections.Generic;
using Matsuri.Script;
using Matsuri.Script.Commands;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// Matsuri Script が書いたグリッド座標を、実際に物を置けるワールド座標へ変換する。
    ///
    /// - Y は地面の高さ（レイキャストで拾い、当たらなければ 0）
    /// - 既に何かが置かれている場所と重なるときは、渦巻き状に少しずつずらして空きを探す
    /// - 会場の外にははみ出さない (§41 で Validator も弾くが、実行時にも守る)
    ///
    /// 仕様書 §66 に従い「置き場所を決める」責務だけを持つ。GameObject は作らない。
    /// </summary>
    public sealed class FestivalPlacement
    {
        /// <summary>すでに占有されている円。</summary>
        struct Occupied
        {
            public Vector2 Center;
            public float Radius;
        }

        /// <summary>地面を探すレイキャストの開始高さ。</summary>
        const float RayHeight = 200f;

        /// <summary>ずらすときの1歩の距離（m）。</summary>
        const float SearchStep = 1.4f;

        /// <summary>最大何歩まで探すか。</summary>
        const int MaxSearchSteps = 220;

        readonly List<Occupied> _occupied = new List<Occupied>(128);

        GroundBounds _bounds = GroundBounds.Default;
        LayerMask _groundMask = ~0;
        float _defaultHeight;

        /// <summary>会場の範囲を設定する。</summary>
        public void Configure(GroundBounds bounds, float defaultHeight = 0f)
        {
            _bounds = bounds;
            _defaultHeight = defaultHeight;
        }

        /// <summary>地面判定に使うレイヤー。既定はすべて。</summary>
        public void SetGroundMask(LayerMask mask) => _groundMask = mask;

        public void Clear() => _occupied.Clear();

        /// <summary>すでに置かれている物の数。</summary>
        public int Count => _occupied.Count;

        /// <summary>
        /// 書かれた座標に置けるワールド座標を返す。
        /// 重なりがあればずらし、その場合 moved が true になる。
        /// 返した位置は自動的に占有済みとして登録される。
        /// </summary>
        public Vector3 Resolve(GridPos grid, float radius, out bool moved)
            => Resolve(new Vector2(grid.X, grid.Z), radius, out moved);

        /// <summary>座標(X, Z)を指定して置き場所を決める。</summary>
        public Vector3 Resolve(Vector2 grid, float radius, out bool moved)
        {
            radius = Mathf.Max(0.4f, radius);

            Vector2 wanted = ClampToBounds(grid, radius);
            Vector2 found = wanted;
            moved = false;

            if (Overlaps(wanted, radius))
            {
                if (TryFindFreeSpot(wanted, radius, out Vector2 alternative))
                {
                    found = alternative;
                    moved = true;
                }
                else
                {
                    // どうしても空きが無ければ、書かれた場所にそのまま置く。
                    // 「建たない」より「少し重なっている」方がプレイヤーにとって親切。
                    found = wanted;
                    moved = false;
                }
            }

            Occupy(found, radius);
            return new Vector3(found.x, SampleGroundHeight(found.x, found.y), found.y);
        }

        /// <summary>ずらしを一切行わず、地面高さだけを解決する（入口・出口など）。</summary>
        public Vector3 ToWorld(Vector2 grid)
            => new Vector3(grid.x, SampleGroundHeight(grid.x, grid.y), grid.y);

        public Vector3 ToWorld(GridPos grid) => ToWorld(new Vector2(grid.X, grid.Z));

        /// <summary>外部で作った物の占有を登録する。</summary>
        public void Occupy(Vector2 center, float radius)
            => _occupied.Add(new Occupied { Center = center, Radius = Mathf.Max(0.4f, radius) });

        public void Occupy(Vector3 world, float radius)
            => Occupy(new Vector2(world.x, world.z), radius);

        /// <summary>会場の中央。イベントの既定位置に使う。</summary>
        public Vector3 Center
            => ToWorld(new Vector2((_bounds.MinX + _bounds.MaxX) * 0.5f, (_bounds.MinZ + _bounds.MaxZ) * 0.5f));

        /// <summary>会場の南端中央。入り口の既定位置。</summary>
        public Vector3 SouthGate
            => ToWorld(new Vector2((_bounds.MinX + _bounds.MaxX) * 0.5f, _bounds.MinZ + 3f));

        bool Overlaps(Vector2 center, float radius)
        {
            for (int i = 0; i < _occupied.Count; i++)
            {
                float min = _occupied[i].Radius + radius;
                if ((_occupied[i].Center - center).sqrMagnitude < min * min) return true;
            }
            return false;
        }

        /// <summary>渦巻き状に外へ広がりながら空きを探す。書かれた場所からなるべく近い所に落とす。</summary>
        bool TryFindFreeSpot(Vector2 wanted, float radius, out Vector2 result)
        {
            // 黄金角を使うと、少ない試行で均等にばらける。
            const float GoldenAngle = 2.39996323f;

            for (int i = 1; i <= MaxSearchSteps; i++)
            {
                float angle = i * GoldenAngle;
                float distance = SearchStep * Mathf.Sqrt(i) * Mathf.Max(1f, radius * 0.75f);

                Vector2 candidate = wanted + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                candidate = ClampToBounds(candidate, radius);

                if (!Overlaps(candidate, radius))
                {
                    result = candidate;
                    return true;
                }
            }

            result = wanted;
            return false;
        }

        Vector2 ClampToBounds(Vector2 p, float radius)
        {
            float minX = _bounds.MinX + radius;
            float maxX = _bounds.MaxX - radius;
            float minZ = _bounds.MinZ + radius;
            float maxZ = _bounds.MaxZ - radius;

            if (minX > maxX) { minX = maxX = (_bounds.MinX + _bounds.MaxX) * 0.5f; }
            if (minZ > maxZ) { minZ = maxZ = (_bounds.MinZ + _bounds.MaxZ) * 0.5f; }

            return new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minZ, maxZ));
        }

        /// <summary>地面の高さを拾う。地面コライダーが無い場合は既定値。</summary>
        public float SampleGroundHeight(float x, float z)
        {
            var origin = new Vector3(x, RayHeight, z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayHeight * 2f, _groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return _defaultHeight;
        }

        /// <summary>置かれた物の見た目の大きさから、占有半径を推定する。</summary>
        public static float EstimateRadius(GameObject go, float fallback)
        {
            if (go == null) return fallback;

            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return fallback;

            bool any = false;
            Bounds bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (!any) { bounds = renderers[i].bounds; any = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }

            if (!any) return fallback;

            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            if (radius <= 0.01f || float.IsNaN(radius)) return fallback;

            // 行列や通路のぶん、少し余白を足す (§30)。
            return radius + 0.6f;
        }
    }
}

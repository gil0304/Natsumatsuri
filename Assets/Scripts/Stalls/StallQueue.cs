using System.Collections.Generic;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Stalls
{
    /// <summary>
    /// 仕様書 §30。屋台の「行列」そのもの。
    /// Stall から切り出し、並ぶ／抜ける／詰める／待ち時間の記録だけに責務を絞る (§66)。
    /// MonoBehaviour ではない。更新は Stall.TickFestival から呼ばれる (§57 更新の集中管理)。
    /// </summary>
    public sealed class StallQueue
    {
        /// <summary>先頭が 0。index が小さいほど屋台に近い。</summary>
        readonly List<VisitorAgent> _members = new List<VisitorAgent>(16);

        /// <summary>_members と添字が一致する「並び始めてからの秒数」。</summary>
        readonly List<float> _waited = new List<float>(16);

        Transform[] _points = System.Array.Empty<Transform>();
        Transform _origin;
        int _maxLength = 12;
        float _spacing = 0.75f;

        /// <summary>これ以上は並べない人数 (StallData.MaxQueueLength)。</summary>
        public int MaxLength => _maxLength;

        /// <summary>並ぶ人の間隔 (m)。QueuePoint が足りないときの延長にも使う。</summary>
        public float Spacing => _spacing;

        public int Count => _members.Count;

        /// <summary>まだ並べるか。</summary>
        public bool CanAccept => _members.Count < _maxLength;

        public bool IsFull => _members.Count >= _maxLength;

        /// <summary>先頭の客。誰も並んでいなければ null。</summary>
        public VisitorAgent Head => _members.Count > 0 ? _members[0] : null;

        public IReadOnlyList<VisitorAgent> Members => _members;

        /// <summary>
        /// 屋台側から並び位置の情報を渡す。
        /// origin は屋台本体の Transform（QueuePoint が1つも無いときの基準）。
        /// </summary>
        public void Configure(Transform origin, Transform[] points, int maxLength, float spacing)
        {
            _origin = origin;
            _points = points ?? System.Array.Empty<Transform>();
            _maxLength = Mathf.Max(1, maxLength);
            _spacing = Mathf.Max(0.25f, spacing);
        }

        /// <summary>行列の最後尾に並ぶ。並べなければ false。</summary>
        public bool TryJoin(VisitorAgent visitor)
        {
            if (visitor == null) return false;
            if (IndexOf(visitor) >= 0) return true;      // 二重に並ばせない
            if (!CanAccept) return false;

            _members.Add(visitor);
            _waited.Add(0f);
            return true;
        }

        /// <summary>
        /// 行列から抜ける。抜けた後ろの客は自動的に1つ前へ詰まる
        /// （List の削除で添字がずれるため、NPC 側は GetSlotPosition(index) を毎回見れば良い）。
        /// </summary>
        public void Leave(VisitorAgent visitor)
        {
            int i = IndexOf(visitor);
            if (i < 0) return;
            _members.RemoveAt(i);
            _waited.RemoveAt(i);
        }

        /// <summary>並んでいなければ -1。</summary>
        public int IndexOf(VisitorAgent visitor)
        {
            if (visitor == null) return -1;
            for (int i = 0; i < _members.Count; i++)
                if (ReferenceEquals(_members[i], visitor)) return i;
            return -1;
        }

        /// <summary>並び始めてからの秒数。NPC の Patience と比較して諦め判定に使う (§34)。</summary>
        public float GetWaitTime(VisitorAgent visitor)
        {
            int i = IndexOf(visitor);
            return i < 0 ? 0f : _waited[i];
        }

        /// <summary>先頭を取り出す（接客に入る）。空なら null。</summary>
        public VisitorAgent Dequeue()
        {
            if (_members.Count == 0) return null;
            VisitorAgent head = _members[0];
            _members.RemoveAt(0);
            _waited.RemoveAt(0);
            return head;
        }

        /// <summary>
        /// index 番目に並ぶ人の立ち位置。
        /// QueuePoint が足りない場合は最後尾の後ろへ Spacing 間隔で仮想的に延長する。
        /// </summary>
        public Vector3 GetSlotPosition(int index)
        {
            if (index < 0) index = 0;

            int n = _points.Length;
            if (n > 0)
            {
                if (index < n && _points[index] != null) return _points[index].position;

                // 実在する最後の QueuePoint を探す
                int last = -1;
                for (int i = n - 1; i >= 0; i--)
                {
                    if (_points[i] != null) { last = i; break; }
                }

                if (last >= 0)
                {
                    Vector3 tail = _points[last].position;
                    Vector3 dir = QueueDirection(last);
                    return tail + dir * (_spacing * (index - last));
                }
            }

            // QueuePoint が1つも無い（Prefab差し替えなどで欠けている）場合の保険。
            Vector3 basePos = _origin != null ? _origin.position : Vector3.zero;
            Vector3 forward = _origin != null ? _origin.forward : Vector3.forward;
            return basePos + forward * (_spacing * (index + 1));
        }

        /// <summary>行列が伸びていく向き。最後の2点から求め、求まらなければ屋台の正面。</summary>
        Vector3 QueueDirection(int lastIndex)
        {
            if (lastIndex >= 1 && _points[lastIndex - 1] != null)
            {
                Vector3 d = _points[lastIndex].position - _points[lastIndex - 1].position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0004f) return d.normalized;
            }
            if (_origin != null)
            {
                Vector3 f = _origin.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.0004f) return f.normalized;
            }
            return Vector3.forward;
        }

        /// <summary>待ち時間の加算と、消えた NPC の掃除。</summary>
        public void Tick(float dt)
        {
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                VisitorAgent v = _members[i];
                if (v == null || !v.isActiveAndEnabled)
                {
                    _members.RemoveAt(i);
                    _waited.RemoveAt(i);
                    continue;
                }
                _waited[i] += dt;
            }
        }

        /// <summary>祭り終了・リセット時に全員解散させる。</summary>
        public void Clear()
        {
            _members.Clear();
            _waited.Clear();
        }
    }
}

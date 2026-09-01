using System.Collections.Generic;
using Matsuri.Core;
using Matsuri.TimeSystem;
using UnityEngine;

namespace Matsuri.Stalls
{
    /// <summary>
    /// 仕様書 §30 / §35 / §36。建った屋台の台帳。
    /// 同じ種類 (id) の屋台が複数あることを前提に、id → 屋台リストの索引を持つ。
    /// §17 の条件式（「たこ焼きの行列 &gt; 10 なら」）はここの集計を見る。
    /// 各 Stall は自前の Update を持たず、この TickAll から一括で更新される (§57)。
    /// </summary>
    public sealed class StallManager : MonoBehaviour
    {
        static readonly Stall[] EmptyStalls = System.Array.Empty<Stall>();

        readonly List<Stall> _stalls = new List<Stall>(64);
        readonly Dictionary<string, List<Stall>> _byId = new Dictionary<string, List<Stall>>(24);

        bool _indexDirty = true;

        public IReadOnlyList<Stall> Stalls => _stalls;

        /// <summary>建っている屋台の総数。</summary>
        public int Count => _stalls.Count;

        public void Register(Stall stall)
        {
            if (stall == null) return;
            if (_stalls.Contains(stall)) return;
            _stalls.Add(stall);
            _indexDirty = true;
        }

        public void Unregister(Stall stall)
        {
            if (stall == null) return;
            if (_stalls.Remove(stall)) _indexDirty = true;
        }

        /// <summary>
        /// Stall.Configure で ObjectId が確定したときに呼ばれる。
        /// 登録順と id 確定順が前後しても索引が壊れないようにするための仕組み。
        /// </summary>
        public void MarkIndexDirty() => _indexDirty = true;

        /// <summary>同じ id の屋台すべて。無ければ空。</summary>
        public IReadOnlyList<Stall> GetById(string stallId)
        {
            if (string.IsNullOrEmpty(stallId)) return EmptyStalls;
            RebuildIndexIfNeeded();
            return _byId.TryGetValue(stallId, out List<Stall> list) ? list : (IReadOnlyList<Stall>)EmptyStalls;
        }

        /// <summary>同じ id の屋台の行列人数の合計 (§17)。</summary>
        public int GetQueueLength(string stallId)
        {
            IReadOnlyList<Stall> list = GetById(stallId);
            int total = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) total += list[i].QueueLength;
            return total;
        }

        /// <summary>同じ id の屋台の売上の合計 (§17)。</summary>
        public long GetRevenue(string stallId)
        {
            IReadOnlyList<Stall> list = GetById(stallId);
            long total = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) total += list[i].Revenue;
            return total;
        }

        /// <summary>同じ id の屋台の軒数 (§17)。</summary>
        public int GetCount(string stallId)
        {
            IReadOnlyList<Stall> list = GetById(stallId);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) n++;
            return n;
        }

        /// <summary>同じ id の屋台の販売件数の合計。</summary>
        public int GetSalesCount(string stallId)
        {
            IReadOnlyList<Stall> list = GetById(stallId);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) n += list[i].SalesCount;
            return n;
        }

        /// <summary>
        /// 結果画面 (§36) の「人気No.1」。
        /// まず種類 (id) 単位で販売件数を合計して一番売れた種類を選び、
        /// その種類の中で最も売った1軒を返す。
        /// 種類全体の売上が欲しい場合は <see cref="GetRevenue"/>(MostPopular.ObjectId) を使う。
        /// </summary>
        public Stall MostPopular
        {
            get
            {
                RebuildIndexIfNeeded();

                string bestId = null;
                int bestSales = -1;
                long bestRevenue = -1;

                foreach (KeyValuePair<string, List<Stall>> pair in _byId)
                {
                    int sales = 0;
                    long revenue = 0;
                    List<Stall> list = pair.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] == null) continue;
                        sales += list[i].SalesCount;
                        revenue += list[i].Revenue;
                    }

                    if (sales > bestSales || (sales == bestSales && revenue > bestRevenue))
                    {
                        bestSales = sales;
                        bestRevenue = revenue;
                        bestId = pair.Key;
                    }
                }

                if (bestId == null) return null;

                // その種類の中で一番売った1軒
                List<Stall> group = _byId[bestId];
                Stall best = null;
                for (int i = 0; i < group.Count; i++)
                {
                    Stall s = group[i];
                    if (s == null) continue;
                    if (best == null) { best = s; continue; }
                    if (s.SalesCount > best.SalesCount) best = s;
                    else if (s.SalesCount == best.SalesCount && s.Revenue > best.Revenue) best = s;
                }
                return best;
            }
        }

        /// <summary>スコアの「利用屋台の種類数」(§35)。</summary>
        public int DistinctStallKinds
        {
            get
            {
                RebuildIndexIfNeeded();
                int kinds = 0;
                foreach (KeyValuePair<string, List<Stall>> pair in _byId)
                {
                    List<Stall> list = pair.Value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null) { kinds++; break; }
                    }
                }
                return kinds;
            }
        }

        /// <summary>「たこ焼きの値段を300円にする」のように、同じ種類すべての価格を変える (§17)。</summary>
        public void SetPriceForAll(string stallId, int price)
        {
            IReadOnlyList<Stall> list = GetById(stallId);
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) list[i].SetPrice(price);
        }

        /// <summary>祭り開催中、全屋台を一括更新する (§57 更新の集中管理)。</summary>
        public void TickAll(float dt, FestivalClock clock)
        {
            bool foundNull = false;
            for (int i = 0; i < _stalls.Count; i++)
            {
                Stall s = _stalls[i];
                if (s == null) { foundNull = true; continue; }
                s.TickFestival(dt, clock);
            }
            if (foundNull) PurgeDestroyed();
        }

        /// <summary>祭り開始。全屋台を営業状態にする。</summary>
        public void OpenAll()
        {
            for (int i = 0; i < _stalls.Count; i++)
                if (_stalls[i] != null) _stalls[i].OnFestivalStart();
        }

        /// <summary>祭り終了。全屋台を閉め、行列を解散させる。</summary>
        public void CloseAll()
        {
            for (int i = 0; i < _stalls.Count; i++)
                if (_stalls[i] != null) _stalls[i].OnFestivalEnd();
        }

        /// <summary>祭りをリセットしたときに台帳を空にする。</summary>
        public void ResetAll()
        {
            _stalls.Clear();
            _byId.Clear();
            _indexDirty = false;
        }

        /// <summary>会場全体の総売上（結果画面の内訳用）。</summary>
        public long TotalRevenue
        {
            get
            {
                long total = 0;
                for (int i = 0; i < _stalls.Count; i++)
                    if (_stalls[i] != null) total += _stalls[i].Revenue;
                return total;
            }
        }

        void PurgeDestroyed()
        {
            for (int i = _stalls.Count - 1; i >= 0; i--)
                if (_stalls[i] == null) _stalls.RemoveAt(i);
            _indexDirty = true;
        }

        void RebuildIndexIfNeeded()
        {
            if (!_indexDirty) return;
            _indexDirty = false;

            foreach (KeyValuePair<string, List<Stall>> pair in _byId) pair.Value.Clear();

            for (int i = 0; i < _stalls.Count; i++)
            {
                Stall s = _stalls[i];
                if (s == null) continue;

                string id = !string.IsNullOrEmpty(s.ObjectId)
                    ? s.ObjectId
                    : (s.Data != null ? s.Data.Id : null);
                if (string.IsNullOrEmpty(id)) continue;

                if (!_byId.TryGetValue(id, out List<Stall> list))
                {
                    list = new List<Stall>(4);
                    _byId[id] = list;
                }
                list.Add(s);
            }
        }
    }
}

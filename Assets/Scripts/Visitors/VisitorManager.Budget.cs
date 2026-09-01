using UnityEngine;

namespace Matsuri.Visitors
{
    /// <summary>
    /// 更新の分散と NavMeshAgent の配分 (§57)。
    ///
    /// 300人でも1000人でも同じ枠組みで回せるようにするのがここの役目。
    ///  - 思考は「バケット」に分けて、1フレームに1バケットだけ考えさせる
    ///  - 人数が増えたらバケットを増やして、1フレームあたりの思考数を一定に保つ
    ///  - NavMeshAgent はカメラに近い順に上限本数だけ配る
    /// </summary>
    public sealed partial class VisitorManager
    {
        /// <summary>思考バケットの上限。これ以上分けると1人あたりの思考間隔が空きすぎる。</summary>
        const int MaxThinkBuckets = 48;

        /// <summary>1バケットに入れたい人数の目安。人数が増えたらバケット数で吸収する。</summary>
        const int AgentsPerBucketTarget = 40;

        /// <summary>
        /// 同時に NavMeshAgent を持てる人数 (§57)。
        /// BalanceConfig で指定があればそれを使い、無ければインスペクタの値を使う。
        /// </summary>
        public int MaxNavAgents
        {
            get
            {
                if (_balance != null && _balance.MaxActiveNavAgents > 0) return _balance.MaxActiveNavAgents;
                return Mathf.Max(1, _maxNavAgents);
            }
        }

        /// <summary>
        /// 人数に合わせて思考バケットの数を調整する。
        /// 1フレームに考える人数を AgentsPerBucketTarget 前後に保つのが狙い。
        /// たまった経過時間は捨てずに引き継ぐ（捨てるとNPCの時間が飛ぶ）。
        /// </summary>
        void EnsureBucketCapacity()
        {
            int desired = Mathf.Clamp(
                Mathf.CeilToInt(_active.Count / (float)AgentsPerBucketTarget),
                Mathf.Max(1, _baseBuckets),
                MaxThinkBuckets);

            int current = _bucketAccum != null ? _bucketAccum.Length : 0;
            if (current == desired) return;

            var next = new float[desired];

            // 既存ぶんの経過時間を引き継ぐ。減るときは残りを最後のバケットにまとめる。
            float carried = 0f;
            for (int i = 0; i < current; i++)
            {
                if (i < desired) next[i] = _bucketAccum[i];
                else carried += _bucketAccum[i];
            }
            if (carried > 0f) next[desired - 1] += carried;

            _bucketAccum = next;
            if (_bucketCursor >= desired) _bucketCursor = 0;
            _bucketAssign %= desired;

            // 割り当てが偏ったままだと一部のバケットだけ重くなるので配り直す。
            for (int i = 0; i < _active.Count; i++)
            {
                var v = _active[i];
                if (v != null) v.Bucket = i % desired;
            }
        }

        // ── 計測・テスト用の覗き口 ────────────────────────
        // ゲーム本編は使わない。性能計測 (§56) と自動テストが中を見るために開けてある。

        /// <summary>来場者のプール。</summary>
        public VisitorPool Pool => _pool;

        /// <summary>距離LODの割り当て器。</summary>
        public VisitorLodController Lod => _lod;

        /// <summary>いまの思考バケット数。人数に応じて増減する。</summary>
        public int ThinkBucketCount => _bucketAccum != null ? _bucketAccum.Length : 0;

        /// <summary>
        /// 指定人数を即座に出現させる。来場カーブを待たずに人数を作るためのもの (§56 の計測用)。
        /// 実際に出せた人数を返す。
        /// </summary>
        public int SpawnBatch(int count)
        {
            if (count <= 0 || _pool == null || _catalog == null) return 0;

            int room = Capacity() - _active.Count;
            int want = Mathf.Min(count, Mathf.Max(0, room));

            int spawned = 0;
            for (int i = 0; i < want; i++)
            {
                if (!SpawnOne()) break;
                spawned++;
            }
            return spawned;
        }

        /// <summary>
        /// 距離に応じた描画と NavMeshAgent の割り当てを更新する。
        /// 実処理は VisitorLodController が持つ (§66)。
        /// </summary>
        void UpdateLodBudget(float dt, Vector3 camPos)
        {
            _lod.UpdateBudget(_active, camPos, dt);
        }
    }
}

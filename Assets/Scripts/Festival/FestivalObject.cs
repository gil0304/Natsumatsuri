using Matsuri.TimeSystem;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>仕様書 §49。祭りに置かれる物の種別。</summary>
    public enum FestivalObjectKind
    {
        Stall,
        Decoration,
        Facility,
        Event
    }

    /// <summary>
    /// 仕様書 §49。祭りに置かれる全オブジェクトの基底。
    /// 派生: Stall / Decoration / Facility / EventObject。
    /// </summary>
    public abstract class FestivalObject : MonoBehaviour
    {
        [Tooltip("データの正規ID (MatsuriIds)。")]
        public string ObjectId;

        [Tooltip("この個体の一意なID。同じ屋台を複数建てたときの区別に使う。")]
        public string InstanceId;

        [Tooltip("このオブジェクトを生んだ Matsuri Script の行番号 (§42 のハイライトに使う)。")]
        public int SourceLine;

        [Tooltip("建設にかかった費用。")]
        public long BuildCost;

        public abstract FestivalObjectKind Kind { get; }

        /// <summary>会場グリッド上の位置 (X, Z)。</summary>
        public Vector2 GridPosition => new Vector2(transform.position.x, transform.position.z);

        /// <summary>建設演出 (§39) が終わり、実体として存在し始めた瞬間。</summary>
        public virtual void OnBuilt() { }

        /// <summary>祭りが始まった瞬間 (§80)。照明が一斉に点く。</summary>
        public virtual void OnFestivalStart() { }

        /// <summary>祭りが終わった瞬間 (§8 22:00)。</summary>
        public virtual void OnFestivalEnd() { }

        /// <summary>祭り開催中、毎フレーム呼ばれる。dt は実時間の秒。</summary>
        public virtual void TickFestival(float dt, FestivalClock clock) { }

        /// <summary>時間帯に応じた見た目の変化 (§8)。normalized は 0(17:00)〜1(22:00)。</summary>
        public virtual void OnTimeOfDayChanged(FestivalClock clock, float nightAmount) { }

        protected virtual void OnDestroy() { }
    }
}

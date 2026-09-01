using System;
using Matsuri.Data;
using Matsuri.Festival;
using Matsuri.TimeSystem;
using UnityEngine;

namespace Matsuri.Events
{
    /// <summary>
    /// 仕様書 §22 / §49。開催中のイベント（花火・盆踊り・太鼓）1件の実体。
    /// 見た目や音は BonOdoriYagura / TaikoStage / FireworksController が持ち、
    /// この class は「いつ始まって、あと何秒続くか」だけを持つ (§66)。
    /// </summary>
    public sealed class EventObject : FestivalObject
    {
        [Header("データ (§22)")]
        public FestivalEventData Data;

        public override FestivalObjectKind Kind => FestivalObjectKind.Event;

        /// <summary>開催中か。Duration を過ぎると false になる。</summary>
        public bool IsActive { get; private set; }

        /// <summary>開始からの経過秒（実時間）。</summary>
        public float Elapsed { get; private set; }

        /// <summary>残り秒。開催していなければ 0。</summary>
        public float Remaining => IsActive && Data != null ? Mathf.Max(0f, Data.Duration - Elapsed) : 0f;

        /// <summary>0(開始)〜1(終了)。演出のフェードに使う。</summary>
        public float Progress
        {
            get
            {
                if (Data == null || Data.Duration <= 0.0001f) return IsActive ? 0f : 1f;
                return Mathf.Clamp01(Elapsed / Data.Duration);
            }
        }

        /// <summary>この場所を効果の中心とする（花火なら打ち上げ会場、盆踊りならやぐら）。</summary>
        public Vector3 Center => transform.position;

        /// <summary>Duration を過ぎたときに一度だけ通知する。EventManager が後片付けに使う。</summary>
        public event Action<EventObject> Finished;

        /// <summary>データを与える。BuildCost には開催費用を入れておく (§31)。</summary>
        public void Configure(FestivalEventData data, Vector3 center)
        {
            Data = data;
            transform.position = center;

            if (data != null)
            {
                ObjectId = data.Id;
                BuildCost = data.Cost;
                if (string.IsNullOrEmpty(InstanceId))
                    InstanceId = $"{data.Id}_{GetInstanceID():X}";
                name = $"Event_{data.Id}";
            }
        }

        /// <summary>開催開始。</summary>
        public void Begin()
        {
            Elapsed = 0f;
            IsActive = true;
        }

        /// <summary>途中で打ち切る（StopAll / 祭り終了）。</summary>
        public void Finish()
        {
            if (!IsActive) return;
            IsActive = false;
            Finished?.Invoke(this);
        }

        public override void OnFestivalEnd() => Finish();

        public override void TickFestival(float dt, FestivalClock clock) => Advance(dt);

        /// <summary>EventManager から毎フレーム呼ばれる (§57)。</summary>
        public void Advance(float dt)
        {
            if (!IsActive || Data == null) return;

            Elapsed += dt;
            if (Elapsed >= Data.Duration) Finish();
        }
    }
}

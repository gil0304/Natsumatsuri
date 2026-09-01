using System;
using Matsuri.Audio;
using Matsuri.CameraSystem;
using Matsuri.Data;
using Matsuri.Economy;
using Matsuri.Events;
using Matsuri.Festival;
using Matsuri.Stalls;
using Matsuri.TimeSystem;
using Matsuri.UI;
using Matsuri.Visitors;
using UnityEngine;

namespace Matsuri.Core
{
    /// <summary>
    /// 仕様書 §50。各マネージャへの参照を持つだけのハブ。
    /// 「巨大な GameManager 一つに全部書かない」ため、ここにゲームロジックは書かない。
    /// フェーズ遷移と、マネージャ同士の橋渡しだけを担当する。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("設定")]
        public BalanceConfig Balance;
        public MatsuriCatalog Catalog;

        [Header("マネージャ (§50)")]
        public TimeManager Time;
        public EconomyManager Economy;
        public FestivalManager Festival;
        public StallManager Stalls;
        public VisitorManager Visitors;
        public EventManager Events;
        public AudioManager Audio;
        public CameraManager Cameras;
        public UIManager UI;
        public ScriptManager Script;

        [Header("状態")]
        [SerializeField] GamePhase _phase = GamePhase.Editing;
        public GameMode Mode = GameMode.Free;

        public GamePhase Phase => _phase;

        /// <summary>フェーズが変わったときに通知する。</summary>
        public event Action<GamePhase> PhaseChanged;

        /// <summary>祭りが始まった瞬間 (§80 の象徴的な瞬間)。</summary>
        public event Action FestivalStarted;

        /// <summary>祭りが終わった瞬間。結果画面 (§36) はこれを受ける。</summary>
        public event Action FestivalEnded;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void SetPhase(GamePhase phase)
        {
            if (_phase == phase) return;
            _phase = phase;
            MatsuriLog.Info($"フェーズ変更: {phase}");
            PhaseChanged?.Invoke(phase);

            if (phase == GamePhase.Running) FestivalStarted?.Invoke();
            else if (phase == GamePhase.Finished) FestivalEnded?.Invoke();
        }

        /// <summary>RUN ボタン (§40)。コードを解析して祭りを建てる。</summary>
        public void RunCode(string sourceCode) => Script?.RunCode(sourceCode);

        /// <summary>「開催」ボタン。祭りを開始する。</summary>
        public void StartFestival() => Festival?.StartFestival();

        /// <summary>「リセット」ボタン。建てた物をすべて消して最初に戻す。</summary>
        public void ResetFestival() => Festival?.ResetAll();
    }
}

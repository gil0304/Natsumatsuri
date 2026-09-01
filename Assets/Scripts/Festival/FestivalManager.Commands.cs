using System;
using Matsuri.Art;
using Matsuri.Core;
using Matsuri.Data;
using Matsuri.Script;
using Matsuri.Script.Commands;
using Matsuri.Stalls;
using UnityEngine;

namespace Matsuri.Festival
{
    /// <summary>
    /// 仕様書 §53。<see cref="IFestivalCommandSink"/> の実装。
    /// Matsuri Script のコマンド1つ1つを、実際の GameObject に変える唯一の場所。
    ///
    /// どのコマンドも同じ手順を踏む:
    ///   1. カタログからデータを引く（無ければ日本語で理由を伝えて中止）
    ///   2. 費用を EconomyManager に払う（足りなければ建てずに理由を伝える §31）
    ///   3. Prefab があれば Instantiate、無ければ手続き生成 (§69)
    ///   4. 置き場所を決める（重なりは FestivalPlacement がずらす）
    ///   5. コンポーネントを付けて設定し、建設演出を始める (§39)
    /// </summary>
    public sealed partial class FestivalManager
    {
        /// <summary>置き場所を決めるときの、大きさが分からない場合の既定半径（m）。</summary>
        const float DefaultStallRadius = 3.2f;
        const float DefaultFacilityRadius = 1.4f;
        const float DefaultDecorationRadius = 1.1f;

        int _instanceCounter;

        // ────────────────────────────────────────────────────
        // 屋台 (§13 / §19)
        // ────────────────────────────────────────────────────

        public void CreateStall(CreateStallCommand cmd)
        {
            if (cmd == null) return;

            var catalog = Catalog;
            StallData data = catalog != null ? catalog.GetStall(cmd.StallId) : null;

            if (data == null)
            {
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目: 「{cmd.SourceName}」という屋台のデータが見つかりませんでした。",
                    DiagnosticSeverity.Error);
                return;
            }

            long cost = cmd.Cost > 0 ? cmd.Cost : data.BuildCost;
            if (!TryPay(cost, data.DisplayName, cmd.SourceLine)) return;

            GameObject visual = null;
            try
            {
                visual = data.Prefab != null
                    ? Instantiate(data.Prefab, BuiltRoot)
                    : ProceduralStallFactory.Build(data, data.VisualRecipe, BuiltRoot);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"屋台「{data.DisplayName}」の生成に失敗しました: {e}");
            }

            if (visual == null)
            {
                ReportRuntimeMessage($"「{data.DisplayName}」の見た目を作れませんでした。", DiagnosticSeverity.Error);
                return;
            }

            visual.name = $"Stall_{data.Id}_{++_instanceCounter:00}";
            PlaceVisual(visual, cmd.Position, cmd.RotationDegrees, DefaultStallRadius, data.DisplayName, cmd.SourceLine);

            var stall = GetOrAdd<Stall>(visual);
            stall.ObjectId = data.Id;
            stall.InstanceId = visual.name;
            stall.SourceLine = cmd.SourceLine;
            stall.BuildCost = cost;

            int price = cmd.Price.HasValue ? data.ClampPrice(cmd.Price.Value) : data.DefaultPrice;
            if (cmd.Price.HasValue && price != cmd.Price.Value)
            {
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目: 「{data.DisplayName}」の値段 {cmd.Price.Value}円 は範囲外なので {price}円 にしました。（{data.MinPrice}〜{data.MaxPrice}円）",
                    DiagnosticSeverity.Warning);
            }

            stall.Configure(data, price);

            var stalls = StallsManager;
            if (stalls != null) stalls.Register(stall);

            RegisterAndAnimate(stall, visual);
        }

        // ────────────────────────────────────────────────────
        // 装飾 (§21)
        // ────────────────────────────────────────────────────

        public void CreateDecoration(CreateDecorationCommand cmd)
        {
            if (cmd == null) return;

            var catalog = Catalog;
            DecorationData data = catalog != null ? catalog.GetDecoration(cmd.DecorationId) : null;

            if (data == null)
            {
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目: 「{cmd.SourceName}」という装飾のデータが見つかりませんでした。",
                    DiagnosticSeverity.Error);
                return;
            }

            long cost = cmd.Cost > 0 ? cmd.Cost : data.BuildCost;
            if (!TryPay(cost, data.DisplayName, cmd.SourceLine)) return;

            GameObject visual = null;
            try
            {
                visual = data.Prefab != null
                    ? Instantiate(data.Prefab, BuiltRoot)
                    : ProceduralDecorationFactory.Build(data, BuiltRoot);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"装飾「{data.DisplayName}」の生成に失敗しました: {e}");
            }

            if (visual == null)
            {
                ReportRuntimeMessage($"「{data.DisplayName}」の見た目を作れませんでした。", DiagnosticSeverity.Error);
                return;
            }

            visual.name = $"Deco_{data.Id}_{++_instanceCounter:00}";
            PlaceVisual(visual, cmd.Position, cmd.RotationDegrees, DefaultDecorationRadius, data.DisplayName, cmd.SourceLine);

            var decoration = GetOrAdd<Decoration>(visual);
            decoration.InstanceId = visual.name;
            decoration.SourceLine = cmd.SourceLine;
            decoration.BuildCost = cost;
            decoration.Configure(data);

            RegisterAndAnimate(decoration, visual);
        }

        // ────────────────────────────────────────────────────
        // 設備 (§20)
        // ────────────────────────────────────────────────────

        public void CreateFacility(CreateFacilityCommand cmd)
        {
            if (cmd == null) return;

            var catalog = Catalog;
            FacilityData data = catalog != null ? catalog.GetFacility(cmd.FacilityId) : null;

            if (data == null)
            {
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目: 「{cmd.SourceName}」という設備のデータが見つかりませんでした。",
                    DiagnosticSeverity.Error);
                return;
            }

            long cost = cmd.Cost > 0 ? cmd.Cost : data.BuildCost;
            if (!TryPay(cost, data.DisplayName, cmd.SourceLine)) return;

            GameObject visual = null;
            try
            {
                visual = data.Prefab != null
                    ? Instantiate(data.Prefab, BuiltRoot)
                    : ProceduralFacilityFactory.Build(data, BuiltRoot);
            }
            catch (Exception e)
            {
                MatsuriLog.Error($"設備「{data.DisplayName}」の生成に失敗しました: {e}");
            }

            if (visual == null)
            {
                ReportRuntimeMessage($"「{data.DisplayName}」の見た目を作れませんでした。", DiagnosticSeverity.Error);
                return;
            }

            visual.name = $"Facility_{data.Id}_{++_instanceCounter:00}";
            PlaceVisual(visual, cmd.Position, cmd.RotationDegrees, DefaultFacilityRadius, data.DisplayName, cmd.SourceLine);

            var facility = GetOrAdd<Facility>(visual);
            facility.InstanceId = visual.name;
            facility.SourceLine = cmd.SourceLine;
            facility.BuildCost = cost;
            facility.Configure(data);

            RegisterAndAnimate(facility, visual);
        }

        // ────────────────────────────────────────────────────
        // 値段の変更 (§32)
        // ────────────────────────────────────────────────────

        public void SetPrice(SetPriceCommand cmd)
        {
            if (cmd == null) return;

            var stalls = StallsManager;
            if (stalls == null) return;

            var targets = stalls.GetById(cmd.StallId);
            if (targets == null || targets.Count == 0)
            {
                ReportRuntimeMessage(
                    $"{cmd.SourceLine}行目: 「{cmd.SourceName}」がまだ建っていないので、値段を変えられませんでした。",
                    DiagnosticSeverity.Warning);
                return;
            }

            stalls.SetPriceForAll(cmd.StallId, cmd.Price);
            ReportRuntimeMessage($"「{cmd.SourceName}」の値段を {cmd.Price}円 にしました。");
        }

        // ────────────────────────────────────────────────────
        // イベント (§22)
        // ────────────────────────────────────────────────────

        public void StartFireworks(StartFireworksCommand cmd)
        {
            if (cmd == null) return;

            var events = Game != null ? Game.Events : null;
            if (events == null)
            {
                ReportRuntimeMessage("花火を打ち上げる仕組みが用意されていません。", DiagnosticSeverity.Error);
                return;
            }

            var data = Catalog != null ? Catalog.GetEvent(MatsuriIds.Fireworks) : null;
            long cost = cmd.Cost > 0 ? cmd.Cost : (data != null ? data.Cost : 300000L);

            if (!TryPay(cost, "花火", cmd.SourceLine)) return;

            events.PlayFireworks(string.IsNullOrEmpty(cmd.Kind) ? MatsuriIds.FireworkKiku : cmd.Kind);
            ReportRuntimeMessage($"花火「{cmd.SourceName}」を打ち上げました。");
        }

        public void StartBonOdori(StartBonOdoriCommand cmd)
        {
            if (cmd == null) return;

            var events = Game != null ? Game.Events : null;
            if (events == null)
            {
                ReportRuntimeMessage("盆踊りを始める仕組みが用意されていません。", DiagnosticSeverity.Error);
                return;
            }

            var data = Catalog != null ? Catalog.GetEvent(MatsuriIds.BonOdori) : null;
            long cost = cmd.Cost > 0 ? cmd.Cost : (data != null ? data.Cost : 150000L);

            if (!TryPay(cost, "盆踊り", cmd.SourceLine)) return;

            Vector3 position = cmd.Position.HasValue ? _placement.ToWorld(cmd.Position.Value) : _placement.Center;
            events.StartBonOdori(position);
            ReportRuntimeMessage("盆踊りが始まりました。");
        }

        public void StartTaiko(StartTaikoCommand cmd)
        {
            if (cmd == null) return;

            var events = Game != null ? Game.Events : null;
            if (events == null)
            {
                ReportRuntimeMessage("太鼓を鳴らす仕組みが用意されていません。", DiagnosticSeverity.Error);
                return;
            }

            var data = Catalog != null ? Catalog.GetEvent(MatsuriIds.Taiko) : null;
            long cost = cmd.Cost > 0 ? cmd.Cost : (data != null ? data.Cost : 100000L);

            if (!TryPay(cost, "太鼓演奏", cmd.SourceLine)) return;

            Vector3 position = cmd.Position.HasValue ? _placement.ToWorld(cmd.Position.Value) : _placement.Center;
            events.StartTaiko(position);
            ReportRuntimeMessage("太鼓の演奏が始まりました。");
        }

        // ────────────────────────────────────────────────────
        // メッセージ (§41)
        // ────────────────────────────────────────────────────

        /// <summary>実行時の出来事をプレイヤーに伝える。必ず日本語で、何が起きたか分かる文にする。</summary>
        public void ReportRuntimeMessage(string message, DiagnosticSeverity severity = DiagnosticSeverity.Info)
        {
            if (string.IsNullOrEmpty(message)) return;

            switch (severity)
            {
                case DiagnosticSeverity.Error: MatsuriLog.Warn(message); break;
                case DiagnosticSeverity.Warning: MatsuriLog.Warn(message); break;
                default: MatsuriLog.Info(message); break;
            }

            var ui = Game != null ? Game.UI : null;
            if (ui != null) ui.ShowToast(message, severity);
        }

        // ────────────────────────────────────────────────────
        // 共通処理
        // ────────────────────────────────────────────────────

        /// <summary>費用を払う。足りなければ「なぜ建たなかったか」を日本語で伝えて false を返す (§31)。</summary>
        bool TryPay(long cost, string what, int sourceLine)
        {
            var economy = Economy;
            if (economy == null) return true;   // 経済システムが無い状況（テスト等）では建てられる

            if (economy.TrySpend(cost, what)) return true;

            ReportRuntimeMessage(
                $"{sourceLine}行目: 予算が足りないので「{what}」を建てられませんでした。" +
                $"（必要 ¥{cost:N0} / 残り ¥{economy.Budget:N0}）",
                DiagnosticSeverity.Error);
            return false;
        }

        /// <summary>大きさを測って置き場所を決め、位置と向きを与える。</summary>
        void PlaceVisual(GameObject visual, GridPos grid, float rotationDegrees, float fallbackRadius, string displayName, int sourceLine)
        {
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            float radius = FestivalPlacement.EstimateRadius(visual, fallbackRadius);
            Vector3 position = _placement.Resolve(grid, radius, out bool moved);

            visual.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationDegrees, 0f));

            if (moved)
            {
                ReportRuntimeMessage(
                    $"{sourceLine}行目: ({grid.X}, {grid.Z}) には他の物があったので、「{displayName}」を少しずらして置きました。",
                    DiagnosticSeverity.Info);
            }
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            if (existing != null) return existing;
            return go.AddComponent<T>();
        }
    }
}

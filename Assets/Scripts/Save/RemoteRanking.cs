using System;
using System.Collections.Generic;
using System.Text;
using Matsuri.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Matsuri.Save
{
    /// <summary>
    /// 仕様書 §37。オンラインランキングの接続先。
    /// ScriptableObject にはせず、static + PlayerPrefs で持つ。
    /// **既定は空＝無効**。空のあいだは一切通信しない。
    /// </summary>
    public static class RankingSettings
    {
        public const string EndpointPrefKey = "matsuri.ranking.endpoint";
        public const string ApiKeyPrefKey   = "matsuri.ranking.apikey";

        /// <summary>通信のタイムアウト（秒）。仕様どおり 8 秒。</summary>
        public const float DefaultTimeoutSeconds = 8f;

        /// <summary>通信のタイムアウト（秒）。</summary>
        public static float TimeoutSeconds = DefaultTimeoutSeconds;

        static string _endpoint;
        static string _apiKey;
        static bool _loaded;

        /// <summary>送信先のURL。空ならオンラインランキングは無効。</summary>
        public static string Endpoint
        {
            get { EnsureLoaded(); return _endpoint; }
            set
            {
                EnsureLoaded();
                _endpoint = Normalize(value);
                Persist(EndpointPrefKey, _endpoint);
            }
        }

        /// <summary>APIキー。サーバー側が要らなければ空でよい。</summary>
        public static string ApiKey
        {
            get { EnsureLoaded(); return _apiKey; }
            set
            {
                EnsureLoaded();
                _apiKey = value != null ? value.Trim() : "";
                Persist(ApiKeyPrefKey, _apiKey);
            }
        }

        /// <summary>接続先が設定されているか。false のあいだは通信しない。</summary>
        public static bool IsConfigured => IsValidEndpoint(Endpoint);

        /// <summary>http / https の URL だけを受け付ける。</summary>
        public static bool IsValidEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return false;
            return endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>設定を消して無効に戻す。</summary>
        public static void Clear()
        {
            Endpoint = "";
            ApiKey = "";
            TimeoutSeconds = DefaultTimeoutSeconds;
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                _endpoint = Normalize(PlayerPrefs.GetString(EndpointPrefKey, ""));
                _apiKey = PlayerPrefs.GetString(ApiKeyPrefKey, "");
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"オンラインランキングの設定を読めませんでした: {e.Message}");
                _endpoint = "";
                _apiKey = "";
            }
        }

        static void Persist(string key, string value)
        {
            try
            {
                PlayerPrefs.SetString(key, value ?? "");
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"オンラインランキングの設定を保存できませんでした: {e.Message}");
            }
        }

        static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            return raw.Trim().TrimEnd('/');
        }
    }

    /// <summary>
    /// 実際の通信を担う口。テストではフェイクに差し替えて
    /// 「未設定なら1回も呼ばれない」ことを確かめる。
    /// 成否とレスポンス本文（失敗時はエラー文）をコールバックで返す。
    /// </summary>
    public interface IRankingTransport
    {
        void Post(string url, string apiKey, string json, float timeoutSeconds, Action<bool, string> onCompleted);
        void Get(string url, string apiKey, float timeoutSeconds, Action<bool, string> onCompleted);
    }

    /// <summary>UnityWebRequest による既定の通信。例外は投げず、失敗はコールバックで返す。</summary>
    public sealed class UnityWebRequestTransport : IRankingTransport
    {
        public void Post(string url, string apiKey, string json, float timeoutSeconds, Action<bool, string> onCompleted)
        {
            try
            {
                var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
                request.uploadHandler = new UploadHandlerRaw(new UTF8Encoding(false).GetBytes(json ?? ""));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                Send(request, apiKey, timeoutSeconds, onCompleted);
            }
            catch (Exception e)
            {
                onCompleted?.Invoke(false, e.Message);
            }
        }

        public void Get(string url, string apiKey, float timeoutSeconds, Action<bool, string> onCompleted)
        {
            try
            {
                Send(UnityWebRequest.Get(url), apiKey, timeoutSeconds, onCompleted);
            }
            catch (Exception e)
            {
                onCompleted?.Invoke(false, e.Message);
            }
        }

        static void Send(UnityWebRequest request, string apiKey, float timeoutSeconds, Action<bool, string> onCompleted)
        {
            if (!string.IsNullOrEmpty(apiKey)) request.SetRequestHeader("X-Matsuri-Key", apiKey);
            request.timeout = Mathf.Clamp(Mathf.RoundToInt(timeoutSeconds), 1, 60);

            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                bool ok = false;
                string body;
                try
                {
                    ok = request.result == UnityWebRequest.Result.Success;
                    body = ok
                        ? (request.downloadHandler != null ? request.downloadHandler.text : "")
                        : request.error;
                }
                catch (Exception e)
                {
                    body = e.Message;
                }
                finally
                {
                    request.Dispose();
                }
                onCompleted?.Invoke(ok, body);
            };
        }
    }

    /// <summary>
    /// 仕様書 §37。オンラインのランキング。
    ///
    /// - 送信先が未設定なら **一切通信しない**（勝手に外へ出さない）
    /// - 送信に失敗したら <see cref="LocalJsonRanking"/> の送信キューに貯め、
    ///   次に送れたときにまとめて送る（オフライン耐性）
    /// - タイムアウトは 8 秒。失敗してもゲームは止めない。例外も投げない
    /// - <see cref="IsOnline"/> は最後の通信結果から決まる
    ///
    /// <see cref="IRankingBackend"/> は同期APIなので、上位一覧は
    /// 直近の取得結果のキャッシュを返し、裏で更新をかける。
    /// キャッシュが空のあいだはローカルの記録を見せる（画面が空にならないように）。
    /// </summary>
    public sealed class RemoteRanking : IRankingBackend
    {
        /// <summary>上位一覧を取りに行くときの既定件数。</summary>
        public const int DefaultTopCount = 20;

        [Serializable]
        sealed class SubmitPayload
        {
            public string Game = "MATSURI.exe";
            public int Version = 1;
            public List<FestivalResult> Entries = new List<FestivalResult>();
        }

        [Serializable]
        sealed class TopPayload
        {
            public List<FestivalResult> Entries = new List<FestivalResult>();
        }

        readonly LocalJsonRanking _fallback;
        readonly IRankingTransport _transport;
        readonly List<FestivalResult> _cachedTop = new List<FestivalResult>();

        string _endpoint;
        string _apiKey;
        bool _online;
        bool _sending;
        bool _fetching;

        public RemoteRanking(string endpoint = null, string apiKey = null,
                             LocalJsonRanking fallback = null, IRankingTransport transport = null)
        {
            _endpoint = endpoint != null ? endpoint.Trim().TrimEnd('/') : RankingSettings.Endpoint;
            _apiKey = apiKey ?? RankingSettings.ApiKey;
            _fallback = fallback ?? new LocalJsonRanking();
            _transport = transport ?? new UnityWebRequestTransport();
        }

        public string DisplayName => IsConfigured ? "オンラインランキング" : "オンラインランキング（未設定）";

        /// <summary>最後の通信が成功していれば true。一度も通信していなければ false。</summary>
        public bool IsOnline => _online;

        /// <summary>送信先が設定されているか。false のあいだは通信しない。</summary>
        public bool IsConfigured => RankingSettings.IsValidEndpoint(_endpoint);

        /// <summary>設定されている送信先。</summary>
        public string Endpoint => _endpoint;

        /// <summary>オフライン時に貯まっている件数。</summary>
        public int PendingCount => _fallback.PendingCount;

        /// <summary>オフライン時の保存先（ローカルランキング）。</summary>
        public LocalJsonRanking Fallback => _fallback;

        /// <summary>通信の成否が変わったときに通知する（UIの表示切り替え用）。</summary>
        public event Action<bool> OnlineStateChanged;

        // ── 送信 ──────────────────────────────────────────────

        /// <summary>
        /// 結果を送る。未設定なら通信せず、何も貯めない。
        /// 設定済みなら送信キューに積んでから、キューをまとめて送る。
        /// </summary>
        public void Submit(FestivalResult result)
        {
            if (result == null)
            {
                MatsuriLog.Warn("オンラインランキングに送る結果が空です。");
                return;
            }

            if (!IsConfigured)
            {
                MatsuriLog.Info("オンラインランキングの送信先が未設定のため、送信しませんでした。");
                return;
            }

            _fallback.EnqueuePending(result);
            FlushQueue();
        }

        /// <summary>
        /// 貯まっている結果をまとめて送る。
        /// 未設定・送信中・キューが空なら何もしない。
        /// </summary>
        public void FlushQueue()
        {
            if (!IsConfigured || _sending) return;

            var pending = _fallback.GetPending();
            if (pending.Count == 0) return;

            var payload = new SubmitPayload { Entries = pending };
            string json;
            try
            {
                json = JsonUtility.ToJson(payload);
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"オンラインランキングへ送るJSONを作れませんでした: {e.Message}");
                return;
            }

            _sending = true;
            _transport.Post(BuildUrl("submit"), _apiKey, json, RankingSettings.TimeoutSeconds,
                (ok, body) =>
                {
                    _sending = false;
                    SetOnline(ok);

                    if (ok)
                    {
                        for (int i = 0; i < pending.Count; i++) _fallback.RemovePending(pending[i]);
                        MatsuriLog.Always($"オンラインランキングへ {pending.Count} 件送信しました。");
                    }
                    else
                    {
                        MatsuriLog.Warn(
                            $"オンラインランキングへ送れませんでした（{pending.Count} 件は次回まとめて送ります）: {body}");
                    }
                });
        }

        // ── 取得 ──────────────────────────────────────────────

        /// <summary>
        /// 上位 count 件。直近に取れた一覧を返し、裏で新しい一覧を取りに行く。
        /// まだ一度も取れていなければローカルの記録を返す。
        /// </summary>
        public List<FestivalResult> GetTop(int count)
        {
            var list = new List<FestivalResult>();
            if (count <= 0) return list;

            if (IsConfigured) RefreshTop(Mathf.Max(count, DefaultTopCount));

            var source = _cachedTop.Count > 0 ? _cachedTop : _fallback.GetTop(count);
            int take = Mathf.Min(count, source.Count);
            for (int i = 0; i < take; i++) list.Add(source[i]);

            return list;
        }

        /// <summary>サーバーから上位一覧を取り直す。未設定・取得中なら何もしない。</summary>
        public void RefreshTop(int count, Action onCompleted = null)
        {
            if (!IsConfigured || _fetching)
            {
                onCompleted?.Invoke();
                return;
            }

            _fetching = true;
            string url = BuildUrl("top") + "?count=" + Mathf.Clamp(count, 1, 200).ToString();

            _transport.Get(url, _apiKey, RankingSettings.TimeoutSeconds, (ok, body) =>
            {
                _fetching = false;
                SetOnline(ok);

                if (ok) ApplyTopResponse(body);
                else MatsuriLog.Warn($"オンラインランキングを取得できませんでした: {body}");

                onCompleted?.Invoke();
            });
        }

        /// <summary>
        /// この結果の順位（1始まり）。
        /// 取得済みの一覧から数える。まだ取れていなければローカルの順位を返す。
        /// </summary>
        public int GetRank(FestivalResult result)
        {
            if (result == null) return 0;
            if (_cachedTop.Count == 0) return _fallback.GetRank(result);

            int better = 0;
            for (int i = 0; i < _cachedTop.Count; i++)
            {
                var other = _cachedTop[i];
                if (other == null || result.IsSameEntry(other)) continue;

                if (other.Revenue > result.Revenue) better++;
                else if (other.Revenue == result.Revenue && other.TotalScore > result.TotalScore) better++;
            }
            return better + 1;
        }

        // ── 内部 ──────────────────────────────────────────────

        void ApplyTopResponse(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;

            try
            {
                string json = body.TrimStart();
                if (json.StartsWith("[", StringComparison.Ordinal))
                    json = "{\"Entries\":" + json + "}";   // 配列だけ返すサーバーにも合わせる

                var payload = JsonUtility.FromJson<TopPayload>(json);
                if (payload == null || payload.Entries == null) return;

                _cachedTop.Clear();
                for (int i = 0; i < payload.Entries.Count; i++)
                    if (payload.Entries[i] != null) _cachedTop.Add(payload.Entries[i]);

                _cachedTop.Sort(CompareByRevenue);
            }
            catch (Exception e)
            {
                MatsuriLog.Warn($"オンラインランキングの応答を解釈できませんでした: {e.Message}");
            }
        }

        void SetOnline(bool value)
        {
            if (_online == value) return;
            _online = value;
            MatsuriLog.Always(value ? "オンラインランキングに接続しました。" : "オンラインランキングに接続できません。ローカルに貯めます。");
            OnlineStateChanged?.Invoke(value);
        }

        string BuildUrl(string path)
        {
            string root = _endpoint ?? "";
            if (root.EndsWith("/", StringComparison.Ordinal)) root = root.TrimEnd('/');
            return root + "/" + path;
        }

        static int CompareByRevenue(FestivalResult a, FestivalResult b)
        {
            if (a == null) return b == null ? 0 : 1;
            if (b == null) return -1;

            int byRevenue = b.Revenue.CompareTo(a.Revenue);
            if (byRevenue != 0) return byRevenue;

            int byScore = b.TotalScore.CompareTo(a.TotalScore);
            if (byScore != 0) return byScore;

            return string.CompareOrdinal(a.CreatedDate, b.CreatedDate);
        }
    }
}

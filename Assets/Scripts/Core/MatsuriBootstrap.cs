using Matsuri.Art;
using Matsuri.Audio;
using Matsuri.CameraSystem;
using Matsuri.Data;
using Matsuri.Economy;
using Matsuri.Events;
using Matsuri.Festival;
using Matsuri.Save;
using Matsuri.Stalls;
using Matsuri.TimeSystem;
using Matsuri.UI;
using Matsuri.Visitors;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Matsuri.Core
{
    /// <summary>
    /// シーンに置く唯一のオブジェクト。ここから世界をすべてコードで組み立てる。
    /// 仕様書 §55 のシーン方針: .unity ファイルに構成を持たせず、C#で再現可能にする。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MatsuriBootstrap : MonoBehaviour
    {
        [Header("データ")]
        [Tooltip("Matsuri/2. Build Scene が自動で差す。無ければ Resources から探す。")]
        [SerializeField] MatsuriCatalog _catalog;

        [Header("起動オプション")]
        [Tooltip("起動時にサンプルコードをエディタへ入れる (§44 1分目)。")]
        [SerializeField] bool _loadStarterCode = true;

        [Tooltip("チュートリアルを出す (§45)。")]
        [SerializeField] bool _showTutorial = true;

        [Tooltip("詳細ログ。")]
        [SerializeField] bool _verboseLog = false;

        [Tooltip("起動時のゲームモード (§46)。既定は §31 の予算100万円で遊ぶ標準のお題。")]
        [SerializeField] GameMode _mode = GameMode.Challenge;

        public static MatsuriBootstrap Instance { get; private set; }

        GameManager _game;
        Transform _root, _world, _built, _visitors, _managers, _cameras;

        void Awake()
        {
            Instance = this;
            MatsuriLog.Verbose = _verboseLog;

            ResolveCatalog();
            if (_catalog == null)
            {
                MatsuriLog.Error(
                    "MatsuriCatalog が見つかりません。Unity のメニュー Matsuri/1. Generate Data Assets を実行してください。");
                return;
            }

            BuildHierarchy();
            BuildWorld();
            BuildCameras();
            BuildManagers();
            WireUp();

            MatsuriLog.Build("Bootstrap 完了");
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void ResolveCatalog()
        {
            if (_catalog != null) return;
            _catalog = Resources.Load<MatsuriCatalog>("MatsuriCatalog");
        }

        void BuildHierarchy()
        {
            _root     = new GameObject("FESTIVAL_ROOT").transform;
            _world    = NewChild(_root, "World");
            _built    = NewChild(_root, "Built");
            _visitors = NewChild(_root, "Visitors");
            _managers = NewChild(_root, "Managers");
            _cameras  = NewChild(_root, "Cameras");

            NewChild(_built, "Stalls");
            NewChild(_built, "Decorations");
            NewChild(_built, "Facilities");
            NewChild(_built, "Events");
        }

        static Transform NewChild(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        void BuildWorld()
        {
            var bounds = _catalog.Bounds;

            // 会場の地面・参道・外周 (§6 の Stylized Realistic)
            var ground = GroundBuilder.Build(_world, bounds);

            // NavMesh は「地面だけ」から焼く。
            // NavMeshSurface を地面のルートに載せて Children に限定することで、
            // 屋根や提灯のような宙に浮く部材が歩行面として拾われるのを防ぐ。
            // 建った屋台は NavMeshObstacle として道をくり抜く (NavigationService.AddCarvingObstacle)。
            //
            // 注意: 実行時に生成したメッシュは PhysicsColliders では収集されないため、
            // ここでは RenderMeshes を使う。これは検証済みの挙動。
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.22f;
            surface.minRegionArea = 4f;
            var nav = NavigationService.Ensure(ground);
            nav.SetSurface(surface);
            nav.RebakeNow();

            // 照明 (§59) と Post Processing (§60)
            var lightingGo = new GameObject("Lighting");
            lightingGo.transform.SetParent(_world, false);
            var rig = lightingGo.AddComponent<LightingRig>();
            rig.Initialize();

            PostProcessRig.Build(_world);
        }

        void BuildCameras()
        {
            var camManagerGo = new GameObject("CameraManager");
            camManagerGo.transform.SetParent(_cameras, false);
            camManagerGo.AddComponent<CameraManager>();
        }

        void BuildManagers()
        {
            var gameGo = new GameObject("GameManager");
            gameGo.transform.SetParent(_managers, false);
            _game = gameGo.AddComponent<GameManager>();

            _game.Catalog = _catalog;
            _game.Balance = _catalog.Balance;
            _game.Mode = _mode;

            _game.Time     = AddManager<TimeManager>("TimeManager");
            _game.Economy  = AddManager<EconomyManager>("EconomyManager");
            _game.Stalls   = AddManager<StallManager>("StallManager");
            _game.Visitors = AddManager<VisitorManager>("VisitorManager");
            _game.Events   = AddManager<EventManager>("EventManager");
            _game.Audio    = AddManager<AudioManager>("AudioManager");
            _game.UI       = AddManager<UIManager>("UIManager");
            _game.Festival = AddManager<FestivalManager>("FestivalManager");
            _game.Script   = AddManager<ScriptManager>("ScriptManager");

            _game.Cameras  = Object.FindFirstObjectByType<CameraManager>();

            AddManager<GameModeController>("GameModeController");
        }

        T AddManager<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(_managers, false);
            return go.AddComponent<T>();
        }

        void WireUp()
        {
            var balance = _catalog.Balance;

            // 予算・敷地・使える屋台はモードが決める (§46 / §47)。
            // ApplyMode の中で EconomyManager.Initialize まで行われる。
            var modeController = Object.FindFirstObjectByType<GameModeController>();
            if (modeController != null)
            {
                modeController.ApplyMode(_mode, null);
                balance = _game.Balance != null ? _game.Balance : balance;
            }
            else
            {
                _game.Economy.Initialize(balance, _mode);
            }

            _game.Visitors.Initialize(_catalog, balance);
            _game.Audio.Initialize();
            _game.UI.Initialize();

            if (_loadStarterCode)
                _game.UI.SetSource(Script.MatsuriSamples.Starter);

            if (_showTutorial)
                _game.UI.ShowTutorial("たこ焼き屋を作ってみよう");
        }
    }
}

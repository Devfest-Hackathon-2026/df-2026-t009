using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Onsite.App.Hud;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class OnsiteApp : MonoBehaviour
    {
        public OnsiteApi Api => OnsiteApi.Instance;
        public string Nickname { get; private set; }
        public AvatarLook Look { get; private set; }
        public double Lat { get; private set; }
        public double Lng { get; private set; }
        public float Accuracy { get; private set; }
        public bool HasLocation { get; private set; }
        public bool DemoMode { get; private set; }
        public bool ShareFootprints { get; private set; }
        public Profile Profile { get; private set; }
        public HashSet<string> OpenedIds { get; } = new HashSet<string>();
        public List<KokoEvent> Events { get; } = new List<KokoEvent>();
        public HashSet<string> JoinedIds { get; } = new HashSet<string>();
        public TrailStore Trail { get; private set; }
        public bool Busy { get; private set; }
        public Drop Selected { get; private set; }
        public string Uid => Api?.Auth?.Uid;
        public MapWorld World { get; private set; }
        public MarkerField Markers { get; private set; }
        public OrbitCamera Orbit { get; private set; }
        public Camera MapCamera { get; private set; }

        VisualElement root, screens, confirmation;
        MapHud hud;
        WorldTags tags;
        Ui.Toast toast;
        SequenceOverlay sequence;
        DeviceBridge bridge;
        AvatarRig avatar;
        PathLayer paths;
        OnsiteApi subscribedApi;
        VisualElement currentScreen;
        string view = "map";
        string observedUid;
        int identityVersion;
        bool refreshingMeta, placing, pressOnMap, playingSequence;
        float nextMeta, nextHud, nextShare, nextComments;
        Vector2 pressPosition;
        float pressTime;
        double originLat, originLng;
        long noticesSeen;
        readonly HashSet<string> completionShown = new HashSet<string>();
        readonly Queue<string> completions = new Queue<string>();
        readonly List<Callable.FootprintPoint> pendingFootprints = new List<Callable.FootprintPoint>();
        float savedDistance = 260, savedPitch = 52, savedYaw;
        bool portraitCamera;
        bool tourRequested, tourDataReady;
        DemoTour demoTour;
        Transform portraitFocus, savedCameraTarget;

        void Awake()
        {
            Sound.Initialize();
            // UI のタップを確実に受けるため、入力の窓口（EventSystem）を用意する
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // デモの出発点：浅草駅の近く（雷門まで約110m）
        const double DemoLat = 35.70990, DemoLng = 139.79560;

        IEnumerator Start()
        {
            Nickname = PlayerPrefs.GetString("onsite.name", "");
            Look = AvatarLook.Load();
            Trail = new TrailStore();
            completionShown.UnionWith(PlayerPrefs.GetString("onsite.completedEvents", "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries));
            long.TryParse(PlayerPrefs.GetString("onsite.noticesSeen", "0"), out noticesSeen);
            tourRequested = QueryFlag("tour");
            DemoMode = tourRequested || PlayerPrefs.GetInt("onsite.demo", 0) == 1 || QueryFlag("demo");
            ShareFootprints = PlayerPrefs.GetInt("onsite.shareFootprints", 0) == 1;
            World = FindFirstObjectByType<MapWorld>();
            Markers = FindFirstObjectByType<MarkerField>();
            paths = FindFirstObjectByType<PathLayer>();
            avatar = FindFirstObjectByType<AvatarRig>();
            Orbit = FindFirstObjectByType<OrbitCamera>();
            MapCamera = Orbit != null ? Orbit.GetComponent<Camera>() : Camera.main;
            bridge = FindFirstObjectByType<DeviceBridge>();
            if (bridge == null) bridge = new GameObject("OnsiteBridge").AddComponent<DeviceBridge>();
            avatar?.Apply(Look);
            BuildUi();
            UpdateTrail();
            if (Orbit != null)
            {
                Orbit.IsOverUi = IsOverUi;
                if (tourRequested) Orbit.ScriptedMotion = true;
            }
            if (World != null) World.OriginChanged += OriginChanged;
            bridge.OnLocation += LocationChanged;
            bridge.LocationError += LocationProblem;
            bridge.PhotoError += Toast;
            bridge.OnPhotoReady += PhotoReady;
            bridge.StartLocation();
            if (DemoMode) SetPosition(DemoLat, DemoLng, 0);
            // 確認・撮影用：?dist=150&pitch=40 でカメラ、?raw=1 で地図の効果を切る
            if (Orbit != null && float.TryParse(QueryValue("dist"), out var qd)) Orbit.distance = qd;
            if (Orbit != null && float.TryParse(QueryValue("pitch"), out var qp)) Orbit.pitch = qp;
            Shader.SetGlobalFloat("_OnsiteRaw", QueryFlag("raw") ? 1 : 0);
            if (string.IsNullOrEmpty(Nickname)) ShowOnboarding();
            if (!tourRequested && QueryFlag("debug") && !string.IsNullOrEmpty(Nickname)) ShowDebug();
            if (tourRequested)
            {
                int.TryParse(QueryValue("start"), out var firstStep);
                // 録画用の自動デモでは、操作が無くても音を鳴らす（ブラウザー側で自動再生が許されているとき）
                Sound.UserGesture();
                demoTour = new DemoTour(this, Mathf.Clamp(firstStep, 1, 7));
                StartCoroutine(demoTour.Run());
            }
            yield return null;
            ApiBootstrap.Ensure();
            nextMeta = Time.unscaledTime + 15;
            if (Api == null)
            {
                tourDataReady = true;
                Toast("通信設定が見つかりません。OnsiteApi の FirebaseConfig を確認してください");
                yield break;
            }
            subscribedApi = Api;
            observedUid = Uid;
            Api.Auth.SignedOut += ForgetPrivateUi;
            Api.DropsChanged += DropsChanged;
            Api.MyLogChanged += LogChanged;
            Api.NoticesChanged += NoticesChanged;
            Api.Error += Toast;
            yield return Api.EnsureSignedIn();
            yield return Api.Refresh();
            DropsChanged();
            LogChanged();
            yield return RefreshMetadata();
            tourDataReady = true;
        }

        void BuildUi()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.AddToClassList("onsite-root");
            root.pickingMode = PickingMode.Ignore;
            root.RegisterCallback<PointerDownEvent>(_ => Sound.UserGesture(), TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationSubmitEvent>(_ => Sound.UserGesture(), TrickleDown.TrickleDown);
            Sound.SetMapActive(true);
            var sheet = Resources.Load<StyleSheet>("onsite");
            if (sheet != null) root.styleSheets.Add(sheet);
            tags = new WorldTags(this);
            root.Add(tags);
            hud = new MapHud(this);
            root.Add(hud);
            screens = new VisualElement { name = "screens", pickingMode = PickingMode.Ignore };
            screens.AddToClassList("screen-layer");
            root.Add(screens);
            sequence = new SequenceOverlay();
            root.Add(sequence);
            toast = new Ui.Toast();
            root.Add(toast);
            root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                root.EnableInClassList("narrow", root.resolvedStyle.width < 850);
                root.EnableInClassList("compact", root.resolvedStyle.height < 950);
                root.EnableInClassList("landscape", root.resolvedStyle.width > root.resolvedStyle.height);
                // スマホ縦持ち：下にタブ、選んだ投稿は下から出るシート
                root.EnableInClassList("phone", root.resolvedStyle.width < 800 && root.resolvedStyle.height > root.resolvedStyle.width);
            });
            hud.Refresh();
        }

        // アプリ版には URL が無いので、画面のボタンから「次の起動だけ有効な指定」を入れられるようにする
        static readonly HashSet<string> launchFlags = new HashSet<string>();
        public static void RestartWith(params string[] flags)
        {
            launchFlags.Clear(); foreach (var f in flags) launchFlags.Add(f);
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        static bool QueryFlag(string key)
        {
            if (launchFlags.Contains(key)) return true;
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri)) return false;
            return uri.Query.TrimStart('?').Split('&').Any(s => s == key + "=1");
        }

        static string QueryValue(string key)
        {
            if (!Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out var uri)) return null;
            var hit = uri.Query.TrimStart('?').Split('&').FirstOrDefault(s => s.StartsWith(key + "="));
            return hit?.Substring(key.Length + 1);
        }

        void Update()
        {
            if (root == null) return;
            if (Input.GetMouseButtonDown(0)) Sound.UserGesture();
            sequence?.Pump();
            if (Api?.Auth != null && Uid != observedUid) { ForgetPrivateUi(); observedUid = Uid; }
            if (Time.unscaledTime >= nextHud)
            {
                nextHud = Time.unscaledTime + 0.5f;
                if (Trail.CheckDay()) { UpdateTrail(); RefreshScreen(); }
                hud.Refresh();
            }
            if (Api != null && Time.unscaledTime >= nextMeta && !refreshingMeta)
            {
                nextMeta = Time.unscaledTime + 15;
                StartCoroutine(RefreshMetadata());
            }
            if (ShareFootprints && Time.unscaledTime >= nextShare)
            {
                nextShare = Time.unscaledTime + 30;
                if (pendingFootprints.Count > 0 && Api != null && !sendingFootprints)
                    StartCoroutine(FlushFootprints());
            }
            if (currentScreen is LetterScreen letter && Api != null && Time.unscaledTime >= nextComments)
            {
                nextComments = Time.unscaledTime + 6;
                LoadComments(letter);
            }
            if (!tourRequested && !playingSequence && completions.Count > 0 && view != "onboarding" && !Busy)
                PlayComplete(completions.Dequeue());
            demoTour?.UpdateCamera();
            HandleMapTap();
        }

        void LateUpdate()
        {
            if (portraitCamera && portraitFocus != null && avatar != null)
                portraitFocus.position = avatar.transform.position + Vector3.up * (avatar.lift + avatar.scale * 0.55f);
            if (tags != null) tags.Project(view == "map" && !placing);
        }

        void OnDestroy()
        {
            demoTour?.Dispose();
            Sound.StopLoop("verify");
            Sound.StopLoop("ambient");
            if (subscribedApi != null)
            {
                subscribedApi.DropsChanged -= DropsChanged;
                subscribedApi.MyLogChanged -= LogChanged;
                subscribedApi.NoticesChanged -= NoticesChanged;
                subscribedApi.Error -= Toast;
                subscribedApi.Auth.SignedOut -= ForgetPrivateUi;
            }
            if (World != null) World.OriginChanged -= OriginChanged;
            if (bridge != null)
            {
                bridge.OnLocation -= LocationChanged;
                bridge.LocationError -= Toast;
                bridge.PhotoError -= Toast;
                bridge.OnPhotoReady -= PhotoReady;
            }
            if (Orbit != null) Orbit.IsOverUi = null;
            (currentScreen as IDisposable)?.Dispose();
            if (portraitFocus != null) Destroy(portraitFocus.gameObject);
        }

        // 位置情報のエラーは日本語で。デモ中は現在地を使わないので出さない
        void LocationProblem(string message)
        {
            if (DemoMode) return;
            var m = message ?? "";
            if (m.StartsWith("1")) Toast("位置情報が許可されていません。ブラウザーの設定で許可するか、デモモードで試せます");
            else if (m.StartsWith("2") || m.StartsWith("3")) Toast("現在地を取得できませんでした。屋外で少し待つか、デモモードで試せます");
            else Toast(m);
        }

        void LocationChanged(double lat, double lng, float accuracy)
        {
            if (!DemoMode) SetPosition(lat, lng, accuracy);
        }

        void SetPosition(double lat, double lng, float accuracy)
        {
            if (double.IsNaN(lat) || double.IsNaN(lng) || lat < -90 || lat > 90 || lng < -180 || lng > 180) return;
            Lat = lat; Lng = lng; Accuracy = Mathf.Max(0, accuracy); HasLocation = true;
            if (World != null && (!World.HasOrigin || GeoMath.Distance(originLat, originLng, lat, lng) > 3000))
            {
                originLat = lat; originLng = lng;
                World.SetOrigin(lat, lng);
            }
            PositionAvatar();
            if (Trail.Record(lat, lng))
            {
                UpdateTrail();
                if (ShareFootprints && !DemoMode)
                {
                    // 共有を始める前の履歴とデモ移動は送らない。
                    if (pendingFootprints.Count < 120) pendingFootprints.Add(new Callable.FootprintPoint { lat = lat, lng = lng });
                }
            }
            UpdateStates();
            hud?.Refresh();
        }

        void PositionAvatar()
        {
            if (HasLocation && World != null && World.HasOrigin && avatar != null)
                avatar.transform.position = World.OnGround(Lat, Lng);
        }

        void OriginChanged()
        {
            PositionAvatar();
            Markers?.Relayout(); paths?.Relayout();
            Orbit?.SnapToTarget();
        }

        void UpdateTrail() => paths?.SetTrail(Trail.Points.Select(p => (p.lat, p.lng)));
        void UpdateStates()
        {
            Markers?.UpdateStates(Uid, OpenedIds, HasLocation ? Lat : (double?)null, HasLocation ? Lng : (double?)null, Accuracy);
            NotifyArrivals();
        }

        // 範囲に入った瞬間を知らせる（この SNS のいちばん大事な瞬間）
        readonly HashSet<string> inRange = new HashSet<string>();
        bool logReady;
        void NotifyArrivals()
        {
            // 開封の記録を読み込む前は、開封済みも「ひらける」に見えてしまうので待つ
            if (Api == null || !HasLocation || string.IsNullOrEmpty(Nickname) || !logReady) return;
            Drop first = null;
            var now = new HashSet<string>();
            foreach (var d in Api.Drops)
            {
                if (StateOf(d) != DropState.InRange) continue;
                now.Add(d.id);
                if (!inRange.Contains(d.id) && first == null) first = d;
            }
            inRange.Clear(); inRange.UnionWith(now);
            if (first == null || view != "map" || placing || playingSequence) return;
            ToastInfo("SIGNAL IN RANGE ／「" + first.teaser + "」がひらけます");
            if (Selected == null && !Busy) SelectDrop(first, false);
            Sound.Play("arrival");
            bridge?.Haptic(HapticKind.Arrival);
        }
        public double DistanceTo(Drop d) => HasLocation ? GeoMath.Distance(Lat, Lng, d.lat, d.lng) : double.PositiveInfinity;
        public DropState StateOf(Drop d) => MarkerField.StateOf(d, Uid, OpenedIds,
            HasLocation ? Lat : (double?)null, HasLocation ? Lng : (double?)null, Accuracy);

        void DropsChanged()
        {
            if (Api == null) return;
            Markers?.SetDrops(Api.Drops);
            if (Selected != null) Selected = Api.Drops.FirstOrDefault(d => d.id == Selected.id);
            UpdateStates(); hud?.Refresh(); tags?.Sync(); RefreshScreen();
        }

        void LogChanged()
        {
            if (Api == null) return;
            OpenedIds.Clear();
            foreach (var item in Api.Opened) OpenedIds.Add(item.id);
            logReady = true;
            UpdateStates(); hud?.Refresh(); RefreshScreen(); CheckCompletions();
        }
        void NoticesChanged() { hud?.Refresh(); RefreshScreen(); }
        void ForgetPrivateUi()
        {
            identityVersion++; Busy = false;
            Sound.StopLoop("verify");
            OpenedIds.Clear(); JoinedIds.Clear(); Profile = null;
            completions.Clear(); pendingFootprints.Clear();
            Selected = null; if (Markers != null) Markers.SelectedId = null;
            activeRoute = null; paths?.SetRoute(null);
            if (currentScreen is LetterScreen) { Busy = false; ShowMap(); }
            UpdateStates(); hud?.Refresh(); RefreshScreen();
        }
        public bool IsUnread(Notice n) => n.createdAt.HasValue && new DateTimeOffset(n.createdAt.Value.ToUniversalTime()).ToUnixTimeMilliseconds() > noticesSeen;
        public void MarkNoticesRead()
        {
            noticesSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerPrefs.SetString("onsite.noticesSeen", noticesSeen.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.Save(); hud?.Refresh();
        }

        void RefreshScreen()
        {
            if (currentScreen is FeedScreen feed) feed.Refresh();
            else if (currentScreen is NoticesScreen notices) notices.Refresh();
            else if (currentScreen is MeScreen me) me.Refresh();
            else if (currentScreen is EventsScreen eventsScreen) eventsScreen.Refresh();
        }

        void Screen(string next, VisualElement element = null)
        {
            if (Busy) { Toast("処理が終わるまでお待ちください"); return; }
            if (view != next) Sound.Play(element != null ? "open_panel" : "close_panel");
            Sound.SetMapActive(next == "map");
            (currentScreen as IDisposable)?.Dispose();
            screens.Clear(); confirmation = null;
            currentScreen = element; view = next; placing = false;
            if (element != null)
            {
                element.AddToClassList("screen"); screens.Add(element);
                // パソコンのホイールで、ひと回しに十分進むように
                element.Query<ScrollView>().ForEach(s => s.mouseWheelScrollSize = 140);
                Anim.Enter(element);
            }
            hud.style.display = next == "onboarding" ? DisplayStyle.None : DisplayStyle.Flex;
            hud.SetPlacement(false);
            hud.SetMapVisible(next == "map");
            hud.SetActiveView(next == "letter" ? "map" : next);
            SetPortraitCamera(next == "onboarding" || next == "me");
        }

        void SetPortraitCamera(bool active)
        {
            if (avatar != null)
            {
                avatar.spin = active;
                // 像を選ぶあいだは、上空に浮かせて街を背景に見せる（地上だと建物の壁に隠れる）
                avatar.lift = active ? 160f : 0f;
            }
            if (Orbit == null || active == portraitCamera) return;
            portraitCamera = active;
            if (active)
            {
                savedDistance = Orbit.distance; savedPitch = Orbit.pitch; savedYaw = Orbit.yaw;
                savedCameraTarget = Orbit.target;
                if (portraitFocus == null) portraitFocus = new GameObject("Portrait Focus").transform;
                portraitFocus.position = avatar != null ? avatar.transform.position + Vector3.up * (avatar.lift + avatar.scale * 0.55f) : Vector3.zero;
                Orbit.target = portraitFocus;
                // 画面の下半分は選択パネルなので、像が上半分に収まるよう少し見下ろす
                Orbit.distance = 46; Orbit.pitch = 18;
            }
            else { Orbit.target = savedCameraTarget; Orbit.distance = savedDistance; Orbit.pitch = savedPitch; Orbit.yaw = savedYaw; }
            Orbit.SnapToTarget();
        }

        void ShowOnboarding() => Screen("onboarding", new OnboardingScreen(this));
        public void ShowMap() { if (string.IsNullOrEmpty(Nickname)) ShowOnboarding(); else Screen("map"); }
        public void ShowFeed() => Screen("feed", new FeedScreen(this));
        public void ShowEvents() { Screen("events", new EventsScreen(this)); RefreshEvents(); }
        public void ShowMe() => Screen("me", new MeScreen(this));
        public void ShowNotices() => Screen("notices", new NoticesScreen(this));
        public void SetLook(AvatarLook look) { Look = look; Look.Save(); avatar?.Apply(look); }
        public void CompleteOnboarding(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 20) { Toast("名前は1〜20文字で入力してください"); return; }
            Nickname = name; PlayerPrefs.SetString("onsite.name", name); PlayerPrefs.Save(); ShowMap();
        }

        public void SelectDrop(Drop drop) => SelectDrop(drop, true);
        void SelectDrop(Drop drop, bool feedback)
        {
            if (drop == null || Busy) return;
            ShowMap(); Selected = drop;
            if (feedback)
            {
                Sound.Play("select");
                bridge?.Haptic(HapticKind.Select);
            }
            if (Markers != null) Markers.SelectedId = drop.id;
            UpdateStates(); hud.Refresh();
            hud.FlashSelection();
        }
        public void ClearSelection()
        {
            Selected = null; if (Markers != null) Markers.SelectedId = null;
            UpdateStates(); hud.Refresh();
        }

        public void SetDemo(bool value)
        {
            DemoMode = value; pendingFootprints.Clear();
            PlayerPrefs.SetInt("onsite.demo", value ? 1 : 0); PlayerPrefs.Save();
            if (value && !HasLocation) SetPosition(DemoLat, DemoLng, 0);
            if (!value) { HasLocation = false; bridge?.StartLocation(); }
            UpdateStates(); hud?.Refresh(); ToastInfo(value ? "DEMO：地図をタップして移動" : "GPS の現在地を取得します");
        }
        public void SetShareFootprints(bool value)
        {
            ShareFootprints = value; pendingFootprints.Clear(); nextShare = Time.unscaledTime + 30;
            PlayerPrefs.SetInt("onsite.shareFootprints", value ? 1 : 0); PlayerPrefs.Save();
        }
        public void Toast(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Sound.Play("error");
            bridge?.Haptic(HapticKind.Error);
            toast?.Show(message);
        }
        void ToastInfo(string message) { if (!string.IsNullOrEmpty(message)) toast?.Show(message); }

        public void ResetLocal()
        {
            Confirm("端末の記録をリセット", "名前・外見・足あと・設定を消します。サーバー上の投稿と開封履歴は残ります。", () =>
            {
                Trail.Reset(); pendingFootprints.Clear(); completionShown.Clear(); completions.Clear();
                foreach (var key in new[] { "onsite.name", "onsite.look.body", "onsite.look.pose", "onsite.look.finish", "onsite.demo", "onsite.shareFootprints", "onsite.noticesSeen", "onsite.completedEvents", "onsite.sound", "onsite.haptics" }) PlayerPrefs.DeleteKey(key);
                Sound.Muted = false; DeviceBridge.HapticsEnabled = true;
                PlayerPrefs.Save(); Nickname = ""; SetDemo(false); ShareFootprints = false;
                noticesSeen = 0; SetLook(AvatarLook.Default); UpdateTrail(); ShowOnboarding();
            });
        }

        public void Confirm(string title, string message, Action action)
        {
            if (confirmation != null) confirmation.RemoveFromHierarchy();
            confirmation = new VisualElement(); confirmation.AddToClassList("confirm-cover");
            var dialog = new HudPanel("CONFIRM", title);
            dialog.AddToClassList("confirm-panel");
            dialog.Body.Add(UiKit.Label(message, "body-copy"));
            var cancel = UiKit.Button("やめる", () => { confirmation?.RemoveFromHierarchy(); confirmation = null; });
            var accept = UiKit.Button("確認して続ける", () =>
            {
                confirmation?.RemoveFromHierarchy(); confirmation = null; action();
            }, true);
            dialog.Body.Add(UiKit.Row(cancel, accept));
            confirmation.Add(dialog); screens.Add(confirmation);
        }

        public void ShowDebug()
        {
            if (tourRequested) return;
            var panel = new HudPanel("DEVELOPER", "フィールド設定", ShowMap);
            panel.Body.Add(UiKit.Label("位置の判定はサーバーが行います。", "muted"));
            var toggle = new Toggle("DEMO / タップで移動") { value = DemoMode };
            toggle.RegisterValueChangedCallback(e => SetDemo(e.newValue)); panel.Body.Add(toggle);
            panel.Body.Add(UiKit.Button("GPS を再取得", () => bridge?.StartLocation()));
            panel.Body.Add(UiKit.Button("UNLOCK 演出", () => sequence.Play(false, () => { })));
            panel.Body.Add(UiKit.Button("COMPLETE 演出", () => sequence.Play(true, () => { })));
            // 紹介動画の撮影用：アプリでも自動デモを流せる（画面録画と組み合わせる）
            panel.Body.Add(UiKit.Button("自動デモ（字幕つき・約1分45秒）", () => RestartWith("tour")));
            panel.Body.Add(UiKit.Button("プレゼン（自動デモ＋説明・約3分半）", () => RestartWith("tour", "pres")));
            panel.Body.Add(UiKit.Label("通信 " + (Api?.Auth?.IsSignedIn == true ? "ONLINE" : "OFFLINE") + " / GPS " + (HasLocation ? "FIX" : "WAIT"), "mono"));
            Screen("debug", panel);
        }

        bool IsOverUi(Vector2 screenPosition)
        {
            if (tourRequested) return true;
            if (root?.panel == null) return false;
            if (view != "map" || playingSequence || confirmation != null) return true;
            var point = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, ScreenHeight - screenPosition.y));
            var hit = root.panel.Pick(point);
            // 画面全体を覆う透明な入れ物は「地図の上」とみなす。色のある面やボタンの上だけを UI とする
            for (var e = hit; e != null && e != root; e = e.parent)
                if (e is Button || e is TextField || e is ScrollView || e.ClassListContains("ui-surface") || e.resolvedStyle.backgroundColor.a > 0.02f) return true;
            return false;
        }
        static int ScreenHeight => UnityEngine.Screen.height;
        void HandleMapTap()
        {
            if (tourRequested || view != "map" || placing || !DemoMode || Busy) return;
            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0))
            {
                pressOnMap = !IsOverUi(mouse); pressPosition = mouse; pressTime = Time.unscaledTime;
            }
            if (Input.GetMouseButtonUp(0) && pressOnMap)
            {
                pressOnMap = false;
                if (Vector2.Distance(mouse, pressPosition) < 14 && Time.unscaledTime - pressTime < 0.4f && !IsOverUi(mouse) && TryGround(mouse, out var lat, out var lng, true)) SetPosition(lat, lng, 0);
            }
        }
        public bool TryGround(Vector2 screenPoint, out double lat, out double lng, bool planeOnly = false)
        {
            lat = lng = 0;
            if (MapCamera == null || World == null || !World.HasOrigin) return false;
            var ray = MapCamera.ScreenPointToRay(screenPoint);
            Vector3 point;
            if (!planeOnly && Physics.Raycast(ray, out var hit, 10000)) point = hit.point;
            else
            {
                var plane = new Plane(Vector3.up, avatar != null ? avatar.transform.position : Vector3.zero);
                if (!plane.Raycast(ray, out var enter) || enter > 10000) return false;
                point = ray.GetPoint(enter);
            }
            (lat, lng) = World.FromUnity(point);
            return lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;
        }
    }
}





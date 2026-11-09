using System;
using System.Collections;
using System.Linq;
using Onsite.App.Hud;
using Onsite.App.Ui;
using Onsite.Map;
using Onsite.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace Onsite.App
{
    public sealed partial class OnsiteApp
    {
        // 実際の画面と操作を順番に呼ぶ、撮影用の進行役。
        sealed class DemoTour : IDisposable
        {
            static readonly string[] SamplePrefixes =
            {
                "雷門の提灯", "仲見世で", "宝蔵門の裏側", "本堂に入ったら", "隅田川テラスで"
            };
            readonly OnsiteApp app;
            readonly int firstStep;
            readonly Caption caption;
            readonly PresentationCard presentation;
            readonly VisualElement inputCover;
            readonly Transform originalTarget;
            Transform routeFocus;
            Drop destination;
            float cameraStarted, cameraDuration, fromDistance, fromPitch, toDistance, toPitch, yawDrift;
            bool movingCamera;

            public DemoTour(OnsiteApp app, int firstStep)
            {
                this.app = app; this.firstStep = firstStep;
                originalTarget = app.Orbit != null ? app.Orbit.target : null;
                // 字幕は操作を拾わず、この透明な面だけが手動操作を受け止める。
                inputCover = new VisualElement { name = "tour-input-cover", focusable = true };
                inputCover.style.position = Position.Absolute;
                inputCover.style.left = inputCover.style.right = inputCover.style.top = inputCover.style.bottom = 0;
                app.root.Add(inputCover);
                caption = new Caption(); app.root.Add(caption);
                if (QueryFlag("pres"))
                {
                    presentation = new PresentationCard(); app.root.Add(presentation);
                }
            }

            public IEnumerator Run()
            {
                inputCover.Focus();
                if (presentation != null) yield return PresentationIntro();
                if (firstStep == 1) yield return Intro();
                else
                {
                    if (string.IsNullOrEmpty(app.Nickname)) app.CompleteOnboarding("旅人");
                    app.ShowMap();
                }

                if (firstStep <= 2)
                {
                    app.ShowMap(); app.ClearSelection();
                    app.SetPosition(DemoLat, DemoLng, 0);
                    CameraTo(650, 60, 2, 8);
                    caption.Show("地図に見えるのは、予告だけ。", "中身は、その場所に行った人にしか読めない。");
                    yield return Hold(10);
                }

                if (firstStep <= 6) yield return WaitForData();
                if (firstStep <= 5)
                {
                    destination = FindDestination();
                    if (destination != null)
                    {
                        if (firstStep <= 3) yield return Walk();
                        else
                        {
                            app.ShowMap(); app.ClearSelection();
                            PlaceNearDestination(); app.SelectDrop(destination);
                            CameraTo(260, 55, 0, 0);
                        }
                        yield return OpenLetter(firstStep <= 4);
                        if (app.currentScreen is LetterScreen letter) yield return React(letter);
                    }
                }

                if (firstStep <= 6) yield return Route();
                yawDrift = 0;
                if (presentation != null) yield return PresentationOutro();
                else
                {
                    caption.ShowEndCard();
                    yield return Hold(10);
                }
                // 終了カードと操作のロックは、そのまま保つ。
            }

            IEnumerator PresentationIntro()
            {
                // 名前が未登録でも地図を背景にし、既存の自己紹介デモはこの後で行う。
                app.Screen("map");
                caption.style.display = DisplayStyle.None;
                yield return presentation.Show("DEVFEST GDGOC JAPAN HACKATHON 2026 / TEAM t009", "ONSITE", 6,
                    new[] { "近づくと、ひらく。", "行かないと読めない、おでかけ SNS" });
                yield return presentation.Show("01 / PROBLEM", "写真を見たら、満足してしまう。", 12,
                    new[] { "SNS で旅先の景色を見ても、それで終わってしまう。", "観光情報は有名な場所に偏り、地元の小さな発見は届かない。" });
                yield return presentation.Show("02 / IDEA", "中身は、その場所に行った人だけ。", 12,
                    new[] { "地図に見えるのは「予告」だけ。", "投稿者が決めた範囲（100m〜1km）に入ると、本文と写真がひらく。", "ひらいた人だけが、寄せ書きできる。" });
                yield return presentation.Show("03 / FOR TRAVEL", "巡りルートで、観光の回り方を共有。", 10,
                    new[] { "投稿をつないでルートに。", "開催期間を付ければ、祭りやツアーの期間限定の巡りにも。" });
                yield return presentation.Show("04 / DEMO", "浅草で、試してみる。", 5, Array.Empty<string>());
                yield return presentation.Hide();
                caption.style.display = DisplayStyle.Flex;
            }

            IEnumerator PresentationOutro()
            {
                caption.Hide(); caption.style.display = DisplayStyle.None;
                app.ShowMap();
                yield return presentation.Show("05 / HOW IT WORKS", "距離の判定は、サーバーで。", 14,
                    new[] { "本文・写真・寄せ書きは Firestore のルールで遮断し、Cloud Functions が現在地との距離を確かめた時だけ返す。", "だから、通信を覗いても先には読めない。" });
                yield return presentation.Show("06 / GOOGLE TECHNOLOGIES", "使った Google の技術", 14,
                    new[] { "Firebase Hosting / Cloud Functions / Cloud Firestore / Cloud Storage / Authentication", "Google Maps Platform：Photorealistic 3D Tiles（Map Tiles API）", "Unity 6 ＋ Cesium で、立体地図を地形模型のように描画" });
                yield return presentation.Show("07 / NEXT", "これから", 10,
                    new[] { "存在ごと隠すシークレット投稿", "主催者向けの特典と集計", "通報と非表示" });
                yield return presentation.Show(null, "ONSITE", 10,
                    new[] { "近づくと、ひらく。", "kokodake-t009.web.app" }, endCard: true);
            }

            IEnumerator Intro()
            {
                caption.Show("行かないと、読めない SNS。", "ONSITE / 近づくと、ひらく。");
                if (string.IsNullOrEmpty(app.Nickname))
                {
                    app.ShowOnboarding();
                    yield return Hold(1);
                    var look = app.Look;
                    for (int i = 0; i < 3; i++)
                    {
                        look.body = (BodyType)(((int)look.body + 1) % 4);
                        look.finish = (Finish)(((int)look.finish + 1) % 4);
                        app.SetLook(look);
                        app.currentScreen?.Q<AvatarPicker>()?.Preview(look);
                        yield return Hold(1.75f);
                    }
                    var name = app.currentScreen?.Q<TextField>();
                    if (name != null) name.value = "旅人";
                    yield return Hold(1.75f);
                    app.CompleteOnboarding("旅人");
                }
                else
                {
                    app.ShowMe();
                    yield return Hold(4);
                    app.ShowMap();
                    yield return Hold(4);
                }
            }

            IEnumerator WaitForData()
            {
                // 起動の認証・Refresh・イベント取得が終わるまで待つ。
                float deadline = Time.unscaledTime + 45;
                while (!app.tourDataReady && Time.unscaledTime < deadline) yield return null;
            }

            Drop FindDestination()
            {
                if (app.Api?.Auth?.IsSignedIn != true) return null;
                var samples = app.Api.Drops.Where(d => d != null &&
                    (d.authorName ?? "").StartsWith("onsite サンプル", StringComparison.Ordinal) &&
                    SamplePrefixes.Any(prefix => (d.teaser ?? "").StartsWith(prefix, StringComparison.Ordinal)) &&
                    ValidPoint(d.lat, d.lng)).OrderBy(d =>
                    Array.FindIndex(SamplePrefixes, prefix => (d.teaser ?? "").StartsWith(prefix, StringComparison.Ordinal))).ToArray();
                var gate = samples.FirstOrDefault(d => (d.teaser ?? "").StartsWith(SamplePrefixes[0], StringComparison.Ordinal));
                return samples.FirstOrDefault(d => !app.OpenedIds.Contains(d.id) && d.authorId != app.Uid) ?? gate;
            }

            IEnumerator Walk()
            {
                app.ShowMap(); app.ClearSelection();
                CameraTo(260, 55, 3, 0);
                caption.Show("浅草を、歩いてみる。");
                float started = Time.unscaledTime;
                bool announced = false;
                double stop = StopDistance();
                while (app.DistanceTo(destination) > stop + 0.1)
                {
                    double remaining = app.DistanceTo(destination);
                    double fraction = Math.Min(4, remaining - stop) / remaining;
                    app.SetPosition(app.Lat + (destination.lat - app.Lat) * fraction,
                        app.Lng + (destination.lng - app.Lng) * fraction, 0);
                    if (!announced && app.StateOf(destination) == DropState.InRange)
                    {
                        announced = true;
                        caption.Show("範囲に入った瞬間、知らせが届く。");
                    }
                    yield return Hold(0.25f);
                }
                // 重なる範囲で別の投稿が選ばれても、撮影対象へ戻す。
                if (app.Selected?.id != destination.id) app.SelectDrop(destination);
                if (!announced && app.StateOf(destination) == DropState.InRange)
                    caption.Show("範囲に入った瞬間、知らせが届く。");
                yield return Hold(Mathf.Max(0, 12 - (Time.unscaledTime - started)));
            }

            double StopDistance() => Math.Min(40, Math.Max(1, destination.radius * 0.4));

            void PlaceNearDestination()
            {
                double distance = GeoMath.Distance(DemoLat, DemoLng, destination.lat, destination.lng);
                double fraction = Math.Min(1, StopDistance() / Math.Max(1, distance));
                app.SetPosition(destination.lat + (DemoLat - destination.lat) * fraction,
                    destination.lng + (DemoLng - destination.lng) * fraction, 0);
            }

            IEnumerator OpenLetter(bool present)
            {
                if (destination == null || app.Api?.Auth?.IsSignedIn != true) yield break;
                float started = Time.unscaledTime;
                if (present) yield return Hold(1.5f);
                app.SelectDrop(destination);
                if (present) caption.Show("距離はサーバーが確かめる。だから、先には読めない。");
                app.OpenSelected();
                yield return null;
                float deadline = Time.unscaledTime + 40;
                // Busy は演出の前に解除されるので、本文の画面を確認する。
                while (!(app.currentScreen is LetterScreen) &&
                    (app.Busy || app.playingSequence) && Time.unscaledTime < deadline) yield return null;
                if (!(app.currentScreen is LetterScreen)) { caption.Hide(); yield break; }
                caption.Hide();
                yield return null;
                var scroll = app.currentScreen.Q<ScrollView>();
                if (scroll != null) scroll.scrollOffset = Vector2.zero;
                yield return Hold(5);
                if (present) yield return Hold(Mathf.Max(0, 16 - (Time.unscaledTime - started)));
            }

            IEnumerator React(LetterScreen letter)
            {
                float started = Time.unscaledTime;
                if (!letter.IsLiked && !app.Busy)
                {
                    app.Like(letter);
                    float deadline = Time.unscaledTime + 30;
                    while (app.Busy && Time.unscaledTime < deadline) yield return null;
                }
                if (app.Busy || app.currentScreen != letter) yield break;
                caption.Show("ひらいた人だけが、寄せ書きできる。");
                var input = letter.Q<TextField>();
                var scroll = letter.Q<ScrollView>();
                if (input != null)
                {
                    yield return null;
                    scroll?.ScrollTo(input);
                    yield return Hold(3);
                    // 入力の見本だけ。送信処理は呼ばない。
                    input.value = "龍、いました！首が痛い。";
                    yield return Hold(3);
                }
                app.ShowMap();
                caption.Hide();
                yield return Hold(Mathf.Max(0, 14 - (Time.unscaledTime - started)));
            }

            IEnumerator Route()
            {
                // リクエスト中の画面を強制的に閉じない。
                float deadline = Time.unscaledTime + 30;
                while (app.Busy && Time.unscaledTime < deadline) yield return null;
                if (app.Busy) yield break;
                app.ShowEvents();
                yield return null;
                deadline = Time.unscaledTime + 30;
                while (app.refreshingMeta && Time.unscaledTime < deadline) yield return null;
                var ev = app.Events.FirstOrDefault(item => (item.title ?? "").StartsWith("浅草 はじめてめぐり", StringComparison.Ordinal));
                if (ev == null) { app.ShowMap(); caption.Hide(); yield break; }
                (app.currentScreen as EventsScreen)?.ShowDetail(ev);
                caption.Show("投稿をつなげて、巡りルートに。", "期間限定の祭りやツアーにも。");
                yield return Hold(4);
                app.ClearSelection();
                app.ShowRoute(ev);
                FocusRoute(ev);
                CameraTo(900, 62, 3, 8);
                caption.Show("歩いた道は、光の線で残る。");
                yield return Hold(16);
            }

            void FocusRoute(KokoEvent ev)
            {
                if (app.Orbit == null || app.World == null || !app.World.HasOrigin || ev.stops == null) return;
                var stops = ev.stops.Where(s => s != null && ValidPoint(s.lat, s.lng)).ToArray();
                if (stops.Length == 0) return;
                routeFocus = new GameObject("Tour Route Focus").transform;
                routeFocus.position = app.World.OnGround((stops.Min(s => s.lat) + stops.Max(s => s.lat)) / 2,
                    (stops.Min(s => s.lng) + stops.Max(s => s.lng)) / 2);
                app.Orbit.target = routeFocus;
            }

            void CameraTo(float distance, float pitch, float seconds, float drift)
            {
                if (app.Orbit == null) return;
                fromDistance = app.Orbit.distance; fromPitch = app.Orbit.pitch;
                toDistance = distance; toPitch = pitch; cameraDuration = seconds;
                cameraStarted = Time.unscaledTime; yawDrift = drift; movingCamera = true;
                UpdateCamera();
            }

            public void UpdateCamera()
            {
                if (!movingCamera || app.Orbit == null) return;
                float t = cameraDuration <= 0 ? 1 : Mathf.Clamp01((Time.unscaledTime - cameraStarted) / cameraDuration);
                float eased = t * t * (3 - 2 * t);
                app.Orbit.distance = Mathf.Lerp(fromDistance, toDistance, eased);
                app.Orbit.pitch = Mathf.Lerp(fromPitch, toPitch, eased);
                app.Orbit.yaw += yawDrift * Time.unscaledDeltaTime;
            }

            static bool ValidPoint(double lat, double lng) => !double.IsNaN(lat) && !double.IsNaN(lng) &&
                lat >= -90 && lat <= 90 && lng >= -180 && lng <= 180;

            static IEnumerator Hold(float seconds)
            {
                float until = Time.unscaledTime + seconds;
                while (Time.unscaledTime < until) yield return null;
            }

            public void Dispose()
            {
                if (app.Orbit != null) { app.Orbit.ScriptedMotion = false; app.Orbit.target = originalTarget; }
                if (routeFocus != null) UnityEngine.Object.Destroy(routeFocus.gameObject);
                inputCover.RemoveFromHierarchy(); caption.RemoveFromHierarchy();
                presentation?.RemoveFromHierarchy();
            }
        }
    }
}

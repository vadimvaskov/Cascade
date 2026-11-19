using Cascade.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Главное меню по макету: герои на фоне, логотип, PLAY, текущая глава, нижняя панель.</summary>
    public sealed class MainMenuScreen : ScreenBase
    {
        protected override void Build()
        {
            CoverBackground("menu/bg_menu", 0.5f);
            SaveData save = SaveSystem.Data;
            int energy = SaveSystem.RefreshEnergy(App.Config);

            // Верхняя панель
            Image avatar = UiKit.Img("Avatar", Safe, "ui/avatar");
            UiKit.Top(avatar.rectTransform, 36f, 24f, 96f, 96f);
            Image avatarRing = UiKit.Img("AvatarRing", Safe, "ui/ring");
            avatarRing.color = UiKit.Gold;
            UiKit.Top(avatarRing.rectTransform, 26f, 14f, 116f, 116f);
            Text name = UiKit.Label("Name", Safe, save.playerName, 40, UiKit.Cream, TextAnchor.MiddleLeft, false);
            UiKit.Top(name.rectTransform, 152f, 46f, 300f, 50f);
            Counter(Safe, "ui/coin", 480f, 42f, 220f, UiKit.FormatNumber(save.coins));
            Counter(Safe, "ui/energy", 722f, 42f, 190f, energy + " / " + App.Config.maxEnergy);
            Image gearBack = UiKit.Img("Settings", Safe, "ui/btn_octagon");
            UiKit.Top(gearBack.rectTransform, 946f, 26f, 96f, 94f);
            Image gear = UiKit.Img("Gear", gearBack.transform, "ui/gear");
            gear.color = UiKit.Cream;
            UiKit.Center(gear.rectTransform, 0f, 0f, 52f, 52f);
            UiKit.MakeButton(gearBack, delegate { App.ShowSettings(); });

            Image logo = UiKit.Img("Logo", Safe, "menu/logo");
            logo.preserveAspect = true;
            UiKit.Top(logo.rectTransform, 190f, 150f, 700f, 282f);

            // Боковые кнопки
            SideButton("menu/btn_quests", 36f, 866f, "daily_quests");
            SideButton("menu/btn_events", 36f, 650f, "events");
            SideButton("menu/btn_library", 894f, 866f, "music_library");

            // PLAY
            Image play = UiKit.Img("Play", Safe, "game/banner_gold");
            UiKit.Bottom(play.rectTransform, 250f, 512f, 580f, 220f);
            Image triangle = UiKit.Img("Triangle", play.transform, "ui/triangle");
            triangle.color = UiKit.Ink;
            UiKit.Center(triangle.rectTransform, -168f, 2f, 96f, 96f);
            Text playText = UiKit.FitLabel("Label", play.transform, Loc.T("play"), 112, UiKit.Ink, TextAnchor.MiddleCenter, true);
            UiKit.Center(playText.rectTransform, 62f, 4f, 330f, 130f);
            UiKit.MakeButton(play, OnPlay);

            // Текущая глава
            LevelDef recommended = LevelCatalog.Recommended();
            WorldDef world = LevelCatalog.World(recommended.World);
            Image chapter = UiKit.Img("Chapter", Safe, "menu/chapter_panel");
            UiKit.Bottom(chapter.rectTransform, 73f, 270f, 934f, 207f);
            Text chapterLabel = UiKit.Label("ChapterLabel", chapter.transform, Loc.T("chapter") + " " + world.Index, 30, UiKit.Cream, TextAnchor.MiddleLeft, false);
            UiKit.Center(chapterLabel.rectTransform, -217f, 62f, 400f, 40f);
            Text chapterName = UiKit.FitLabel("ChapterName", chapter.transform, world.Name.ToUpperInvariant(), 54, Color.white, TextAnchor.MiddleLeft, true);
            UiKit.Center(chapterName.rectTransform, -237f, 12f, 360f, 64f);
            Text chapterStars = UiKit.Label("ChapterStars", chapter.transform,
                LevelCatalog.StarsInWorld(world) + " / " + (world.Levels.Length * 3), 36, UiKit.Cream, TextAnchor.MiddleLeft, true);
            UiKit.Center(chapterStars.rectTransform, -207f, -47f, 300f, 44f);
            UiKit.MakeButton(chapter, delegate { App.ShowWorldMap(); });

            // Нижняя панель
            Image dock = UiKit.Img("Dock", Safe, "menu/dock");
            UiKit.Bottom(dock.rectTransform, 0f, 24f, 1080f, 152f);
            string[] keys = { "home", "tracks", "heroes", "shop" };
            float[] centers = { 0.150f, 0.390f, 0.623f, 0.853f };
            for (int i = 0; i < keys.Length; i++)
            {
                float cx = (centers[i] - 0.5f) * 1080f;
                Text label = UiKit.FitLabel("Tab", dock.transform, Loc.T(keys[i]), 26, i == 0 ? UiKit.Gold : UiKit.Cream, TextAnchor.MiddleCenter, false);
                UiKit.Center(label.rectTransform, cx, -36f, 200f, 36f);
                int index = i;
                Button hit = UiKit.HitArea("TabHit", dock.transform, delegate { OnTab(index); });
                UiKit.Center((RectTransform)hit.transform, cx, 0f, 240f, 150f);
            }

            if (!save.tutorialDone)
            {
                Anim.DelayUnscaled(this, 0.05f, delegate { App.StartTutorial(); });
            }
        }

        private void SideButton(string sprite, float x, float yFromBottom, string key)
        {
            Image image = UiKit.Img(key, Safe, sprite);
            UiKit.Bottom(image.rectTransform, x, yFromBottom, 150f, 203f);
            Text label = UiKit.FitLabel("Label", image.transform, Loc.T(key), 28, UiKit.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Center(label.rectTransform, 0f, -44f, 126f, 70f);
            label.lineSpacing = 0.8f;
            UiKit.MakeButton(image, delegate { App.Toast(Loc.T("coming_soon")); });
        }

        private void OnPlay()
        {
            App.StartLevel(LevelCatalog.Recommended());
        }

        private void OnTab(int index)
        {
            if (index != 0)
            {
                App.Toast(Loc.T("coming_soon"));
            }
        }
    }
}

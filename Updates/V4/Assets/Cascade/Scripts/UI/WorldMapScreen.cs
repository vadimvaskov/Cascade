using Cascade.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Карта мира: девять островов-глав на одной прокручиваемой картинке и окно с треками главы.</summary>
    public sealed class WorldMapScreen : ScreenBase
    {
        // Координаты в пикселях исходной картинки карты (941 × 1846)
        private const float SourceWidth = 941f;
        private const float SourceHeight = 1846f;
        private static readonly Vector2[] StarPositions =
        {
            new Vector2(628f, 259f), new Vector2(640f, 440f), new Vector2(640f, 610f),
            new Vector2(640f, 776f), new Vector2(640f, 934f), new Vector2(640f, 1107f),
            new Vector2(640f, 1280f), new Vector2(640f, 1454f), new Vector2(640f, 1630f)
        };

        private RectTransform content;
        private float scale;
        private RectTransform popup;

        protected override void Build()
        {
            Image fill = UiKit.Img("BackFill", Root, null, UiKit.Night);
            UiKit.Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();

            scale = UiKit.DesignWidth / SourceWidth;
            float contentHeight = SourceHeight * scale;

            RectTransform scrollRect = UiKit.Stretch(UiKit.Rect("Scroll", Column));
            scrollRect.SetAsFirstSibling();
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = UiKit.Stretch(UiKit.Rect("Viewport", scrollRect));
            Image viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f);
            viewportImage.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0.5f, 1f);
            content.anchorMax = new Vector2(0.5f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(UiKit.DesignWidth, contentHeight + App.TopInset);
            content.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            Image map = UiKit.Img("Map", content, "map/worldmap");
            UiKit.Top(map.rectTransform, 0f, App.TopInset, UiKit.DesignWidth, contentHeight);

            for (int i = 0; i < LevelCatalog.Worlds.Count && i < StarPositions.Length; i++)
            {
                BuildWorld(LevelCatalog.Worlds[i], StarPositions[i], i == 0);
            }
            Text swipe = UiKit.Label("Swipe", content, Loc.T("swipe"), 24, UiKit.Cream, TextAnchor.MiddleCenter, false);
            Place(swipe.rectTransform, 170f, 1796f, 600f, 30f);

            // Неподвижная шапка
            Image back = UiKit.Img("Back", Safe, "ui/btn_back");
            UiKit.Top(back.rectTransform, 24f, 24f, 96f, 94f);
            UiKit.MakeButton(back, delegate { App.ShowMenu(); });
            Text title = UiKit.Label("Title", Safe, Loc.T("world_map"), 34, UiKit.Cream, TextAnchor.MiddleLeft, false);
            UiKit.Top(title.rectTransform, 140f, 46f, 300f, 50f);
            UiKit.AddShadow(title, 2f);
            Counter(Safe, "ui/coin", 560f, 42f, 220f, UiKit.FormatNumber(SaveSystem.Data.coins));
            Counter(Safe, "ui/energy", 802f, 42f, 190f, SaveSystem.RefreshEnergy(App.Config) + " / " + App.Config.maxEnergy);
        }

        /// <summary>Размещение по координатам исходной картинки карты.</summary>
        private void Place(RectTransform rt, float sourceX, float sourceY, float sourceW, float sourceH)
        {
            UiKit.Top(rt, sourceX * scale, sourceY * scale + App.TopInset, sourceW * scale, sourceH * scale);
        }

        private void BuildWorld(WorldDef world, Vector2 star, bool first)
        {
            float iconX = first ? 843f : star.x + 201f;
            float iconY = first ? 229f : star.y - 25f;
            bool unlocked = LevelCatalog.IsUnlocked(world.Levels[0]);

            Text chapter = UiKit.Label("Chapter", content, Loc.T("chapter") + " " + world.Index + "  ·  " + world.Genre, 20, UiKit.Cream, TextAnchor.MiddleLeft, false);
            Place(chapter.rectTransform, star.x - 12f, star.y - 59f, 200f, 20f);
            Text name = UiKit.FitLabel("Name", content, world.Name.ToUpperInvariant(), 32, Color.white, TextAnchor.MiddleLeft, true);
            Place(name.rectTransform, star.x - 12f, star.y - 40f, 190f, 28f);
            Text stars = UiKit.Label("Stars", content, LevelCatalog.StarsInWorld(world) + " / " + (world.Levels.Length * 3), 26, UiKit.Cream,
                TextAnchor.MiddleLeft, true);
            Place(stars.rectTransform, star.x + 18f, star.y - 13f, 120f, 26f);

            Image icon = UiKit.Img("Icon", content, unlocked ? "ui/triangle" : "ui/lock");
            icon.color = unlocked ? UiKit.Gold : UiKit.Muted;
            Place(icon.rectTransform, iconX - 22f, iconY - 22f, 44f, 44f);

            WorldDef captured = world;
            Button hit = UiKit.HitArea("WorldHit", content, delegate { OpenWorld(captured); });
            Place((RectTransform)hit.transform, 100f, star.y - 115f, 800f, 165f);
        }

        public void OpenWorld(WorldDef world)
        {
            ClosePopup();
            RectTransform panel;
            float height = 420f + world.Levels.Length * 230f;
            popup = Modal.Create(App.OverlayRoot, "WorldPopup", 940f, height, 0.7f, out panel);
            float top = height * 0.5f;
            Text chapter = UiKit.Label("Chapter", panel, Loc.T("chapter") + " " + world.Index + "  ·  " + world.Genre, 34, UiKit.Gold, TextAnchor.MiddleCenter, false);
            UiKit.Center(chapter.rectTransform, 0f, top - 70f, 860f, 44f);
            Text name = UiKit.FitLabel("Name", panel, world.Name.ToUpperInvariant(), 72, Color.white, TextAnchor.MiddleCenter, true);
            UiKit.Center(name.rectTransform, 0f, top - 150f, 860f, 90f);

            for (int i = 0; i < world.Levels.Length; i++)
            {
                BuildTrackCard(panel, world.Levels[i], top - 330f - i * 230f);
            }
            Modal.PlainButton(panel, 0f, -top + 90f, 560f, Loc.T("close"), ClosePopup);
        }

        private void BuildTrackCard(RectTransform panel, LevelDef level, float centerY)
        {
            LevelProgress progress = SaveSystem.GetLevel(level.Id);
            bool unlocked = LevelCatalog.IsUnlocked(level);
            Image card = UiKit.SlicedImg("Track", panel, "ui/round16", 24f, new Color(1f, 1f, 1f, 0.07f));
            UiKit.Center(card.rectTransform, 0f, centerY, 860f, 200f);
            Text title = UiKit.FitLabel("Title", card.transform, level.Title, 50, UiKit.Cream, TextAnchor.MiddleLeft, true);
            UiKit.Center(title.rectTransform, -150f, 52f, 480f, 60f);
            Text info = UiKit.Label("Info", card.transform, UiKit.FormatTime(level.Duration) + "   ·   " + Mathf.RoundToInt(level.Bpm) + " BPM   ·   "
                + Loc.T("goal") + " " + UiKit.FormatNumber(level.MinScore), 28, UiKit.Muted, TextAnchor.MiddleLeft, false);
            UiKit.Center(info.rectTransform, -150f, 0f, 480f, 40f);
            for (int s = 0; s < 3; s++)
            {
                Image star = UiKit.Img("Star", card.transform, "ui/star");
                star.color = s < progress.stars ? UiKit.Gold : new Color(1f, 1f, 1f, 0.16f);
                UiKit.Center(star.rectTransform, -366f + s * 50f, -54f, 44f, 44f);
            }
            Text best = UiKit.Label("Best", card.transform, Loc.T("best") + " " + UiKit.FormatNumber(progress.bestScore), 30, UiKit.Cream, TextAnchor.MiddleLeft, false);
            UiKit.Center(best.rectTransform, -40f, -54f, 360f, 40f);

            Image playBack = UiKit.Img("PlayBack", card.transform, "ui/circle");
            playBack.color = unlocked ? UiKit.Gold : new Color(1f, 1f, 1f, 0.14f);
            UiKit.Center(playBack.rectTransform, 330f, 0f, 130f, 130f);
            Image icon = UiKit.Img("Icon", playBack.transform, unlocked ? "ui/triangle" : "ui/lock");
            icon.color = unlocked ? UiKit.Ink : UiKit.Muted;
            UiKit.Center(icon.rectTransform, unlocked ? 6f : 0f, 0f, 64f, 64f);

            LevelDef captured = level;
            UiKit.MakeButton(card, delegate
            {
                if (App.StartLevel(captured))
                {
                    popup = null;
                }
            });
        }

        private void ClosePopup()
        {
            if (popup != null)
            {
                Destroy(popup.gameObject);
                popup = null;
            }
        }
    }
}

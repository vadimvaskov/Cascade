using Cascade.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>
    /// Ритм-диаграмма: метки едут справа налево и в момент своей доли проходят через кольцо.
    /// Крупные метки — сильные доли и четверти, мелкие — восьмые и шестнадцатые.
    /// </summary>
    public sealed class RhythmBar : MonoBehaviour
    {
        private const int PoolSize = 56;
        private const float LookAhead = 1.9f;
        private const float LookBehind = 0.22f;

        private static readonly float[] MarkerSizes = { 40f, 30f, 20f, 13f };

        private RectTransform rect;
        private BeatMapData map;
        private RhythmJudge judge;
        private Image[] markers;
        private RectTransform[] markerRects;
        private Image ring;
        private float hitX;
        private float pixelsPerSecond;
        private int firstIndex;
        private float ringPulse;

        public void Build(RectTransform parent, float x, float y, float width, float height, BeatMapData beatMap, RhythmJudge rhythmJudge)
        {
            map = beatMap;
            judge = rhythmJudge;
            rect = (RectTransform)transform;
            UiKit.Top(rect, x, y, width, height);
            Image back = gameObject.AddComponent<Image>();
            back.sprite = UiKit.GetSliced("ui/pill", 30f);
            back.type = Image.Type.Sliced;
            back.color = new Color(0.05f, 0.03f, 0.1f, 0.62f);
            back.raycastTarget = false;
            gameObject.AddComponent<RectMask2D>();

            Image line = UiKit.Img("Line", rect, null, new Color(1f, 1f, 1f, 0.10f));
            UiKit.Center(line.rectTransform, 0f, 0f, width - 60f, 3f);

            hitX = -width * 0.5f + height * 0.5f + 36f;
            pixelsPerSecond = (width * 0.5f - hitX - 20f) / LookAhead;

            markers = new Image[PoolSize];
            markerRects = new RectTransform[PoolSize];
            Sprite circle = UiKit.GetSprite("ui/circle");
            for (int i = 0; i < PoolSize; i++)
            {
                Image marker = UiKit.Img("Marker", rect, circle, Color.white);
                UiKit.Center(marker.rectTransform, 0f, 0f, 20f, 20f);
                marker.enabled = false;
                markers[i] = marker;
                markerRects[i] = marker.rectTransform;
            }

            ring = UiKit.Img("Ring", rect, "ui/ring");
            ring.color = UiKit.Gold;
            UiKit.Center(ring.rectTransform, hitX, 0f, height * 0.82f, height * 0.82f);
        }

        public void ResetTo(double songTime)
        {
            firstIndex = 0;
        }

        /// <summary>Вспышка кольца при попадании.</summary>
        public void Flash(HitGrade grade)
        {
            ringPulse = 1f;
            ring.color = grade == HitGrade.Perfect ? UiKit.Gold : (grade == HitGrade.Good ? UiKit.Cream : UiKit.Red);
        }

        public void Render(double songTime)
        {
            double[] times = map.Times;
            int[] levels = map.Levels;
            int count = times.Length;
            while (firstIndex < count && times[firstIndex] < songTime - LookBehind)
            {
                firstIndex++;
            }
            int used = 0;
            for (int i = firstIndex; i < count && used < PoolSize; i++)
            {
                double dt = times[i] - songTime;
                if (dt > LookAhead)
                {
                    break;
                }
                if (judge.IsUsed(i))
                {
                    continue;
                }
                int level = levels[i];
                if (level < 0)
                {
                    level = 0;
                }
                if (level > 3)
                {
                    level = 3;
                }
                Image marker = markers[used];
                RectTransform rt = markerRects[used];
                used++;
                float size = MarkerSizes[level];
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(hitX + (float)dt * pixelsPerSecond, 0f);
                Color color = level == 0 ? UiKit.Gold : (level == 1 ? UiKit.Cream : UiKit.Violet);
                float alpha = level >= 3 ? 0.7f : 1f;
                if (dt < 0)
                {
                    alpha *= Mathf.Clamp01(1f + (float)dt / LookBehind);
                }
                color.a = alpha;
                marker.color = color;
                marker.enabled = true;
            }
            for (int i = used; i < PoolSize; i++)
            {
                if (markers[i].enabled)
                {
                    markers[i].enabled = false;
                }
            }
            if (ringPulse > 0f)
            {
                ringPulse = Mathf.Max(0f, ringPulse - Time.deltaTime * 5f);
                float s = 1f + ringPulse * 0.28f;
                ring.rectTransform.localScale = new Vector3(s, s, 1f);
                if (ringPulse <= 0f)
                {
                    ring.color = UiKit.Gold;
                }
            }
        }
    }
}

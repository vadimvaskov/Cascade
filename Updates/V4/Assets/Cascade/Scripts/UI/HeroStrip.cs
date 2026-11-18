using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>
    /// Верхняя сцена: герой бежит слева направо, перед ним летит текущая сфера.
    /// Пока анимации бега нет, герой статичный с лёгким покачиванием; движение передают встречные полосы света.
    /// Сцена ничего не меняет в игровой логике.
    /// </summary>
    public sealed class HeroStrip : MonoBehaviour
    {
        private const int StreakCount = 7;

        private RectTransform hero;
        private RectTransform orbRoot;
        private Image orbFront;
        private Image orbBack;
        private Image swirl;
        private Sprite[] orbSprites;
        private RectTransform[] streaks;
        private float[] streakSpeed;
        private Vector2 heroHome;
        private Vector2 orbHome;
        private float fade = 1f;
        private float kick;
        private float flyAway;
        private float stripWidth;
        private float stripTop;
        private float stripHeight;
        private double bpm = 120;

        public void Build(RectTransform parent, float feetY, double trackBpm)
        {
            bpm = trackBpm > 1 ? trackBpm : 120;
            RectTransform rect = (RectTransform)transform;
            UiKit.Stretch(rect);
            stripWidth = UiKit.DesignWidth;
            stripTop = feetY - 330f;
            stripHeight = 330f;

            streaks = new RectTransform[StreakCount];
            streakSpeed = new float[StreakCount];
            Sprite pill = UiKit.GetSliced("ui/pill", 30f);
            for (int i = 0; i < StreakCount; i++)
            {
                Image streak = UiKit.Img("Streak", rect, pill, new Color(1f, 0.86f, 0.7f, 0.06f));
                streak.type = Image.Type.Sliced;
                float w = Random.Range(90f, 260f);
                float y = stripTop + 40f + (stripHeight - 60f) * i / (StreakCount - 1);
                UiKit.Top(streak.rectTransform, Random.Range(0f, stripWidth), y, w, 5f);
                streaks[i] = streak.rectTransform;
                streakSpeed[i] = Random.Range(380f, 760f);
            }

            Sprite heroSprite = UiKit.GetSprite("game/hero_run");
            float heroHeight = 312f;
            float heroWidth = heroSprite != null ? heroHeight * heroSprite.rect.width / heroSprite.rect.height : heroHeight * 1.4f;
            Image heroImage = UiKit.Img("Hero", rect, heroSprite, Color.white);
            UiKit.Top(heroImage.rectTransform, 150f, feetY - heroHeight + 6f, heroWidth, heroHeight);
            hero = heroImage.rectTransform;
            heroHome = hero.anchoredPosition;

            swirl = UiKit.Img("Swirl", rect, "game/swirl");
            swirl.color = new Color(1f, 1f, 1f, 0.85f);
            UiKit.Top(swirl.rectTransform, 520f, feetY - 235f, 380f, 118f);

            orbRoot = UiKit.Rect("Orb", rect);
            UiKit.Top(orbRoot, 760f, feetY - 250f, 150f, 150f);
            orbHome = orbRoot.anchoredPosition;
            orbBack = UiKit.Img("Back", orbRoot, null, Color.white);
            UiKit.Stretch(orbBack.rectTransform);
            orbFront = UiKit.Img("Front", orbRoot, null, Color.white);
            UiKit.Stretch(orbFront.rectTransform);

            orbSprites = new Sprite[5];
            for (int level = 1; level <= 4; level++)
            {
                orbSprites[level] = UiKit.GetSprite("game/orb_" + level);
            }
        }

        /// <summary>Сфера остаётся на месте и плавно меняет цвет на цвет следующей.</summary>
        public void SetSphere(int level, bool instant)
        {
            if (level < 1 || level > 4)
            {
                return;
            }
            if (instant || orbFront.sprite == null)
            {
                orbFront.sprite = orbSprites[level];
                orbBack.sprite = orbSprites[level];
                fade = 1f;
                return;
            }
            orbBack.sprite = orbFront.sprite;
            orbFront.sprite = orbSprites[level];
            fade = 0f;
        }

        /// <summary>Толчок сферы: вперёд при попадании в ритм, назад при промахе.</summary>
        public void Kick(float amount)
        {
            kick = amount;
        }

        /// <summary>Поражение из-за бездействия: сфера улетает вперёд.</summary>
        public void FlyAway()
        {
            flyAway = 0.0001f;
        }

        public void Render(double songTime, int multiplier, float deltaTime)
        {
            float beat = (float)(songTime * bpm / 60.0);
            float bob = Mathf.Sin(beat * Mathf.PI * 2f);
            hero.anchoredPosition = heroHome + new Vector2(0f, Mathf.Abs(bob) * 7f);

            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + deltaTime * 9f);
            }
            orbFront.color = new Color(1f, 1f, 1f, fade);
            orbBack.color = new Color(1f, 1f, 1f, 1f - fade);

            kick = Mathf.MoveTowards(kick, 0f, deltaTime * 3.5f);
            float phase = beat - Mathf.Floor(beat);
            float pulse = 1f + 0.07f * Mathf.Exp(-phase * 6f);
            Vector2 orbPosition = orbHome + new Vector2(kick * 46f, Mathf.Sin(beat * Mathf.PI) * 9f);
            if (flyAway > 0f)
            {
                flyAway += deltaTime;
                orbPosition.x += flyAway * flyAway * 2600f;
            }
            orbRoot.anchoredPosition = orbPosition;
            orbRoot.localScale = new Vector3(pulse, pulse, 1f);
            swirl.rectTransform.anchoredPosition = new Vector2(orbPosition.x - 205f, orbPosition.y - 6f);

            float speedScale = 0.75f + multiplier * 0.09f;
            for (int i = 0; i < streaks.Length; i++)
            {
                RectTransform rt = streaks[i];
                Vector2 p = rt.anchoredPosition;
                p.x -= streakSpeed[i] * speedScale * deltaTime;
                if (p.x < -stripWidth * 0.5f - 300f)
                {
                    p.x = stripWidth * 0.5f + Random.Range(100f, 400f);
                }
                rt.anchoredPosition = p;
            }
        }
    }
}

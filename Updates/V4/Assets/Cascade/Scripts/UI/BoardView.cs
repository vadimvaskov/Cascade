using System;
using System.Collections.Generic;
using Cascade.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Клетка поля: сообщает о касании сразу в момент нажатия, не дожидаясь отпускания.</summary>
    public sealed class CellButton : MonoBehaviour, IPointerDownHandler
    {
        public int Index;
        public Action<int> Pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Pressed != null)
            {
                Pressed(Index);
            }
        }
    }

    /// <summary>
    /// Показ поля 6×6. Логика хода уже посчитана ядром; здесь только анимации событий.
    /// Каждое отложенное событие в конце сверяет клетку с настоящим состоянием поля,
    /// поэтому быстрые ходы подряд не оставляют «призраков».
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        // Геометрия сетки внутри картинки поля (доли ширины и высоты спрайта)
        private const float FirstCenterX = 0.12835f;
        private const float StepX = 0.14884f;
        private const float FirstCenterY = 0.12070f;
        private const float StepY = 0.15120f;
        private const float FirstWaveDelay = 0.13f;
        private const float WaveDelay = 0.09f;
        private const float MaxShowTime = 0.45f;

        public static readonly Color[] LevelColors =
        {
            Color.white,
            new Color32(110, 165, 255, 255),
            new Color32(255, 196, 84, 255),
            new Color32(255, 98, 98, 255),
            new Color32(196, 120, 255, 255)
        };

        private RectTransform rect;
        private Board board;
        private int size;
        private Image[] orbs;
        private RectTransform[] orbRects;
        private Vector2[] centers;
        private Sprite[] orbSprites;
        private Image frameGlow;
        private float orbSize;
        private readonly List<Image> burstPool = new List<Image>();
        private readonly List<Text> scorePool = new List<Text>();
        private RectTransform effects;
        private float pulse;
        private int[] shownAfterMove;
        private int moveSerial;
        private float busyUntil;

        public Action<int> CellPressed;
        public RectTransform Rect { get { return rect; } }

        public void Build(RectTransform parent, float x, float y, float width, Board model)
        {
            board = model;
            size = model.Size;
            Sprite boardSprite = UiKit.GetSprite("game/board");
            float aspect = boardSprite != null ? boardSprite.rect.width / boardSprite.rect.height : 1.038f;
            float height = width / aspect;
            rect = (RectTransform)transform;
            UiKit.Top(rect, x, y, width, height);
            Image back = gameObject.AddComponent<Image>();
            back.sprite = boardSprite;
            back.raycastTarget = false;

            frameGlow = UiKit.SlicedImg("FrameGlow", rect, "ui/frame16", 22f, new Color(1f, 0.78f, 0.36f, 0f));
            UiKit.Stretch(frameGlow.rectTransform);
            frameGlow.rectTransform.offsetMin = new Vector2(-6f, -6f);
            frameGlow.rectTransform.offsetMax = new Vector2(6f, 6f);

            orbSprites = new Sprite[5];
            for (int level = 1; level <= 4; level++)
            {
                orbSprites[level] = UiKit.GetSprite("game/orb_" + level);
            }

            int count = size * size;
            orbs = new Image[count];
            shownAfterMove = new int[count];
            orbRects = new RectTransform[count];
            centers = new Vector2[count];
            float cellW = StepX * width;
            float cellH = StepY * height;
            orbSize = Mathf.Min(cellW, cellH) * 0.86f;
            for (int i = 0; i < count; i++)
            {
                int row = i / size;
                int col = i % size;
                float cx = (FirstCenterX + StepX * col) * width - width * 0.5f;
                float cy = height * 0.5f - (FirstCenterY + StepY * row) * height;
                centers[i] = new Vector2(cx, cy);

                Image hit = UiKit.Img("Cell" + i, rect, null, new Color(0f, 0f, 0f, 0f));
                hit.raycastTarget = true;
                UiKit.Center(hit.rectTransform, cx, cy, cellW, cellH);
                CellButton button = hit.gameObject.AddComponent<CellButton>();
                button.Index = i;
                button.Pressed = OnPressed;

                Image orb = UiKit.Img("Orb", hit.transform, null, Color.white);
                UiKit.Center(orb.rectTransform, 0f, 0f, orbSize, orbSize);
                orb.enabled = false;
                orbs[i] = orb;
                orbRects[i] = orb.rectTransform;
            }
            effects = UiKit.Stretch(UiKit.Rect("Effects", rect));
            SyncAll();
        }

        private void OnPressed(int index)
        {
            if (CellPressed != null)
            {
                CellPressed(index);
            }
        }

        public Vector2 CellCenter(int index)
        {
            return centers[index];
        }

        /// <summary>Мгновенно показать настоящее состояние поля (незаконченные анимации отменяются).</summary>
        public void SyncAll()
        {
            moveSerial++;
            busyUntil = 0f;
            for (int i = 0; i < orbs.Length; i++)
            {
                int level = board.Get(i);
                shownAfterMove[i] = level;
                SetShown(i, level);
            }
        }

        private void SetShown(int index, int level)
        {
            Image orb = orbs[index];
            if (level <= Board.EmptyLevel)
            {
                orb.enabled = false;
                return;
            }
            orb.sprite = orbSprites[level > Board.MaxStableLevel ? Board.MaxStableLevel : level];
            orb.color = Color.white;
            orb.enabled = true;
        }

        private float WaveTime(int wave, float step)
        {
            return wave <= 0 ? 0f : FirstWaveDelay + (wave - 1) * step;
        }

        /// <summary>
        /// Показ хода по шагам: сначала поставленная сфера, затем волны взрывов и повышения,
        /// в конце сфера, добавленная игрой. Поставленная сфера видна всегда, даже если сама
        /// взорвётся в том же каскаде. Если следующий ход пришёл раньше, чем доигралась анимация,
        /// поле сразу показывает итог прошлого хода и начинает новый показ.
        /// </summary>
        public void Play(MoveResult result)
        {
            if (Time.time < busyUntil)
            {
                for (int i = 0; i < orbs.Length; i++)
                {
                    SetShown(i, shownAfterMove[i]);
                }
            }
            moveSerial++;
            int serial = moveSerial;

            float step = WaveDelay;
            if (result.WaveCount > 1 && FirstWaveDelay + (result.WaveCount - 1) * WaveDelay > MaxShowTime)
            {
                step = (MaxShowTime - FirstWaveDelay) / (result.WaveCount - 1);
            }
            float lastDelay = 0f;
            List<MoveEvent> events = result.Events;
            for (int i = 0; i < events.Count; i++)
            {
                MoveEvent e = events[i];
                int cell = e.Cell;
                int level = e.Level;
                if (e.Type == MoveEventType.Placed)
                {
                    SetShown(cell, level);
                    Pop(cell, 0.55f, 0.14f);
                    Burst(cell, LevelColors[level], 1.25f, 0.55f);
                    continue;
                }
                float delay = WaveTime(e.Wave, step);
                if (e.Type == MoveEventType.Upgraded)
                {
                    delay += 0.03f;
                    Anim.Delay(this, delay, delegate
                    {
                        if (serial != moveSerial)
                        {
                            return;
                        }
                        SetShown(cell, level);
                        Pop(cell, 1.22f, 0.12f);
                    });
                }
                else if (e.Type == MoveEventType.Exploded)
                {
                    Anim.Delay(this, delay, delegate
                    {
                        if (serial != moveSerial)
                        {
                            return;
                        }
                        SetShown(cell, Board.EmptyLevel);
                        Burst(cell, LevelColors[4], 2.1f, 0.9f);
                    });
                }
                else
                {
                    // Сфера, которую добавила игра: появляется после каскада с белой вспышкой
                    delay += 0.05f;
                    Anim.Delay(this, delay, delegate
                    {
                        if (serial != moveSerial)
                        {
                            return;
                        }
                        SetShown(cell, level);
                        Pop(cell, 0.2f, 0.22f);
                        Burst(cell, Color.white, 1.6f, 0.8f);
                    });
                }
                if (delay > lastDelay)
                {
                    lastDelay = delay;
                }
            }
            busyUntil = Time.time + lastDelay + 0.02f;
            for (int i = 0; i < orbs.Length; i++)
            {
                shownAfterMove[i] = board.Get(i);
            }
            if (result.FinalScore > 0)
            {
                ShowScore(result.Cell, result.FinalScore, result.Explosions > 0);
            }
        }

        private void Pop(int cell, float fromScale, float duration)
        {
            RectTransform rt = orbRects[cell];
            Anim.Run(rt, duration, delegate(float t)
            {
                float s = Mathf.LerpUnclamped(fromScale, 1f, Anim.EaseOutBack(t));
                rt.localScale = new Vector3(s, s, 1f);
            });
        }

        /// <summary>Занятая клетка: сфера коротко вздрагивает.</summary>
        public void Shake(int cell)
        {
            RectTransform rt = orbRects[cell];
            Anim.Run(rt, 0.18f, delegate(float t)
            {
                float offset = Mathf.Sin(t * Mathf.PI * 4f) * 7f * (1f - t);
                rt.anchoredPosition = new Vector2(offset, 0f);
            });
        }

        private void Burst(int cell, Color color, float maxScale, float alpha)
        {
            Image glow = TakeBurst();
            glow.color = color;
            RectTransform rt = glow.rectTransform;
            rt.anchoredPosition = centers[cell];
            float baseSize = orbSize * 1.2f;
            Anim.Run(glow, 0f, 0.26f, delegate(float t)
            {
                float s = Mathf.Lerp(0.6f, maxScale, Anim.EaseOut(t));
                rt.sizeDelta = new Vector2(baseSize * s, baseSize * s);
                Color c = color;
                c.a = (1f - t) * alpha;
                glow.color = c;
            }, delegate { glow.enabled = false; }, false);
        }

        private Image TakeBurst()
        {
            for (int i = 0; i < burstPool.Count; i++)
            {
                if (!burstPool[i].enabled)
                {
                    burstPool[i].enabled = true;
                    return burstPool[i];
                }
            }
            Image glow = UiKit.Img("Burst", effects, "ui/glow");
            UiKit.Center(glow.rectTransform, 0f, 0f, orbSize, orbSize);
            burstPool.Add(glow);
            return glow;
        }

        private void ShowScore(int cell, int points, bool big)
        {
            Text label = null;
            for (int i = 0; i < scorePool.Count; i++)
            {
                if (!scorePool[i].enabled)
                {
                    label = scorePool[i];
                    break;
                }
            }
            if (label == null)
            {
                label = UiKit.Label("Points", effects, "", 40, UiKit.Cream, TextAnchor.MiddleCenter, true);
                UiKit.AddShadow(label, 3f);
                UiKit.Center(label.rectTransform, 0f, 0f, 300f, 60f);
                scorePool.Add(label);
            }
            label.enabled = true;
            label.text = "+" + UiKit.FormatNumber(points);
            label.fontSize = big ? 52 : 36;
            Color color = big ? UiKit.Gold : UiKit.Cream;
            RectTransform rt = label.rectTransform;
            Vector2 start = centers[cell] + new Vector2(0f, orbSize * 0.45f);
            Text captured = label;
            Anim.Run(label, 0f, 0.7f, delegate(float t)
            {
                rt.anchoredPosition = start + new Vector2(0f, 70f * Anim.EaseOut(t));
                Color c = color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                captured.color = c;
            }, delegate { captured.enabled = false; }, false);
        }

        /// <summary>Мягкая пульсация рамки в ритм. strength 0..1.</summary>
        public void Pulse(float strength)
        {
            if (strength > pulse)
            {
                pulse = strength;
            }
        }

        public void SetPulseVisible(bool visible)
        {
            frameGlow.enabled = visible;
        }

        private void Update()
        {
            if (pulse > 0f)
            {
                pulse = Mathf.Max(0f, pulse - Time.deltaTime * 3.2f);
                Color c = frameGlow.color;
                c.a = pulse * 0.75f;
                frameGlow.color = c;
            }
        }
    }
}

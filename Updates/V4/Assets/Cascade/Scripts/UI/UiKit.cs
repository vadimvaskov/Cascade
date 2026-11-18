using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Мелкие помощники для сборки интерфейса кодом: спрайты, шрифты, размещение, кнопки.</summary>
    public static class UiKit
    {
        public const float DesignWidth = 1080f;
        public const float DesignHeight = 1920f;

        public static readonly Color Cream = new Color32(240, 225, 210, 255);
        public static readonly Color Gold = new Color32(246, 190, 84, 255);
        public static readonly Color Ink = new Color32(34, 22, 48, 255);
        public static readonly Color Panel = new Color32(30, 22, 46, 245);
        public static readonly Color Violet = new Color32(178, 120, 240, 255);
        public static readonly Color Red = new Color32(240, 96, 96, 255);
        public static readonly Color Muted = new Color32(150, 138, 168, 255);
        public static readonly Color Night = new Color32(24, 18, 40, 255);

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> SlicedSprites = new Dictionary<string, Sprite>();
        private static Font displayFont;
        private static Font textFont;

        public static Font DisplayFont
        {
            get
            {
                if (displayFont == null)
                {
                    displayFont = LoadFont("Fonts/CascadeDisplay");
                }
                return displayFont;
            }
        }

        public static Font TextFont
        {
            get
            {
                if (textFont == null)
                {
                    textFont = LoadFont("Fonts/CascadeText");
                }
                return textFont;
            }
        }

        private static Font LoadFont(string path)
        {
            Font font = Resources.Load<Font>(path);
            if (font == null)
            {
                Debug.LogWarning("[Cascade] Font not found: " + path);
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return font;
        }

        public static Sprite GetSprite(string path)
        {
            Sprite sprite;
            if (Sprites.TryGetValue(path, out sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = Resources.Load<Sprite>("Sprites/" + path);
            if (sprite == null)
            {
                Texture2D texture = Resources.Load<Texture2D>("Sprites/" + path);
                if (texture != null)
                {
                    sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }
            if (sprite == null)
            {
                Debug.LogWarning("[Cascade] Sprite not found: " + path);
            }
            Sprites[path] = sprite;
            return sprite;
        }

        /// <summary>Спрайт с рамкой для растягивания (9-slice), граница в пикселях текстуры.</summary>
        public static Sprite GetSliced(string path, float border)
        {
            string key = path + "#" + border;
            Sprite sprite;
            if (SlicedSprites.TryGetValue(key, out sprite) && sprite != null)
            {
                return sprite;
            }
            Texture2D texture = Resources.Load<Texture2D>("Sprites/" + path);
            if (texture == null)
            {
                return GetSprite(path);
            }
            sprite = Sprite.Create(texture, new UnityEngine.Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            SlicedSprites[key] = sprite;
            return sprite;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Размещение по макету шириной 1080: x и y — левый верхний угол, отсчёт от верха родителя.</summary>
        public static RectTransform Top(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w * 0.5f - DesignWidth * 0.5f, -(y + h * 0.5f));
            return rt;
        }

        /// <summary>То же, но y — расстояние нижнего края элемента от низа родителя.</summary>
        public static RectTransform Bottom(RectTransform rt, float x, float yFromBottom, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x + w * 0.5f - DesignWidth * 0.5f, yFromBottom + h * 0.5f);
            return rt;
        }

        /// <summary>Размещение относительно центра родителя.</summary>
        public static RectTransform Center(RectTransform rt, float centerX, float centerY, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(centerX, centerY);
            return rt;
        }

        public static Image Img(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rt = Rect(name, parent);
            Image image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Img(string name, Transform parent, string spritePath)
        {
            return Img(name, parent, GetSprite(spritePath), Color.white);
        }

        public static Image SlicedImg(string name, Transform parent, string spritePath, float border, Color color)
        {
            Image image = Img(name, parent, GetSliced(spritePath, border), color);
            image.type = Image.Type.Sliced;
            return image;
        }

        public static Text Label(string name, Transform parent, string content, int size, Color color, TextAnchor anchor, bool display)
        {
            RectTransform rt = Rect(name, parent);
            Text text = rt.gameObject.AddComponent<Text>();
            text.font = display ? DisplayFont : TextFont;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        /// <summary>Текст, который сам уменьшается, чтобы поместиться в рамку (для длинных русских строк).</summary>
        public static Text FitLabel(string name, Transform parent, string content, int maxSize, Color color, TextAnchor anchor, bool display)
        {
            Text text = Label(name, parent, content, maxSize, color, anchor, display);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 14;
            text.resizeTextMaxSize = maxSize;
            return text;
        }

        public static Shadow AddShadow(Text text, float distance)
        {
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(distance * 0.6f, -distance);
            return shadow;
        }

        public static Button MakeButton(Image image, Action onClick)
        {
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(delegate { onClick(); });
            return button;
        }

        /// <summary>Невидимая область нажатия.</summary>
        public static Button HitArea(string name, Transform parent, Action onClick)
        {
            Image image = Img(name, parent, null, new Color(0f, 0f, 0f, 0f));
            return MakeButton(image, onClick);
        }

        public static string FormatNumber(int value)
        {
            return value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", " ");
        }

        public static string FormatTime(double seconds)
        {
            int total = (int)Math.Max(0, Math.Round(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}

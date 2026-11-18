using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>Всплывающее окно поверх экрана: затемнение и панель в общем стиле.</summary>
    public static class Modal
    {
        public static RectTransform Create(Transform overlayRoot, string name, float width, float height, float dimAlpha, out RectTransform panel)
        {
            RectTransform root = UiKit.Stretch(UiKit.Rect(name, overlayRoot));
            Image dim = UiKit.Img("Dim", root, null, new Color(0.03f, 0.02f, 0.06f, dimAlpha));
            dim.raycastTarget = true;
            UiKit.Stretch(dim.rectTransform);
            Image back = UiKit.SlicedImg("Panel", root, "ui/round16", 24f, UiKit.Panel);
            UiKit.Center(back.rectTransform, 0f, 0f, width, height);
            Image frame = UiKit.SlicedImg("Frame", back.transform, "ui/frame16", 22f, new Color(0.96f, 0.74f, 0.33f, 0.55f));
            UiKit.Stretch(frame.rectTransform);
            panel = back.rectTransform;
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            RectTransform captured = panel;
            Anim.Run(root, 0f, 0.18f, delegate(float t)
            {
                group.alpha = t;
                float s = Mathf.Lerp(0.92f, 1f, Anim.EaseOut(t));
                captured.localScale = new Vector3(s, s, 1f);
            }, null, true);
            return root;
        }

        /// <summary>Главная кнопка: золотой мазок с тёмным текстом.</summary>
        public static Button GoldButton(Transform parent, float centerX, float centerY, float width, string label, Action onClick)
        {
            Image image = UiKit.Img("GoldButton", parent, "game/banner_gold");
            float height = width / 2.64f;
            UiKit.Center(image.rectTransform, centerX, centerY, width, height);
            Text text = UiKit.FitLabel("Label", image.transform, label, 58, UiKit.Ink, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(width * 0.14f, height * 0.2f);
            text.rectTransform.offsetMax = new Vector2(-width * 0.14f, -height * 0.2f);
            return UiKit.MakeButton(image, onClick);
        }

        /// <summary>Второстепенная кнопка: тёмная плашка со светлым текстом.</summary>
        public static Button PlainButton(Transform parent, float centerX, float centerY, float width, string label, Action onClick)
        {
            Image image = UiKit.Img("PlainButton", parent, "settings/row_blank");
            float height = width / 9f;
            if (height < 92f)
            {
                height = 92f;
            }
            UiKit.Center(image.rectTransform, centerX, centerY, width, height);
            Text text = UiKit.FitLabel("Label", image.transform, label, 44, UiKit.Cream, TextAnchor.MiddleCenter, true);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(40f, 12f);
            text.rectTransform.offsetMax = new Vector2(-40f, -12f);
            return UiKit.MakeButton(image, onClick);
        }

        /// <summary>Строка «название — значение».</summary>
        public static Text StatRow(Transform parent, float centerY, float width, string label, string value)
        {
            Text left = UiKit.Label("StatName", parent, label, 38, UiKit.Muted, TextAnchor.MiddleLeft, false);
            UiKit.Center(left.rectTransform, -width * 0.25f, centerY, width * 0.5f, 50f);
            Text right = UiKit.Label("StatValue", parent, value, 44, UiKit.Cream, TextAnchor.MiddleRight, true);
            UiKit.Center(right.rectTransform, width * 0.25f, centerY, width * 0.5f, 50f);
            return right;
        }
    }
}

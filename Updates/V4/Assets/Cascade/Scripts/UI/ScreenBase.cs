using UnityEngine;
using UnityEngine.UI;

namespace Cascade.UI
{
    /// <summary>
    /// Основа экрана. Root — весь холст; Column — колонка шириной 1080 по центру; Safe — она же с учётом
    /// выреза и нижней полосы телефона. Элементы интерфейса кладём в Safe, фон — в Root.
    /// </summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected CascadeApp App { get; private set; }
        protected RectTransform Root { get; private set; }
        protected RectTransform Column { get; private set; }
        protected RectTransform Safe { get; private set; }

        public void Init(CascadeApp app, RectTransform root)
        {
            App = app;
            Root = root;
            Column = UiKit.Rect("Column", root);
            Column.anchorMin = new Vector2(0.5f, 0f);
            Column.anchorMax = new Vector2(0.5f, 1f);
            Column.pivot = new Vector2(0.5f, 0.5f);
            Column.sizeDelta = new Vector2(UiKit.DesignWidth, 0f);
            Column.anchoredPosition = Vector2.zero;
            Safe = UiKit.Stretch(UiKit.Rect("Safe", Column));
            Safe.offsetMax = new Vector2(0f, -app.TopInset);
            Safe.offsetMin = new Vector2(0f, app.BottomInset);
            Build();
        }

        protected abstract void Build();

        public virtual void OnClose()
        {
        }

        public virtual void OnAppPaused()
        {
        }

        /// <summary>Фон на весь экран: картинка закрывает холст целиком, лишнее обрезается по краям.</summary>
        protected Image CoverBackground(string spritePath, float verticalPivot)
        {
            Image fill = UiKit.Img("BackFill", Root, null, UiKit.Night);
            UiKit.Stretch(fill.rectTransform);
            fill.transform.SetAsFirstSibling();
            Sprite sprite = UiKit.GetSprite(spritePath);
            Image image = UiKit.Img("Background", Root, sprite, Color.white);
            if (sprite != null)
            {
                float aspect = sprite.rect.width / sprite.rect.height;
                float width = App.CanvasWidth;
                float height = width / aspect;
                if (height < App.CanvasHeight)
                {
                    height = App.CanvasHeight;
                    width = height * aspect;
                }
                RectTransform rt = image.rectTransform;
                rt.anchorMin = new Vector2(0.5f, verticalPivot);
                rt.anchorMax = new Vector2(0.5f, verticalPivot);
                rt.pivot = new Vector2(0.5f, verticalPivot);
                rt.sizeDelta = new Vector2(width, height);
                rt.anchoredPosition = Vector2.zero;
            }
            image.transform.SetSiblingIndex(1);
            return image;
        }

        /// <summary>Счётчик с иконкой (монеты, энергия): тёмная плашка, иконка слева, число справа.</summary>
        protected Text Counter(Transform parent, string iconPath, float x, float y, float width, string value)
        {
            Image plate = UiKit.SlicedImg("Counter", parent, "ui/pill", 30f, new Color(0.05f, 0.03f, 0.1f, 0.6f));
            UiKit.Top(plate.rectTransform, x, y, width, 60f);
            Image icon = UiKit.Img("Icon", plate.transform, iconPath);
            UiKit.Center(icon.rectTransform, -width * 0.5f + 30f, 0f, 62f, 62f);
            Text text = UiKit.Label("Value", plate.transform, value, 34, UiKit.Cream, TextAnchor.MiddleCenter, false);
            UiKit.Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(58f, 0f);
            text.rectTransform.offsetMax = new Vector2(-14f, 0f);
            return text;
        }
    }
}

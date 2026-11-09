using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    // Resolution-independent light capsule, driven by the real song progress.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CascadeProgressGraphic : MaskableGraphic
    {
        public Image source;
        int lastPixel = -1;
        void LateUpdate()
        {
            int pixel = Mathf.RoundToInt((source == null ? 0 : source.fillAmount) * rectTransform.rect.width);
            if (pixel == lastPixel) return;
            lastPixel = pixel;
            SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (source == null || source.fillAmount <= 0) return;
            var rect = rectTransform.rect;
            float width = Mathf.Clamp(rect.width * source.fillAmount, 0, rect.width);
            if (width < 1) return;
            var fill = new Rect(rect.xMin, rect.yMin, width, rect.height);
            for (int i = 12; i > 0; i--)
                Pill(vh, new Rect(fill.x - i, fill.y - i, fill.width + i * 2, fill.height + i * 2),
                    new Color(1, .34f, .1f, .008f), new Color(1, .1f, .83f, .016f));
            Pill(vh, fill, new Color(1, .48f, .21f), new Color(1, .98f, .68f));
            for(int i=0;i<4;i++)
                Pill(vh,new Rect(fill.x+1,fill.y+fill.height*(.22f+i*.07f),Mathf.Max(0,fill.width-2),fill.height*(.56f-i*.14f)),
                    new Color(1,.93f,.58f,.22f),new Color(1,1,.86f,.5f));
        }
        static void Pill(VertexHelper vh, Rect r, Color left, Color right)
        {
            if (r.width <= 0) return;
            float radius = Mathf.Min(r.height, r.width) * .5f;
            int start = vh.currentVertCount;
            vh.AddVert(r.center, Color.Lerp(left, right, .5f), Vector2.zero);
            const int count = 36;
            for (int c = 0; c < 4; c++)
                for (int j = 0; j < 9; j++)
                {
                    float angle = (c * 90 + j * 90f / 8) * Mathf.Deg2Rad;
                    float x = (c == 0 || c == 3 ? r.xMax - radius : r.xMin + radius) + Mathf.Cos(angle) * radius;
                    float y = (c < 2 ? r.yMax - radius : r.yMin + radius) + Mathf.Sin(angle) * radius;
                    vh.AddVert(new Vector3(x, y), Color.Lerp(left, right, Mathf.InverseLerp(r.xMin, r.xMax, x)), Vector2.zero);
                }
            for (int i = 0; i < count; i++) vh.AddTriangle(start, start + i + 1, start + (i + 1) % count + 1);
        }
    }
}

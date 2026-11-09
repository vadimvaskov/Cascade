using System;
namespace Cascade.Prototype
{
    [Serializable] public sealed class CascadeMenuLayout { public CascadeMenuScreen[] screens; }
    [Serializable] public sealed class CascadeMenuScreen
    {public string name,kind,record_id;public CascadeMenuElement[] elements_back_to_front;public CascadeMenuAction[] actions;}
    [Serializable] public sealed class CascadeMenuElement
    {
        public string id,type,asset,text,color,anchor,clip,svg_markup;
        public float x,y,width,height,opacity=1,baseline_y,figma_text_top_y,font_size;
        public int font_weight;
        public CascadeMenuGeometry geometry;
        public CascadeMenuCircle clip_circle;
    }
    [Serializable] public sealed class CascadeMenuGeometry {public float[] center;public float size;public string direction;public bool enabled;}
    [Serializable] public sealed class CascadeMenuCircle {public float[] center;public float radius;}
    [Serializable] public sealed class CascadeMenuAction {public string id,action;public float[] rect;public bool enabled;}
}

using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Prototype
{
    [RequireComponent(typeof(Text))]
    public sealed class CascadeLetterSpacing : BaseMeshEffect
    {
        public float spacing=1;
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive()||mesh.currentVertCount==0)return;
            var text=GetComponent<Text>();int count=mesh.currentVertCount/4;
            float align=text.alignment==TextAnchor.MiddleLeft?0:text.alignment==TextAnchor.MiddleRight?1:.5f;
            var vertex=new UIVertex();
            for(int i=0;i<mesh.currentVertCount;i++)
            {
                mesh.PopulateUIVertex(ref vertex,i);
                vertex.position.x+=spacing*(i/4-(count-1)*align);
                mesh.SetUIVertex(vertex,i);
            }
        }
    }
}

using System.Collections.Generic;
using Cascade.Dream;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Bake rounded scenery once in the editor, keeping the original world prefab
// untouched and the same triangle counts / material batches during gameplay.
public static class CascadeSoftScenery
{
    const string Folder="Assets/Cascade/Art/Evening/";
    public static void Apply(DreamRunWorld world)
    {
        var cache=new Dictionary<Mesh,Mesh>();
        foreach(var filter in world.GetComponentsInChildren<MeshFilter>(true))
        {
            bool petals=filter.name.StartsWith("Batch | Petal");
            bool cliff=filter.name.StartsWith("Batch | Cliff");
            bool stone=filter.name=="Batch | Warm sandstone";
            if((!petals&&!cliff&&!stone)||!filter.sharedMesh)continue;
            var original=filter.sharedMesh;
            if(!cache.TryGetValue(original,out var softened))
            {
                softened=Soften(original,petals?8:4,petals||stone);
                string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
                softened=Save(softened,"Soft-"+guid);cache.Add(original,softened);
            }
            filter.sharedMesh=softened;PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }
        var tile=DreamGeometry.Rounded(new Vector3(1.093f,.16f,1.594f),.066f);
        for(int sectionIndex=0;sectionIndex<world.layers[0].sections.Length;sectionIndex++)
        {
            var section=world.layers[0].sections[sectionIndex];
            for(int colour=0;colour<4;colour++)
            {
                var filter=section.Find("Batch | Paver"+colour)?.GetComponent<MeshFilter>();
                if(!filter)continue;
                var instances=new List<CombineInstance>();
                for(int row=0;row<20;row++)for(int col=0;col<5;col++)
                    if((row/3+col+sectionIndex)%4==colour)
                        instances.Add(new CombineInstance{mesh=tile,transform=Matrix4x4.TRS(new Vector3((col-2)*1.10f,-.045f,.8f+row*1.6f),Quaternion.identity,Vector3.one)});
                var mesh=new Mesh();mesh.CombineMeshes(instances.ToArray(),true,true);
                filter.sharedMesh=Save(mesh,"SoftPaver-"+sectionIndex+"-"+colour);PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            }
        }
        Object.DestroyImmediate(tile);
    }
    static Mesh Save(Mesh mesh,string name)
    {
        string path=Folder+name+".asset";mesh.name=name;
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!existing){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        EditorUtility.CopySerialized(mesh,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(mesh);return existing;
    }
    static Mesh Soften(Mesh source,int passes,bool roundComponents)
    {
        var mesh=Object.Instantiate(source);var vertices=mesh.vertices;var triangles=mesh.triangles;
        var lookup=new Dictionary<Vector3Int,int>();var points=new List<Vector3>();var map=new int[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var key=Vector3Int.RoundToInt(vertices[i]*10000);
            if(!lookup.TryGetValue(key,out int index)){index=points.Count;lookup.Add(key,index);points.Add(vertices[i]);}
            map[i]=index;
        }
        var neighbours=new HashSet<int>[points.Count];for(int i=0;i<neighbours.Length;i++)neighbours[i]=new HashSet<int>();
        void Edge(int a,int b){if(a==b)return;neighbours[a].Add(b);neighbours[b].Add(a);}
        for(int i=0;i<triangles.Length;i+=3){int a=map[triangles[i]],b=map[triangles[i+1]],c=map[triangles[i+2]];Edge(a,b);Edge(b,c);Edge(c,a);}
        var current=points.ToArray();var next=new Vector3[current.Length];
        var roundedNormals=new Vector3[current.Length];
        if(roundComponents)
        {
            // Each crown is a disconnected closed surface in the foliage batch.
            // Relax its high-frequency lobes towards a rounded ellipsoid; tiny
            // individual petals retain their silhouette.
            var visited=new bool[current.Length];var component=new List<int>();var pending=new Stack<int>();
            for(int seed=0;seed<current.Length;seed++)
            {
                if(visited[seed])continue;component.Clear();pending.Push(seed);visited[seed]=true;
                while(pending.Count>0)
                {
                    int index=pending.Pop();component.Add(index);
                    foreach(int adjacent in neighbours[index])if(!visited[adjacent]){visited[adjacent]=true;pending.Push(adjacent);}
                }
                if(component.Count<400||component.Count>1000)continue;
                var bounds=new Bounds(current[seed],Vector3.zero);foreach(int index in component)bounds.Encapsulate(current[index]);
                var radii=bounds.extents;if(Mathf.Min(radii.x,Mathf.Min(radii.y,radii.z))<.01f)continue;
                foreach(int index in component)
                {
                    var offset=current[index]-bounds.center;var direction=new Vector3(offset.x/radii.x,offset.y/radii.y,offset.z/radii.z).normalized;
                    current[index]=Vector3.Lerp(current[index],bounds.center+Vector3.Scale(direction,radii),.9f);
                    roundedNormals[index]=new Vector3(direction.x/radii.x,direction.y/radii.y,direction.z/radii.z).normalized;
                }
            }
        }
        // Paired positive/negative relaxation reduces small angular lumps without
        // collapsing the canopy volume or shrinking the floating islands.
        for(int pass=0;pass<passes*2;pass++)
        {
            float amount=pass%2==0?.5f:-.53f;
            for(int i=0;i<current.Length;i++)
            {
                Vector3 sum=Vector3.zero;foreach(int adjacent in neighbours[i])sum+=current[adjacent];
                next[i]=neighbours[i].Count==0?current[i]:current[i]+amount*(sum/neighbours[i].Count-current[i]);
            }
            var swap=current;current=next;next=swap;
        }
        for(int i=0;i<vertices.Length;i++)vertices[i]=current[map[i]];
        var normals=new Vector3[current.Length];
        for(int i=0;i<triangles.Length;i+=3)
        {
            int a=map[triangles[i]],b=map[triangles[i+1]],c=map[triangles[i+2]];
            var normal=Vector3.Cross(current[b]-current[a],current[c]-current[a]);normals[a]+=normal;normals[b]+=normal;normals[c]+=normal;
        }
        var outputNormals=new Vector3[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            int index=map[i];var normal=normals[index].normalized;
            outputNormals[i]=roundedNormals[index]==Vector3.zero?normal:Vector3.Lerp(normal,roundedNormals[index],.65f).normalized;
        }
        mesh.vertices=vertices;mesh.normals=outputNormals;mesh.RecalculateBounds();return mesh;
    }
}

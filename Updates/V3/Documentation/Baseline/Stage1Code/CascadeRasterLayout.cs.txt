using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Cascade.Prototype
{
    // The supplied top-left pixel coordinates remain the source of truth.
    [Serializable] public sealed class CascadeRasterLayout
    {
        public float[] canvas, grid_centers_x, grid_centers_y;
        public CascadeRasterLayer[] layers_back_to_front;
        public CascadeRasterLayer Asset(string name) => layers_back_to_front.First(x => x.file == "Assets/" + name);
        public Vector2 Size => new Vector2(canvas[0], canvas[1]);
        public Vector2 Center(int cell) => new Vector2(grid_centers_x[cell % 6], grid_centers_y[cell / 6]);
        public Vector4[] OrbRects()
        {
            var result = new Vector4[4];
            foreach (var layer in layers_back_to_front)
            {
                var match = Regex.Match(layer.name, @"^R(\d) C(\d) Sphere (\d)$");
                if (!match.Success) continue;
                int row = int.Parse(match.Groups[1].Value) - 1, col = int.Parse(match.Groups[2].Value) - 1;
                int state = int.Parse(match.Groups[3].Value) - 1;
                var rect = new Vector4(layer.x-grid_centers_x[col],layer.y-grid_centers_y[row],layer.width,layer.height);
                if (result[state].z > 0 && Vector4.Distance(result[state],rect) > .01f)
                    throw new InvalidOperationException("Inconsistent sphere placement in supplied layout: " + layer.name);
                result[state] = rect;
            }
            if (result.Any(x => x.z <= 0 || x.w <= 0)) throw new InvalidOperationException("The layout must describe all four sphere states.");
            return result;
        }
        public int[] ReferenceCells()
        {
            var cells = new int[36];
            foreach (var layer in layers_back_to_front)
            {
                var match = Regex.Match(layer.name, @"^R(\d) C(\d) Sphere (\d)$");
                if (match.Success) cells[(int.Parse(match.Groups[1].Value)-1)*6+int.Parse(match.Groups[2].Value)-1]=int.Parse(match.Groups[3].Value);
            }
            return cells;
        }
        public void Validate()
        {
            if (canvas == null || canvas.Length != 2 || canvas[0] != 941 || canvas[1] != 1672 ||
                grid_centers_x == null || grid_centers_x.Length != 6 || grid_centers_y == null || grid_centers_y.Length != 6 ||
                layers_back_to_front == null || layers_back_to_front.Length != 21)
                throw new InvalidOperationException("Expected the supplied Cascade 941 x 1672 layout with 21 layers and a 6 x 6 grid.");
            OrbRects();
        }
    }
    [Serializable] public sealed class CascadeRasterLayer
    {
        public string name, file;
        public float x,y,width,height;
    }
}

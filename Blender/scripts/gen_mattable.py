import sys, os, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from matdefs import MATS
sizes = json.load(open(sys.argv[1]))
out = sys.argv[2]
def c(v): return "new Color(%.3ff, %.3ff, %.3ff)" % tuple(v)
lines = []
for n, (tex, col, sm, me, ex) in MATS.items():
    lines.append('            new MatDef("%s", %s, %s, %.2ff, %.2ff, %.2ff, %s, %.3ff),' % (
        n, ('"%s"' % tex) if tex else "null", c(col), sm, me, ex.get("alpha", 1.0),
        c(ex["emission"]) if "emission" in ex else "Color.black", sizes.get(tex, 1.0) if tex else 1.0))
src = '''// ARCHIVO GENERADO por tools/gen_mattable.py a partir de la tabla de materiales del modelo.
using UnityEngine;

namespace CasaVR.EditorTools
{
    public struct MatDef
    {
        public string name;
        public string texture;
        public Color color;
        public float smoothness;
        public float metallic;
        public float alpha;
        public Color emission;
        public float textureMeters;

        public MatDef(string name, string texture, Color color, float smoothness, float metallic, float alpha, Color emission, float textureMeters)
        {
            this.name = name; this.texture = texture; this.color = color; this.smoothness = smoothness;
            this.metallic = metallic; this.alpha = alpha; this.emission = emission; this.textureMeters = textureMeters;
        }
    }

    public static class CasaVRMaterialTable
    {
        public static readonly MatDef[] All =
        {
%s
        };
    }
}
''' % "\n".join(lines)
open(out, "w").write(src)
print("ok", len(MATS))

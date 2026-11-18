# Renders de verificación (Cycles) con texturas y luces de las habitaciones.
import bpy, sys, os, math, json
from mathutils import Vector
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from matdefs import MATS

blend, texdir, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
only = sys.argv[4].split(",") if len(sys.argv) > 4 and sys.argv[4] else None
samples = int(sys.argv[5]) if len(sys.argv) > 5 else 32
bpy.ops.wm.open_mainfile(filepath=blend)
sizes = json.load(open(os.path.join(texdir, "tex_sizes.json")))

def s2l(c):
    return tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c)

for m in bpy.data.materials:
    if m.name not in MATS:
        continue
    tex, colr, smooth, metal, extra = MATS[m.name]
    m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bs = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bs.outputs[0], out.inputs[0])
    bs.inputs["Roughness"].default_value = 1.0 - smooth
    bs.inputs["Metallic"].default_value = metal
    lc = s2l(colr)
    if tex:
        img = bpy.data.images.load(os.path.join(texdir, tex + ".png"), check_existing=True)
        ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = img
        uv = nt.nodes.new("ShaderNodeTexCoord"); mp = nt.nodes.new("ShaderNodeMapping")
        s = 1.0 / sizes.get(tex, 1.0)
        mp.inputs["Scale"].default_value = (s, s, s)
        nt.links.new(uv.outputs["UV"], mp.inputs["Vector"]); nt.links.new(mp.outputs[0], ti.inputs[0])
        mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = 'MULTIPLY'; mix.inputs[0].default_value = 1.0
        nt.links.new(ti.outputs[0], mix.inputs[1]); mix.inputs[2].default_value = (*lc, 1)
        nt.links.new(mix.outputs[0], bs.inputs["Base Color"])
    else:
        bs.inputs["Base Color"].default_value = (*lc, 1)
    if "alpha" in extra:
        bs.inputs["Alpha"].default_value = extra["alpha"]
        bs.inputs["Roughness"].default_value = 0.02
    if "emission" in extra:
        bs.inputs["Emission Color"].default_value = (*s2l(extra["emission"]), 1)
        bs.inputs["Emission Strength"].default_value = 6.0

sc = bpy.context.scene
# ocultar colisiones
for o in bpy.data.objects:
    if o.name.startswith("COL_"):
        o.hide_render = True
# luces de habitaciones
for o in list(bpy.data.objects):
    if o.name.startswith("Luz_"):
        ld = bpy.data.lights.new(o.name + "_L", "POINT"); ld.energy = 70; ld.color = (1.0, 0.9, 0.78)
        ld.shadow_soft_size = 0.15
        lo = bpy.data.objects.new(o.name + "_L", ld); sc.collection.objects.link(lo); lo.location = o.matrix_world.translation
sun = bpy.data.lights.new("Sol", "SUN"); sun.energy = 3.5; sun.angle = math.radians(2)
so = bpy.data.objects.new("Sol", sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(40), math.radians(10), math.radians(200))
w = bpy.data.worlds.new("Cielo"); sc.world = w; w.use_nodes = True
sky = w.node_tree.nodes["Background"]; sky.inputs[0].default_value = (0.55, 0.7, 0.95, 1); sky.inputs[1].default_value = 0.8

cam_d = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cam_d); sc.collection.objects.link(cam); sc.camera = cam
sc.render.engine = 'CYCLES'; sc.cycles.samples = samples; sc.cycles.device = 'CPU'
sc.cycles.use_denoising = True
try:
    sc.cycles.denoiser = 'OPENIMAGEDENOISE'
except Exception:
    pass
sc.cycles.max_bounces = 4; sc.cycles.diffuse_bounces = 3; sc.cycles.glossy_bounces = 2
sc.cycles.transparent_max_bounces = 8
sc.render.resolution_x = 960; sc.render.resolution_y = 600
sc.view_settings.view_transform = 'AgX' if 'AgX' in [v.identifier for v in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items] else 'Filmic'

VIEWS = {
    "P1_sala": ((-4.25, -2.15, 1.6), (-2.0, 1.6, 0.9), 16),
    "P1_cocina": ((-1.95, 1.3, 1.65), (-4.3, 4.3, 1.0), 16),
    "P1_dormitorio": ((-1.05, 1.15, 1.65), (2.2, -1.6, 0.6), 16),
    "P1_bano": ((-0.1, 1.75, 1.65), (2.2, 4.0, 0.9), 16),
    "P1_lavanderia": ((-0.55, 2.75, 1.6), (-1.2, 4.7, 0.8), 15),
    "P1_patio": ((-2.0, 5.05, 1.6), (-4.2, 6.6, 0.6), 15),
    "P2_balcon": ((-3.2, 4.98, 4.6), (-3.2, 6.6, 3.7), 15),
    "escalera": ((-4.85, -3.7, 1.6), (-6.6, 0.0, 1.6), 16),
    "P2_sala": ((-4.25, -2.15, 4.6), (-2.0, 1.6, 3.9), 16),
    "ext_frente": ((7.0, -15.0, 4.5), (-2.0, 0.0, 2.6), 26),
    "ext_atras": ((-9.0, 16.0, 5.0), (-2.0, 3.0, 2.2), 26),
}
os.makedirs(outdir, exist_ok=True)
for name, (p, t, lens) in VIEWS.items():
    if only and name not in only:
        continue
    cam.location = p; d = Vector(t) - Vector(p); cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cam_d.lens = lens; cam_d.clip_start = 0.05
    sc.render.filepath = os.path.join(outdir, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("render", name, flush=True)

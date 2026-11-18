# Organiza la jerarquía por colecciones y exporta el FBX para Unity (+ verificación de reimportación)
import bpy, sys, os, json
from mathutils import Vector
blend, out_fbx = sys.argv[1], sys.argv[2]
bpy.ops.wm.open_mainfile(filepath=blend)
vl = bpy.context.view_layer
stats = {"objects": 0, "tris": 0, "verts": 0}
# Jerarquía PLANA: 'bake_space_transform' solo es fiable para objetos raíz.
# Unity reconstruye la jerarquía por nombre (sufijos _Hoja, _Marco, _Manija, ...).
vl.update()
for o in list(bpy.data.objects):
    if o.parent is not None:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
vl.update()
for o in bpy.data.objects:
    stats["objects"] += 1
    if o.type == 'MESH':
        o.data.calc_loop_triangles()
        stats["tris"] += len(o.data.loop_triangles); stats["verts"] += len(o.data.vertices)
print("STATS", stats)
bpy.ops.export_scene.fbx(filepath=out_fbx, use_selection=False, object_types={'EMPTY', 'MESH'},
                         apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                         use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False,
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False,
                         use_custom_props=False, use_triangles=False)
# verificación: reimportar en una escena vacía y comparar marcadores
ref = {o.name: tuple(round(c, 4) for c in o.matrix_world.translation) for o in bpy.data.objects}
refbb = {}
for o in bpy.data.objects:
    if o.type == 'MESH' and len(o.data.vertices):
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        refbb[o.name] = (Vector([min(v[i] for v in vs) for i in range(3)]), Vector([max(v[i] for v in vs) for i in range(3)]))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=out_fbx)
bad = 0
for n, p in ref.items():
    o = bpy.data.objects.get(n)
    if o is None:
        print("FALTA", n); bad += 1; continue
    q = o.matrix_world.translation
    if (Vector(p) - q).length > 0.002:
        print("DIF", n, p, tuple(q)); bad += 1
badbb = 0
for n, (a, b) in refbb.items():
    o = bpy.data.objects.get(n)
    if o is None or o.type != 'MESH':
        continue
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    a2 = Vector([min(v[i] for v in vs) for i in range(3)]); b2 = Vector([max(v[i] for v in vs) for i in range(3)])
    if (a - a2).length > 0.003 or (b - b2).length > 0.003:
        badbb += 1
        if badbb < 5: print("BBOX", n, tuple(a), tuple(a2))
print("REIMPORT objetos:", len(bpy.data.objects), "de", len(ref), "materiales:", len(bpy.data.materials),
      "posiciones distintas:", bad, "mallas con bbox distinto:", badbb)

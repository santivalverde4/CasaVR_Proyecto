# Utilidades comunes para los scripts de Blender del proyecto CasaVR
import bpy, bmesh, math
from mathutils import Vector, Matrix

def get_coll(name, parent=None):
    c = bpy.data.collections.get(name)
    if c is None:
        c = bpy.data.collections.new(name)
        (parent or bpy.context.scene.collection).children.link(c)
    return c

def link_only(obj, coll):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)

MAT_COLORS = {}

def mat(name, color=(0.8, 0.8, 0.8), rough=0.6, metal=0.0, alpha=1.0, emit=None):
    """Material de Blender (solo para previsualizar). El nombre se usa en Unity."""
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        b = m.node_tree.nodes.get("Principled BSDF")
        b.inputs["Base Color"].default_value = (*color, 1.0)
        b.inputs["Roughness"].default_value = rough
        b.inputs["Metallic"].default_value = metal
        if alpha < 1.0:
            b.inputs["Alpha"].default_value = alpha
            try:
                m.surface_render_method = 'BLENDED'
            except Exception:
                pass
        if emit is not None:
            b.inputs["Emission Color"].default_value = (*emit, 1.0)
            b.inputs["Emission Strength"].default_value = 2.0
        m.diffuse_color = (*color, alpha)
        MAT_COLORS[name] = color
    return m

def ensure_mat_slot(me, m):
    for i, s in enumerate(me.materials):
        if s == m:
            return i
    me.materials.append(m)
    return len(me.materials) - 1

def world_uv(bm, scale=1.0):
    """UV en metros por proyección de caja según la normal de cada cara."""
    uvl = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for l in f.loops:
            co = l.vert.co
            if az >= ax and az >= ay:
                u, v = co.x, co.y
            elif ax >= ay:
                u, v = co.y, co.z
            else:
                u, v = co.x, co.z
            l[uvl].uv = (u * scale, v * scale)

def bm_world(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(obj.matrix_world)
    return bm

def face_components(bm, faces=None):
    faces = list(bm.faces) if faces is None else list(faces)
    allowed = set(f.index for f in faces)
    bm.faces.ensure_lookup_table()
    seen = set(); res = []
    for f in faces:
        if f.index in seen:
            continue
        st = [f]; seen.add(f.index); comp = []
        while st:
            x = st.pop(); comp.append(x)
            for v in x.verts:
                for g in v.link_faces:
                    if g.index in allowed and g.index not in seen:
                        seen.add(g.index); st.append(g)
        res.append(comp)
    return res

def bbox_faces(faces):
    vs = {v for f in faces for v in f.verts}
    mn = Vector([min(v.co[i] for v in vs) for i in range(3)])
    mx = Vector([max(v.co[i] for v in vs) for i in range(3)])
    return mn, mx

def new_obj_from_faces(name, src_bm, faces, coll, origin=None, mat_for_face=None):
    """Crea un objeto nuevo con copia de las caras dadas (coordenadas de mundo)."""
    bm = bmesh.new()
    vmap = {}
    uv_src = src_bm.loops.layers.uv.active
    for f in faces:
        vs = []
        for v in f.verts:
            if v not in vmap:
                vmap[v] = bm.verts.new(v.co)
            vs.append(vmap[v])
        try:
            nf = bm.faces.new(vs)
        except ValueError:
            continue
        nf.smooth = False
        nf.material_index = f.material_index
        if mat_for_face is not None:
            nf.material_index = mat_for_face(f)
    me = bpy.data.meshes.new(name)
    if origin is not None:
        bm.transform(Matrix.Translation(-Vector(origin)))
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    if origin is not None:
        ob.location = Vector(origin)
    coll.objects.link(ob)
    return ob

def add_empty(name, loc, coll, parent=None, size=0.1):
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = size
    e.empty_display_type = 'PLAIN_AXES'
    coll.objects.link(e)
    e.location = Vector(loc)
    if parent is not None:
        mw = e.matrix_world.copy()
        e.parent = parent
        e.matrix_parent_inverse = parent.matrix_world.inverted()
    return e

def set_parent_keep(child, parent):
    bpy.context.view_layer.update()
    mw = child.matrix_world.copy()
    child.parent = parent
    child.matrix_world = mw

def obj_bbox(o):
    vs = [o.matrix_world @ v.co for v in o.data.vertices]
    mn = Vector([min(v[i] for v in vs) for i in range(3)])
    mx = Vector([max(v[i] for v in vs) for i in range(3)])
    return mn, mx

def obj_center(o):
    a, b = obj_bbox(o)
    return (a + b) / 2

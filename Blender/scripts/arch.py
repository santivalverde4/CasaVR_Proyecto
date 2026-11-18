# Procesa la arquitectura original (Revit -> Blender) para Unity.
# - Elimina duplicados, cámaras, luces y marcadores de nivel
# - Aplica transformaciones (escala real en metros)
# - Separa hojas de puertas (pivote en la bisagra) y hojas de ventanas corredizas
# - Asigna materiales por cara según habitación, UVs en metros
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from common import *

# --------------------------------------------------------------------------------------
# Habitaciones (coordenadas interiores medidas del modelo, metros, Blender Z arriba)
# --------------------------------------------------------------------------------------
X_W, X_PAT, X_SAL, X_LAV, X_E = -4.5158, -1.8158, -1.3658, -0.3658, 2.4842
Y_S, Y_DOR, Y_LAV, Y_N, Y_PAT = -2.3593, 1.3907, 2.5907, 4.7407, 6.4907

def floor_rooms(f):
    z0 = 0.0 if f == 1 else 3.0
    p = f"P{f}_"
    rooms = [
        dict(id=p+"Sala", group=p+"SalaCocina", kind="sala", z0=z0,
             rects=[(X_W, Y_S, X_SAL, 2.4407), (X_SAL, 1.5407, X_LAV, 2.4407)]),
        dict(id=p+"Cocina", group=p+"SalaCocina", kind="cocina", z0=z0,
             rects=[(X_W, 2.4407, X_PAT, Y_N)]),
        dict(id=p+"Dormitorio", group=p+"Dormitorio", kind="dormitorio", z0=z0,
             rects=[(-1.2158, Y_S, X_E, Y_DOR)]),
        dict(id=p+"Bano", group=p+"Bano", kind="bano", z0=z0,
             rects=[(-0.2158, 1.5407, X_E, Y_N)]),
        dict(id=p+"Lavanderia", group=p+"Lavanderia", kind="lavanderia", z0=z0,
             rects=[(-1.6658, Y_LAV, X_LAV, Y_N)]),
    ]
    if f == 1:
        rooms.append(dict(id="P1_Patio", group="P1_Patio", kind="patio", z0=z0,
                          rects=[(X_W, 4.8907, X_PAT, Y_PAT)]))
        rooms.append(dict(id="P1_Porche", group="P1_Porche", kind="porche", z0=z0,
                          rects=[(X_W, -4.3193, -3.1158, -2.5093)]))
    else:
        rooms.append(dict(id="P2_Balcon", group="P2_Balcon", kind="balcon", z0=z0,
                          rects=[(X_W, 4.8907, X_PAT, Y_PAT)]))
        rooms.append(dict(id="P2_Pasillo", group="P2_Pasillo", kind="pasillo", z0=z0,
                          rects=[(X_W, -3.8543, X_E, -2.5093)]))
    return rooms

ROOMS = floor_rooms(1) + floor_rooms(2) + [
    dict(id="Escalera", group="Escalera", kind="escalera", z0=0.0,
         rects=[(-7.0708, -4.0093, -4.6658, 1.6657)]),
]

def room_at(p, zc=None):
    z = p.z if zc is None else zc
    for r in ROOMS:
        if r["kind"] == "escalera":
            zok = -0.5 < z < 6.5
        elif r["z0"] == 0.0:
            zok = -0.2 < z < 2.95
        else:
            zok = 2.95 <= z < 9.5
        if not zok:
            continue
        for (x0, y0, x1, y1) in r["rects"]:
            if x0 - 1e-3 <= p.x <= x1 + 1e-3 and y0 - 1e-3 <= p.y <= y1 + 1e-3:
                return r
    return None

# --------------------------------------------------------------------------------------
# Materiales (los nombres se usan en Unity para crear/asignar materiales URP)
# --------------------------------------------------------------------------------------
def make_materials():
    M = {}
    def m(n, *a, **k):
        M[n] = mat(n, *a, **k)
    m("M_Pared_Interior", (0.86, 0.83, 0.78), 0.9)
    m("M_Pared_Acento", (0.62, 0.66, 0.58), 0.9)
    m("M_Pared_Escalera", (0.80, 0.79, 0.76), 0.9)
    m("M_Fachada", (0.72, 0.68, 0.60), 0.95)
    m("M_Azulejo_Bano", (0.88, 0.89, 0.90), 0.3)
    m("M_Azulejo_Lavanderia", (0.78, 0.82, 0.84), 0.35)
    m("M_Techo", (0.92, 0.91, 0.89), 0.95)
    m("M_Cubierta", (0.22, 0.23, 0.25), 0.6, 0.3)
    m("M_Fascia", (0.90, 0.90, 0.88), 0.7)
    m("M_Concreto", (0.55, 0.54, 0.52), 0.95)
    m("M_Piso_Madera", (0.55, 0.38, 0.24), 0.55)
    m("M_Piso_Porcelanato", (0.78, 0.74, 0.68), 0.35)
    m("M_Piso_Ceramica", (0.62, 0.62, 0.62), 0.4)
    m("M_Piso_Exterior", (0.58, 0.55, 0.50), 0.9)
    m("M_Piso_Deck", (0.45, 0.30, 0.18), 0.7)
    m("M_Umbral", (0.85, 0.84, 0.82), 0.25)
    m("M_Escalera_Huella", (0.70, 0.68, 0.64), 0.7)
    m("M_Madera_Puerta", (0.50, 0.33, 0.20), 0.5)
    m("M_Madera_Puerta_Principal", (0.30, 0.19, 0.11), 0.45)
    m("M_Marco_Puerta", (0.92, 0.91, 0.89), 0.5)
    m("M_Metal_Cromo", (0.85, 0.85, 0.86), 0.15, 1.0)
    m("M_Aluminio", (0.16, 0.16, 0.17), 0.4, 0.6)
    m("M_Vidrio", (0.75, 0.85, 0.90), 0.05, 0.0, 0.25)
    m("M_Moldura", (0.93, 0.92, 0.90), 0.5)
    m("M_Madera_Celosia", (0.36, 0.23, 0.13), 0.6)
    m("M_Columna", (0.20, 0.20, 0.21), 0.5, 0.5)
    return M

# --------------------------------------------------------------------------------------
DELETE_IDS = ["[165078]"]
DUP_TOPLEVEL_PREFIX = ("Tramo monolítico", "Descansillo monolítico")

DOOR_NAMES = {
    "169201": "Puerta_P1_Principal", "169295": "Puerta_P1_Patio", "167995": "Puerta_P1_Lavanderia",
    "168910": "Puerta_P1_Dormitorio", "169023": "Puerta_P1_Bano",
    "186962": "Puerta_P2_Principal", "186907": "Puerta_P2_Balcon", "186209": "Puerta_P2_Lavanderia",
    "186790": "Puerta_P2_Dormitorio", "186727": "Puerta_P2_Bano",
}
WIN_NAMES = {
    "175709": "Ventana_P1_Cocina", "179202": "Ventana_P1_Lavanderia", "179860": "Ventana_P1_Bano",
    "181308": "Ventana_P1_Dormitorio", "251844": "Ventana_P1_Sala",
    "187185": "Ventana_P2_Cocina", "187590": "Ventana_P2_Lavanderia", "187911": "Ventana_P2_Bano",
    "188300": "Ventana_P2_Dormitorio", "188035": "Ventana_P2_Sala", "199658": "Ventana_P2_Balcon",
}
FIXED_WIN_NAMES = {"228525": "VentanaFija_Escalera", "249795": "VentanaFija_P2_Pasillo"}
STAIR_NAMES = {
    "214375": "Escalera_Tramo1", "214376": "Escalera_Tramo1_Huellas",
    "214508": "Escalera_Tramo2", "214509": "Escalera_Tramo2_Huellas",
    "214510": "Escalera_Descanso", "214511": "Escalera_Descanso_Acabado",
    "214623": "Pasillo_P2_Losa", "214624": "Pasillo_P2_Acabado",
}
HOUSE_CENTER = Vector((-1.0, 1.2, 0.0))

def rid(o):
    """Último identificador [nnnnnn] del nombre Revit."""
    n = o.name
    i = n.rfind("[")
    return n[i + 1:n.rfind("]")] if i >= 0 else n

INFO = {"doors": [], "windows": [], "stairs": {}}

def clean_scene():
    for o in list(bpy.data.objects):
        if o.type in ('CAMERA', 'LIGHT') or o.name.startswith("Nivel") or any(d in o.name for d in DELETE_IDS):
            bpy.data.objects.remove(o, do_unlink=True)
            continue
        if o.name.startswith(DUP_TOPLEVEL_PREFIX):
            bpy.data.objects.remove(o, do_unlink=True)
    # des-emparentar y aplicar transformaciones a todo
    for o in list(bpy.data.objects):
        if o.parent is not None:
            mw = o.matrix_world.copy(); o.parent = None; o.matrix_world = mw
    for o in list(bpy.data.objects):
        if o.type == 'EMPTY':
            bpy.data.objects.remove(o, do_unlink=True)
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.data = o.data.copy()
            o.data.transform(o.matrix_world)
            o.matrix_world = Matrix.Identity(4)

def recalc_normals(bm):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])

# --------------------------------------------------------------------------------------
def process_door(o, M, coll):
    name = DOOR_NAMES[rid(o)]
    bm = bm_world(o)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0002)
    bm.faces.ensure_lookup_table()
    comps = face_components(bm)
    def diag(c):
        a, b = bbox_faces(c); return (b - a).length
    comps.sort(key=lambda c: -diag(c))
    big = comps[0]; small = [f for c in comps[1:] for f in c]
    small_set = set(small)
    mn, mx = bbox_faces(big)
    ext = mx - mn
    t_ax = 0 if ext.x < ext.y else 1          # eje del espesor del muro
    w_ax = 1 - t_ax                           # eje del ancho de la puerta
    # caras grandes del panel (normal según el eje del espesor)
    bm.normal_update()
    leaf_big = [f for f in big if f.calc_area() > 0.25 and abs(f.normal[t_ax]) > 0.95]
    if not leaf_big:
        print("DEBUG door", o.name, ext, sorted([(round(f.calc_area(),3), tuple(round(x,2) for x in f.normal)) for f in big], reverse=True)[:6])
    lmn, lmx = bbox_faces(leaf_big)
    tol = 0.0015
    def inside(f):
        a, b = bbox_faces([f])
        return all(a[i] >= lmn[i] - tol and b[i] <= lmx[i] + tol for i in range(3))
    leaf_faces = [f for f in big if inside(f)]
    frame_faces = [f for f in big if not inside(f)]
    # lado de la manija -> bisagra en el lado opuesto
    hmn, hmx = bbox_faces(small)
    hc = (hmn + hmx) / 2
    lc = (lmn + lmx) / 2
    hinge_w = lmn[w_ax] if hc[w_ax] > lc[w_ax] else lmx[w_ax]
    # lado hacia donde abre: el panel está al ras del lado de apertura
    fc = (mn + mx) / 2
    open_sign = 1.0 if lc[t_ax] > fc[t_ax] else -1.0
    hinge_t = lmx[t_ax] if open_sign > 0 else lmn[t_ax]
    hinge = Vector((0, 0, 0)); hinge[w_ax] = hinge_w; hinge[t_ax] = hinge_t; hinge.z = lmn.z
    width = lmx[w_ax] - lmn[w_ax]
    open_pt = hinge.copy(); open_pt[t_ax] += open_sign * width
    principal = "Principal" in name
    m_leaf = M["M_Madera_Puerta_Principal"] if principal else M["M_Madera_Puerta"]
    mats = [M["M_Marco_Puerta"], m_leaf, M["M_Metal_Cromo"]]
    frame = new_obj_from_faces(name + "_Marco", bm, frame_faces, coll, mat_for_face=lambda f: 0)
    leaf = new_obj_from_faces(name + "_Hoja", bm, leaf_faces + small, coll, origin=hinge,
                              mat_for_face=lambda f: 2 if f in small_set else 1)
    for ob in (frame, leaf):
        for mm in mats:
            ob.data.materials.append(mm)
        b2 = bmesh.new(); b2.from_mesh(ob.data); recalc_normals(b2)
        b2.transform(ob.matrix_world); world_uv(b2); b2.transform(ob.matrix_world.inverted())
        b2.to_mesh(ob.data); b2.free()
    root = bpy.data.objects.new(name, None); coll.objects.link(root)
    root.location = hinge
    set_parent_keep(frame, root); set_parent_keep(leaf, root)
    e = add_empty(name + "_Abre", open_pt, coll); set_parent_keep(e, root)
    INFO["doors"].append(dict(name=name, hinge=tuple(hinge), open=tuple(open_pt), t_ax=t_ax, w_ax=w_ax,
                              width=width, leaf_min=tuple(lmn), leaf_max=tuple(lmx), open_sign=open_sign,
                              frame_min=tuple(mn), frame_max=tuple(mx)))
    bm.free()
    bpy.data.objects.remove(o, do_unlink=True)

# --------------------------------------------------------------------------------------
def process_sliding_window(o, M, coll, muntins, trims):
    name = WIN_NAMES[rid(o)]
    bm = bm_world(o)
    bm.faces.ensure_lookup_table()
    comps = face_components(bm)
    glass = [c for c in comps if len({v for f in c for v in f.verts}) == 8]
    frame = max(comps, key=len)
    gmn, gmx = bbox_faces(glass[0]); ext = gmx - gmn
    t_ax = 0 if ext.x < ext.y else 1
    w_ax = 1 - t_ax
    regions = []
    for g in glass:
        a, b = bbox_faces(g)
        rmn, rmx = a.copy(), b.copy()
        rmn[w_ax] -= 0.075; rmx[w_ax] += 0.075; rmn.z -= 0.075; rmx.z += 0.075
        rmn[t_ax] -= 0.03; rmx[t_ax] += 0.03
        regions.append((rmn, rmx, a, b))
    assign = {}
    for f in frame:
        a, b = bbox_faces([f])
        for k, (rmn, rmx, _, _) in enumerate(regions):
            if all(a[i] >= rmn[i] - 0.002 and b[i] <= rmx[i] + 0.002 for i in range(3)):
                assign[f] = k; break
    # hoja interior = la más cercana al interior de la casa
    fmn, fmx = bbox_faces(frame); fcen = (fmn + fmx) / 2
    inward = 1.0 if (HOUSE_CENTER[t_ax] - fcen[t_ax]) > 0 else -1.0
    gcent = [((r[2] + r[3]) / 2) for r in regions]
    inner = max(range(len(glass)), key=lambda k: gcent[k][t_ax] * inward)
    outer = 1 - inner
    def is_glass(f, k):
        a, b = regions[k][2], regions[k][3]
        if abs(f.normal[t_ax]) < 0.95:
            return False
        fa, fb = bbox_faces([f])
        return fa[w_ax] >= a[w_ax] - 0.002 and fb[w_ax] <= b[w_ax] + 0.002 and fa.z >= a.z - 0.002 and fb.z <= b.z + 0.002
    mats = [M["M_Aluminio"], M["M_Vidrio"]]
    static_faces = [f for f in frame if assign.get(f) != inner] + glass[outer]
    sash_faces = [f for f in frame if assign.get(f) == inner] + glass[inner]
    def mf_static(f):
        if f in set(glass[outer]):
            return 1
        k = assign.get(f)
        return 1 if (k is not None and is_glass(f, k)) else 0
    def mf_sash(f):
        if f in set(glass[inner]):
            return 1
        return 1 if is_glass(f, inner) else 0
    sc = gcent[inner]
    frame_ob = new_obj_from_faces(name + "_Marco", bm, static_faces, coll, mat_for_face=mf_static)
    sash_ob = new_obj_from_faces(name + "_Hoja", bm, sash_faces, coll, origin=sc, mat_for_face=mf_sash)
    for ob in (frame_ob, sash_ob):
        for mm in mats:
            ob.data.materials.append(mm)
    # montantes (cuadrícula) -> unir a la hoja correspondiente
    for mo in list(muntins):
        c = obj_center(mo)
        for k, (rmn, rmx, _, _) in enumerate(regions):
            if all(rmn[i] - 0.01 <= c[i] <= rmx[i] + 0.01 for i in range(3)):
                mo.data.materials.clear(); mo.data.materials.append(M["M_Aluminio"])
                target = sash_ob if k == inner else frame_ob
                bpy.ops.object.select_all(action='DESELECT')
                mo.select_set(True); target.select_set(True)
                bpy.context.view_layer.objects.active = target
                bpy.ops.object.join()
                muntins.remove(mo)
                break
    for ob in (frame_ob, sash_ob):
        b2 = bmesh.new(); b2.from_mesh(ob.data); recalc_normals(b2)
        b2.transform(ob.matrix_world); world_uv(b2); b2.transform(ob.matrix_world.inverted())
        b2.to_mesh(ob.data); b2.free()
    root = bpy.data.objects.new(name, None); coll.objects.link(root)
    root.location = fcen
    set_parent_keep(frame_ob, root); set_parent_keep(sash_ob, root)
    # destino de la hoja abierta: sobre la hoja exterior (misma vía)
    tgt = sc.copy(); tgt[w_ax] = gcent[outer][w_ax]
    e = add_empty(name + "_HojaAbierta", tgt, coll); set_parent_keep(e, root)
    # molduras de esta ventana
    for tr in list(trims):
        c = obj_center(tr)
        if abs(c[w_ax] - fcen[w_ax]) < 0.3 and abs(c.z - fcen.z) < 0.5 and abs(c[t_ax] - fcen[t_ax]) < 0.3:
            kind = "MolduraInt" if "Cubrejuntas" in tr.name else "MolduraExt"
            tr.name = f"{name}_{kind}"
            tr.data.materials.clear(); tr.data.materials.append(M["M_Moldura"])
            set_parent_keep(tr, root); link_only(tr, coll)
            trims.remove(tr)
    INFO["windows"].append(dict(name=name, center=tuple(fcen), sash=tuple(sc), target=tuple(tgt), t_ax=t_ax,
                                w_ax=w_ax, inward=inward, frame_min=tuple(fmn), frame_max=tuple(fmx)))
    bm.free()
    bpy.data.objects.remove(o, do_unlink=True)

def process_fixed_window(o, M, coll):
    name = FIXED_WIN_NAMES[rid(o)]
    bm = bm_world(o)
    comps = face_components(bm)
    glass = [c for c in comps if len({v for f in c for v in f.verts}) == 8]
    gl = set(f for c in glass for f in c)
    ob = new_obj_from_faces(name, bm, list(bm.faces), coll, mat_for_face=lambda f: 1 if f in gl else 0)
    ob.data.materials.append(M["M_Aluminio"]); ob.data.materials.append(M["M_Vidrio"])
    b2 = bmesh.new(); b2.from_mesh(ob.data); recalc_normals(b2); world_uv(b2); b2.to_mesh(ob.data); b2.free()
    bm.free()
    bpy.data.objects.remove(o, do_unlink=True)
    return ob

# --------------------------------------------------------------------------------------
CUT_X = [-4.5908, -1.7408, -1.2908, -0.2908, 2.5592, -3.1908]
CUT_Y = [-2.4343, 1.4657, 2.5157, 4.8157, 6.5657, -3.9293, -4.0843, 1.7407]
CUT_Z = [1.4, 2.88, 3.0, 4.4]

def wall_material(f, M):
    n = f.normal; c = f.calc_center_median()
    if abs(n.z) > 0.7:
        return "M_Fachada"
    p = c + Vector((n.x, n.y, 0)).normalized() * 0.2
    r = room_at(p, c.z)
    if r is None:
        return "M_Fachada"
    k = r["kind"]
    if k in ("patio", "porche", "pasillo"):
        return "M_Fachada"
    if k == "escalera":
        return "M_Pared_Escalera"
    if k == "bano":
        return "M_Azulejo_Bano"
    if k == "lavanderia":
        return "M_Azulejo_Lavanderia" if (c.z - r["z0"]) < 1.4 else "M_Pared_Interior"
    if k == "dormitorio" and abs(c.x - X_E) < 0.01 and n.x < -0.9:
        return "M_Pared_Acento"
    return "M_Pared_Interior"

def process_walls(M, coll):
    walls = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith("Muro")]
    for o in walls:
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0002)
        for x in CUT_X:
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(x, 0, 0), plane_no=(1, 0, 0), dist=0.0005)
        for y in CUT_Y:
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, y, 0), plane_no=(0, 1, 0), dist=0.0005)
        for z in CUT_Z:
            geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
            bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, 0, z), plane_no=(0, 0, 1), dist=0.0005)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
        recalc_normals(bm)
        o.data.materials.clear()
        idx = {}
        for f in bm.faces:
            mn = wall_material(f, M)
            if mn not in idx:
                o.data.materials.append(M[mn]); idx[mn] = len(o.data.materials) - 1
            f.material_index = idx[mn]
        world_uv(bm)
        bm.to_mesh(o.data); bm.free()
        o.name = "Muro_" + rid(o)
        link_only(o, coll)

def assign_by_normal(o, M, top, bottom, side, newname, coll):
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.0002)
    recalc_normals(bm)
    o.data.materials.clear()
    for mn in (top, bottom, side):
        o.data.materials.append(M[mn])
    for f in bm.faces:
        f.material_index = 0 if f.normal.z > 0.3 else (1 if f.normal.z < -0.3 else 2)
    world_uv(bm)
    bm.to_mesh(o.data); bm.free()
    o.name = newname
    link_only(o, coll)

def process_other(M, colls):
    for o in list(bpy.data.objects):
        if o.type != 'MESH':
            continue
        r = rid(o)
        if o.name.startswith("Suelo"):
            nm = "Losa_P1" if r == "166294" else "Losa_P2"
            assign_by_normal(o, M, "M_Concreto", "M_Techo", "M_Fachada", nm, colls["arq"])
        elif o.name.startswith("Cubierta"):
            assign_by_normal(o, M, "M_Cubierta", "M_Techo", "M_Fascia", "Cubierta", colls["arq"])
        elif r in STAIR_NAMES:
            nm = STAIR_NAMES[r]
            if nm == "Pasillo_P2_Acabado":
                top = "M_Piso_Exterior"
            elif nm.endswith(("Huellas", "Acabado")):
                top = "M_Escalera_Huella"
            else:
                top = "M_Concreto"
            assign_by_normal(o, M, top, "M_Concreto", "M_Concreto", nm, colls["esc"])
        elif o.name.startswith("Pilar rectangular madera"):
            assign_by_normal(o, M, "M_Madera_Celosia", "M_Madera_Celosia", "M_Madera_Celosia",
                             "Celosia_" + r, colls["esc"])
        elif o.name.startswith("Pilar rectangular"):
            assign_by_normal(o, M, "M_Columna", "M_Columna", "M_Columna", "Columna_" + r, colls["arq"])

def run(M):
    colls = {
        "arq": get_coll("Arquitectura"),
        "esc": get_coll("Escalera"),
        "pue": get_coll("Puertas"),
        "ven": get_coll("Ventanas"),
    }
    clean_scene()
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and rid(o) in DOOR_NAMES:
            process_door(o, M, colls["pue"])
    muntins = [o for o in bpy.data.objects if "montante" in o.name]
    trims = [o for o in bpy.data.objects if "Cubrejuntas" in o.name or "Moldura" in o.name]
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and rid(o) in WIN_NAMES:
            process_sliding_window(o, M, colls["ven"], muntins, trims)
    for o in list(bpy.data.objects):
        if o.type == 'MESH' and rid(o) in FIXED_WIN_NAMES:
            process_fixed_window(o, M, colls["ven"])
    for tr in trims:
        tr.name = "Moldura_" + rid(tr); tr.data.materials.clear(); tr.data.materials.append(M["M_Moldura"])
        link_only(tr, colls["ven"])
    for mo in muntins:
        print("WARN montante sin ventana:", mo.name)
    process_walls(M, colls["arq"])
    process_other(M, colls)
    # todo lo que quede en la colección original
    for o in list(bpy.data.objects):
        if o.users_collection and o.users_collection[0].name == "Collection":
            print("WARN objeto sin clasificar:", o.name)
    old = bpy.data.collections.get("Collection")
    if old and len(old.objects) == 0:
        bpy.data.collections.remove(old)
    return colls

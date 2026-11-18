# Verificación de transitabilidad: ¿puede la cápsula del jugador llegar a todas las salas y rodear las mesas?
# Rasteriza los mismos objetos que reciben collider en Unity (paredes, marcos, muebles con BoxCollider,
# hojas de puerta ABIERTAS), dilata por el radio de la cápsula y hace flood fill desde el punto de inicio.
import bpy, sys, json, math
import numpy as np
from mathutils import Vector, Matrix
from scipy.ndimage import binary_dilation, label
import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt

blend, info_path, out_png, level = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4])
radius = float(sys.argv[5]) if len(sys.argv) > 5 else 0.23
bpy.ops.wm.open_mainfile(filepath=blend)
info = json.load(open(info_path))
z0 = 0.0 if level == 1 else 3.0
RES = 0.025
X0, X1, Y0, Y1 = -7.4, 2.9, -4.7, 7.0
nx, ny = int((X1 - X0) / RES), int((Y1 - Y0) / RES)
grid = np.zeros((ny, nx), bool)

def cell(x, y):
    return int((y - Y0) / RES), int((x - X0) / RES)

def fill_rect(x0, y0, x1, y1):
    r0, c0 = cell(min(x0, x1), min(y0, y1)); r1, c1 = cell(max(x0, x1), max(y0, y1))
    grid[max(0, r0):max(0, r1 + 1), max(0, c0):max(0, c1 + 1)] = True

def draw_seg(a, b):
    L = max(abs(b[0] - a[0]), abs(b[1] - a[1])) / RES
    n = int(L) + 2
    for t in np.linspace(0, 1, n):
        x = a[0] + (b[0] - a[0]) * t; y = a[1] + (b[1] - a[1]) * t
        r, c = cell(x, y)
        if 0 <= r < ny and 0 <= c < nx: grid[r, c] = True

SKIP = ("Alfombra", "Tapete", "Espejo", "Salpicadero", "_TV_", "Toallero", "LamparaPie")
LAMP_R = 0.16
def is_section_obj(n):
    return (n.startswith(("Muro_", "Columna_", "Celosia_", "VentanaFija", "Baranda", "COL_Baranda", "COL_Limite", "Seto"))
            or n.endswith("_Marco") or "Mampara" in n)

dg = bpy.context.evaluated_depsgraph_get()
for o in bpy.data.objects:
    if o.type != 'MESH':
        continue
    n = o.name
    M = o.matrix_world
    if is_section_obj(n):
        me = o.data; me.calc_loop_triangles()
        V = [M @ v.co for v in me.vertices]
        for zc in (z0 + 0.3, z0 + 1.0, z0 + 1.6):
            for t in me.loop_triangles:
                p = [V[i] for i in t.vertices]; d = [q.z - zc for q in p]; pts = []
                for i in range(3):
                    a, b = p[i], p[(i + 1) % 3]; da, db = d[i], d[(i + 1) % 3]
                    if (da < 0) != (db < 0):
                        s = da / (da - db); pts.append(a + (b - a) * s)
                if len(pts) == 2: draw_seg(pts[0], pts[1])
    elif n.startswith("Mueble_") and not any(k in n for k in SKIP):
        vs = [M @ v.co for v in o.data.vertices]
        zmin = min(v.z for v in vs); zmax = max(v.z for v in vs)
        if zmax < z0 + 0.05 or zmin > z0 + 1.7:
            continue
        fill_rect(min(v.x for v in vs), min(v.y for v in vs), max(v.x for v in vs), max(v.y for v in vs))
    elif n.endswith("_Hoja") and n.startswith("Puerta_"):
        d = next((d for d in info["doors"] if d["name"] + "_Hoja" == n), None)
        if d is None or abs(d["hinge"][2] - z0) > 0.5: continue
        h = Vector(d["hinge"]); op = Vector(d["open"])
        vs = [M @ v.co for v in o.data.vertices]
        c = sum(vs, Vector()) / len(vs)
        a0 = math.atan2(c.y - h.y, c.x - h.x); a1 = math.atan2(op.y - h.y, op.x - h.x)
        da = (a1 - a0 + math.pi) % (2 * math.pi) - math.pi
        R = Matrix.Rotation(da, 3, 'Z')
        rv = [R @ (v - h) + h for v in vs]
        fill_rect(min(v.x for v in rv), min(v.y for v in rv), max(v.x for v in rv), max(v.y for v in rv))
    elif n.startswith("Mueble_") and "LamparaPie" in n:
        vs = [M @ v.co for v in o.data.vertices]
        cx = sum(v.x for v in vs) / len(vs); cy = sum(v.y for v in vs) / len(vs)
        if abs(min(v.z for v in vs) - z0) < 0.5:
            fill_rect(cx - LAMP_R, cy - LAMP_R, cx + LAMP_R, cy + LAMP_R)
    elif n.endswith("_Tronco"):
        vs = [M @ v.co for v in o.data.vertices]
        cx = sum(v.x for v in vs) / len(vs); cy = sum(v.y for v in vs) / len(vs)
        fill_rect(cx - 0.15, cy - 0.15, cx + 0.15, cy + 0.15)

# dilatación por el radio de la cápsula
k = int(math.ceil(radius / RES))
yy, xx = np.mgrid[-k:k + 1, -k:k + 1]
disk = (xx ** 2 + yy ** 2) <= k * k
blocked = binary_dilation(grid, structure=disk)
free = ~blocked
lab, nlab = label(free)
spawns = {o.name[6:]: o.matrix_world.translation for o in bpy.data.objects
          if o.name.startswith("Spawn_") and not o.name.endswith("_Mira")}
start = spawns["P1_Sala"] if level == 1 else spawns["P2_Sala"]
sr, sc = cell(start.x, start.y)
main = lab[sr, sc]
reach = lab == main
report = {}
for k_, p in spawns.items():
    if (level == 1 and (k_.startswith("P1_") or k_ in ("Ext_Entrada",))) or (level == 2 and k_.startswith("P2_")):
        r, c = cell(p.x, p.y)
        report[k_] = bool(reach[r, c])
# rodear mesas: puntos a 0.45 m de cada lado de mesa de comedor y mesa de centro
P = f"P{level}"
for tname, group in ((f"Mueble_{P}_Comedor", f"Mueble_{P}_Silla_"), (f"Mueble_{P}_MesaCentro", None)):
    o = bpy.data.objects.get(tname)
    if o is None: continue
    objs = [o] + ([x for x in bpy.data.objects if x.name.startswith(group)] if group else [])
    vs = [x.matrix_world @ v.co for x in objs for v in x.data.vertices]
    x0, x1 = min(v.x for v in vs), max(v.x for v in vs); y0, y1 = min(v.y for v in vs), max(v.y for v in vs)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    m = radius + 0.06   # justo afuera del mueble (centro de la cápsula pegado al borde)
    sides = {"N": (cx, y1 + m), "S": (cx, y0 - m), "E": (x1 + m, cy), "O": (x0 - m, cy)}
    for sd, (x, y) in sides.items():
        r, c = cell(x, y)
        report[f"{tname[10:]}_{sd}"] = bool(reach[r, c])
    # rodeable de verdad: la "isla" bloqueada que contiene la mesa no toca ningún otro obstáculo,
    # es decir, hay piso alcanzable formando un anillo completo alrededor.
    nl, _ = label(~reach)
    rr, cc_ = cell(cx, cy); isl = nl == nl[rr, cc_]
    rows, cols = np.nonzero(isl)
    ix0, ix1 = X0 + cols.min() * RES, X0 + (cols.max() + 1) * RES
    iy0, iy1 = Y0 + rows.min() * RES, Y0 + (rows.max() + 1) * RES
    tol = radius + 2 * RES
    report[f"{tname[10:]}_rodeable"] = bool(ix0 >= x0 - tol and ix1 <= x1 + tol and iy0 >= y0 - tol and iy1 <= y1 + tol)
    # mismo recorrido que la prueba PlayMode de Unity: rectángulo a radio+0.06 m de la mesa/sillas
    from scipy.ndimage import distance_transform_edt
    dist = distance_transform_edt(~grid) * RES
    pr = [(x0 - m, y0 - m), (x1 + m, y0 - m), (x1 + m, y1 + m), (x0 - m, y1 + m)]
    clear = 9.0
    for i in range(4):
        (ax, ay), (bx, by) = pr[i], pr[(i + 1) % 4]
        for t in np.linspace(0, 1, 80):
            r, c = cell(ax + (bx - ax) * t, ay + (by - ay) * t)
            clear = min(clear, dist[r, c])
    report[f"{tname[10:]}_holgura_recorrido_m"] = round(float(clear - radius), 3)
print("NIVEL", level, "radio", radius, json.dumps(report))
if "--runs" in sys.argv:
    looks = {o.name[6:-5]: o.matrix_world.translation for o in bpy.data.objects if o.name.startswith("Spawn_") and o.name.endswith("_Mira")}
    for k_, p in spawns.items():
        if k_ not in looks or abs(p.z - z0) > 1.0: continue
        dv = looks[k_] - p; dv.z = 0; dv.normalize()
        run = 0.0
        while run < 6.0:
            r, c = cell(p.x + dv.x * run, p.y + dv.y * run)
            if not (0 <= r < ny and 0 <= c < nx) or blocked[r, c]: break
            run += 0.02
        print(f"  recorrido libre desde {k_:16s} {run:.2f} m")
if "--doors" in sys.argv:
    from scipy.ndimage import distance_transform_edt
    dist = distance_transform_edt(~grid) * RES
    for o in sorted(bpy.data.objects, key=lambda o: o.name):
        if not (o.name.startswith("Puerta_P%d_" % level) and o.name.endswith("_Marco")): continue
        vs = [o.matrix_world @ v.co for v in o.data.vertices]
        x0_, x1_ = min(v.x for v in vs), max(v.x for v in vs); y0_, y1_ = min(v.y for v in vs), max(v.y for v in vs)
        cx, cy = (x0_ + x1_) / 2, (y0_ + y1_) / 2
        n = (1, 0) if (x1_ - x0_) < (y1_ - y0_) else (0, 1)
        for off in (0.6, 0.75):
            pa = (cx - n[0] * off, cy - n[1] * off); pb = (cx + n[0] * off, cy + n[1] * off)
            m = 9.0
            for t in np.linspace(0, 1, 120):
                r, c = cell(pa[0] + (pb[0] - pa[0]) * t, pa[1] + (pb[1] - pa[1]) * t)
                m = min(m, dist[r, c])
            print(f"  {o.name[7:-6]:16s} off {off:.2f}  holgura en la recta {m - radius:+.3f} m")
# --- cuellos de botella: camino "más ancho" (max-min de la distancia al obstáculo) desde el inicio a cada sala
if "--choke" in sys.argv:
    import heapq
    from scipy.ndimage import distance_transform_edt
    dist = distance_transform_edt(~grid) * RES
    best = np.full(grid.shape, -1.0); prev = {}
    best[sr, sc] = dist[sr, sc]; hq = [(-dist[sr, sc], sr, sc)]
    while hq:
        nv, r, c = heapq.heappop(hq); v = -nv
        if v < best[r, c]: continue
        for dr, dc in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
            r2, c2 = r + dr, c + dc
            if 0 <= r2 < ny and 0 <= c2 < nx:
                w = min(v, dist[r2, c2])
                if w > best[r2, c2]:
                    best[r2, c2] = w; prev[(r2, c2)] = (r, c); heapq.heappush(hq, (-w, r2, c2))
    for k_, p in spawns.items():
        if not ((level == 1 and (k_.startswith("P1_") or k_ == "Ext_Entrada")) or (level == 2 and k_.startswith("P2_"))): continue
        r, c = cell(p.x, p.y); w = best[r, c]
        # localizar la celda del cuello
        node = (r, c); wc = None
        while node in prev:
            if abs(dist[node] - w) < 1e-9: wc = node
            node = prev[node]
        loc = (X0 + wc[1] * RES, Y0 + wc[0] * RES) if wc else None
        print(f"  {k_:16s} ancho libre minimo {2*w:.2f} m en {tuple(round(q,2) for q in loc) if loc else '-'}")
img = np.zeros((ny, nx, 3))
img[grid] = (0.15, 0.15, 0.15)
img[blocked & ~grid] = (0.75, 0.75, 0.75)
img[free & ~reach] = (1.0, 0.8, 0.2)
img[reach] = (0.55, 0.85, 0.55)
fig, ax = plt.subplots(figsize=(11, 12))
ax.imshow(img, origin="lower", extent=(X0, X1, Y0, Y1))
for k_, p in spawns.items():
    if abs(p.z - z0) < 1.0:
        ok = report.get(k_)
        ax.plot(p.x, p.y, "o", color=("blue" if ok else "red") if ok is not None else "purple", ms=6)
        ax.text(p.x + 0.05, p.y + 0.05, k_, fontsize=7)
ax.set_xlim(-7.4, 2.9); ax.set_ylim(-4.7, 7.0)
ax.set_title(f"Nivel {level}: verde = alcanzable por el jugador (radio {radius:.2f} m), amarillo = libre pero inaccesible")
fig.savefig(out_png, dpi=80, bbox_inches="tight")

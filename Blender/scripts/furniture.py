# Mobiliario, iluminación, interruptores, grifería, exteriores y marcadores para Unity
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree
from common import mat, get_coll, add_empty, set_parent_keep, world_uv, obj_bbox
from geo import Builder

FACE = {"S": 0.0, "N": math.pi, "E": math.pi / 2, "W": -math.pi / 2}
INFO = {"markers": []}

def mk_mats():
    m = mat
    m("M_Tela_Sofa", (0.33, 0.38, 0.42), 0.9)
    m("M_Cojin_Acento", (0.78, 0.58, 0.22), 0.9)
    m("M_Cojin_Claro", (0.85, 0.82, 0.75), 0.9)
    m("M_Madera_Mueble", (0.62, 0.46, 0.30), 0.55)
    m("M_Madera_Oscura", (0.25, 0.16, 0.10), 0.5)
    m("M_Laca_Blanca", (0.90, 0.89, 0.87), 0.35)
    m("M_Gabinete_Cocina", (0.36, 0.42, 0.38), 0.45)
    m("M_Encimera", (0.88, 0.87, 0.84), 0.25)
    m("M_Acero_Inox", (0.70, 0.71, 0.72), 0.25, 1.0)
    m("M_Porcelana", (0.95, 0.95, 0.94), 0.12)
    m("M_Negro_Mate", (0.05, 0.05, 0.05), 0.6)
    m("M_Pantalla_TV", (0.01, 0.01, 0.015), 0.08)
    m("M_Espejo", (0.85, 0.88, 0.90), 0.02, 1.0)
    m("M_Plastico_Blanco", (0.92, 0.92, 0.92), 0.4)
    m("M_Tela_Cama", (0.93, 0.93, 0.91), 0.9)
    m("M_Tela_Cobija", (0.42, 0.52, 0.62), 0.9)
    m("M_Tela_Cabecera", (0.55, 0.50, 0.45), 0.9)
    m("M_Alfombra", (0.70, 0.64, 0.56), 1.0)
    m("M_Maceta", (0.62, 0.36, 0.24), 0.8)
    m("M_Tierra", (0.20, 0.14, 0.09), 1.0)
    m("M_Hoja", (0.20, 0.42, 0.16), 0.7)
    m("M_Hoja_Clara", (0.36, 0.56, 0.22), 0.7)
    m("M_Corteza", (0.30, 0.22, 0.15), 0.9)
    m("M_Cesped", (0.24, 0.45, 0.16), 0.95)
    m("M_Seto", (0.16, 0.33, 0.12), 0.9)
    m("M_Flor_Roja", (0.80, 0.15, 0.20), 0.7)
    m("M_Flor_Amarilla", (0.95, 0.80, 0.20), 0.7)
    m("M_Lampara_Base", (0.94, 0.94, 0.93), 0.4)
    m("M_Lampara_Difusor", (1.0, 0.96, 0.88), 0.5, emit=(1.0, 0.92, 0.80))
    m("M_Interruptor", (0.96, 0.96, 0.95), 0.35)
    m("M_Pila", (0.66, 0.66, 0.64), 0.8)
    m("M_Vidrio_Oscuro", (0.10, 0.12, 0.14), 0.05)
    m("M_Vitroceramica", (0.02, 0.02, 0.02), 0.08)
    m("M_Azulejo_Cocina", (0.86, 0.86, 0.84), 0.2)
    m("M_Libro_A", (0.55, 0.20, 0.18), 0.8)
    m("M_Libro_B", (0.20, 0.32, 0.50), 0.8)
    m("M_Ratan", (0.68, 0.54, 0.36), 0.8)
    m("M_Toalla", (0.80, 0.88, 0.88), 1.0)

# ---------------------------------------------------------------------------------------
class Placer:
    """Modela en coordenadas locales (frente hacia -Y, origen al centro de la base) y coloca."""
    def __init__(self, name, loc, face="S", coll=None):
        self.b = Builder(name)
        self.loc = Vector(loc)
        self.ang = FACE[face] if isinstance(face, str) else face
        self.coll = coll

    def __getattr__(self, k):
        return getattr(self.b, k)

    def done(self, coll=None):
        M = Matrix.Translation(self.loc) @ Matrix.Rotation(self.ang, 4, 'Z')
        bmesh.ops.transform(self.b.bm, matrix=M, verts=self.b.bm.verts[:])
        return self.b.build(coll or self.coll, origin=self.loc)

def to_world(loc, ang, p):
    return tuple(Matrix.Translation(Vector(loc)) @ Matrix.Rotation(ang, 4, 'Z') @ Vector(p))

# ---------------------------------------------------------------------------------------
# Muebles (local: ancho en X, fondo en Y, frente hacia -Y)
# ---------------------------------------------------------------------------------------
def sofa(p, w=2.0, d=0.9, h=0.82):
    hw, hd = w / 2, d / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.cyl((sx * (hw - 0.08), sy * (hd - 0.08), 0), 0.025, 0.1, "M_Madera_Oscura", seg=8)
    p.rbox((-hw, -hd, 0.1), (hw, hd, 0.25), "M_Tela_Sofa", 0.02)
    p.rbox((-hw, hd - 0.2, 0.25), (hw, hd, h), "M_Tela_Sofa", 0.04)
    for sx in (-1, 1):
        p.rbox((sx * hw - (0.16 if sx > 0 else 0), -hd, 0.25), (sx * hw + (0 if sx > 0 else 0.16), hd - 0.2, 0.6),
               "M_Tela_Sofa", 0.04)
    inner = w - 0.32; n = 3; cw = inner / n
    for i in range(n):
        x0 = -inner / 2 + i * cw
        p.rbox((x0 + 0.01, -hd + 0.02, 0.25), (x0 + cw - 0.01, hd - 0.2, 0.43), "M_Tela_Sofa", 0.04)
        p.rbox((x0 + 0.02, hd - 0.36, 0.43), (x0 + cw - 0.02, hd - 0.2, 0.78), "M_Tela_Sofa", 0.05)
    p.rbox((-inner / 2 + 0.05, hd - 0.46, 0.43), (-inner / 2 + 0.45, hd - 0.36, 0.78), "M_Cojin_Acento", 0.04)
    p.rbox((inner / 2 - 0.45, hd - 0.46, 0.43), (inner / 2 - 0.05, hd - 0.36, 0.78), "M_Cojin_Claro", 0.04)

def coffee_table(p, w=1.0, d=0.55, h=0.42):
    hw, hd = w / 2, d / 2
    p.rbox((-hw, -hd, h - 0.04), (hw, hd, h), "M_Madera_Mueble", 0.01)
    p.box((-hw + 0.04, -hd + 0.04, 0.12), (hw - 0.04, hd - 0.04, 0.14), "M_Madera_Mueble")
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * (hw - 0.05) - 0.015, sy * (hd - 0.05) - 0.015, 0), (sx * (hw - 0.05) + 0.015, sy * (hd - 0.05) + 0.015, h - 0.04), "M_Negro_Mate")
    p.cyl((0.2, 0.0, h), 0.06, 0.12, "M_Porcelana", seg=12)
    p.box((-0.3, -0.12, h), (-0.05, 0.08, h + 0.03), "M_Libro_B")

def media_unit(p, w=1.6, d=0.4, h=0.5):
    hw, hd = w / 2, d / 2
    p.box((-hw + 0.05, -hd + 0.05, 0), (hw - 0.05, hd - 0.03, 0.08), "M_Negro_Mate")
    p.rbox((-hw, -hd, 0.08), (hw, hd, h), "M_Madera_Mueble", 0.008)
    for i in range(1, 4):
        x = -hw + i * w / 4
        p.box((x - 0.002, -hd - 0.002, 0.1), (x + 0.002, -hd + 0.002, h - 0.02), "M_Madera_Oscura")
    p.cyl((-hw + 0.25, 0, h), 0.07, 0.28, "M_Maceta", seg=12, r2=0.05)
    p.box((hw - 0.45, -0.1, h), (hw - 0.15, 0.12, h + 0.06), "M_Libro_A")

def tv(p, w=1.23, hgt=0.71, z0=0.75, wall_y=0.0):
    """TV de pared; local y=0 es la superficie del muro, frente hacia -Y."""
    p.box((-w / 2, wall_y - 0.045, z0), (w / 2, wall_y, z0 + hgt), "M_Negro_Mate")
    p.box((-w / 2 + 0.012, wall_y - 0.047, z0 + 0.012), (w / 2 - 0.012, wall_y - 0.045, z0 + hgt - 0.012), "M_Pantalla_TV")

def rug(p, w, d, m="M_Alfombra"):
    p.box((-w / 2, -d / 2, 0), (w / 2, d / 2, 0.012), m)

def plant(p, r=0.18, h=0.38, fol=0.45, seed=1, m="M_Hoja"):
    p.cyl((0, 0, 0), r * 0.75, h, "M_Maceta", seg=14, r2=r)
    p.cyl((0, 0, h - 0.03), r * 0.95, 0.02, "M_Tierra", seg=14)
    rnd = random.Random(seed)
    for i in range(5):
        a = rnd.uniform(0, 6.28); rr = rnd.uniform(0, r * 0.6)
        c = (math.cos(a) * rr, math.sin(a) * rr, h + fol * rnd.uniform(0.35, 0.8))
        p.sphere(c, fol * rnd.uniform(0.3, 0.45), m if i % 2 == 0 else "M_Hoja_Clara", sub=1, jitter=0.18, seed=seed + i)

def floor_lamp(p, h=1.55):
    p.cyl((0, 0, 0), 0.15, 0.02, "M_Negro_Mate", seg=16)
    p.cyl((0, 0, 0.02), 0.012, h - 0.25, "M_Negro_Mate", seg=8)
    p.cyl((0, 0, h - 0.3), 0.2, 0.28, "M_Lampara_Base", seg=20, r2=0.13, cap=False)

def dining_table(p, w=1.2, d=0.8, h=0.76):
    hw, hd = w / 2, d / 2
    p.rbox((-hw, -hd, h - 0.035), (hw, hd, h), "M_Madera_Mueble", 0.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * (hw - 0.07) - 0.025, sy * (hd - 0.07) - 0.025, 0), (sx * (hw - 0.07) + 0.025, sy * (hd - 0.07) + 0.025, h - 0.035), "M_Madera_Oscura")
    p.cyl((0, 0, h), 0.1, 0.02, "M_Madera_Oscura", seg=16)
    p.cyl((0, 0, h + 0.02), 0.06, 0.2, "M_Porcelana", seg=12, r2=0.035)

def chair(p, w=0.45, d=0.48, seat=0.46, h=0.86):
    hw, hd = w / 2, d / 2
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * (hw - 0.03) - 0.018, sy * (hd - 0.03) - 0.018, 0), (sx * (hw - 0.03) + 0.018, sy * (hd - 0.03) + 0.018, seat - 0.04), "M_Madera_Oscura")
    p.rbox((-hw, -hd, seat - 0.05), (hw, hd, seat), "M_Madera_Mueble", 0.008)
    for sx in (-1, 1):
        p.box((sx * (hw - 0.03) - 0.018, hd - 0.048, seat), (sx * (hw - 0.03) + 0.018, hd - 0.012, h), "M_Madera_Oscura")
    p.rbox((-hw + 0.02, hd - 0.05, h - 0.2), (hw - 0.02, hd - 0.01, h), "M_Madera_Mueble", 0.008)

def base_cabinets(p, w, d=0.6, h=0.9, sink_x=None, cook_x=None, sink_w=0.6):
    """Muebles bajos de cocina. Encimera con hueco para fregadero en sink_x."""
    hw, hd = w / 2, d / 2
    p.box((-hw, -hd + 0.06, 0), (hw, hd, 0.1), "M_Negro_Mate")
    p.box((-hw, -hd + 0.02, 0.1), (hw, hd, h - 0.04), "M_Gabinete_Cocina")
    n = max(1, int(round(w / 0.6)))
    for i in range(1, n):
        x = -hw + i * w / n
        p.box((x - 0.002, -hd + 0.015, 0.12), (x + 0.002, -hd + 0.021, h - 0.06), "M_Negro_Mate")
    for i in range(n):
        xc = -hw + (i + 0.5) * w / n
        p.box((xc - 0.12, -hd + 0.0, h - 0.12), (xc + 0.12, -hd + 0.02, h - 0.105), "M_Acero_Inox")
    t0 = h - 0.04
    ov = 0.025
    if sink_x is None:
        p.box((-hw, -hd - ov, t0), (hw, hd, h), "M_Encimera")
    else:
        sx0, sx1 = sink_x - sink_w / 2, sink_x + sink_w / 2
        sy0, sy1 = -hd + 0.08, hd - 0.12
        p.box((-hw, -hd - ov, t0), (sx0, hd, h), "M_Encimera")
        p.box((sx1, -hd - ov, t0), (hw, hd, h), "M_Encimera")
        p.box((sx0, -hd - ov, t0), (sx1, sy0, h), "M_Encimera")
        p.box((sx0, sy1, t0), (sx1, hd, h), "M_Encimera")
        # cubeta
        bz = h - 0.2
        p.box((sx0 + 0.01, sy0 + 0.01, bz), (sx1 - 0.01, sy1 - 0.01, bz + 0.01), "M_Acero_Inox")
        p.box((sx0, sy0, bz), (sx0 + 0.01, sy1, h - 0.001), "M_Acero_Inox")
        p.box((sx1 - 0.01, sy0, bz), (sx1, sy1, h - 0.001), "M_Acero_Inox")
        p.box((sx0 + 0.01, sy0, bz), (sx1 - 0.01, sy0 + 0.01, h - 0.001), "M_Acero_Inox")
        p.box((sx0 + 0.01, sy1 - 0.01, bz), (sx1 - 0.01, sy1, h - 0.001), "M_Acero_Inox")
        p.cyl((sink_x, (sy0 + sy1) / 2, bz + 0.01), 0.035, 0.003, "M_Negro_Mate", seg=12)
    if cook_x is not None:
        p.box((cook_x - 0.29, -hd + 0.03, h), (cook_x + 0.29, hd - 0.05, h + 0.006), "M_Vitroceramica")
        for (dx, dy, r) in ((-0.14, -0.1, 0.09), (0.14, -0.1, 0.07), (-0.14, 0.12, 0.07), (0.14, 0.12, 0.09)):
            p.cyl((cook_x + dx, dy, h + 0.006), r, 0.0015, "M_Negro_Mate", seg=20, cap=True)
        p.box((cook_x - 0.29, -hd - 0.001, 0.15), (cook_x + 0.29, -hd + 0.02, h - 0.1), "M_Acero_Inox")
        p.box((cook_x - 0.22, -hd - 0.003, 0.25), (cook_x + 0.22, -hd + 0.0, h - 0.28), "M_Vidrio_Oscuro")
        p.box((cook_x - 0.22, -hd - 0.04, h - 0.16), (cook_x + 0.22, -hd - 0.02, h - 0.145), "M_Acero_Inox")
        for i in range(4):
            p.cyl((cook_x - 0.18 + i * 0.12, -hd - 0.001, h - 0.06), 0.018, 0.02, "M_Negro_Mate", seg=10, axis='Y')

def upper_cabinets(p, w, d=0.35, z0=1.45, h=0.72):
    hw, hd = w / 2, d / 2
    p.box((-hw, -hd, z0), (hw, hd, z0 + h), "M_Laca_Blanca")
    n = max(1, int(round(w / 0.5)))
    for i in range(1, n):
        x = -hw + i * w / n
        p.box((x - 0.002, -hd - 0.003, z0 + 0.01), (x + 0.002, -hd + 0.001, z0 + h - 0.01), "M_Negro_Mate")
    for i in range(n):
        xc = -hw + (i + 0.5) * w / n
        p.box((xc - 0.08, -hd - 0.02, z0 + 0.04), (xc + 0.08, -hd, z0 + 0.055), "M_Acero_Inox")

def range_hood(p, z0=1.55):
    p.box((-0.3, -0.25, z0), (0.3, 0.25, z0 + 0.12), "M_Acero_Inox")
    p.box((-0.13, 0.0, z0 + 0.12), (0.13, 0.25, z0 + 0.75), "M_Acero_Inox")

def fridge(p, w=0.7, d=0.7, h=1.82):
    hw, hd = w / 2, d / 2
    p.rbox((-hw, -hd, 0.02), (hw, hd, h), "M_Acero_Inox", 0.015)
    p.box((-hw + 0.01, -hd - 0.003, 1.15), (hw - 0.01, -hd + 0.002, 1.16), "M_Negro_Mate")
    p.box((hw - 0.08, -hd - 0.05, 0.6), (hw - 0.06, -hd - 0.01, 1.1), "M_Negro_Mate")
    p.box((hw - 0.08, -hd - 0.05, 1.22), (hw - 0.06, -hd - 0.01, 1.5), "M_Negro_Mate")

def bed(p, w=1.5, L=2.0):
    hw = w / 2
    p.box((-hw, -L / 2, 0.08), (hw, L / 2, 0.3), "M_Madera_Mueble")
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * (hw - 0.06) - 0.03, sy * (L / 2 - 0.06) - 0.03, 0), (sx * (hw - 0.06) + 0.03, sy * (L / 2 - 0.06) + 0.03, 0.08), "M_Madera_Oscura")
    p.rbox((-hw + 0.02, -L / 2 + 0.02, 0.3), (hw - 0.02, L / 2 - 0.02, 0.52), "M_Tela_Cama", 0.04)
    p.rbox((-hw - 0.01, -L / 2 - 0.01, 0.35), (hw + 0.01, L / 2 - 0.65, 0.555), "M_Tela_Cobija", 0.03)
    for sx in (-1, 1):
        p.rbox((sx * 0.36 - 0.3, L / 2 - 0.55, 0.52), (sx * 0.36 + 0.3, L / 2 - 0.12, 0.66), "M_Tela_Cama", 0.05)
    p.rbox((-hw - 0.05, L / 2 - 0.02, 0.0), (hw + 0.05, L / 2 + 0.06, 1.1), "M_Tela_Cabecera", 0.03)

def nightstand(p, w=0.45, d=0.4, h=0.5, lamp=True):
    hw, hd = w / 2, d / 2
    p.rbox((-hw, -hd, 0.12), (hw, hd, h), "M_Madera_Mueble", 0.008)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * (hw - 0.04) - 0.012, sy * (hd - 0.04) - 0.012, 0), (sx * (hw - 0.04) + 0.012, sy * (hd - 0.04) + 0.012, 0.12), "M_Negro_Mate")
    p.box((-hw + 0.02, -hd - 0.002, 0.3), (hw - 0.02, -hd + 0.002, 0.302), "M_Madera_Oscura")
    p.box((-0.06, -hd - 0.02, 0.38), (0.06, -hd, 0.39), "M_Acero_Inox")
    if lamp:
        p.cyl((0.05, 0.05, h), 0.06, 0.02, "M_Porcelana", seg=12)
        p.cyl((0.05, 0.05, h + 0.02), 0.012, 0.25, "M_Acero_Inox", seg=8)
        p.cyl((0.05, 0.05, h + 0.22), 0.12, 0.17, "M_Lampara_Base", seg=16, r2=0.09, cap=False)

def wardrobe(p, w=1.6, d=0.6, h=2.1):
    hw, hd = w / 2, d / 2
    p.box((-hw + 0.03, -hd + 0.04, 0), (hw - 0.03, hd, 0.08), "M_Negro_Mate")
    p.box((-hw, -hd, 0.08), (hw, hd, h), "M_Madera_Mueble")
    n = 3
    for i in range(1, n):
        x = -hw + i * w / n
        p.box((x - 0.003, -hd - 0.003, 0.1), (x + 0.003, -hd + 0.001, h - 0.02), "M_Madera_Oscura")
    for i in range(n):
        xc = -hw + (i + 0.5) * w / n + (0.2 if i % 2 == 0 else -0.2) * (w / n) / 1.0 * 0.5
        p.box((xc - 0.01, -hd - 0.03, 0.9), (xc + 0.01, -hd - 0.003, 1.3), "M_Acero_Inox")

def desk(p, w=1.2, d=0.6, h=0.75):
    hw, hd = w / 2, d / 2
    p.rbox((-hw, -hd, h - 0.03), (hw, hd, h), "M_Madera_Mueble", 0.006)
    for sx in (-1, 1):
        p.box((sx * (hw - 0.05) - 0.02, -hd + 0.05, 0), (sx * (hw - 0.05) + 0.02, hd - 0.05, 0.03), "M_Negro_Mate")
        p.box((sx * (hw - 0.05) - 0.02, -0.02, 0), (sx * (hw - 0.05) + 0.02, 0.02, h - 0.03), "M_Negro_Mate")
    p.box((hw - 0.45, -hd + 0.02, h - 0.17), (hw - 0.05, hd - 0.02, h - 0.03), "M_Madera_Mueble")
    # portátil
    p.box((-0.18, -0.12, h), (0.18, 0.12, h + 0.015), "M_Acero_Inox")
    p.box((-0.18, 0.115, h + 0.015), (0.18, 0.13, h + 0.24), "M_Acero_Inox")
    p.box((-0.17, 0.112, h + 0.025), (0.17, 0.116, h + 0.23), "M_Pantalla_TV")
    p.cyl((-hw + 0.15, 0.15, h), 0.07, 0.02, "M_Negro_Mate", seg=12)
    p.cyl((-hw + 0.15, 0.15, h + 0.02), 0.01, 0.35, "M_Negro_Mate", seg=8)
    p.cyl((-hw + 0.15, 0.08, h + 0.33), 0.06, 0.1, "M_Negro_Mate", seg=12, r2=0.04)

def office_chair(p):
    for i in range(5):
        a = 2 * math.pi * i / 5
        p.box((-0.015, 0, 0.03), (0.015, 0.3, 0.06), "M_Negro_Mate", rot_z=a, pivot=(0, 0, 0))
        p.cyl((0.28 * math.sin(-a), 0.28 * math.cos(a), 0), 0.025, 0.03, "M_Negro_Mate", seg=8)
    p.cyl((0, 0, 0.06), 0.025, 0.36, "M_Acero_Inox", seg=10)
    p.rbox((-0.25, -0.24, 0.42), (0.25, 0.24, 0.49), "M_Tela_Sofa", 0.02)
    p.box((-0.02, 0.2, 0.45), (0.02, 0.24, 0.62), "M_Negro_Mate")
    p.rbox((-0.23, 0.2, 0.6), (0.23, 0.26, 1.0), "M_Tela_Sofa", 0.02)

def toilet(p):
    p.cyl((0, -0.05, 0), 0.14, 0.32, "M_Porcelana", seg=16, r2=0.17)
    p.sphere((0, -0.08, 0.32), 0.2, "M_Porcelana", sub=2, scale=(0.95, 1.25, 0.45))
    p.box((-0.19, -0.33, 0.39), (0.19, 0.18, 0.41), "M_Porcelana")
    p.rbox((-0.19, 0.16, 0.38), (0.19, 0.33, 0.78), "M_Porcelana", 0.02)
    p.cyl((0.1, 0.25, 0.78), 0.02, 0.01, "M_Metal_Cromo", seg=10)

def shower_tray(p, w=1.0, d=1.0):
    p.box((-w / 2, -d / 2, 0), (w / 2, d / 2, 0.05), "M_Porcelana")
    p.box((-0.06, -0.06, 0.05), (0.06, 0.06, 0.052), "M_Metal_Cromo")

def vanity(p, w=0.8, d=0.48):
    hw, hd = w / 2, d / 2
    p.box((-hw, -hd, 0.32), (hw, hd, 0.8), "M_Madera_Mueble")
    p.box((-hw + 0.02, -hd - 0.003, 0.56), (hw - 0.02, -hd + 0.001, 0.565), "M_Madera_Oscura")
    p.box((-0.1, -hd - 0.02, 0.68), (0.1, -hd, 0.69), "M_Acero_Inox")
    p.box((-hw, -hd, 0.8), (hw, hd, 0.83), "M_Encimera")
    # lavatorio de sobreponer (abierto)
    bx0, bx1, by0, by1, bz0, bz1 = -0.24, 0.24, -0.18, 0.12, 0.83, 0.97
    p.box((bx0 + 0.015, by0 + 0.015, bz0 + 0.001), (bx1 - 0.015, by1 - 0.015, bz0 + 0.015), "M_Porcelana")
    p.box((bx0, by0, bz0 + 0.001), (bx0 + 0.015, by1, bz1), "M_Porcelana")
    p.box((bx1 - 0.015, by0, bz0 + 0.001), (bx1, by1, bz1), "M_Porcelana")
    p.box((bx0 + 0.015, by0, bz0 + 0.001), (bx1 - 0.015, by0 + 0.015, bz1), "M_Porcelana")
    p.box((bx0 + 0.015, by1 - 0.015, bz0 + 0.001), (bx1 - 0.015, by1, bz1), "M_Porcelana")
    p.cyl((0, -0.03, bz0 + 0.015), 0.025, 0.002, "M_Metal_Cromo", seg=12)

def washer(p):
    p.rbox((-0.3, -0.3, 0.0), (0.3, 0.3, 0.85), "M_Plastico_Blanco", 0.015)
    p.box((-0.29, -0.302, 0.72), (0.29, -0.29, 0.84), "M_Plastico_Blanco")
    p.cyl((0.15, -0.31, 0.78), 0.03, 0.02, "M_Acero_Inox", seg=14, axis='Y')
    p.box((-0.25, -0.304, 0.76), (0.0, -0.3, 0.8), "M_Vidrio_Oscuro")
    p.cyl((0, -0.32, 0.4), 0.2, 0.03, "M_Acero_Inox", seg=24, axis='Y')
    p.cyl((0, -0.33, 0.4), 0.15, 0.01, "M_Vidrio_Oscuro", seg=24, axis='Y')

def pila(p, w=0.63, d=0.55):
    hw, hd = w / 2, d / 2
    p.box((-hw, -hd, 0), (hw, hd, 0.55), "M_Pila")
    # tina (izquierda) y lavadero estriado (derecha)
    t = 0.04
    x_mid = -0.02
    p.box((-hw + t, -hd, 0.55), (hw - t, -hd + t, 0.85), "M_Pila")
    p.box((-hw + t, hd - t, 0.55), (hw - t, hd, 0.92), "M_Pila")
    p.box((-hw, -hd, 0.55), (-hw + t, hd, 0.86), "M_Pila")
    p.box((hw - t, -hd, 0.55), (hw, hd, 0.86), "M_Pila")
    p.box((x_mid - 0.02, -hd + t, 0.55), (x_mid + 0.02, hd - t, 0.84), "M_Pila")
    p.box((-hw + t, -hd + t, 0.55), (x_mid - 0.02, hd - t, 0.58), "M_Pila")
    # lavadero inclinado
    for i in range(7):
        y = -hd + t + 0.03 + i * 0.06
        p.box((x_mid + 0.02, y, 0.7 + i * 0.012), (hw - t, y + 0.03, 0.73 + i * 0.012), "M_Pila")
    p.box((x_mid + 0.02, -hd + t, 0.6), (hw - t, hd - t, 0.7), "M_Pila")

def shelf(p, w=0.6, d=0.25, z=1.45):
    p.box((-w / 2, -d / 2, z), (w / 2, d / 2, z + 0.025), "M_Madera_Mueble")
    p.box((-0.22, -0.06, z + 0.025), (-0.12, 0.06, z + 0.27), "M_Libro_B")
    p.box((-0.08, -0.05, z + 0.025), (0.0, 0.05, z + 0.21), "M_Cojin_Acento")
    p.box((0.08, -0.06, z + 0.025), (0.24, 0.06, z + 0.12), "M_Plastico_Blanco")

def lounge_chair(p):
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.box((sx * 0.3 - 0.015, sy * 0.3 - 0.015, 0), (sx * 0.3 + 0.015, sy * 0.3 + 0.015, 0.36), "M_Madera_Oscura")
    p.rbox((-0.34, -0.36, 0.3), (0.34, 0.36, 0.38), "M_Ratan", 0.02)
    p.rbox((-0.3, -0.33, 0.38), (0.3, 0.25, 0.48), "M_Cojin_Claro", 0.03)
    p.rbox((-0.34, 0.25, 0.38), (0.34, 0.36, 0.85), "M_Ratan", 0.02)
    for sx in (-1, 1):
        p.rbox((sx * 0.34 - 0.04, -0.36, 0.38), (sx * 0.34 + 0.04, 0.3, 0.6), "M_Ratan", 0.015)

def side_table_round(p, r=0.28, h=0.5):
    p.cyl((0, 0, h - 0.03), r, 0.03, "M_Madera_Mueble", seg=24)
    p.cyl((0, 0, 0), 0.15, 0.02, "M_Negro_Mate", seg=16)
    p.cyl((0, 0, 0.02), 0.02, h - 0.05, "M_Negro_Mate", seg=8)

def bench(p, w=1.2):
    for sx in (-1, 1):
        p.box((sx * (w / 2 - 0.08) - 0.03, -0.18, 0), (sx * (w / 2 - 0.08) + 0.03, 0.18, 0.4), "M_Madera_Oscura")
    for i in range(4):
        y = -0.19 + i * 0.1
        p.box((-w / 2, y, 0.4), (w / 2, y + 0.08, 0.44), "M_Madera_Mueble")

def planter(p, w, d, h=0.45, seed=3):
    p.box((-w / 2, -d / 2, 0), (w / 2, d / 2, h), "M_Concreto")
    p.box((-w / 2 + 0.04, -d / 2 + 0.04, h - 0.04), (w / 2 - 0.04, d / 2 - 0.04, h - 0.02), "M_Tierra")
    rnd = random.Random(seed)
    n = max(2, int(w / 0.3))
    for i in range(n):
        x = -w / 2 + (i + 0.5) * w / n
        p.sphere((x, rnd.uniform(-d / 6, d / 6), h + 0.12), rnd.uniform(0.14, 0.2),
                 "M_Hoja" if i % 2 else "M_Hoja_Clara", sub=1, jitter=0.2, seed=seed + i)
        if i % 2 == 0:
            p.sphere((x + 0.05, 0, h + 0.28), 0.05, "M_Flor_Roja" if i % 4 == 0 else "M_Flor_Amarilla", sub=1)

def mirror(p, w=0.6, h=0.8, z0=1.15):
    p.box((-w / 2, -0.02, z0), (w / 2, 0.0, z0 + h), "M_Negro_Mate")
    p.box((-w / 2 + 0.015, -0.022, z0 + 0.015), (w / 2 - 0.015, -0.02, z0 + h - 0.015), "M_Espejo")

def towel_bar(p, z=1.1, w=0.6):
    p.cyl((-w / 2, -0.06, z), 0.008, 0.06, "M_Metal_Cromo", seg=8, axis='Y')
    p.cyl((w / 2, -0.06, z), 0.008, 0.06, "M_Metal_Cromo", seg=8, axis='Y')
    p.cyl((-w / 2, -0.06, z), 0.01, w, "M_Metal_Cromo", seg=8, axis='X')
    p.box((-w / 2 + 0.05, -0.08, z - 0.45), (w / 2 - 0.05, -0.05, z + 0.01), "M_Toalla")

# ---------------------------------------------------------------------------------------
# Grifería interactiva: devuelve (raíz)
# ---------------------------------------------------------------------------------------
def faucet_deck(name, base, face, coll, spout_len=0.17, basin_z=None):
    """Mezcladora de una palanca sobre cubierta. base = punto en la superficie."""
    ang = FACE[face]
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.location = Vector(base)
    b = Placer(name + "_Cuerpo", base, ang, coll)
    b.cyl((0, 0, 0), 0.028, 0.012, "M_Metal_Cromo", seg=16)
    b.cyl((0, 0, 0.012), 0.02, 0.2, "M_Metal_Cromo", seg=16)
    b.tube([(0, 0, 0.2), (0, -0.04, 0.24), (0, -spout_len, 0.24)], 0.012, "M_Metal_Cromo")
    body = b.done()
    hb = Placer(name + "_Manija", (base[0], base[1], base[2] + 0.19), ang, coll)
    hb.cyl((0, 0, -0.005), 0.016, 0.02, "M_Metal_Cromo", seg=12)
    hb.box((-0.007, 0.0, 0.01), (0.007, 0.11, 0.022), "M_Metal_Cromo")
    handle = hb.done()
    out = to_world(base, ang, (0, -spout_len, 0.225))
    e_out = add_empty(name + "_Salida", out, coll)
    bottom = (out[0], out[1], basin_z if basin_z is not None else base[2] - 0.15)
    e_bot = add_empty(name + "_Fondo", bottom, coll)
    # eje: palanca se levanta (rotación sobre eje horizontal perpendicular a la palanca)
    axis_pt = to_world((base[0], base[1], base[2] + 0.19), ang, (0.1, 0, 0))
    e_ax = add_empty(name + "_EjeManija", axis_pt, coll)
    for o in (body, handle, e_out, e_bot, e_ax):
        set_parent_keep(o, root)
    return root

def faucet_wall(name, wall_pt, face, coll, bottom_z, cross=True, out_len=0.14):
    """Tubo de pared (pila) con llave de cruz. wall_pt = punto en la superficie del muro."""
    ang = FACE[face]
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.location = Vector(wall_pt)
    b = Placer(name + "_Cuerpo", wall_pt, ang, coll)
    b.cyl((0, 0, 0), 0.03, 0.01, "M_Metal_Cromo", seg=16, axis='Y')   # roseta
    b.tube([(0, 0, 0), (0, -out_len, 0), (0, -out_len - 0.03, -0.04), (0, -out_len - 0.035, -0.07)], 0.011, "M_Metal_Cromo")
    b.cyl((0, -0.07, 0.0), 0.016, 0.05, "M_Metal_Cromo", seg=12, axis='Z')
    body = b.done()
    hpivot = to_world(wall_pt, ang, (0, -0.07, 0.06))
    hb = Placer(name + "_Manija", hpivot, ang, coll)
    hb.cyl((0, 0, -0.012), 0.01, 0.02, "M_Metal_Cromo", seg=10)
    hb.box((-0.045, -0.007, 0.0), (0.045, 0.007, 0.014), "M_Metal_Cromo")
    hb.box((-0.007, -0.045, 0.0), (0.007, 0.045, 0.014), "M_Metal_Cromo")
    hb.cyl((0, 0, 0.012), 0.012, 0.008, "M_Flor_Roja", seg=10)
    handle = hb.done()
    out = to_world(wall_pt, ang, (0, -out_len - 0.035, -0.075))
    e_out = add_empty(name + "_Salida", out, coll)
    e_bot = add_empty(name + "_Fondo", (out[0], out[1], bottom_z), coll)
    e_ax = add_empty(name + "_EjeManija", (hpivot[0], hpivot[1], hpivot[2] + 0.1), coll)  # eje vertical
    for o in (body, handle, e_out, e_bot, e_ax):
        set_parent_keep(o, root)
    return root

def shower_set(name, wall_pt, face, coll, head_z, valve_z, floor_z):
    ang = FACE[face]
    root = bpy.data.objects.new(name, None); coll.objects.link(root); root.location = Vector(wall_pt)
    b = Placer(name + "_Cuerpo", wall_pt, ang, coll)
    dz = head_z - wall_pt[2]
    b.cyl((0, 0, dz), 0.025, 0.01, "M_Metal_Cromo", seg=14, axis='Y')
    b.tube([(0, 0, dz), (0, -0.3, dz + 0.03)], 0.012, "M_Metal_Cromo")
    b.cyl((0, -0.38, dz - 0.02), 0.11, 0.025, "M_Metal_Cromo", seg=24)
    b.cyl((0, -0.38, dz - 0.022), 0.1, 0.003, "M_Negro_Mate", seg=24)
    vz = valve_z - wall_pt[2]
    b.cyl((0, 0, vz), 0.075, 0.012, "M_Metal_Cromo", seg=24, axis='Y')
    body = b.done()
    hpivot = to_world(wall_pt, ang, (0, -0.012, vz))
    hb = Placer(name + "_Manija", hpivot, ang, coll)
    hb.cyl((0, 0, 0), 0.02, 0.03, "M_Metal_Cromo", seg=12, axis='Y', r2=0.018)
    hb.box((-0.01, -0.04, -0.008), (0.1, -0.025, 0.008), "M_Metal_Cromo")
    handle = hb.done()
    out = to_world(wall_pt, ang, (0, -0.38, dz - 0.03))
    e_out = add_empty(name + "_Salida", out, coll)
    e_bot = add_empty(name + "_Fondo", (out[0], out[1], floor_z), coll)
    e_ax = add_empty(name + "_EjeManija", to_world(wall_pt, ang, (0, -0.112, vz)), coll)  # eje normal al muro
    for o in (body, handle, e_out, e_bot, e_ax):
        set_parent_keep(o, root)
    return root

# ---------------------------------------------------------------------------------------
def light_fixture(name, x, y, ceil_z, coll, pendant_to=None):
    if pendant_to is None:
        b = Builder(name)
        b.cyl((x, y, ceil_z - 0.05), 0.19, 0.05, "M_Lampara_Base", seg=24)
        b.cyl((x, y, ceil_z - 0.065), 0.165, 0.015, "M_Lampara_Difusor", seg=24)
        ob = b.build(coll, origin=(x, y, ceil_z))
        lz = ceil_z - 0.15
    else:
        b = Builder(name)
        b.cyl((x, y, ceil_z - 0.02), 0.06, 0.02, "M_Lampara_Base", seg=16)
        b.cyl((x, y, pendant_to + 0.2), 0.004, ceil_z - pendant_to - 0.2, "M_Negro_Mate", seg=6)
        b.cyl((x, y, pendant_to), 0.24, 0.2, "M_Lampara_Base", seg=24, r2=0.08, cap=False)
        b.cyl((x, y, pendant_to + 0.02), 0.12, 0.02, "M_Lampara_Difusor", seg=20)
        ob = b.build(coll, origin=(x, y, pendant_to))
        lz = pendant_to - 0.05
    return ob, (x, y, lz)

def wall_switch(group, pt, face, coll):
    ang = FACE[face]
    root = bpy.data.objects.new("Interruptor_" + group, None); coll.objects.link(root); root.location = Vector(pt)
    b = Placer("Interruptor_" + group + "_Placa", pt, ang, coll)
    b.rbox((-0.04, -0.008, -0.06), (0.04, 0.0, 0.06), "M_Interruptor", 0.003)
    plate = b.done()
    t = Placer("Interruptor_" + group + "_Tecla", to_world(pt, ang, (0, -0.008, 0)), ang, coll)
    t.box((-0.016, -0.009, -0.03), (0.016, 0.0, 0.03), "M_Interruptor")
    t.box((-0.012, -0.0095, 0.012), (0.012, -0.009, 0.02), "M_Negro_Mate")
    key = t.done()
    front = add_empty("Interruptor_" + group + "_Frente", to_world(pt, ang, (0, -0.1, 0)), coll)
    for o in (plate, key, front):
        set_parent_keep(o, root)
    INFO["markers"].append(dict(p=tuple(pt), c="m", m="s", label="Int " + group))
    return root

# ---------------------------------------------------------------------------------------
def roof_bottom(x, y, z_from, bvh):
    hit = bvh.ray_cast(Vector((x, y, z_from)), Vector((0, 0, 1)), 10.0)
    return hit[0].z if hit[0] is not None else z_from + 2.5

def run(M, arch):
    mk_mats()
    floors(arch)
    C = {f: get_coll(f"Muebles_P{f}") for f in (1, 2)}
    CL = get_coll("Iluminacion")
    CI = get_coll("Interactivos")
    CE = get_coll("Exterior")
    CK = get_coll("Marcadores")
    CC = get_coll("Colisiones")
    # BVH del techo (cubierta) y de lo construido para alturas
    roof = bpy.data.objects["Cubierta"]
    bm = bmesh.new(); bm.from_mesh(roof.data); bm.transform(roof.matrix_world)
    roof_bvh = BVHTree.FromBMesh(bm); bm.free()

    lights = []  # (group, name, pos, range)
    for f in (1, 2):
        z = 0.0 if f == 1 else 3.0
        zf = z + 0.004            # superficie del acabado de piso
        c = C[f]; P = f"P{f}"
        # ---------------- Sala / comedor / cocina
        # Distribución con pasillos libres >= 0.6 m alrededor de las mesas (jugador: cápsula de 0.40 m)
        s = Placer(f"Mueble_{P}_Sofa", (-1.3658 - 0.435, -1.35, zf), "W", c); sofa(s, 1.8, 0.85); s.done()
        s = Placer(f"Mueble_{P}_MesaCentro", (-3.175, -0.85, zf + 0.012), "W", c); coffee_table(s, 0.9, 0.45); s.done()
        s = Placer(f"Mueble_{P}_Alfombra", (-3.15, -0.9, zf), "W", c); rug(s, 2.0, 1.5); s.done()
        s = Placer(f"Mueble_{P}_MuebleTV", (-4.5158 + 0.2, -0.7, zf), "E", c); media_unit(s, 1.3, 0.4, 0.5); s.done()
        s = Placer(f"Mueble_{P}_TV_Sala", (-4.5158, -0.7, zf), "E", c); tv(s, 1.23, 0.71, 0.85); s.done()
        s = Placer(f"Mueble_{P}_LamparaPie", (-4.32, 0.2, zf), "E", c); floor_lamp(s); s.done()
        s = Placer(f"Mueble_{P}_Comedor", (-2.71, 1.105, zf), "S", c); dining_table(s, 1.1, 0.75); s.done()
        for i, (cx, cy, fc) in enumerate(((-2.985, 0.59, "N"), (-2.435, 0.59, "N"), (-2.985, 1.62, "S"), (-2.435, 1.62, "S"))):
            s = Placer(f"Mueble_{P}_Silla_{i + 1}", (cx, cy, zf), fc, c); chair(s); s.done()
        # cocina en L: pared oeste (x=-4.5158) y norte (y=4.7407); refrigeradora al inicio de la pared oeste
        s = Placer(f"Mueble_{P}_Estufa", (-4.5158 + 0.3, 3.30, zf), "E", c)
        base_cabinets(s, 0.6, 0.6, 0.9, cook_x=0.0); s.done()
        s = Placer(f"Mueble_{P}_Cocina_Oeste_2", (-4.5158 + 0.3, (3.60 + 4.1407) / 2, zf), "E", c)
        base_cabinets(s, 4.1407 - 3.60, 0.6, 0.9); s.done()
        s = Placer(f"Mueble_{P}_Cocina_Esquina", (-4.5158 + 0.3, 4.1407 + 0.3, zf), "E", c)
        base_cabinets(s, 0.6, 0.6, 0.9); s.done()
        s = Placer(f"Mueble_{P}_Cocina_Norte", ((-3.9158 + -2.98) / 2, 4.7407 - 0.3, zf), "S", c)
        sink_cx = -3.45; Ln = -2.98 - -3.9158; ncx = (-3.9158 + -2.98) / 2
        base_cabinets(s, Ln, 0.6, 0.9, sink_x=sink_cx - ncx, sink_w=0.5); s.done()
        s = Placer(f"Mueble_{P}_Alacenas_2", (-4.5158 + 0.175, (3.6 + 4.5) / 2, zf), "E", c)
        upper_cabinets(s, 4.5 - 3.6); s.done()
        s = Placer(f"Mueble_{P}_Campana", (-4.5158 + 0.25, 3.3, zf), "E", c); range_hood(s, 1.55); s.done()
        s = Placer(f"Mueble_{P}_Refrigeradora", (-4.5158 + 0.36, 2.65, zf), "E", c); fridge(s); s.done()
        bs = Builder(f"Mueble_{P}_Salpicadero")
        bs.box((-4.5158, 3.00, zf + 0.9), (-4.5108, 4.7407, zf + 1.45), "M_Azulejo_Cocina")
        bs.build(c)
        faucet_deck(f"Grifo_{P}_Fregadero", (sink_cx, 4.7407 - 0.06, zf + 0.9), "S", CI, spout_len=0.2,
                    basin_z=zf + 0.71)
        # ---------------- Dormitorio
        s = Placer(f"Mueble_{P}_Cama", (2.4842 - 1.07, -0.75, zf), "W", c)
        bed(s, 1.5, 2.0); s.done()   # cabecera (local +Y) contra el muro este
        s = Placer(f"Mueble_{P}_MesaNoche_1", (2.4842 - 0.25, -1.78, zf), "W", c); nightstand(s); s.done()
        s = Placer(f"Mueble_{P}_MesaNoche_2", (2.4842 - 0.25, 0.28, zf), "W", c); nightstand(s); s.done()
        s = Placer(f"Mueble_{P}_Closet", (0.8, 1.3907 - 0.3, zf), "S", c); wardrobe(s, 1.4, 0.6, 2.1); s.done()
        s = Placer(f"Mueble_{P}_Escritorio", (-0.65, -2.3593 + 0.3, zf), "N", c); desk(s, 1.0, 0.6); s.done()
        s = Placer(f"Mueble_{P}_SillaEscritorio", (-0.65, -1.5, zf), "S", c); office_chair(s); s.done()
        s = Placer(f"Mueble_{P}_MuebleTV_Dorm", (-1.2158 + 0.2, -0.45, zf), "E", c); media_unit(s, 1.2, 0.4, 0.45); s.done()
        s = Placer(f"Mueble_{P}_TV_Dormitorio", (-1.2158, -0.45, zf), "E", c); tv(s, 0.97, 0.56, 1.0); s.done()
        s = Placer(f"Mueble_{P}_Alfombra_Dorm", (0.0, -0.75, zf), "W", c); rug(s, 1.6, 0.8, "M_Cojin_Claro"); s.done()
        # ---------------- Baño
        s = Placer(f"Mueble_{P}_Ducha", (2.4842 - 0.5, 1.5407 + 0.5, zf), "S", c); shower_tray(s); s.done()
        g = Builder(f"Mueble_{P}_Mampara")
        gx = 2.4842 - 1.0
        g.box((gx - 0.005, 1.5407, zf + 0.05), (gx + 0.005, 1.5407 + 1.0, zf + 2.0), "M_Vidrio")
        g.box((gx, 1.5407 + 1.0 - 0.005, zf + 0.05), (gx + 0.38, 1.5407 + 1.0 + 0.005, zf + 2.0), "M_Vidrio")
        g.box((gx - 0.012, 1.5407, zf + 2.0), (gx + 0.012, 1.5407 + 1.0, zf + 2.02), "M_Metal_Cromo")
        g.box((gx, 1.5407 + 1.0 - 0.012, zf + 2.0), (gx + 0.38, 1.5407 + 1.0 + 0.012, zf + 2.02), "M_Metal_Cromo")
        g.box((gx + 0.37, 1.5407 + 1.0 - 0.012, zf + 0.05), (gx + 0.39, 1.5407 + 1.0 + 0.012, zf + 2.0), "M_Metal_Cromo")
        g.build(c)
        shower_set(f"Grifo_{P}_Ducha", (2.4842 - 0.32, 1.5407, zf), "N", CI, head_z=zf + 2.1, valve_z=zf + 1.1,
                   floor_z=zf + 0.05)
        s = Placer(f"Mueble_{P}_Inodoro", (2.4842 - 0.33, 3.35, zf), "W", c); toilet(s); s.done()
        s = Placer(f"Mueble_{P}_Lavatorio", (-0.2158 + 0.24, 3.85, zf), "E", c); vanity(s); s.done()
        s = Placer(f"Mueble_{P}_Espejo", (-0.2158, 3.85, zf), "E", c); mirror(s, 0.6, 0.8, 1.15); s.done()
        faucet_deck(f"Grifo_{P}_Lavatorio", (-0.2158 + 0.08, 3.85, zf + 0.83), "E", CI, spout_len=0.13,
                    basin_z=zf + 0.845)
        s = Placer(f"Mueble_{P}_Toallero", (2.4842, 2.85, zf), "W", c); towel_bar(s, 1.1, 0.5); s.done()
        s = Placer(f"Mueble_{P}_Tapete", (0.6, 2.85, zf), "S", c); rug(s, 0.6, 0.4, "M_Toalla"); s.done()
        s = Placer(f"Mueble_{P}_Planta_Bano", (2.25, 4.45, zf), "S", c); plant(s, 0.14, 0.3, 0.4, seed=f * 11); s.done()
        # ---------------- Lavandería
        s = Placer(f"Mueble_{P}_Lavadora", (-1.6658 + 0.31, 4.7407 - 0.31, zf), "S", c); washer(s); s.done()
        s = Placer(f"Mueble_{P}_Pila", (-0.3658 - 0.32, 4.7407 - 0.28, zf), "S", c); pila(s); s.done()
        faucet_wall(f"Grifo_{P}_Pila", (-0.3658 - 0.42, 4.7407, zf + 1.12), "S", CI, bottom_z=zf + 0.6)
        s = Placer(f"Mueble_{P}_Repisa_Lav", (-1.6658 + 0.31, 4.7407 - 0.125, zf), "S", c); shelf(s, 0.6, 0.25, 1.35); s.done()
        s = Placer(f"Mueble_{P}_Canasto", (-1.45, 3.85, zf), "S", c)
        s.cyl((0, 0, 0), 0.2, 0.45, "M_Ratan", seg=16, r2=0.22, cap=True); s.done()
        # ---------------- Patio / balcón
        if f == 1:
            s = Placer("Mueble_P1_Jardinera_1", (-4.5158 + 0.25, 5.69, zf), "E", c); planter(s, 1.4, 0.4, 0.45, seed=21); s.done()
            s = Placer("Mueble_P1_Banca", (-1.8158 - 0.2, 5.8, zf), "W", c); bench(s, 1.0); s.done()
            s = Placer("Mueble_P1_ArbolMaceta", (-3.55, 6.2, zf), "S", c); plant(s, 0.22, 0.5, 0.6, seed=5); s.done()
        else:
            s = Placer("Mueble_P2_Sillon_1", (-4.1, 5.98, zf), "N", c); lounge_chair(s); s.done()
            s = Placer("Mueble_P2_Sillon_2", (-2.8, 5.98, zf), "N", c); lounge_chair(s); s.done()
            s = Placer("Mueble_P2_MesaBalcon", (-3.45, 6.1, zf), "S", c); side_table_round(s, 0.24); s.done()
            s = Placer("Mueble_P2_Planta_Balcon_1", (-4.25, 5.12, zf), "S", c); plant(s, 0.18, 0.4, 0.45, seed=31); s.done()
            s = Placer("Mueble_P2_Alfombra_Balcon", (-3.45, 5.75, zf), "S", c); rug(s, 2.0, 0.9, "M_Ratan"); s.done()
        # ---------------- Iluminación de techo
        def ceil(x, y):
            return (2.88 if f == 1 else roof_bottom(x, y, 3.2, roof_bvh))
        fix = {
            f"{P}_SalaCocina": [(-3.1, -0.85), (-2.71, 1.105), (-3.0, 3.4), (-0.87, 1.99)],
            f"{P}_Dormitorio": [(0.63, -0.45)],
            f"{P}_Bano": [(1.0, 3.2)],
            f"{P}_Lavanderia": [(-1.02, 3.6)],
        }
        fix["P1_Patio" if f == 1 else "P2_Balcon"] = [(-3.17, 5.69)]
        for grp, pts in fix.items():
            for i, (x, y) in enumerate(pts):
                cz = ceil(x, y)
                pend = None if (f == 1 or cz - z < 2.9) else z + 2.45
                ob, lp = light_fixture(f"Lampara_{grp}_{i + 1}", x, y, cz, CL, pendant_to=pend)
                e = add_empty(f"Luz_{grp}_{i + 1}", lp, CL)
                INFO["markers"].append(dict(p=lp, c="gold", m="*", label=""))
        # ---------------- Interruptores (cerca de la entrada, lado de la manija)
        sw = 1.2 + z
        wall_switch(f"{P}_SalaCocina", (-3.36, -2.3593, sw), "N", CI)
        wall_switch(f"{P}_Dormitorio", (-0.12, 1.3907, sw), "S", CI)
        wall_switch(f"{P}_Bano", (-0.2158, 2.66, sw), "E", CI)
        wall_switch(f"{P}_Lavanderia", (-1.37, 2.5907, sw), "N", CI)
        wall_switch("P1_Patio" if f == 1 else "P2_Balcon", (-2.985, 4.8907, sw), "N", CI)
    # ---------------- Áreas comunes: porche, escalera, pasillo P2
    for grp, (x, y, zc) in (("P1_Porche", (-3.85, -3.35, 2.76)), ("Escalera", (-5.25, -3.3, 2.76)),
                            ("P2_Pasillo", (-2.6, -3.2, None)), ("P2_Pasillo", (0.9, -3.2, None))):
        if zc is None:
            zc = min(roof_bottom(x, y, 3.2, roof_bvh), 5.9)
        n = 1 + sum(1 for k in bpy.data.objects if k.name.startswith(f"Lampara_{grp}_"))
        ob, lp = light_fixture(f"Lampara_{grp}_{n}", x, y, zc, CL)
        add_empty(f"Luz_{grp}_{n}", lp, CL)
    zc = roof_bottom(-6.35, 0.6, 3.2, roof_bvh)
    ob, lp = light_fixture("Lampara_Escalera_2", -6.35, 0.6, zc, CL, pendant_to=4.4)
    add_empty("Luz_Escalera_2", lp, CL)
    wall_switch("P1_Porche", (-3.36, -2.5093, 1.2), "S", CI)
    wall_switch("Escalera", (-4.6658, -3.925, 1.2), "W", CI)
    wall_switch("P2_Pasillo", (-3.36, -2.5093, 4.2), "S", CI)
    # ---------------- Marcadores de teletransporte
    spawns = {
        "P1_Sala": ((-3.7, -1.7, 0), (-2.4, 1.5, 0)),
        "P1_Cocina": ((-2.55, 3.1, 0), (-4.2, 3.6, 0)),
        "P1_Dormitorio": ((-0.35, 0.4, 0), (1.5, -0.8, 0)),
        "P1_Bano": ((0.45, 2.3, 0), (1.9, 3.8, 0)),
        "P1_Lavanderia": ((-1.0, 3.05, 0), (-1.0, 4.7, 0)),
        "P1_Patio": ((-2.6, 5.45, 0), (-3.2, 9.0, 0)),
        "P2_Sala": ((-3.7, -1.7, 3), (-2.4, 1.5, 3)),
        "P2_Cocina": ((-2.55, 3.1, 3), (-4.2, 3.6, 3)),
        "P2_Dormitorio": ((-0.35, 0.4, 3), (1.5, -0.8, 3)),
        "P2_Bano": ((0.45, 2.3, 3), (1.9, 3.8, 3)),
        "P2_Lavanderia": ((-1.0, 3.05, 3), (-1.0, 4.7, 3)),
        "P2_Balcon": ((-2.6, 5.3, 3), (-3.2, 8.0, 3)),
        "P2_Pasillo": ((1.2, -3.2, 3), (-4.0, -3.2, 3)),
        "Ext_Entrada": ((-3.8, -3.6, 0), (-3.8, -1.0, 0)),
        "Ext_Escalera": ((-6.3, -3.35, 0), (-6.3, -1.0, 0)),
        "Ext_JardinFrontal": ((-1.0, -9.5, -0.02), (-1.0, 0.0, 1.5)),
        "Ext_JardinTrasero": ((-2.5, 10.0, -0.02), (-2.5, 3.0, 1.5)),
    }
    for k, (p, t) in spawns.items():
        e = add_empty("Spawn_" + k, p, CK)
        e2 = add_empty("Spawn_" + k + "_Mira", t, CK)
        set_parent_keep(e2, e)
        INFO["markers"].append(dict(p=p, c="blue", m="^", label=k))
    exterior(CE, CC)
    colliders(CC)

# ---------------------------------------------------------------------------------------
def tree(name, x, y, coll, h=4.5, r=1.6, seed=0):
    rnd = random.Random(seed)
    b = Builder(name + "_Copa")
    for i in range(6):
        a = rnd.uniform(0, 6.28); rr = rnd.uniform(0, r * 0.45)
        b.sphere((x + math.cos(a) * rr, y + math.sin(a) * rr, h - r * 0.5 + rnd.uniform(-0.5, 0.6)),
                 r * rnd.uniform(0.45, 0.7), "M_Hoja" if i % 2 else "M_Hoja_Clara", sub=1, jitter=0.2, seed=seed + i)
    crown = b.build(coll, origin=(x, y, 0))
    t = Builder(name + "_Tronco")
    t.cyl((x, y, -0.05), 0.16, h - r * 0.6, "M_Corteza", seg=8, r2=0.09)
    t.tube([(x, y, h * 0.45), (x + 0.5, y + 0.3, h * 0.65)], 0.06, "M_Corteza", seg=6)
    trunk = t.build(coll, origin=(x, y, 0))
    return crown, trunk

def exterior(CE, CC):
    gz = -0.02
    b = Builder("Terreno")
    b.box((-26, -24, gz - 0.3), (22, 26, gz), "M_Cesped")
    b.build(CE)
    # caminos
    b = Builder("Camino_Entrada")
    b.box((-4.45, -15.5, gz), (-3.25, -4.32, gz + 0.012), "M_Piso_Exterior")
    b.box((-3.12, -3.85, gz), (2.63, -2.52, gz + 0.012), "M_Piso_Exterior")
    b.build(CE)
    # cerco vivo perimetral
    b = Builder("Seto_Perimetral")
    X0, X1, Y0, Y1, th, hh = -17.0, 13.0, -15.5, 17.0, 0.6, 1.3
    b.box((X0, Y0, gz), (-4.6, Y0 + th, hh), "M_Seto")
    b.box((-3.1, Y0, gz), (X1, Y0 + th, hh), "M_Seto")
    b.box((X0, Y1 - th, gz), (X1, Y1, hh), "M_Seto")
    b.box((X0, Y0, gz), (X0 + th, Y1, hh), "M_Seto")
    b.box((X1 - th, Y0, gz), (X1, Y1, hh), "M_Seto")
    b.build(CE)
    for nm, mn, mx in (("COL_Limite_Sur", (X0, Y0 - 0.3, gz), (X1, Y0 + th, 3.0)),
                       ("COL_Limite_Norte", (X0, Y1 - th, gz), (X1, Y1 + 0.3, 3.0)),
                       ("COL_Limite_Oeste", (X0 - 0.3, Y0, gz), (X0 + th, Y1, 3.0)),
                       ("COL_Limite_Este", (X1 - th, Y0, gz), (X1 + 0.3, Y1, 3.0))):
        c = Builder(nm); c.box(mn, mx, "M_Concreto"); c.build(CC)
    trees = [(-7.0, 9.8, 5.2, 1.9), (-0.5, 11.0, 4.6, 1.7), (4.5, 9.0, 5.0, 1.8), (7.5, 2.5, 4.4, 1.6),
             (7.8, -4.0, 5.0, 1.8), (-9.5, -9.0, 4.8, 1.7), (1.0, -10.5, 4.2, 1.5), (6.0, -10.5, 5.2, 1.9),
             (-12.0, -1.0, 5.5, 2.0), (-11.0, 7.0, 4.6, 1.7), (-13.5, 12.5, 5.0, 1.8), (9.5, 13.0, 4.8, 1.7)]
    for i, (x, y, h, r) in enumerate(trees):
        tree(f"Arbol_{i + 1}", x, y, CE, h, r, seed=100 + i)
    # arbustos y jardineras
    rnd = random.Random(7)
    b = Builder("Arbustos")
    spots = [(3.2, -1.5), (3.2, 0.8), (3.2, 3.2), (0.0, 5.5), (1.8, 5.5), (-7.8, -3.5), (-7.8, 0.5),
             (-6.0, -4.9), (-2.5, -4.9), (-0.5, -4.9), (1.5, -4.9), (-5.2, -6.5), (-2.5, -6.5),
             (-5.4, 6.0), (-5.4, 4.2), (-1.0, 7.2), (-5.8, 7.5)]
    for i, (x, y) in enumerate(spots):
        b.sphere((x, y, 0.3), rnd.uniform(0.35, 0.55), "M_Seto" if i % 3 else "M_Hoja_Clara", sub=1,
                 scale=(1, 1, 0.8), jitter=0.2, seed=200 + i)
        if i % 2 == 0:
            for k in range(3):
                b.sphere((x + rnd.uniform(-0.4, 0.4), y + rnd.uniform(-0.4, 0.4), 0.62), 0.06,
                         "M_Flor_Roja" if k % 2 else "M_Flor_Amarilla", sub=1)
    b.build(CE)

def ramp(name, x0, x1, y_lo, z_lo, y_hi, z_hi, coll, th=0.12):
    """Rampa invisible (colisión) desde (y_lo,z_lo) hasta (y_hi,z_hi)."""
    b = Builder(name)
    v = [(x0, y_lo, z_lo), (x1, y_lo, z_lo), (x1, y_hi, z_hi), (x0, y_hi, z_hi)]
    vb = [(p[0], p[1], p[2] - th) for p in v]
    b._finish(v + vb, [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)],
              "M_Concreto")
    bmesh.ops.recalc_face_normals(b.bm, faces=b.bm.faces[:])
    return b.build(coll)

def colliders(CC):
    # rampas: pasan por el centro de cada huella (pendiente 1/6 : 0.3)
    k = 0.16667 / 0.3
    # tramo 1 (sube hacia +y): huella 1 centro y=-2.239, z=0.1667
    ramp("COL_Rampa_Tramo1", -6.891, -5.791, -2.239 - 0.1667 / k, 0.0, -2.239 + (1.5 - 0.1667) / k, 1.5, CC)
    # tramo 2 (sube hacia -y): huella 1 centro y=0.371, z=1.667
    ramp("COL_Rampa_Tramo2", -5.766, -4.666, 0.371 + (1.667 - 1.5) / k, 1.5, 0.371 - (3.0 - 1.667) / k, 3.0, CC)
    # barandas (colisión)
    for nm, mn, mx in (("COL_Baranda_Central", (-5.795, -2.55, 0.0), (-5.765, 0.0, 4.2)),
                       ("COL_Baranda_Pasillo", (-5.795, -4.01, 2.9), (-5.765, -2.55, 4.1))):
        c = Builder(nm); c.box(mn, mx, "M_Concreto"); c.build(CC)
    # baranda visual (metal negro)
    b = Builder("Baranda_Escalera")
    rails = [((-5.8, -2.45, 1.06), (-5.8, 0.0, 2.42)),            # tramo 1
             ((-5.755, 0.57, 2.56), (-5.755, -1.88, 3.93)),       # tramo 2
             ((-5.755, -1.88, 3.95), (-5.755, -4.0, 3.95))]       # descanso P2
    for a, c in rails:
        b.tube([a, c], 0.022, "M_Aluminio", seg=10)
        a = Vector(a); c = Vector(c)
        L = (c - a).length; n = max(2, int(L / 0.13))
        for i in range(n + 1):
            p = a.lerp(c, i / n)
            # base del balaustre sobre la huella / descanso
            if abs(a.z - c.z) < 1e-3:
                zb = 3.0
            else:
                zb = p.z - 0.9
            b.cyl((p.x, p.y, zb), 0.009, p.z - zb, "M_Aluminio", seg=6)
    b.build(bpy.data.collections["Escalera"])

FLOOR_MAT = {"sala": "M_Piso_Madera", "cocina": "M_Piso_Porcelanato", "dormitorio": "M_Piso_Madera",
             "bano": "M_Piso_Ceramica", "lavanderia": "M_Piso_Ceramica", "patio": "M_Piso_Exterior",
             "porche": "M_Piso_Exterior", "escalera": "M_Piso_Exterior", "balcon": "M_Piso_Deck"}

def floors(arch):
    CF = get_coll("Pisos")
    for r in arch.ROOMS:
        if r["kind"] not in FLOOR_MAT:
            continue
        b = Builder("Piso_" + r["id"])
        z = r["z0"] + 0.004
        for (x0, y0, x1, y1) in r["rects"]:
            b.quad([(x0, y0, z), (x1, y0, z), (x1, y1, z), (x0, y1, z)], FLOOR_MAT[r["kind"]])
        b.build(CF)
    for d in arch.INFO["doors"]:
        w, t = d["w_ax"], d["t_ax"]
        lmn, lmx = d["leaf_min"], d["leaf_max"]
        fmn, fmx = d["frame_min"], d["frame_max"]
        tc = (fmn[t] + fmx[t]) / 2
        z = (0.0 if d["hinge"][2] < 1.5 else 3.0) + 0.005
        mn = [0, 0]; mx = [0, 0]
        mn[w] = lmn[w] - 0.01; mx[w] = lmx[w] + 0.01
        mn[t] = tc - 0.075; mx[t] = tc + 0.075
        b = Builder("Umbral_" + d["name"].replace("Puerta_", ""))
        b.box((mn[0], mn[1], z - 0.004), (mx[0], mx[1], z), "M_Umbral")
        b.build(CF)

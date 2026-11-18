# Primitivas de modelado (bmesh, coordenadas de mundo) para muebles y exteriores
import bpy, bmesh, math, random
from mathutils import Vector, Matrix
from common import mat, world_uv, get_coll

class Builder:
    """Acumula geometría (multi-material) y la convierte en un objeto."""
    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.mats = []

    def mi(self, m):
        if isinstance(m, str):
            m = bpy.data.materials[m]
        if m not in self.mats:
            self.mats.append(m)
        return self.mats.index(m)

    def _finish(self, verts, faces, m, smooth=False):
        idx = self.mi(m)
        vs = [self.bm.verts.new(v) for v in verts]
        out = []
        for f in faces:
            try:
                nf = self.bm.faces.new([vs[i] for i in f])
                nf.material_index = idx
                nf.smooth = smooth
                out.append(nf)
            except ValueError:
                pass
        return out

    def box(self, mn, mx, m, rot_z=0.0, pivot=None):
        x0, x1 = sorted((mn[0], mx[0])); y0, y1 = sorted((mn[1], mx[1])); z0, z1 = sorted((mn[2], mx[2]))
        v = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
             (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        if rot_z:
            p = Vector(pivot if pivot else ((x0 + x1) / 2, (y0 + y1) / 2, 0))
            R = Matrix.Rotation(rot_z, 3, 'Z')
            v = [tuple(R @ (Vector(q) - p) + p) for q in v]
        f = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]
        return self._finish(v, f, m)

    def bbox(self, c, size, m):
        c = Vector(c); s = Vector(size) / 2
        return self.box(tuple(c - s), tuple(c + s), m)

    def rbox(self, mn, mx, m, r=0.02, seg=2):
        """Caja con bordes biselados."""
        faces = self.box(mn, mx, m)
        idx = self.mi(m)
        edges = set(e for f in faces for e in f.edges)
        try:
            bmesh.ops.bevel(self.bm, geom=list(edges), offset=r, segments=seg, profile=0.5, affect='EDGES',
                            clamp_overlap=True, material=idx)
        except Exception:
            pass
        return faces

    def cyl(self, c, r, h, m, seg=16, axis='Z', r2=None, cap=True, smooth=True):
        c = Vector(c); r2 = r if r2 is None else r2
        verts = []
        for k in (0, 1):
            rr = r if k == 0 else r2
            for i in range(seg):
                a = 2 * math.pi * i / seg
                p = Vector((math.cos(a) * rr, math.sin(a) * rr, h * k))
                verts.append(p)
        if axis == 'X':
            R = Matrix.Rotation(math.pi / 2, 3, 'Y')
        elif axis == 'Y':
            R = Matrix.Rotation(-math.pi / 2, 3, 'X')
        else:
            R = Matrix.Identity(3)
        verts = [tuple(R @ p + c) for p in verts]
        faces = [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
        if cap:
            faces.append(tuple(reversed(range(seg))))
            faces.append(tuple(range(seg, 2 * seg)))
        return self._finish(verts, faces, m, smooth=smooth)

    def sphere(self, c, r, m, sub=2, scale=(1, 1, 1), jitter=0.0, seed=0, smooth=True):
        tmp = bmesh.new()
        bmesh.ops.create_icosphere(tmp, subdivisions=sub, radius=1.0)
        rnd = random.Random(seed)
        verts = []
        for v in tmp.verts:
            k = 1.0 + (rnd.uniform(-jitter, jitter) if jitter else 0.0)
            verts.append((c[0] + v.co.x * r * scale[0] * k, c[1] + v.co.y * r * scale[1] * k,
                          c[2] + v.co.z * r * scale[2] * k))
        faces = [tuple(v.index for v in f.verts) for f in tmp.faces]
        tmp.free()
        return self._finish(verts, faces, m, smooth=smooth)

    def quad(self, pts, m):
        return self._finish(pts, [(0, 1, 2, 3)], m)

    def tube(self, pts, r, m, seg=8):
        """Tubo a lo largo de una polilínea."""
        for a, b in zip(pts[:-1], pts[1:]):
            a = Vector(a); b = Vector(b); d = b - a; L = d.length
            if L < 1e-6:
                continue
            q = d.to_track_quat('Z', 'Y')
            M = q.to_matrix()
            verts = []
            for k in (0, 1):
                for i in range(seg):
                    t = 2 * math.pi * i / seg
                    p = Vector((math.cos(t) * r, math.sin(t) * r, L * k))
                    verts.append(tuple(M @ p + a))
            faces = [(i, (i + 1) % seg, seg + (i + 1) % seg, seg + i) for i in range(seg)]
            faces.append(tuple(reversed(range(seg)))); faces.append(tuple(range(seg, 2 * seg)))
            self._finish(verts, faces, m, smooth=True)

    def build(self, coll, origin=None, uv=True):
        if uv:
            world_uv(self.bm)
        me = bpy.data.meshes.new(self.name)
        if origin is not None:
            self.bm.transform(Matrix.Translation(-Vector(origin)))
        self.bm.normal_update()
        self.bm.to_mesh(me)
        self.bm.free()
        for m in self.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(self.name, me)
        if origin is not None:
            ob.location = Vector(origin)
        coll.objects.link(ob)
        return ob

def rot_pts(pts, ang, pivot):
    R = Matrix.Rotation(ang, 3, 'Z'); p = Vector(pivot)
    return [tuple(R @ (Vector(q) - p) + p) for q in pts]

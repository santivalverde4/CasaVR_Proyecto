# Genera texturas procedurales "tileables" para el proyecto (PNG).
# Cada textura representa un tamaño real (metros) indicado en TEX_SIZES (para el tiling en Unity).
import numpy as np, os, sys, json
from PIL import Image
from scipy.ndimage import gaussian_filter

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(42)

def tile_noise(n, sigma, seed):
    r = np.random.default_rng(seed).random((n, n))
    g = gaussian_filter(r, sigma, mode='wrap')
    g = (g - g.min()) / (g.max() - g.min() + 1e-9)
    return g

def fbm(n, seed, octaves=5, base=32):
    acc = np.zeros((n, n)); amp = 1.0; tot = 0
    for o in range(octaves):
        acc += amp * tile_noise(n, max(0.6, n / base / (2 ** o)), seed + o)
        tot += amp; amp *= 0.5
    return acc / tot

def save(name, rgb, alpha=None):
    rgb = np.clip(rgb, 0, 1)
    arr = (rgb * 255).astype(np.uint8)
    if alpha is not None:
        a = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
        img = Image.fromarray(np.dstack([arr, a]), "RGBA")
    else:
        img = Image.fromarray(arr, "RGB")
    img.save(os.path.join(OUT, name + ".png"), optimize=True)

def col(c):
    return np.array(c, dtype=float).reshape(1, 1, 3)

TEX_SIZES = {}

# ---------------- madera de piso: 6 hileras de tablones de 0.18 m en 1.08 m
def wood_floor(n=1024):
    size = 1.08
    rows = 6
    img = np.zeros((n, n, 3))
    yy, xx = np.mgrid[0:n, 0:n] / n
    grain_base = fbm(n, 3, 4, 64)
    stretch = tile_noise(n, 1.2, 9)
    for r in range(rows):
        y0, y1 = r / rows, (r + 1) / rows
        mask = (yy >= y0) & (yy < y1)
        off = (r * 0.37) % 1.0
        seg = ((xx + off) % 1.0)
        plank = np.floor(((xx + off) % 1.0) * 1.0 / 0.5).astype(int)  # 2 tablones por fila (0.54 m)
        tone = 0.85 + 0.25 * np.random.default_rng(100 + r).random(3)[plank % 3]
        # vetas: bandas estiradas en x
        v = np.sin((yy * n * 0.9 + grain_base * 18 + stretch * 6) * 0.9) * 0.5 + 0.5
        base = col((0.60, 0.42, 0.27)) * (0.82 + 0.18 * v[..., None]) * tone[..., None]
        img[mask] = base[mask]
        # juntas
        jy = (np.abs(yy - y0) < 1.5 / n)
        img[jy] *= 0.55
        jx = mask & ((np.abs(seg - 0.0) < 1.5 / n) | (np.abs(seg - 0.5) < 1.5 / n))
        img[jx] *= 0.55
    img *= (0.94 + 0.06 * fbm(n, 77, 4, 16))[..., None]
    save("T_Madera_Piso", img); TEX_SIZES["T_Madera_Piso"] = size

def tiles(name, n, ntiles, size, c1, c2, grout, gw=2.0, veins=0.0, var=0.04, seed=1, offset_rows=False, aspect=1):
    yy, xx = np.mgrid[0:n, 0:n] / n
    tx = xx * ntiles
    ty = yy * ntiles * aspect
    if offset_rows:
        tx = tx + 0.5 * (np.floor(ty) % 2)
    fx = tx % 1.0; fy = ty % 1.0
    idx = (np.floor(tx) + 37 * np.floor(ty)).astype(int)
    r = np.random.default_rng(seed).random(4096)
    t = r[idx % 4096]
    base = col(c1) * (1 - t[..., None]) + col(c2) * t[..., None]
    base *= (1 - var + var * 2 * fbm(n, seed + 5, 4, 24))[..., None]
    if veins > 0:
        vn = fbm(n, seed + 9, 5, 6)
        vn2 = fbm(n, seed + 19, 5, 24)
        vv = np.exp(-np.abs(np.sin(vn * 7.0 + vn2 * 4.0)) * 22) * (0.4 + 0.6 * vn2)
        base = base * (1 - veins * vv[..., None]) + veins * vv[..., None] * col((0.55, 0.53, 0.5))
    gpx = gw / n * ntiles
    g = (fx < gpx) | (fy < gpx * aspect)
    base[g] = col(grout)[0, 0]
    save(name, base); TEX_SIZES[name] = size

def plaster(n=512):
    g = fbm(n, 11, 5, 16)
    img = np.ones((n, n, 3)) * (0.93 + 0.07 * g)[..., None]
    save("T_Estuco", img); TEX_SIZES["T_Estuco"] = 2.0

def concrete(n=512):
    g = fbm(n, 21, 6, 8)
    s = (np.random.default_rng(3).random((n, n)) > 0.997).astype(float)
    s = gaussian_filter(s, 0.7, mode='wrap') * 3
    img = col((0.62, 0.61, 0.59)) * (0.85 + 0.2 * g[..., None]) - 0.15 * s[..., None]
    save("T_Concreto", img); TEX_SIZES["T_Concreto"] = 2.0

def pavers(n=1024):
    tiles("T_Pavimento", n, 4, 1.6, (0.62, 0.58, 0.52), (0.52, 0.49, 0.45), (0.35, 0.33, 0.30), gw=5, var=0.12,
          seed=31, offset_rows=True)

def grass(n=1024):
    g1 = fbm(n, 41, 6, 6)
    g2 = tile_noise(n, 0.8, 42)
    img = col((0.20, 0.38, 0.12)) * (0.7 + 0.5 * g1[..., None]) + col((0.10, 0.12, 0.02)) * (g2[..., None] - 0.5)
    img = img * (0.9 + 0.2 * (g2 > 0.8)[..., None])
    save("T_Cesped", img); TEX_SIZES["T_Cesped"] = 4.0

def furniture_wood(n=512):
    yy, xx = np.mgrid[0:n, 0:n] / n
    g = fbm(n, 51, 4, 32)
    g2 = fbm(n, 52, 3, 8)
    v = np.sin((yy * 90 + g * 6 + g2 * 3)) * 0.5 + 0.5
    img = col((0.66, 0.50, 0.33)) * (0.9 + 0.1 * v[..., None]) * (0.95 + 0.08 * g2[..., None])
    save("T_Madera_Mueble", img); TEX_SIZES["T_Madera_Mueble"] = 1.0

def fabric(n=512):
    yy, xx = np.mgrid[0:n, 0:n]
    w = ((np.sin(xx * np.pi / 2) * np.sin(yy * np.pi / 2)) * 0.5 + 0.5)
    g = fbm(n, 61, 4, 32)
    img = np.ones((n, n, 3)) * (0.85 + 0.08 * w + 0.07 * g)[..., None]
    save("T_Tela", img); TEX_SIZES["T_Tela"] = 0.5

def deck(n=512):
    yy, xx = np.mgrid[0:n, 0:n] / n
    boards = 8
    fy = (yy * boards) % 1.0
    idx = np.floor(yy * boards).astype(int)
    r = np.random.default_rng(71).random(64)
    g = fbm(n, 72, 4, 32)
    v = np.sin(xx * 40 + g * 10) * 0.5 + 0.5
    img = col((0.48, 0.32, 0.20)) * (0.8 + 0.2 * r[idx % 64][..., None]) * (0.85 + 0.15 * v[..., None])
    img[fy < 0.06] *= 0.35
    save("T_Deck", img); TEX_SIZES["T_Deck"] = 1.12

def roof(n=512):
    yy, xx = np.mgrid[0:n, 0:n] / n
    f = (xx * 4) % 1.0
    seam = np.exp(-((f - 0.5) ** 2) / 0.0008)
    g = fbm(n, 81, 4, 16)
    img = col((0.27, 0.28, 0.30)) * (0.9 + 0.1 * g[..., None]) + 0.18 * seam[..., None]
    save("T_Cubierta", img); TEX_SIZES["T_Cubierta"] = 2.0

def leaves(name, n, c, seed):
    g = fbm(n, seed, 6, 8)
    s = tile_noise(n, 0.7, seed + 1)
    img = col(c) * (0.65 + 0.6 * g[..., None]) * (0.85 + 0.3 * (s[..., None] > 0.6))
    save(name, img); TEX_SIZES[name] = 1.0

def particle(n=128):
    yy, xx = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1
    r = np.sqrt(xx ** 2 + yy ** 2)
    a = np.clip(1 - r, 0, 1) ** 1.6
    img = np.ones((n, n, 3))
    save("T_Gota", img, a)

def stream(w=64, h=256):
    rr = np.random.default_rng(5)
    x = np.linspace(-1, 1, w)
    prof = np.clip(1 - np.abs(x) ** 2, 0, 1)
    streak = gaussian_filter(rr.random((h, w)), (6, 1.0), mode='wrap')
    streak = (streak - streak.min()) / (streak.max() - streak.min())
    a = prof[None, :] * (0.45 + 0.55 * streak)
    img = np.ones((h, w, 3)) * (0.85 + 0.15 * streak[..., None])
    save("T_Agua_Chorro", img, a)

wood_floor()
tiles("T_Porcelanato", 1024, 2, 1.2, (0.83, 0.79, 0.73), (0.80, 0.76, 0.70), (0.70, 0.67, 0.62), gw=2.5,
      veins=0.25, var=0.03, seed=3)
tiles("T_Ceramica", 512, 4, 1.2, (0.60, 0.60, 0.60), (0.55, 0.55, 0.56), (0.42, 0.42, 0.42), gw=2.0, var=0.05, seed=4)
tiles("T_Azulejo", 512, 2, 0.6, (0.95, 0.95, 0.95), (0.90, 0.91, 0.92), (0.75, 0.75, 0.75), gw=2.0, var=0.02,
      seed=5, offset_rows=True, aspect=3)
tiles("T_Azulejo_Cocina", 512, 4, 0.6, (0.82, 0.86, 0.86), (0.74, 0.80, 0.81), (0.92, 0.92, 0.92), gw=2.0,
      var=0.03, seed=6, offset_rows=True, aspect=2)
tiles("T_Marmol", 512, 1, 0.6, (0.90, 0.89, 0.87), (0.88, 0.87, 0.85), (0.88, 0.87, 0.85), gw=0.0, veins=0.35,
      var=0.02, seed=7)
plaster(); concrete(); pavers(); grass(); furniture_wood(); fabric(); deck(); roof()
leaves("T_Hojas", 512, (0.22, 0.42, 0.16), 91)
particle(); stream()
json.dump(TEX_SIZES, open(os.path.join(OUT, "tex_sizes.json"), "w"), indent=1)
print("ok", sorted(TEX_SIZES))

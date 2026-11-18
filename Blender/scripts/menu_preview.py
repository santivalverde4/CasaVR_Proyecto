# Vista previa del menú de teletransporte con las mismas medidas, colores, sprites y textos que
# TeleportMenu.cs (fuente Liberation Sans = mismas métricas que la fuente Arial de Unity).
# Uso: python3 menu_preview.py <carpeta Textures/UI> <salida.png> [piso_actual] [destino_resaltado]
import sys, os
from PIL import Image, ImageDraw, ImageFont, ImageFilter

UI, OUT = sys.argv[1], sys.argv[2]
CUR = int(sys.argv[3]) if len(sys.argv) > 3 else 1
HOVER = sys.argv[4] if len(sys.argv) > 4 else "P2_Cocina"
SS = 1  # 1 unidad del canvas = 1 px

W, H = 1640, 1010
COL_W, COL_GAP, COL_TOP, COL_H = 506, 31, -180, 690
HEADER_H, BTN_H, BTN_GAP, PAD = 96, 68, 10, 14

def c(r, g, b, a=1.0): return (int(r * 255), int(g * 255), int(b * 255), int(a * 255))
PANEL_BG = c(0.045, 0.065, 0.11, 0.95); PANEL_LINE = c(0.35, 0.55, 0.95, 0.45)
COLUMN = c(0.085, 0.12, 0.19, 0.96); COLUMN_LINE = c(0.30, 0.45, 0.75, 0.22)
HEADER = c(0.12, 0.18, 0.29); ACCENT = c(0.16, 0.47, 0.96); ACCENT_GLOW = c(0.25, 0.55, 1.0, 0.55)
BTN = c(0.13, 0.19, 0.31); BTN_HOVER = c(0.19, 0.33, 0.58)
TEXT = c(0.95, 0.97, 1.0); MUTED = c(0.66, 0.72, 0.82)

ROOMS = {1: [("P1_Sala", "Sala"), ("P1_Cocina", "Cocina y comedor"), ("P1_Dormitorio", "Dormitorio"), ("P1_Bano", "Baño"),
             ("P1_Lavanderia", "Lavandería"), ("P1_Patio", "Patio")],
         2: [("P2_Sala", "Sala"), ("P2_Cocina", "Cocina y comedor"), ("P2_Dormitorio", "Dormitorio"), ("P2_Bano", "Baño"),
             ("P2_Lavanderia", "Lavandería"), ("P2_Balcon", "Balcón techado"), ("P2_Pasillo", "Pasillo exterior")],
         0: [("Ext_Entrada", "Entrada principal"), ("Ext_Escalera", "Escalera"), ("Ext_JardinFrontal", "Jardín frontal"),
             ("Ext_JardinTrasero", "Jardín trasero")]}
TITLES = {0: "Exterior", 1: "Piso 1", 2: "Piso 2"}

FR = "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf"
FB = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
def font(size, bold=False): return ImageFont.truetype(FB if bold else FR, size)

# fondo: una habitación borrosa, como se vería en el visor
img = Image.new("RGBA", (W + 200, H + 200), (120, 98, 80, 255))
bgd = ImageDraw.Draw(img)
bgd.rectangle((0, 0, 520, H + 200), fill=(150, 120, 96, 255)); bgd.rectangle((1300, 0, W + 200, H + 200), fill=(196, 182, 160, 255))
img = img.filter(ImageFilter.GaussianBlur(30))
OX, OY = 100, 100
layer = Image.new("RGBA", img.size, (0, 0, 0, 0))

def P(x, y): return (OX + W / 2 + x, OY + H / 2 - y)          # coordenadas del canvas (centro) -> píxeles
def box(cx, cy, w, h):
    x0, y0 = P(cx - w / 2, cy + h / 2); x1, y1 = P(cx + w / 2, cy - h / 2); return (x0, y0, x1, y1)

def rrect(b, radius, fill=None, outline=None, width=0):
    global layer
    tmp = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    ImageDraw.Draw(tmp).rounded_rectangle(b, radius, fill=fill, outline=outline, width=width)
    layer = Image.alpha_composite(layer, tmp)

def glow(b, radius, col):
    global layer
    tmp = Image.new("RGBA", layer.size, (0, 0, 0, 0))
    x0, y0, x1, y1 = b
    ImageDraw.Draw(tmp).rounded_rectangle((x0 + 20, y0 + 20, x1 - 20, y1 - 20), radius, fill=col)
    layer = Image.alpha_composite(layer, tmp.filter(ImageFilter.GaussianBlur(11)))

def icon(name, cx, cy, size):
    global layer
    p = os.path.join(UI, "Icono_" + name + ".png")
    if not os.path.exists(p): return
    ic = Image.open(p).convert("RGBA").resize((int(size), int(size)), Image.LANCZOS)
    x, y = P(cx - size / 2, cy + size / 2)
    tmp = Image.new("RGBA", layer.size, (0, 0, 0, 0)); tmp.paste(ic, (int(x), int(y)), ic)
    layer = Image.alpha_composite(layer, tmp)

def text(s, cx, cy, size, col, bold=False, anchor="mm"):
    d = ImageDraw.Draw(layer)
    d.text(P(cx, cy), s, font=font(size, bold), fill=col, anchor=anchor)

def text_w(s, size, bold=False): return font(size, bold).getlength(s)

# radios de esquina = 48 px del sprite / multiplicador; grosor de borde = 5 px / multiplicador
rrect(box(0, 0, W, H), 48, fill=PANEL_BG)
rrect(box(0, 0, W, H), 48, outline=PANEL_LINE, width=5)
text("¿A dónde quieres ir?", 0, H / 2 - 72, 62, TEXT, bold=True)
text("Estás en: " + TITLES[CUR] + "     ·     Apunta con el rayo y presiona el gatillo", 0, H / 2 - 134, 30, MUTED)

warn = []
for ci, floor in enumerate((1, 2, 0)):
    cx = (ci - 1) * (COL_W + COL_GAP); ccy = H / 2 + COL_TOP - COL_H / 2
    here = floor == CUR
    rrect(box(cx, ccy, COL_W, COL_H), 32, fill=COLUMN)
    rrect(box(cx, ccy, COL_W, COL_H), 32, outline=(ACCENT[:3] + (217,)) if here else COLUMN_LINE, width=3)
    top = ccy + COL_H / 2
    hy = top - PAD - HEADER_H / 2
    if here: glow(box(cx, hy, COL_W + 30, HEADER_H + 44), 40, ACCENT_GLOW)
    rrect(box(cx, hy, COL_W - 2 * PAD, HEADER_H), 24, fill=ACCENT if here else HEADER)
    title = TITLES[floor]; tw = text_w(title, 46, True) + 4; group = 54 + 18 + tw; x0 = cx - group / 2
    icon("Exterior" if floor == 0 else "Piso", x0 + 27, hy, 54)
    text(title, x0 + 54 + 18 + tw / 2, hy, 46, TEXT, bold=True)
    y = top - PAD - HEADER_H - 18 - BTN_H / 2
    bw = COL_W - 2 * PAD
    for rid, label in ROOMS[floor]:
        rrect(box(cx, y, bw, BTN_H), 22, fill=BTN_HOVER if rid == HOVER else BTN)
        left = -bw / 2 + 26
        key = rid.split("_", 1)[1]
        icon(key, cx + left + 22, y, 44)
        tx = left + 44 + 22; twmax = bw / 2 - 60 - tx
        if text_w(label, 34) > twmax: warn.append(f"texto largo: {label}")
        text(label, cx + tx, y, 34, TEXT, anchor="lm")
        text("›", cx + bw / 2 - 30, y + 3, 46, MUTED)
        y -= BTN_H + BTN_GAP
    if y + BTN_H / 2 + BTN_GAP < ccy - COL_H / 2: warn.append(f"columna {title}: destinos fuera de la tarjeta")

fy = -H / 2 + 70
hint = "Gatillo: elegir   •   Menú / Y / B: abrir o cerrar   •   PC: clic, Tab o M"
hx = -W / 2 + 40
if text_w(hint, 25) > 940: warn.append("ayuda demasiado larga")
text(hint, hx, fy, 25, MUTED, anchor="lm")
dnx = W / 2 - 40 - 240 - 24 - 160
if hx + text_w(hint, 25) > dnx - 160: warn.append("la ayuda choca con Modo noche")
rrect(box(dnx, fy, 320, 78), 24, fill=BTN)
lab = "Modo noche"; tw = text_w(lab, 32) + 4; g = 40 + 18 + tw; x0 = dnx - g / 2
icon("Noche", x0 + 20, fy, 40); text(lab, x0 + 58 + tw / 2, fy, 32, TEXT)
clx = W / 2 - 40 - 120
rrect(box(clx, fy, 240, 78), 24, fill=BTN)
rrect(box(clx, fy, 240, 78), 24, outline=ACCENT[:3] + (230,), width=3)
lab = "Cerrar"; tw = text_w(lab, 34, True) + 4; g = 30 + 18 + tw; x0 = clx - g / 2
icon("Cerrar", x0 + 15, fy, 30); text(lab, x0 + 48 + tw / 2, fy, 34, TEXT, bold=True)

out = Image.alpha_composite(img, layer).convert("RGB")
out.save(OUT, quality=92)
print("vista previa:", OUT, "| avisos:", warn or "ninguno")

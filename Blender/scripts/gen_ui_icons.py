# Genera los sprites del menú de teletransporte: íconos de línea blancos (128 px, fondo transparente)
# y rectángulos redondeados para 9-slice. Uso: python3 gen_ui_icons.py <carpeta destino>
import sys, os, math
from PIL import Image, ImageDraw, ImageFilter

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
S = 512            # se dibuja a 4x y se reduce (antialias)
W = 40             # grosor de línea a 4x (~10 px finales): se ve firme en el visor


def canvas():
    im = Image.new("L", (S, S), 0)
    return im, ImageDraw.Draw(im)


def line(d, pts, w=W):
    d.line(pts, fill=255, width=w, joint="curve")
    for p in (pts[0], pts[-1]):
        d.ellipse((p[0] - w / 2, p[1] - w / 2, p[0] + w / 2, p[1] + w / 2), fill=255)


def rrect(d, box, r, w=W, fill=False):
    if fill:
        d.rounded_rectangle(box, r, fill=255)
    else:
        d.rounded_rectangle(box, r, outline=255, width=w)


def circle(d, c, r, w=W, fill=False):
    box = (c[0] - r, c[1] - r, c[0] + r, c[1] + r)
    if fill:
        d.ellipse(box, fill=255)
    else:
        d.ellipse(box, outline=255, width=w)


def save(im, name, size=128):
    a = im.resize((size, size), Image.LANCZOS)
    rgba = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    rgba.putalpha(a)
    rgba.save(os.path.join(OUT, name + ".png"))


# ------------------------------------------------------------------ íconos
def sala():
    im, d = canvas()
    rrect(d, (96, 150, 416, 300), 40)                 # respaldo
    rrect(d, (56, 240, 136, 380), 34)                 # brazo izq
    rrect(d, (376, 240, 456, 380), 34)                # brazo der
    line(d, [(136, 300), (376, 300)])                 # asiento
    line(d, [(56, 380), (456, 380)])
    line(d, [(100, 380), (100, 420)]); line(d, [(412, 380), (412, 420)])
    return im


def cocina():
    im, d = canvas()
    # tenedor
    for x in (130, 175, 220):
        line(d, [(x, 80), (x, 190)])
    d.arc((130 - 15, 140, 220 + 15, 250), 0, 180, fill=255, width=W)
    line(d, [(175, 250), (175, 440)])
    # cuchillo
    d.polygon([(330, 80), (390, 110), (395, 270), (340, 270)], outline=255, width=W)
    line(d, [(360, 270), (360, 440)])
    return im


def dormitorio():
    im, d = canvas()
    line(d, [(70, 130), (70, 410)])                   # cabecera
    rrect(d, (110, 200, 230, 270), 30)                # almohada
    rrect(d, (70, 270, 442, 360), 26)                 # colchón
    line(d, [(442, 360), (442, 410)])
    return im


def bano():
    im, d = canvas()
    rrect(d, (140, 70, 372, 170), 24)                 # tanque
    d.chord((110, 160, 402, 380), 0, 180, outline=255, width=W)   # taza
    line(d, [(110, 236), (402, 236)])
    d.polygon([(200, 330), (312, 330), (330, 440), (182, 440)], outline=255, width=W)
    return im


def lavanderia():
    im, d = canvas()
    rrect(d, (90, 60, 422, 452), 46)
    line(d, [(90, 150), (422, 150)])
    circle(d, (150, 105), 12, fill=True); circle(d, (200, 105), 12, fill=True)
    circle(d, (256, 300), 100)
    d.arc((196, 240, 316, 360), 200, 330, fill=255, width=W - 6)
    return im


def patio():
    im, d = canvas()
    d.chord((76, 70, 436, 300), 180, 360, outline=255, width=W)     # sombrilla
    line(d, [(256, 70), (256, 440)])
    line(d, [(150, 330), (362, 330)])                                # mesa
    line(d, [(110, 300), (130, 440)]); line(d, [(402, 300), (382, 440)])  # sillas
    line(d, [(70, 380), (130, 380)]); line(d, [(442, 380), (382, 380)])
    return im


def balcon():
    im, d = canvas()
    line(d, [(60, 150), (452, 150)])
    line(d, [(60, 420), (452, 420)])
    for x in (60, 158, 256, 354, 452):
        line(d, [(x, 150), (x, 420)])
    line(d, [(60, 285), (452, 285)], w=W - 8)
    return im


def pasillo():
    im, d = canvas()
    d.line([(80, 440), (80, 90), (432, 90), (432, 440)], fill=255, width=W, joint="curve")
    d.line([(190, 440), (190, 200), (322, 200), (322, 440)], fill=255, width=W, joint="curve")
    line(d, [(80, 90), (190, 200)]); line(d, [(432, 90), (322, 200)])
    return im


def entrada():
    im, d = canvas()
    rrect(d, (130, 60, 382, 452), 18)
    line(d, [(70, 452), (442, 452)])
    circle(d, (318, 270), 18, fill=True)
    return im


def escalera():
    im, d = canvas()
    pts = [(60, 440), (60, 360), (160, 360), (160, 270), (260, 270), (260, 180), (360, 180), (360, 90), (452, 90)]
    d.line(pts, fill=255, width=W, joint="curve")
    line(d, [(60, 440), (452, 440)])
    line(d, [(452, 90), (452, 440)])
    return im


def flor():
    im, d = canvas()
    line(d, [(256, 250), (256, 450)])
    d.chord((110, 260, 270, 390), 200, 380, outline=255, width=W)
    d.chord((242, 260, 402, 390), 160, 340, outline=255, width=W)
    # tulipán
    d.line([(170, 90), (170, 170)], fill=255, width=W)
    d.line([(342, 90), (342, 170)], fill=255, width=W)
    d.line([(170, 90), (213, 140), (256, 80), (299, 140), (342, 90)], fill=255, width=W, joint="curve")
    d.arc((170, 80, 342, 250), 0, 180, fill=255, width=W)
    for p in ((170, 90), (342, 90), (256, 80)):
        circle(d, p, W / 2, fill=True)
    return im


def pino():
    im, d = canvas()
    d.polygon([(256, 50), (120, 230), (190, 230), (90, 370), (422, 370), (322, 230), (392, 230)], outline=255, width=W)
    line(d, [(256, 370), (256, 455)])
    return im


def arbol():
    im, d = canvas()
    for c, r in (((180, 210), 90), ((332, 210), 90), ((256, 140), 100), ((256, 250), 90)):
        circle(d, c, r, fill=True)
    inner, di = canvas()
    for c, r in (((180, 210), 90 - W), ((332, 210), 90 - W), ((256, 140), 100 - W), ((256, 250), 90 - W)):
        circle(di, c, r, fill=True)
    im.paste(0, mask=inner)
    line(d, [(256, 300), (256, 455)])
    line(d, [(256, 380), (196, 330)]); line(d, [(256, 360), (316, 315)])
    return im


def piso():
    im, d = canvas()
    d.polygon([(256, 60), (450, 170), (256, 280), (62, 170)], outline=255, width=W)
    d.line([(62, 260), (256, 370), (450, 260)], fill=255, width=W, joint="curve")
    d.line([(62, 350), (256, 460), (450, 350)], fill=255, width=W, joint="curve")
    return im


def noche():
    im, d = canvas()
    circle(d, (250, 256), 170, fill=True)
    m, dm = canvas()
    circle(dm, (330, 190), 150, fill=True)
    im.paste(0, mask=m)
    inner, di = canvas()
    circle(di, (250, 256), 170 - W, fill=True)
    circle(di, (330, 190), 150 + W, fill=False, w=1)
    m2, dm2 = canvas()
    circle(dm2, (330, 190), 150 + W, fill=True)
    inner.paste(0, mask=m2)
    im.paste(0, mask=inner)
    return im


def dia():
    im, d = canvas()
    circle(d, (256, 256), 92)
    for k in range(8):
        a = math.radians(k * 45)
        line(d, [(256 + 150 * math.cos(a), 256 + 150 * math.sin(a)), (256 + 210 * math.cos(a), 256 + 210 * math.sin(a))])
    return im


def cerrar():
    im, d = canvas()
    line(d, [(120, 120), (392, 392)], w=W + 8)
    line(d, [(392, 120), (120, 392)], w=W + 8)
    return im


def gatillo():
    im, d = canvas()
    d.arc((90, 60, 420, 470), 200, 340, fill=255, width=W)
    rrect(d, (170, 200, 340, 460), 70)
    line(d, [(255, 250), (255, 320)])
    return im


ICONS = {
    "Icono_Sala": sala, "Icono_Cocina": cocina, "Icono_Dormitorio": dormitorio, "Icono_Bano": bano,
    "Icono_Lavanderia": lavanderia, "Icono_Patio": patio, "Icono_Balcon": balcon, "Icono_Pasillo": pasillo,
    "Icono_Entrada": entrada, "Icono_Escalera": escalera, "Icono_JardinFrontal": flor, "Icono_JardinTrasero": pino,
    "Icono_Piso": piso, "Icono_Exterior": arbol, "Icono_Noche": noche, "Icono_Dia": dia, "Icono_Cerrar": cerrar,
    "Icono_Gatillo": gatillo,
}
for n, f in ICONS.items():
    save(f(), n)

# ------------------------------------------------------------------ fondos 9-slice (128 px, borde 56)
def rounded(name, outline=False, glow=False):
    im, d = canvas()
    r = 48 * 4
    if glow:
        d.rounded_rectangle((80, 80, S - 80, S - 80), r - 60, fill=255)
        im = im.filter(ImageFilter.GaussianBlur(44))
    elif outline:
        d.rounded_rectangle((8, 8, S - 8, S - 8), r, outline=255, width=20)
    else:
        d.rounded_rectangle((0, 0, S - 1, S - 1), r, fill=255)
    save(im, name)

rounded("UI_Redondeado")
rounded("UI_RedondeadoBorde", outline=True)
rounded("UI_Brillo", glow=True)
print("sprites:", len(ICONS) + 3, "->", OUT)

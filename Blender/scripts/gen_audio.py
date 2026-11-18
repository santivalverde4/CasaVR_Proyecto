import numpy as np, wave, sys, os
from scipy.signal import butter, lfilter
OUT = sys.argv[1]; SR = 44100
rng = np.random.default_rng(1)
def save(name, x):
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.8
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
def band(x, lo, hi, order=2):
    b, a = butter(order, [lo / (SR / 2), hi / (SR / 2)], btype='band'); return lfilter(b, a, x)
def loopify(x, fade=0.25):
    n = int(fade * SR); head = x[:n].copy(); body = x[n:].copy()
    t = np.linspace(0, 1, n); body[-n:] = body[-n:] * (1 - t) + head * t
    return body
def water(seconds, lo, hi, mod_hz, depth, seed):
    r = np.random.default_rng(seed); n = int((seconds + 0.25) * SR)
    x = band(r.standard_normal(n), lo, hi) * 0.7 + band(r.standard_normal(n), lo * 3, min(hi * 1.8, 18000)) * 0.3
    t = np.arange(n) / SR
    env = 1 + depth * np.sin(2 * np.pi * mod_hz * t) * np.sin(2 * np.pi * mod_hz * 0.37 * t + 1.0)
    # pequeñas burbujas
    bub = np.zeros(n)
    for k in range(int(seconds * 25)):
        p = r.integers(0, n - 2000); f = r.uniform(700, 2200); L = r.integers(300, 1500)
        tt = np.arange(L) / SR; bub[p:p + L] += np.sin(2 * np.pi * f * tt * (1 + 3 * tt)) * np.exp(-tt * 60) * r.uniform(0.1, 0.3)
    return loopify(x * env + bub * 0.4)
save("CasaVR_Agua_Grifo.wav", water(3.0, 600, 7000, 1.3, 0.15, 3))
save("CasaVR_Agua_Ducha.wav", water(3.0, 300, 11000, 0.9, 0.10, 4))
t = np.arange(int(0.05 * SR)) / SR
save("CasaVR_Click.wav", np.concatenate([band(rng.standard_normal(len(t)), 1500, 9000) * np.exp(-t * 300), np.zeros(800)]))
n = int(0.7 * SR); t = np.arange(n) / SR
creak = np.sin(2 * np.pi * (180 + 60 * np.sin(2 * np.pi * 3 * t)) * t) * 0.25 * np.exp(-((t - 0.3) ** 2) / 0.05)
swish = band(rng.standard_normal(n), 200, 2500) * np.exp(-((t - 0.35) ** 2) / 0.03) * 0.6
save("CasaVR_Puerta_Abrir.wav", creak + swish)
n = int(0.5 * SR); t = np.arange(n) / SR
thud = np.sin(2 * np.pi * 70 * t) * np.exp(-t * 18) + band(rng.standard_normal(n), 100, 1500) * np.exp(-t * 35) * 0.8
save("CasaVR_Puerta_Cerrar.wav", np.concatenate([np.zeros(int(0.05 * SR)), thud]))
n = int(0.6 * SR); t = np.arange(n) / SR
save("CasaVR_Ventana.wav", band(rng.standard_normal(n), 300, 4000) * np.sin(np.pi * t / t[-1]) ** 2 * (1 + 0.3 * np.sin(2 * np.pi * 23 * t)))
print("ok", sorted(os.listdir(OUT)))

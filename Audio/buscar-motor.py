# -*- coding: utf-8 -*-
"""Donde esta el motor solo, sin los otros ruidos.

El mp3 dura 17,8 s y ahi dentro hay arranques, golpes y cambios de nivel. Para
un bucle hace falta un tramo donde el motor este ya girando estable: si se cuela
un golpe, se va a oir repetido cada vez que el bucle de la vuelta, y eso canta
muchisimo mas que en una reproduccion suelta.

Un motor de verdad no es constante, asi que no vale pedir marcos identicos: lo
que se hace es deslizar una ventana del largo que quiero y puntuar todas.

La nota castiga tres cosas, y la primera mucho mas que las otras dos:
  - el salto de nivel mas grande que haya dentro (eso es un golpe)
  - que el nivel se vaya yendo de un lado a otro
  - que se mueva el timbre (si cambia el centro espectral hay otra cosa sonando
    encima, aunque el nivel no cambie)
"""
import aud
import numpy as np

RUTA = "C:/Users/xveli/Desktop/motor movimiento gancho.mp3"
LARGO = 1.2

s = aud.Sound.file(RUTA)
rate = s.specs[0]
d = s.data()
mono = d.mean(axis=1) if d.ndim > 1 else d.ravel()
mono = mono.astype(np.float64)

VENTANA = int(rate * 0.025)
SALTO = int(rate * 0.010)

marcos = (len(mono) - VENTANA) // SALTO
ven = np.hanning(VENTANA)
frec = np.fft.rfftfreq(VENTANA, 1.0 / rate)

rms = np.zeros(marcos)
centro = np.zeros(marcos)

for i in range(marcos):
    t = mono[i * SALTO:i * SALTO + VENTANA]
    rms[i] = np.sqrt((t ** 2).mean())

    esp = np.abs(np.fft.rfft(t * ven))
    total = esp.sum()
    centro[i] = float((esp * frec).sum() / total) if total > 1e-12 else 0.0

lrms = np.log(rms + 1e-9)
salto = np.abs(np.diff(lrms))
suelo = np.median(rms) * 0.6
cmed = np.median(centro)

ancho = int(LARGO / 0.010)
notas = []

for i in range(marcos - ancho):
    r = rms[i:i + ancho]

    if r.mean() < suelo:
        continue

    golpe = salto[i:i + ancho - 1].max()
    deriva = lrms[i:i + ancho].std()
    timbre = centro[i:i + ancho].std() / cmed

    notas.append((golpe * 3.0 + deriva + timbre * 2.0, i, golpe, deriva, timbre,
                  r.mean(), centro[i:i + ancho].mean()))

notas.sort()

print("\n" + "=" * 76)
print("MEJORES VENTANAS DE %.1f s EN %.1f s DE GRABACION" % (LARGO, len(mono) / rate))
print("=" * 76)
print("  %8s %7s %8s %8s %8s %8s" % ("desde", "nota", "golpe", "deriva", "timbre", "nivel"))

vistos = []

for nota, i, golpe, deriva, timbre, nivel, cen in notas:
    t = i * 0.010

    # Solapadas dicen lo mismo: solo las que esten separadas de verdad.
    if any(abs(t - v) < LARGO for v in vistos):
        continue

    vistos.append(t)
    print("  %7.2fs %7.3f %8.3f %8.3f %8.3f %8.4f   timbre %.0f Hz"
          % (t, nota, golpe, deriva, timbre, nivel, cen))

    if len(vistos) >= 6:
        break

print("=" * 76 + "\n")

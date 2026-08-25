# -*- coding: utf-8 -*-
"""Genera el golpe de una caja de carton contra el suelo.

Una caja de carton no es un tambor ni un golpe seco: son tres cosas a la vez, y
si falta alguna no se reconoce.

  1. El golpe grave del fondo contra el suelo. Muy corto, casi un chasquido
     grave: el carton no resuena, se apaga en cuarenta milisegundos.

  2. Un par de modos de la caja vibrando, graves y muy amortiguados. Es lo que
     la separa de un golpe en el suelo a secas.

  3. El crujido del carton. Ruido de banda ancha filtrado, todavia mas corto.
     Sin esto suena a caja de madera.

Las frecuencias de los modos van deliberadamente desafinadas entre si. Una caja
es una placa, no una cuerda: sus modos no son armonicos, y si se ponen en
proporciones enteras el oido lo interpreta como una nota y suena a instrumento.
"""
import io
import math
import wave

import numpy as np

SALIDA = "D:/APLHA ClayWorks simulator/Assets/_Project/Audio/Caja_Golpe.wav"
RATE = 44100
LARGO = 0.30
PICO = 0.850


def envolvente(n, ataque, caida):
    """Sube casi de golpe y baja exponencial, que es como decae un golpe."""
    t = np.arange(n) / float(RATE)

    sube = 1.0 - np.exp(-t / max(1e-5, ataque))
    baja = np.exp(-t / max(1e-5, caida))

    return sube * baja


def unpolo(x, corte, paso_alto=False):
    """Filtro de un polo. Cae 6 dB por octava, que para esto sobra."""
    a = math.exp(-2.0 * math.pi * corte / RATE)
    y = np.empty_like(x)
    z = 0.0

    for i in range(len(x)):
        z = (1.0 - a) * x[i] + a * z
        y[i] = z

    return x - y if paso_alto else y


n = int(RATE * LARGO)
t = np.arange(n) / float(RATE)

# ------------------------------------------------------------ 1. el golpe
# La frecuencia cae mientras suena: es lo que hace que se oiga como un impacto
# y no como una nota grave. Un golpe real tensa el material y lo suelta.
f = 95.0 * np.exp(-t / 0.030) + 48.0
fase = 2.0 * np.pi * np.cumsum(f) / RATE

golpe = np.sin(fase) * envolvente(n, 0.0008, 0.045)

# ------------------------------------------------ 2. la caja resonando
modos = np.zeros(n)

for frec, peso, caida in ((173.0, 0.34, 0.055),
                          (268.0, 0.22, 0.040),
                          (391.0, 0.13, 0.028)):
    modos += peso * np.sin(2.0 * np.pi * frec * t) * envolvente(n, 0.0012, caida)

# ------------------------------------------------------- 3. el crujido
r = np.random.RandomState(4)
ruido = r.uniform(-1.0, 1.0, n)

# Se le quita el grave, que ya lo pone el golpe, y sobre todo el brillo.
#
# El paso bajo va DOS veces. Un polo solo cae 6 dB por octava y a 3.800 Hz
# dejaba pasar tanto agudo que el centro espectral del golpe entero se iba a
# 4.700 Hz: sonaba a hoja de papel arrugada, no a caja. Con dos polos a 1.500
# cae 12 por octava y el centro baja a donde tiene que estar.
cruje = unpolo(ruido, 260.0, paso_alto=True)
cruje = unpolo(unpolo(cruje, 1500.0), 1500.0)
cruje *= envolvente(n, 0.0004, 0.022)
cruje *= 0.55 / max(1e-9, np.abs(cruje).max())

# Y un segundo crujidito un pelo despues, como el carton acomodandose.
tarde = np.zeros(n)
d = int(RATE * 0.035)
tarde[d:] = cruje[:n - d] * 0.45

mezcla = golpe * 1.0 + modos * 0.9 + cruje + tarde

# Rampa al final para que no corte en seco.
cola = int(RATE * 0.02)
mezcla[-cola:] *= np.linspace(1.0, 0.0, cola)

mezcla *= PICO / np.abs(mezcla).max()

with wave.open(SALIDA, "wb") as w:
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(RATE)
    w.writeframes((np.clip(mezcla, -1.0, 1.0) * 32767.0).astype(np.int16).tobytes())

esp = np.abs(np.fft.rfft(mezcla))
frec = np.fft.rfftfreq(n, 1.0 / RATE)
centro = float((esp * frec).sum() / esp.sum())

print("CAJA_GOLPE: %.2f s, pico %.3f, rms %.4f, centro %.0f Hz"
      % (LARGO, float(np.abs(mezcla).max()), float(np.sqrt((mezcla ** 2).mean())), centro))

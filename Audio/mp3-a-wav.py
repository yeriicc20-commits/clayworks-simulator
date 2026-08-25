# -*- coding: utf-8 -*-
"""Pasa los tres mp3 a los wav del juego.

Se conservan los nombres que ya existen a proposito: asi Unity mantiene el .meta
y su guid, y ninguna referencia del prefab de la maquina se rompe. Cambiar el
nombre obligaria a reasignar los AudioClip a mano.

Todos los sonidos del proyecto estan a 44100 Hz y pico 0,850. Los nuevos entran
igual: la mezcla del juego se ajusto a oido contra ese nivel, y meter algo mas
se ajusto a oido contra ese nivel, y meter algo mas alto o mas bajo tira por
tierra ese ajuste haciendo parecer que el problema es el mezclador.
"""
import aud
import numpy as np
import wave

AUDIO = "D:/APLHA ClayWorks simulator/Assets/_Project/Audio/"
FUENTES = "D:/APLHA ClayWorks simulator/Audio/Fuentes/"
SALIDA = 44100
PICO = 0.850


def cargar(ruta):
    s = aud.Sound.file(ruta)
    d = s.data()
    mono = d.mean(axis=1) if d.ndim > 1 else d.ravel()
    return mono.astype(np.float64), s.specs[0]


def remuestrear(x, de, a):
    if de == a:
        return x

    n = int(round(len(x) * a / float(de)))
    return np.interp(np.arange(n) * (de / float(a)), np.arange(len(x)), x)


def recortar_silencio(x, umbral=0.02, cola=0.03, rate=SALIDA):
    """Fuera el silencio de delante y de detras.

    El de delante importa mas de lo que parece: la moneda suena al pulsar, y una
    decima de retraso se nota como que el boton va lento.
    """
    fuerte = np.abs(x) > np.abs(x).max() * umbral

    if not fuerte.any():
        return x

    i = int(np.argmax(fuerte))
    j = len(x) - int(np.argmax(fuerte[::-1]))

    return x[max(0, i - int(rate * 0.005)):min(len(x), j + int(rate * cola))]


def rampa(x, entra=0.004, sale=0.010, rate=SALIDA):
    """Un pelin de rampa en los bordes: un corte en seco hace 'clac'."""
    x = x.copy()
    a, b = int(rate * entra), int(rate * sale)

    if a > 0 and len(x) > a:
        x[:a] *= np.linspace(0.0, 1.0, a)
    if b > 0 and len(x) > b:
        x[-b:] *= np.linspace(1.0, 0.0, b)

    return x


def normalizar(x, pico=PICO):
    m = np.abs(x).max()
    return x * (pico / m) if m > 1e-9 else x


def guardar(nombre, x, rate=SALIDA):
    d = np.clip(x, -1.0, 1.0)
    d = (d * 32767.0).astype(np.int16)

    with wave.open(AUDIO + nombre + ".wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(d.tobytes())

    rms = float(np.sqrt((x ** 2).mean()))
    print("  %-14s %5.2f s  pico %.3f  rms %.4f"
          % (nombre + ".wav", len(x) / rate, float(np.abs(x).max()), rms))

    return rms


print("\n" + "=" * 70)
print("MP3 -> WAV")
print("=" * 70)

# ------------------------------------------------------------- la moneda
x, r = cargar(FUENTES + "insert-coin.mp3")
x = remuestrear(x, r, SALIDA)
x = recortar_silencio(x)
guardar("Moneda", normalizar(rampa(x)))

# ------------------------------------------------- el de partida perdida
x, r = cargar(FUENTES + "juego-perdido.mp3")
x = remuestrear(x, r, SALIDA)
x = recortar_silencio(x)
guardar("Fallo", normalizar(rampa(x)))

# ------------------------------------------------------------- el motor
# 3,13 a 4,33 s: de las quince mil ventanas posibles es la que menos salta de
# nivel y la que menos mueve el timbre. Lo de fuera lleva arranques y golpes, y
# un golpe dentro de un bucle se oye repetido cada vuelta.
x, r = cargar(FUENTES + "motor-gancho.mp3")
x = remuestrear(x, r, SALIDA)

DESDE, LARGO, CRUCE = 3.13, 1.20, 0.15

i = int(DESDE * SALIDA)
n = int(LARGO * SALIDA)
f = int(CRUCE * SALIDA)

trozo = x[i:i + n + f]

# Bucle sin costura: la cola se funde ENCIMA de la cabeza. Asi la ultima muestra
# del bucle enlaza con lo que de verdad venia despues en la grabacion, que es lo
# que ahora ocupa el principio. Cortar y pegar sin esto da un 'clic' cada vuelta.
bucle = trozo[:n].copy()
sube = np.linspace(0.0, 1.0, f)
bucle[:f] = bucle[:f] * sube + trozo[n:n + f] * (1.0 - sube)

antes_rms = 0.5444          # lo que tenia el Motor_Carro sintetizado
bucle = normalizar(bucle)
rms = guardar("Motor_Carro", bucle)

# Comprobacion de la costura: el salto entre la ultima muestra y la primera,
# comparado con el salto normal dentro del propio bucle.
salto = abs(float(bucle[0] - bucle[-1]))
normal = float(np.abs(np.diff(bucle)).mean())

print("\n  costura del bucle ... salto %.5f contra %.5f de media normal  (%s)"
      % (salto, normal, "sin clic" if salto < normal * 4 else "SE VA A OIR"))

print("  nivel del motor ..... rms %.4f contra %.4f del que habia  (x%.2f)"
      % (rms, antes_rms, rms / antes_rms))
print("=" * 70 + "\n")

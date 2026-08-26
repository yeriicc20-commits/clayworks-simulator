# -*- coding: utf-8 -*-
# Un interruptor de pared, de los de tecla basculante.
#
# Medidas de uno de verdad: placa de 86 mm en cuadro, que es el estandar, y
# tecla de 45 x 55. Respetarlas importa mas de lo que parece: al lado de una
# puerta o de una maquina, un interruptor de tamano inventado canta al momento
# aunque nadie sepa decir por que.
#
# Se ejecuta con:
#   blender --background --python Modelos/interruptor.py
import sys

sys.path.append("D:/APLHA ClayWorks simulator/Modelos")

import piezas as P

# --- medidas, en metros ----------------------------------------------------
PLACA = 0.086          # el cuadro de la placa
PLACA_FONDO = 0.010    # lo que sobresale de la pared
MARCO = 0.006          # el reborde de la placa

TECLA_ANCHO = 0.045
TECLA_ALTO = 0.055
TECLA_FONDO = 0.007    # lo que sobresale la tecla por encima de la placa

# La cara que da a la habitacion mira hacia -Y en Blender, que al exportar cae
# en +Z de Unity: asi el "delante" del prefab es el delante de Unity y no hay
# que acordarse de girarlo al colocarlo.
FUERA = -1.0


def construir():
    P.limpiar()

    blanco = P.material("Interruptor_Placa", (0.90, 0.90, 0.88, 1.0), 0.0, 0.42)
    tecla_mat = P.material("Interruptor_Tecla", (0.95, 0.95, 0.93, 1.0), 0.0, 0.35)
    hueco = P.material("Interruptor_Hueco", (0.14, 0.14, 0.15, 1.0), 0.0, 0.60)

    piezas = []

    # La placa. Biselada, que un canto vivo en plastico blanco se ve falso
    # justo por el brillo del borde.
    piezas.append(P.caja(
        "Placa",
        (PLACA, PLACA_FONDO, PLACA),
        (0.0, FUERA * PLACA_FONDO * 0.5, 0.0),
        blanco, bisel=0.0015))

    # El hueco donde vive la tecla: una caja oscura un pelo mas grande que la
    # tecla, para que se vea la sombra del contorno y no un boton pegado.
    piezas.append(P.caja(
        "Hueco",
        (TECLA_ANCHO + 0.004, 0.006, TECLA_ALTO + 0.004),
        (0.0, FUERA * (PLACA_FONDO - 0.002), 0.0),
        hueco))

    # La tecla, inclinada como cuando esta pulsada por abajo: un basculante
    # recto parece un boton, y torcido se lee al momento como interruptor.
    tecla = P.caja(
        "Tecla",
        (TECLA_ANCHO, TECLA_FONDO, TECLA_ALTO),
        (0.0, FUERA * (PLACA_FONDO + TECLA_FONDO * 0.4), 0.0),
        tecla_mat, bisel=0.0012)

    tecla.rotation_euler = (0.10, 0.0, 0.0)

    import bpy
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)


    # Los dos tornillos del marco. Son 3 mm y casi no se ven, pero son lo que
    # hace que parezca montado en la pared y no dibujado encima.
    for lado in (-1.0, 1.0):
        piezas.append(P.cilindro(
            "Tornillo", 0.0028, 0.003,
            (0.0, FUERA * (PLACA_FONDO - 0.0015), lado * (PLACA * 0.5 - MARCO)),
            "Y", hueco, 10))

    # La tecla se queda FUERA de la union, y es a proposito.
    #
    # Unida al resto no puede bascular: seria una sola malla y girarla giraria la
    # placa entera. Separada y colgando del cuerpo, Unity la encuentra por su
    # nombre y el interruptor la mueve al pulsarlo.
    cuerpo = P.unir(piezas, "Interruptor")

    tecla.parent = cuerpo
    tecla.name = "Tecla"

    return cuerpo


if __name__ == "__main__":
    construir()

    print("=== Interruptor ===")
    P.exportar("Interruptor")
    P.foto("interruptor", (0.0, 0.0, 0.0), 0.26, angulo=0.7, alto=0.3)

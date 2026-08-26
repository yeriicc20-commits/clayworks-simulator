# -*- coding: utf-8 -*-
# Una bombilla colgando del techo, con su cable y su casquillo.
#
# Medidas de una A60 de verdad: bulbo de 60 mm y unos 110 de alto con el
# casquillo. Cuelga 30 cm del techo, que a 5 m de altura la deja bien por
# encima de la cabeza -- el jugador mide 1,8 -- pero lo bastante baja como para
# que se lea que es una bombilla y no una mancha en el techo.
#
# El origen del modelo esta en la roseta, o sea en el punto que toca el techo.
# Eso no es casual: al colocarla, ese es el punto que se pega a la superficie,
# asi que el pivote tiene que estar justo ahi.
#
# Se ejecuta con:
#   blender --background --python Modelos/bombilla.py
import sys

sys.path.append("D:/APLHA ClayWorks simulator/Modelos")

import piezas as P

# --- medidas, en metros ----------------------------------------------------
ROSETA_RADIO = 0.038
ROSETA_ALTO = 0.014

CABLE_RADIO = 0.0035
CABLE_LARGO = 0.235

CASQUILLO_RADIO = 0.0130
CASQUILLO_ALTO = 0.026

BULBO_RADIO = 0.030

# Todo cuelga hacia -Z en Blender, que al exportar cae en -Y de Unity: hacia
# abajo, que es a donde cuelgan las bombillas.
ABAJO = -1.0


def construir():
    P.limpiar()

    metal = P.material("Bombilla_Roseta", (0.16, 0.16, 0.18, 1.0), 0.65, 0.35)
    cable_mat = P.material("Bombilla_Cable", (0.10, 0.10, 0.11, 1.0), 0.0, 0.55)
    laton = P.material("Bombilla_Casquillo", (0.72, 0.58, 0.28, 1.0), 0.90, 0.28)

    # El vidrio va con emision desde el modelo para que se vea encendida aunque
    # nadie toque nada. Apagarla es cosa de Unity: alli se cambia la emision y
    # se apaga la luz de verdad, que un material no ilumina nada por si solo.
    vidrio = P.material("Bombilla_Vidrio", (1.00, 0.93, 0.74, 1.0), 0.0, 0.12,
                        emision=2.2)

    piezas = []

    # La roseta, pegada al techo.
    piezas.append(P.cilindro(
        "Roseta", ROSETA_RADIO, ROSETA_ALTO,
        (0.0, 0.0, ABAJO * ROSETA_ALTO * 0.5), "Z", metal, 20))

    # Un rebaje debajo, para que no parezca un disco pegado.
    piezas.append(P.cilindro(
        "Roseta_Cuello", ROSETA_RADIO * 0.55, 0.010,
        (0.0, 0.0, ABAJO * (ROSETA_ALTO + 0.004)), "Z", metal, 16))

    cable_z = ABAJO * (ROSETA_ALTO + CABLE_LARGO * 0.5)

    piezas.append(P.cilindro(
        "Cable", CABLE_RADIO, CABLE_LARGO,
        (0.0, 0.0, cable_z), "Z", cable_mat, 10))

    casquillo_z = ABAJO * (ROSETA_ALTO + CABLE_LARGO + CASQUILLO_ALTO * 0.5)

    piezas.append(P.cilindro(
        "Casquillo", CASQUILLO_RADIO, CASQUILLO_ALTO,
        (0.0, 0.0, casquillo_z), "Z", laton, 20))

    # El cuello: del casquillo al bulbo. Un cono corto, que es lo que hace que
    # la bombilla no parezca una bola clavada en un tubo.
    cuello_z = casquillo_z - ABAJO * 0.0 + ABAJO * (CASQUILLO_ALTO * 0.5 + 0.009)

    piezas.append(P.cono(
        "Cuello", CASQUILLO_RADIO, BULBO_RADIO * 0.72, 0.018,
        (0.0, 0.0, cuello_z), vidrio, 20))

    bulbo_z = cuello_z + ABAJO * (0.009 + BULBO_RADIO * 0.72)

    # Un pelin achatada, como las de verdad: la esfera perfecta se ve a plastico.
    piezas.append(P.esfera(
        "Bulbo", BULBO_RADIO, (0.0, 0.0, bulbo_z), vidrio, 28, achatado=0.94))

    return P.unir(piezas, "Bombilla")


if __name__ == "__main__":
    construir()

    largo = ROSETA_ALTO + CABLE_LARGO + CASQUILLO_ALTO + BULBO_RADIO * 2.0

    print("=== Bombilla ===")
    print("  cuelga %.3f m del techo" % largo)

    P.exportar("Bombilla")
    P.foto("bombilla", (0.0, 0.0, ABAJO * largo * 0.5), 0.62, angulo=0.5, alto=0.12)

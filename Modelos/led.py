# -*- coding: utf-8 -*-
# Una pantalla LED de techo, de las alargadas.
#
# Medidas de una regleta de 4 pies, que es la de toda la vida: 1,20 m de largo
# por 76 mm de ancho y unos 55 de alto. En un local de 11 m dan la escala justa
# -- se ve que es una luminaria de tienda y no una lampara de casa.
#
# Va pegada al techo, no colgando. Eso arregla de paso lo de antes: la carcasa
# tapa la luz por arriba, asi que el techo deja de llevarse el foco y todo lo
# que da la luminaria baja a la sala.
#
# El origen esta arriba, en la cara que toca el techo: es el punto que se pega a
# la superficie al colocarla, asi que el pivote tiene que estar ahi.
#
# Se ejecuta con:
#   blender --background --python Modelos/led.py
import sys

sys.path.append("D:/APLHA ClayWorks simulator/Modelos")

import piezas as P

# --- medidas, en metros ----------------------------------------------------
LARGO = 1.20
ANCHO = 0.076

CARCASA_ALTO = 0.028
DIFUSOR_ALTO = 0.026
DIFUSOR_ANCHO = 0.062

TAPA = 0.010          # las tapas de los extremos, que sobresalen un pelin

ABAJO = -1.0


def construir():
    P.limpiar()

    # Blanco de luminaria: casi blanco pero no del todo, que el blanco puro se
    # ve plano y de plastico barato en cuanto le da una luz.
    chapa = P.material("Led_Carcasa", (0.90, 0.91, 0.92, 1.0), 0.25, 0.38)

    # El difusor lleva emision desde el modelo para que se vea encendido. En
    # Unity se enciende y se apaga: un material no ilumina nada por si solo.
    difusor_mat = P.material("Led_Difusor", (1.00, 0.97, 0.90, 1.0), 0.0, 0.22,
                             emision=2.6)

    piezas = []

    # La carcasa, pegada al techo.
    piezas.append(P.caja(
        "Carcasa",
        (LARGO, ANCHO, CARCASA_ALTO),
        (0.0, 0.0, ABAJO * CARCASA_ALTO * 0.5),
        chapa, bisel=0.002))

    # Las tapas de los extremos. Son 1 cm y casi no se ven de frente, pero de
    # lado son lo que hace que parezca una luminaria montada y no una pastilla.
    for lado in (-1.0, 1.0):
        piezas.append(P.caja(
            "Tapa",
            (TAPA, ANCHO + 0.004, CARCASA_ALTO + DIFUSOR_ALTO * 0.55),
            (lado * (LARGO * 0.5 - TAPA * 0.5), 0.0,
             ABAJO * (CARCASA_ALTO + DIFUSOR_ALTO * 0.55) * 0.5),
            chapa, bisel=0.0015))

    cuerpo = P.unir(piezas, "LedTecho")

    # El difusor va SUELTO, y es a proposito.
    #
    # Unido a la carcasa seria una sola malla, y encender la luz encenderia
    # tambien la chapa: la luminaria entera brillando como una barra de neon.
    # Separado, en Unity se le enciende solo a el.
    difusor = P.caja(
        "Difusor",
        (LARGO - TAPA * 2.0, DIFUSOR_ANCHO, DIFUSOR_ALTO),
        (0.0, 0.0, ABAJO * (CARCASA_ALTO + DIFUSOR_ALTO * 0.5 - 0.004)),
        difusor_mat, bisel=0.004)

    difusor.parent = cuerpo
    difusor.name = "Difusor"

    return cuerpo


if __name__ == "__main__":
    construir()

    alto = CARCASA_ALTO + DIFUSOR_ALTO

    print("=== Pantalla LED ===")
    print("  %.2f x %.3f m, y sobresale %.3f del techo" % (LARGO, ANCHO, alto))

    P.exportar("LedTecho")
    P.foto("led", (0.0, 0.0, ABAJO * alto * 0.5), 1.35, angulo=0.75, alto=-0.35)

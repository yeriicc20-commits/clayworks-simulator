"""
Peluche de aguacate con cara, sobre las mismas herramientas que Panxeta.

Lo que se mira de la foto, y en este orden:

  - la SILUETA. Un aguacate no es un huevo: es una pera con el culo muy
    redondo y el cuello estrecho pero rematado en cupula, no en punta.

  - la PROFUNDIDAD. Es un peluche relleno, no un cojin. Va achatado solo un
    12%, y la carne y el hueso no son pegatinas: la carne es la propia
    superficie pintada de otro color, y el hueso abulta de verdad porque en
    la foto se le ve el bulto y la sombra alrededor.

  - los OJOS. Son PEQUENOS, y es lo que mas cambia la cara. En la foto miden
    poco mas de un centimetro sobre un cuerpo de casi veinte: un 6% del
    ancho. Poniendolos del tamano que uno diria a ojo salen unos ojos de
    dibujo animado y deja de parecerse.

  - los MOFLETES. Son espirales, no manchas redondas. Es el detalle que
    identifica a este peluche entre los mil aguacates que hay.

Las herramientas salen de panxeta.py tal cual: torneado con la mancha metida
en la propia malla (nada pegado encima que pueda hacer un salto), bultos,
cordones y la verificacion. Copiarlas aqui seria tener dos versiones del
mismo codigo y arreglar los fallos dos veces.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))

import panxeta as p


# --- Medidas ----------------------------------------------------------------

ALTO = 0.268           # con los pies y el rabito
ANCHO = 0.190

CUERPO_ALTO = 0.240
CUERPO_RADIO = 0.095

# Las herramientas de panxeta colocan todo respecto a su propio Z0, que centra
# el modelo a SU altura. Aqui la altura es otra, asi que se le dice cual es.
# Es una linea y se ve; la alternativa era duplicar cuatrocientas de codigo.
p.Z0 = -ALTO * 0.5

FRENTE = math.radians(-90.0)     # hacia donde mira

COLORES = {
    "Aguacate_Piel":     (0.22, 0.38, 0.10, 1.0),
    "Aguacate_Carne":    (0.69, 0.80, 0.36, 1.0),
    "Aguacate_Hueso":    (0.30, 0.13, 0.08, 1.0),
    "Aguacate_Marron":   (0.30, 0.18, 0.10, 1.0),
    "Aguacate_Negro":    (0.06, 0.06, 0.07, 1.0),
    "Aguacate_Rosa":     (0.90, 0.55, 0.58, 1.0),
    "Aguacate_Blanco":   (0.97, 0.97, 0.95, 1.0),
}

# Perfil del cuerpo, de abajo arriba. Culo ancho y redondo, cuello estrecho y
# cupula arriba. La cintura no esta en el medio: esta a un tercio de la altura,
# que es lo que hace que se lea como aguacate y no como pera.
PERFIL = [
    (0.000, 0.000), (0.046, 0.007), (0.070, 0.020), (0.085, 0.038),
    (0.092, 0.058), (0.095, 0.080), (0.094, 0.102), (0.089, 0.124),
    (0.082, 0.146), (0.073, 0.166), (0.063, 0.185), (0.053, 0.201),
    (0.042, 0.215), (0.029, 0.228), (0.000, 0.240),
]

ACHATADO = 0.88        # relleno, no cojin


def radio(z):
    return p.radio_perfil(PERFIL, z, 0.0)


def en_superficie(ang, z, fuera=0.0):
    """Un punto sobre la piel del aguacate, en coordenadas de mundo."""
    r = radio(z) + fuera

    return Vector((math.cos(ang) * r,
                   math.sin(ang) * r * ACHATADO,
                   z + p.Z0))


def hilo(nombre, mat, puntos, grosor, lados=8):
    """Un cordon fino siguiendo puntos que ya estan sobre la superficie.

    Sirve para la boca y para las espirales de los mofletes. Los puntos vienen
    ya proyectados: asi el hilo se pega a la curva del cuerpo en vez de cruzar
    por el aire, que es lo que pasa si se dibuja en un plano y se coloca encima.
    """
    verts, faces = [], []
    n = len(puntos)

    for i, q in enumerate(puntos):
        if i == 0:
            tg = puntos[1] - puntos[0]
        elif i == n - 1:
            tg = puntos[-1] - puntos[-2]
        else:
            tg = puntos[i + 1] - puntos[i - 1]

        if tg.length < 1e-9:
            tg = Vector((0.0, 0.0, 1.0))
        tg.normalize()

        # Normal: hacia fuera del cuerpo, para que el hilo quede tumbado sobre
        # la piel y no de canto.
        fuera = Vector((q.x, q.y / (ACHATADO * ACHATADO), 0.0))
        if fuera.length < 1e-9:
            fuera = Vector((0.0, -1.0, 0.0))
        fuera.normalize()

        lado = tg.cross(fuera)
        if lado.length < 1e-9:
            lado = Vector((1.0, 0.0, 0.0))
        lado.normalize()

        for k in range(lados):
            a = 2.0 * math.pi * k / lados
            v = q + lado * (math.cos(a) * grosor) + fuera * (math.sin(a) * grosor)
            verts.append((v.x, v.y, v.z))

    for s in range(n - 1):
        for k in range(lados):
            a0 = s * lados + k
            a1 = s * lados + (k + 1) % lados
            faces.append([a0, a1, a1 + lados, a0 + lados])

    faces.append(list(range(lados - 1, -1, -1)))
    u = (n - 1) * lados
    faces.append(list(range(u, u + lados)))

    return p.malla(nombre, verts, faces, mat)


def espiral(nombre, mat, centro_ang, z_centro, radio_max, vueltas, grosor, sentido):
    """El moflete: una espiral dibujada sobre la piel.

    Es lo que distingue a este peluche. Con una mancha redonda y ya esta,
    parecerian los colores de cualquier otro aguacate de los mil que hay.
    """
    puntos = []
    pasos = int(28 * vueltas)

    for i in range(pasos + 1):
        t = i / pasos

        a = t * vueltas * 2.0 * math.pi * sentido
        r = radio_max * t

        # El desplazamiento a lo ancho se convierte en angulo dividiendo por el
        # radio del cuerpo a esa altura: si no, la espiral sale estirada de un
        # lado en cuanto el cuerpo se estrecha.
        z = z_centro + math.sin(a) * r
        rc = max(0.01, radio(z))

        puntos.append(en_superficie(centro_ang + math.cos(a) * r / rc, z, 0.0012))

    return hilo(nombre, mat, puntos, grosor)


def construir():
    p.limpiar()

    m = {n: p.material(n, c) for n, c in COLORES.items()}

    piel = m["Aguacate_Piel"]
    carne = m["Aguacate_Carne"]
    hueso = m["Aguacate_Hueso"]
    marron = m["Aguacate_Marron"]
    negro = m["Aguacate_Negro"]
    rosa = m["Aguacate_Rosa"]
    blanco = m["Aguacate_Blanco"]

    piezas = []

    # --- cuerpo, con la carne pintada en su propia malla --------------------
    # La carne NO es una pieza pegada encima. Es la misma superficie con otro
    # material, con el borde cayendo donde tiene que caer porque la rejilla se
    # amolda a el. Pegada encima se le veria el canto levantado, y eso ya se
    # probo con Panxeta y se veia.
    piezas.append(p.torneado("Cuerpo", PERFIL, piel, aplanado=ACHATADO,
                             mancha=(0.022, 0.190, FRENTE,
                                     math.radians(52.0), carne)))

    # --- el hueso -----------------------------------------------------------
    # Media bola: la mitad dentro del cuerpo y la mitad fuera.
    #
    # Por eso su centro va EXACTAMENTE sobre la piel, ni un milimetro por
    # delante. Estaba 8 mm afuera, y con eso sobresalia mas de dos tercios: se
    # veia una pelota pegada al aguacate en vez de un hueso metido en la carne.
    #
    # Y es una ESFERA, con sus tres medidas iguales. Aplastandola de fondo sale
    # un boton cosido, no un hueso: lo que sobresale tiene que ser media bola
    # con su volumen, no una chapa redonda.
    hueso_z = 0.078

    # Sentado un poco por dentro de la piel, no a media bola exacta. Con el
    # centro justo en la superficie asomaba media esfera entera y parecia
    # pegada; metido nueve milimetros se ve como un hueso al que le ha crecido
    # la carne alrededor.
    centro = en_superficie(FRENTE, hueso_z, -0.009)

    piezas.append(p.bulto("Hueso", (0.058, 0.058, 0.058),
                          (centro.x, centro.y, hueso_z),
                          hueso, segmentos=30))

    # --- la cara ------------------------------------------------------------
    # Los ojos, pequenos. En la foto miden un 6% del ancho del cuerpo, y ese
    # dato es el que decide si se parece o no: agrandandolos un poco ya no es
    # este peluche, es un personaje de dibujos.
    ojo_z = 0.168
    ojo_sep = 0.026

    for lado, sx in (("Izq", -1), ("Der", 1)):
        ang = FRENTE + sx * ojo_sep / max(0.01, radio(ojo_z))
        c = en_superficie(ang, ojo_z, -0.004)

        piezas.append(p.bulto("Ojo_" + lado, (0.012, 0.011, 0.014),
                              (c.x, c.y, ojo_z), negro, segmentos=16))

        # El brillo, arriba y hacia dentro, que es donde lo pone quien los cose.
        b = en_superficie(ang - sx * 0.10, ojo_z + 0.004, 0.0)
        piezas.append(p.bulto("Brillo_" + lado, (0.004, 0.004, 0.004),
                              (b.x, b.y, ojo_z + 0.004), blanco, segmentos=10))

    # La boca: pequena y poco honda, entre los ojos y por debajo.
    boca_z = 0.153
    boca = []

    for i in range(19):
        t = i / 18.0
        a = math.radians(200.0 + 140.0 * t)

        dx = math.cos(a) * 0.019
        dz = math.sin(a) * 0.019 * 0.55

        z = boca_z + dz + 0.019 * 0.55
        rc = max(0.01, radio(z))

        boca.append(en_superficie(FRENTE + dx / rc, z, 0.0012))

    piezas.append(hilo("Boca", negro, boca, 0.0018))

    # Los mofletes, en espiral y girando cada uno hacia su lado, como en la foto.
    for lado, sx in (("Izq", -1), ("Der", 1)):
        ang = FRENTE + sx * 0.052 / max(0.01, radio(0.152))

        piezas.append(espiral("Moflete_" + lado, rosa, ang, 0.152,
                              0.016, 1.6, 0.0022, sx))

    # --- brazos -------------------------------------------------------------
    # Munones cortos que salen del costado y caen. En la foto no tienen mano ni
    # codo: son dos porras redondeadas, estrechas donde nacen y gordas al final.
    #
    # Se barren con seccion variable, no con un cilindro mas una bola. El
    # cilindro deja ver su tapa plana por el canto y la bola hace un escalon
    # donde se juntan: de perfil se veia un trozo de tuberia pegado al cuerpo.
    for lado, sx in (("Izq", -1), ("Der", 1)):
        z0 = 0.136
        arranque = en_superficie(FRENTE + sx * math.radians(72.0), z0, -0.014)

        # Cortos y caidos, no abiertos en cruz.
        #
        # Con brazos largos y horizontales el peluche medía 300 mm de punta a
        # punta y NO cabia en la garra: la boca abierta son 318 y los dedos
        # tienen que entrar 20 mm para que cuente como agarre. Un peluche que no
        # se puede coger no sirve de nada dentro de una maquina de coger
        # peluches. En la foto ademas caen, no van en cruz.
        direccion = Vector((sx * 0.58, 0.14, -0.80)).normalized()

        camino = []
        pasos = 16

        for i in range(pasos + 1):
            t = i / pasos

            # Nace fino, engorda a media altura y se cierra en punta roma.
            if t < 0.16:
                escala = 0.66 + 0.34 * math.sin(t / 0.16 * math.pi * 0.5)
            elif t > 0.62:
                escala = 0.18 + 0.82 * math.cos((t - 0.62) / 0.38 * math.pi * 0.5)
            else:
                escala = 1.0

            # Con una curvita hacia dentro al final, que es lo que hace el peso
            # de la tela en un munon relleno.
            desvio = Vector((-sx * 0.009, 0.0, -0.011)) * (t * t)

            camino.append((direccion * (0.070 * t) + desvio, escala))

        piezas.append(p.solapa("Brazo_" + lado,
                               (arranque.x, arranque.y, arranque.z - p.Z0),
                               camino, 0.036, 0.036, marron, lados=16))

    # --- pies ---------------------------------------------------------------
    # Pequenos y abiertos hacia fuera, que es lo que le da el aire de estar de
    # pie en vez de apoyado.
    for lado, sx in (("Izq", -1), ("Der", 1)):
        piezas.append(p.bulto("Pie_" + lado, (0.034, 0.046, 0.022),
                              (sx * 0.036, -0.012, 0.010), marron,
                              giro=(0, sx * 22, 0), segmentos=18))

    # --- rabito y hoja ------------------------------------------------------
    piezas.append(p.cordon("Rabito", (0.0, 0.0, 0.236), (0.0, -0.004, 0.262),
                           0.008, marron, lados=12))

    piezas.append(p.bulto("Hoja", (0.022, 0.008, 0.012),
                          (0.014, -0.005, 0.254), piel,
                          giro=(0, 0, -24), segmentos=14))

    return piezas


def verificar(piezas):
    """Lo que hay que mirar de este peluche antes de darlo por bueno."""
    print("")
    print("=" * 68)
    print("AGUACATE")
    print("=" * 68)

    malas = [o.name for o in piezas if any(abs(v - 1.0) > 1e-4 for v in o.scale)]
    print("  piezas ....................... %d" % len(piezas))
    print("  con escala distinta de 1 ..... %s" % (", ".join(malas) if malas else "ninguna"))

    cajas = {}
    mn = Vector((1e9,) * 3)
    mx = Vector((-1e9,) * 3)

    for o in piezas:
        b = [o.matrix_world @ Vector(e) for e in o.bound_box]
        lo = Vector(tuple(min(q[i] for q in b) for i in range(3)))
        hi = Vector(tuple(max(q[i] for q in b) for i in range(3)))
        cajas[o.name] = (lo, hi)
        mn = Vector(tuple(min(mn[i], lo[i]) for i in range(3)))
        mx = Vector(tuple(max(mx[i], hi[i]) for i in range(3)))

    t = mx - mn
    print("  medidas ...................... %.3f x %.3f x %.3f m" % (t.x, t.y, t.z))

    cue = cajas["Cuerpo"]
    ancho_cuerpo = cue[1].x - cue[0].x
    print("  cuerpo ....................... %.3f m de ancho" % ancho_cuerpo)
    print("  con brazos ................... %.3f m  (x%.2f el cuerpo)"
          % (t.x, t.x / ancho_cuerpo))

    ojo = cajas["Ojo_Izq"]
    d = ojo[1].x - ojo[0].x
    print("  ojo .......................... %.0f mm  (%.0f%% del ancho del cuerpo)"
          % (d * 1000, d / ancho_cuerpo * 100))

    # Cabe en la garra? La boca abierta mide 318 mm y hace falta que los dedos
    # entren 20 mm para que cuente como agarre.
    print("")
    print("  Contra la garra (boca abierta 318 mm):")
    print("    lo mas ancho que tiene ..... %.0f mm  %s"
          % (max(t.x, t.y) * 1000,
             "cabe" if max(t.x, t.y) < 0.298 else "NO CABE, la garra no lo rodea"))

    print("")
    print("  Volumen por parte, que decide si lleva collider:")
    vols = [(n, (hi - lo).x * (hi - lo).y * (hi - lo).z) for n, (lo, hi) in sorted(cajas.items())]
    mayor = max(v for _, v in vols)

    for nombre, v in vols:
        print("    %-16s %8.1f cm3   %s" % (nombre, v * 1e6,
                                            "COLLIDER" if v >= mayor * 0.2 else "."))

    print("=" * 68)
    print("")


if __name__ == "__main__":
    piezas = construir()
    verificar(piezas)

    if "--salida" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=sys.argv[sys.argv.index("--salida") + 1])
        print("GUARDADO")

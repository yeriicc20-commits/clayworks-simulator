"""Panxeta, el peluche de perro.

Segunda version, hecha con formas y no con esferas apiladas.

La primera era una pila de elipsoides y se notaba: un peluche cosido no tiene
secciones circulares en ningun sitio. Aqui el cuerpo es un torneado con perfil
propio, que se estrecha arriba donde se cose a la cabeza; las orejas son
solapas PLANAS barridas por una curva que arranca en la superficie de la
cabeza, no bultos clavados dentro de ella; y las manos van sueltas, unidas al
cuerpo por un cordon, que es como estan en la foto.

Se construye por partes separadas a proposito: en Unity, PlushItem pone un
collider por parte, y son esas partes las que deciden como se apila y como lo
agarra la garra. De una sola malla acabaria dentro de una esfera del tamano de
todo el bicho, orejas incluidas, y no habria quien lo cogiese.
"""

import bpy
import math
import sys
from mathutils import Matrix, Vector

# Todo se construye con la base en cero y al final baja media altura, para que
# el origen quede en el centro. Un peluche cae girado al azar, y con el origen
# en los pies giraria como una peonza descentrada.
ALTO = 0.276
Z0 = -ALTO * 0.5

COLORES = {
    "Panxeta_Blanco":     (0.94, 0.93, 0.89, 1.0),
    "Panxeta_Naranja":    (0.76, 0.26, 0.06, 1.0),
    "Panxeta_NaranjaOsc": (0.48, 0.15, 0.04, 1.0),
    "Panxeta_Negro":      (0.06, 0.06, 0.07, 1.0),
}


PERFIL_CUERPO = [
    (0.000, 0.000), (0.030, 0.005), (0.048, 0.015), (0.059, 0.029),
    (0.065, 0.046), (0.066, 0.062), (0.063, 0.078), (0.056, 0.094),
    (0.045, 0.109), (0.034, 0.121), (0.026, 0.130), (0.000, 0.136),
]

# La cabeza dejo de ser una bola: mas ancha que alta, con la coronilla
# aplastada y la barbilla estrechandose. Una bola perfecta no tiene arriba ni
# abajo, y por eso el muneco no tenia expresion.
PERFIL_CABEZA = [
    (0.000, -0.072), (0.034, -0.068), (0.056, -0.058), (0.072, -0.042),
    (0.081, -0.022), (0.085, 0.000), (0.084, 0.020), (0.079, 0.038),
    (0.069, 0.052), (0.052, 0.062), (0.030, 0.068), (0.000, 0.070),
]


def limpiar():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for grupo in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
        for dato in list(grupo):
            if getattr(dato, "users", 0) == 0:
                grupo.remove(dato)


def material(nombre, color):
    if nombre in bpy.data.materials:
        return bpy.data.materials[nombre]

    mat = bpy.data.materials.new(nombre)
    mat.use_nodes = True

    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = 0.0

    # Felpa: la luz se dispersa y no hay reflejo. Con brillo parece plastico.
    bsdf.inputs["Roughness"].default_value = 0.94

    mat.diffuse_color = color
    return mat


def malla(nombre, verts, faces, mat, suave=True):
    m = bpy.data.meshes.new(nombre)
    m.from_pydata(verts, [], faces)
    m.validate()
    m.update()

    if suave:
        for p in m.polygons:
            p.use_smooth = True

    ob = bpy.data.objects.new(nombre, m)
    bpy.context.collection.objects.link(ob)
    ob.data.materials.append(mat)

    return ob


def densificar(perfil, paso=0.006):
    """Mete anillos intermedios en un perfil.

    Solo para suavizar la punta de arriba y la de abajo de una mancha, que
    son lo unico que sigue cortandose por anillos. La silueta no cambia ni
    un micron: los puntos nuevos caen sobre la misma recta que ya habia.
    """
    salida = [perfil[0]]

    for i in range(len(perfil) - 1):
        r0, z0 = perfil[i]
        r1, z1 = perfil[i + 1]

        n = max(1, int(round(math.hypot(r1 - r0, z1 - z0) / paso)))

        for k in range(1, n + 1):
            t = k / n
            salida.append((r0 + (r1 - r0) * t, z0 + (z1 - z0) * t))

    return salida


def torneado(nombre, perfil, mat, centro=(0.0, 0.0, 0.0), aplanado=1.0,
             segmentos=64, mancha=None):
    """Cuerpo de revolucion a partir de un perfil de (radio, altura).

    Es lo que permite darle silueta. Un cuerpo que se ensancha en la barriga
    y se estrecha en el cuello no sale de una esfera estirada por mucho que
    se retuerza la escala: la escala estira TODO por igual.

    La MANCHA va dentro de esta misma malla, y esa es la parte que costo.

    Primero fue una pieza aparte puesta por encima: por poco que la separes
    se le ve el borde levantado, y si no la separas parpadea. Luego fue
    pintar las caras que caian dentro, que no hace ningun salto porque es la
    propia superficie, pero el contorno solo podia seguir la rejilla y salia
    a escalones, como dibujada en un Tetris. Subir la resolucion tampoco
    valia: a 17.000 caras los escalones seguian viendose.

    Lo que se hace ahora es al reves: en vez de amoldar la mancha a la
    rejilla, se amolda la REJILLA a la mancha. Cada anillo reparte sus
    puntos en dos tramos, el que va por dentro del contorno y el que va por
    fuera, asi que el borde cae exactamente donde tiene que caer y sale una
    curva limpia. Todos los anillos llevan el mismo numero de puntos, con lo
    que la malla sigue siendo una cuadricula normal y corriente.
    """
    if mancha:
        perfil = densificar(perfil)
        z0, z1, cang, abertura = mancha[0], mancha[1], mancha[2], mancha[3]

        # Reparto los puntos segun lo que ocupa cada tramo, para que salgan
        # igual de juntos dentro y fuera de la mancha.
        dentro = max(8, int(round(segmentos * abertura / math.pi)))
        fuera = max(8, segmentos - dentro)
    else:
        dentro, fuera = 0, segmentos

    por_anillo = dentro + fuera

    def anillo(z_abs):
        # Los angulos de un anillo, y si a esa altura hay mancha.
        if not mancha:
            return [2.0 * math.pi * k / fuera for k in range(fuera)], False

        if z_abs <= z0 or z_abs >= z1:
            forma, hay = 0.03, False
        else:
            # Elipse: el borde llega perpendicular arriba y abajo y la
            # mancha queda redonda. Con un seno saldria un rombo.
            u = (z_abs - z0) / (z1 - z0)
            forma = max(0.03, math.sqrt(max(0.0, 1.0 - (2.0 * u - 1.0) ** 2)))
            hay = True

        media = abertura * forma
        lo, hi = cang - media, cang + media

        angs = [lo + (hi - lo) * k / (dentro - 1) for k in range(dentro)]

        resto = lo + 2.0 * math.pi - hi
        angs += [hi + resto * k / (fuera + 1) for k in range(1, fuera + 1)]

        return angs, hay

    verts, faces, indices, hay_mancha = [], [], [], []

    cx, cy, cz = centro[0], centro[1], centro[2] + Z0
    n = len(perfil)

    verts.append((cx, cy, cz + perfil[0][1]))

    for i in range(1, n - 1):
        r, z = perfil[i]
        angs, hay = anillo(z + centro[2])
        hay_mancha.append(hay)

        for a in angs:
            verts.append((cx + math.cos(a) * r,
                          cy + math.sin(a) * r * aplanado,
                          cz + z))

    verts.append((cx, cy, cz + perfil[-1][1]))

    anillos = n - 2
    ultimo = 1 + (anillos - 1) * por_anillo

    for k in range(por_anillo):
        k2 = (k + 1) % por_anillo
        faces.append([0, 1 + k2, 1 + k])
        indices.append(0)
        faces.append([len(verts) - 1, ultimo + k, ultimo + k2])
        indices.append(0)

    for i in range(anillos - 1):
        a0 = 1 + i * por_anillo
        a1 = a0 + por_anillo

        pinta = bool(mancha) and (hay_mancha[i] or hay_mancha[i + 1])

        for k in range(por_anillo):
            k2 = (k + 1) % por_anillo
            faces.append([a0 + k, a0 + k2, a1 + k2, a1 + k])
            indices.append(1 if (pinta and k < dentro - 1) else 0)

    ob = malla(nombre, verts, faces, mat)

    if mancha:
        ob.data.materials.append(mancha[4])

        for p, idx in zip(ob.data.polygons, indices):
            p.material_index = idx

    return ob


def bulto(nombre, tam, centro, mat, giro=None, segmentos=22):
    """Esfera estirada, para lo que SI es redondo: ojos, nariz, manchas."""
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.5, segments=segmentos,
                                         ring_count=max(6, segmentos // 2),
                                         location=(0.0, 0.0, 0.0))
    ob = bpy.context.object
    ob.name = nombre
    ob.scale = Vector(tam)

    if giro:
        ob.rotation_euler = tuple(math.radians(g) for g in giro)

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ob.location = Vector((centro[0], centro[1], centro[2] + Z0))

    for p in ob.data.polygons:
        p.use_smooth = True

    ob.data.materials.clear()
    ob.data.materials.append(mat)

    return ob


def solapa(nombre, arranque, camino, ancho, grosor, mat, lados=12):
    """Una solapa PLANA barrida por una curva: la oreja.

    Plana y no redonda porque una oreja de trapo son dos telas cosidas: ancha y
    larga, pero casi sin fondo. Y arranca EN la superficie de la cabeza,
    saliendo hacia fuera, para que no se le meta dentro.

    El origen queda en el arranque, que es la costura: es de ahi de donde tiene
    que colgar cuando se balancee.
    """
    verts, faces = [], []
    n = len(camino)

    for i, (p, escala) in enumerate(camino):
        # Seccion en ejes FIJOS: el grosor en X, que es hacia donde esta la
        # cabeza, y el ancho en Y, de delante a atras. Asi la oreja se apoya de
        # canto contra el craneo en vez de cruzarlo.
        for k in range(lados):
            a = 2.0 * math.pi * k / lados
            q = (p
                 + Vector((math.cos(a) * grosor * 0.5 * escala, 0.0, 0.0))
                 + Vector((0.0, math.sin(a) * ancho * 0.5 * escala, 0.0)))
            verts.append((q.x, q.y, q.z))

    for s in range(n - 1):
        for k in range(lados):
            a0 = s * lados + k
            a1 = s * lados + (k + 1) % lados
            faces.append([a0, a1, a1 + lados, a0 + lados])

    faces.append(list(range(lados - 1, -1, -1)))
    u = (n - 1) * lados
    faces.append(list(range(u, u + lados)))

    ob = malla(nombre, verts, faces, mat)

    # El camino ya viene RELATIVO al arranque, asi que aqui solo se coloca el
    # objeto. Trasladar ademas la malla aplicaba el arranque dos veces y las
    # orejas salian disparadas a 18 cm de la cabeza.
    ob.location = Vector((arranque[0], arranque[1], arranque[2] + Z0))

    return ob


def radio_perfil(perfil, z, centro_z):
    # Radio del torneado a una altura dada.
    zl = z - centro_z

    if zl <= perfil[0][1] or zl >= perfil[-1][1]:
        return 0.0

    for i in range(len(perfil) - 1):
        r0, z0 = perfil[i]
        r1, z1 = perfil[i + 1]

        if z0 <= zl <= z1:
            t = (zl - z0) / max(1e-6, z1 - z0)
            return r0 + (r1 - r0) * t

    return 0.0


def oreja(nombre, sx, mat, ancho=0.028, grosor=0.024):
    """Oreja corta y casi cilindrica, apoyada por fuera de la cabeza.

    Ancho y grueso casi iguales a proposito: con 42 por 20 la seccion era
    una lenteja y la oreja parecia una hoja: de frente se veia ancha y de
    perfil desaparecia. A 28 por 24 el corte es casi redondo y se ve igual
    desde cualquier lado.

    Y contorno() ya no hace forma de hoja. Se mantiene del mismo grueso casi
    todo el recorrido y solo se redondea en las dos puntas, que es lo que
    hace un tubo con los extremos cerrados.

    El camino se apoya a cada altura JUSTO por fuera del radio de la cabeza
    mientras hay cabeza, y a partir de ahi cuelga recto: por eso la x no baja
    nunca, se queda en el maximo que alcanzo. Si siguiera el perfil, pasado
    el ecuador se estrecharia con el y la punta acabaria metida detras del
    cuerpo. Como la seccion solo puede encoger, tampoco la atraviesa.
    """
    def contorno(t):
        if t < 0.08:
            u = t / 0.08
            return 0.72 + 0.28 * math.sqrt(max(0.0, 1.0 - (1.0 - u) ** 2))

        if t > 0.88:
            u = (t - 0.88) / 0.12
            return 0.10 + 0.90 * math.sqrt(max(0.0, 1.0 - u * u))

        return 1.0

    arranque_z = 0.234
    largo = 0.148
    pasos = 22

    camino = []
    base = None
    x = 0.0

    for i in range(pasos + 1):
        t = i / pasos
        z = arranque_z - largo * t

        r = radio_perfil(PERFIL_CABEZA, z, 0.196)
        x = max(x, r + grosor * 0.5 + 0.002)

        # Cae un poco hacia atras, que es por donde nace la oreja, y asi
        # ademas la punta no le pega a la mano.
        p = Vector((sx * x, 0.004 + t * 0.020, z))

        if base is None:
            base = p

        camino.append((p - base, contorno(t)))

    return solapa(nombre, (base.x, base.y, base.z), camino, ancho, grosor,
                  mat, lados=16)


def sonrisa(nombre, mat):
    """La boca: un hilo en U pegado a la cara.

    Iba en un plano vertical fijo, a una Y constante. Como la cara se va
    hacia atras conforme te separas del centro, los dos extremos de la boca
    quedaban 5 mm DENTRO de la cabeza y no se veian: por eso parecia una
    rayita corta por mucho que se ensanchara el arco.

    Ahora cada punto se apoya en la superficie: se calcula el radio de la
    cabeza a esa altura y se despeja la Y que le toca.
    """
    radio = 0.031
    grosor = 0.0019
    alto = 0.186
    aplanado = 0.90

    pasos, lados = 26, 8
    camino = []

    for i in range(pasos + 1):
        # 160 grados centrados abajo: una U, no una raya.
        a = math.radians(190.0 + 160.0 * i / pasos)

        x = math.cos(a) * radio
        z = alto + math.sin(a) * radio

        # El -0.004 es el desplazamiento de la cabeza, que no esta centrada
        # en el eje. Sin el, la boca entera quedaba 3 mm por dentro del morro y
        # no se veia ni ensanchando el arco.
        r = radio_perfil(PERFIL_CABEZA, z, 0.196)
        y = -0.004 - aplanado * math.sqrt(max(0.0, r * r - x * x)) - 0.0012

        camino.append(Vector((x, y, z + Z0)))

    verts, faces = [], []

    for i, p in enumerate(camino):
        if i == 0:
            tg = camino[1] - camino[0]
        elif i == len(camino) - 1:
            tg = camino[-1] - camino[-2]
        else:
            tg = camino[i + 1] - camino[i - 1]
        tg.normalize()

        nor = Vector((tg.z, 0.0, -tg.x))
        nor.normalize()

        for k in range(lados):
            a = 2 * math.pi * k / lados
            q = (p + nor * (math.cos(a) * grosor)
                 + Vector((0.0, math.sin(a) * grosor, 0.0)))
            verts.append((q.x, q.y, q.z))

    for t in range(len(camino) - 1):
        for k in range(lados):
            a0 = t * lados + k
            a1 = t * lados + (k + 1) % lados
            faces.append([a0, a1, a1 + lados, a0 + lados])

    faces.append(list(range(lados - 1, -1, -1)))
    u = (len(camino) - 1) * lados
    faces.append(list(range(u, u + lados)))

    return malla(nombre, verts, faces, mat)


def cordon(nombre, desde, hasta, radio, mat, lados=10):
    """El cordon que une una mano al cuerpo. En la foto las manos no salen del
    cuerpo: cuelgan de un cosido fino y se mueven sueltas."""
    a = Vector((desde[0], desde[1], desde[2] + Z0))
    b = Vector((hasta[0], hasta[1], hasta[2] + Z0))

    eje = b - a
    largo = eje.length
    eje.normalize()

    u = eje.cross(Vector((0.0, 0.0, 1.0)))
    if u.length < 0.1:
        u = eje.cross(Vector((1.0, 0.0, 0.0)))
    u.normalize()
    v = eje.cross(u).normalized()

    verts, faces = [], []

    for extremo in (0.0, largo):
        c = a + eje * extremo
        for k in range(lados):
            ang = 2 * math.pi * k / lados
            q = c + u * (math.cos(ang) * radio) + v * (math.sin(ang) * radio)
            verts.append((q.x, q.y, q.z))

    for k in range(lados):
        k2 = (k + 1) % lados
        faces.append([k, k2, k2 + lados, k + lados])

    faces.append(list(range(lados - 1, -1, -1)))
    faces.append(list(range(lados, lados * 2)))

    return malla(nombre, verts, faces, mat)


def construir():
    limpiar()

    m = {k: material(k, v) for k, v in COLORES.items()}
    blanco = m["Panxeta_Blanco"]
    naranja = m["Panxeta_Naranja"]
    naranja_osc = m["Panxeta_NaranjaOsc"]
    negro = m["Panxeta_Negro"]

    piezas = []

    # --- cuerpo: pera, ancha abajo y estrecha donde se cose a la cabeza -----
    perfil_cuerpo = PERFIL_CUERPO
    _sin_usar = [
        (0.000, 0.000), (0.030, 0.005), (0.048, 0.015), (0.059, 0.029),
        (0.065, 0.046), (0.066, 0.062), (0.063, 0.078), (0.056, 0.094),
        (0.045, 0.109), (0.034, 0.121), (0.026, 0.130), (0.000, 0.136),
    ]
    piezas.append(torneado("Cuerpo", perfil_cuerpo, blanco, aplanado=0.92,
                           mancha=(0.026, 0.094, math.radians(-90.0),
                                   math.radians(48.0), naranja)))

    # --- cabeza: mas ancha que el cuerpo y algo achatada --------------------
    piezas.append(torneado("Cabeza", PERFIL_CABEZA, blanco,
                           centro=(0.0, -0.004, 0.196), aplanado=0.90,
                           mancha=(0.186, 0.264, math.radians(-52.0),
                                   math.radians(33.0), naranja)))

    # --- orejas: solapas planas que arrancan EN la cabeza -------------------
    piezas.append(oreja("Oreja_Izq", -1, negro))
    piezas.append(oreja("Oreja_Der", 1, negro))

    # --- cara: ojos y nariz pequenos ----------------------------------------
    piezas.append(bulto("Ojo_Izq", (0.013, 0.010, 0.014),
                        (-0.029, -0.073, 0.214), negro, segmentos=16))
    piezas.append(bulto("Ojo_Der", (0.013, 0.010, 0.014),
                        (0.038, -0.070, 0.216), negro, segmentos=16))

    piezas.append(bulto("Nariz", (0.038, 0.032, 0.030),
                        (0.0, -0.078, 0.202), negro, segmentos=20))

    piezas.append(sonrisa("Boca", negro))

    piezas.append(bulto("Ombligo", (0.015, 0.013, 0.015),
                        (0.0, -0.0552, 0.058), naranja_osc, segmentos=16))

    # --- manos naranjas colgando de un cordon --------------------------------
    for lado, sx in (("Izq", -1), ("Der", 1)):
        # El cordon es NEGRO y fino: en la foto la mano cuelga de un cosido,
        # no de un brazo. A 7,5 mm y en naranja parecia un brazo corto.
        piezas.append(cordon("Cordon_" + lado,
                             (sx * 0.052, -0.008, 0.084),
                             (sx * 0.074, -0.008, 0.078), 0.0042, negro))

        piezas.append(bulto("Mano_" + lado, (0.040, 0.036, 0.046),
                            (sx * 0.084, -0.008, 0.074), naranja,
                            giro=(0, sx * -12, 0), segmentos=18))

    # --- pies pequenos -------------------------------------------------------
    for lado, sx in (("Izq", -1), ("Der", 1)):
        piezas.append(bulto("Pie_" + lado, (0.038, 0.048, 0.024),
                            (sx * 0.026, -0.016, 0.012), naranja, segmentos=18))

    piezas.append(bulto("Cola", (0.024, 0.030, 0.024),
                        (0.0, 0.058, 0.072), naranja, segmentos=16))

    return piezas


def verificar(piezas):
    print("\n" + "=" * 68)
    print("PANXETA")
    print("=" * 68)

    malas = [o.name for o in piezas if any(abs(v - 1.0) > 1e-4 for v in o.scale)]
    print("  piezas ....................... %d" % len(piezas))
    print("  con escala distinta de 1 ..... %s" % (", ".join(malas) if malas else "ninguna"))

    cajas = {}
    mn = Vector((1e9,) * 3)
    mx = Vector((-1e9,) * 3)

    for o in piezas:
        b = [o.matrix_world @ Vector(e) for e in o.bound_box]
        lo = Vector(tuple(min(p[i] for p in b) for i in range(3)))
        hi = Vector(tuple(max(p[i] for p in b) for i in range(3)))
        cajas[o.name] = (lo, hi)
        mn = Vector(tuple(min(mn[i], lo[i]) for i in range(3)))
        mx = Vector(tuple(max(mx[i], hi[i]) for i in range(3)))

    t = mx - mn
    print("  medidas ...................... %.3f x %.3f x %.3f m" % (t.x, t.y, t.z))

    cab = cajas["Cabeza"]
    cue = cajas["Cuerpo"]
    print("  cabeza ....................... %.3f m de ancho" % (cab[1].x - cab[0].x))
    print("  cuerpo ....................... %.3f m de ancho" % (cue[1].x - cue[0].x))

    # La oreja no puede quedarse metida dentro de la cabeza
    print("\n  Orejas contra la cabeza:")
    for nombre in ("Oreja_Izq", "Oreja_Der"):
        lo, hi = cajas[nombre]
        fuera = lo.x < cab[0].x + 0.002 or hi.x > cab[1].x - 0.002
        cuelga = lo.z < cab[0].z - 0.02
        print("    %-12s x[%6.3f %6.3f]  z hasta %6.3f   %s" % (
            nombre, lo.x, hi.x, lo.z,
            "sale por fuera y cuelga" if (fuera and cuelga) else "REVISAR"))

    print("\n  Volumen por parte, que decide si lleva collider:")
    vols = [(n, (hi - lo).x * (hi - lo).y * (hi - lo).z) for n, (lo, hi) in sorted(cajas.items())]
    mayor = max(v for _, v in vols)

    for nombre, v in vols:
        print("    %-16s %8.1f cm3   %s" % (nombre, v * 1e6,
                                            "COLLIDER" if v >= mayor * 0.2 else "."))

    print("=" * 68 + "\n")
    return len(malas)


if __name__ == "__main__":
    piezas = construir()
    verificar(piezas)

    if "--salida" in sys.argv:
        bpy.ops.wm.save_as_mainfile(filepath=sys.argv[sys.argv.index("--salida") + 1])
        print("GUARDADO")

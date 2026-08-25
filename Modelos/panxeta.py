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

PERFIL_CABEZA = [
    (0.000, -0.078), (0.030, -0.073), (0.052, -0.061), (0.068, -0.043),
    (0.077, -0.021), (0.080, 0.003), (0.077, 0.027), (0.068, 0.049),
    (0.051, 0.066), (0.029, 0.076), (0.000, 0.080),
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


def torneado(nombre, perfil, mat, centro=(0.0, 0.0, 0.0), aplanado=1.0,
             segmentos=32):
    """Cuerpo de revolucion a partir de un perfil de (radio, altura).

    Es lo que permite darle silueta. Un cuerpo que se ensancha en la barriga y
    se estrecha en el cuello no sale de una esfera estirada por mucho que se
    retuerza la escala: la escala estira TODO por igual.

    El aplanado lo achata en Y, que un peluche cosido siempre es mas ancho que
    hondo.
    """
    verts, faces = [], []

    cx, cy, cz = centro[0], centro[1], centro[2] + Z0
    n = len(perfil)

    # El perfil empieza y acaba en radio cero: esos dos son los polos.
    verts.append((cx, cy, cz + perfil[0][1]))

    for i in range(1, n - 1):
        r, z = perfil[i]
        for k in range(segmentos):
            a = 2.0 * math.pi * k / segmentos
            verts.append((cx + math.cos(a) * r,
                          cy + math.sin(a) * r * aplanado,
                          cz + z))

    verts.append((cx, cy, cz + perfil[-1][1]))

    anillos = n - 2
    ultimo = 1 + (anillos - 1) * segmentos

    for k in range(segmentos):
        k2 = (k + 1) % segmentos
        faces.append([0, 1 + k2, 1 + k])
        faces.append([len(verts) - 1, ultimo + k, ultimo + k2])

    for i in range(anillos - 1):
        a0 = 1 + i * segmentos
        a1 = 1 + (i + 1) * segmentos
        for k in range(segmentos):
            k2 = (k + 1) % segmentos
            faces.append([a0 + k, a0 + k2, a1 + k2, a1 + k])

    return malla(nombre, verts, faces, mat)


def parche(nombre, perfil, centro, aplanado, mat, z0, z1, ang, abertura,
           sep=0.0009, grosor=0.0016, filas=12, columnas=16):
    """Una MANCHA sobre la superficie, no un bulto pegado encima.

    Antes las manchas eran esferas aplastadas puestas sobre la cabeza, y se
    veian como lo que eran: pegotes que sobresalen. Una mancha de un peluche
    es tela de otro color cosida al patron: sigue la curva exacta de la
    pieza y no abulta.

    Asi que se genera a partir del MISMO perfil que la pieza sobre la que
    va, muestreando su superficie en una ventana de alturas y angulos, y
    separandola menos de un milimetro para que se dibuje por delante.

    La ventana se estrecha por arriba y por abajo, con lo que la mancha sale
    ovalada en vez de un rectangulo pegado.
    """
    def radio(z):
        zl = z - centro[2]

        if zl <= perfil[0][1] or zl >= perfil[-1][1]:
            return 0.0

        for i in range(len(perfil) - 1):
            r0, za = perfil[i]
            r1, zb = perfil[i + 1]

            if za <= zl <= zb:
                t = (zl - za) / max(1e-6, zb - za)
                return r0 + (r1 - r0) * t

        return 0.0

    verts, faces = [], []

    for fuera in (grosor, 0.0):
        for i in range(filas + 1):
            u = i / filas
            z = z0 + (z1 - z0) * u

            # Elipse, no seno. Con seno los extremos se cierran en punta y la
            # mancha sale con forma de rombo; con la raiz el borde llega
            # perpendicular y queda redondo.
            forma = math.sqrt(max(0.0, 1.0 - (2.0 * u - 1.0) ** 2))
            media = abertura * max(0.06, forma)

            r = radio(z) + sep + fuera

            for k in range(columnas + 1):
                a = ang - media + 2.0 * media * k / columnas
                verts.append((math.cos(a) * r + centro[0],
                              math.sin(a) * r * aplanado + centro[1],
                              z + centro[2] * 0.0 + Z0))

    porCapa = (filas + 1) * (columnas + 1)

    for capa in (0, 1):
        base = capa * porCapa
        for i in range(filas):
            for k in range(columnas):
                a0 = base + i * (columnas + 1) + k
                a1 = a0 + 1
                b0 = a0 + (columnas + 1)
                b1 = b0 + 1

                if capa == 0:
                    faces.append([a0, a1, b1, b0])
                else:
                    faces.append([a0, b0, b1, a1])

    # El canto, para que no se vea una lamina de papel por el borde
    for i in range(filas):
        for lado in (0, columnas):
            a0 = i * (columnas + 1) + lado
            b0 = a0 + (columnas + 1)
            faces.append([a0, b0, b0 + porCapa, a0 + porCapa])

    for k in range(columnas):
        for fila in (0, filas):
            a0 = fila * (columnas + 1) + k
            faces.append([a0, a0 + 1, a0 + 1 + porCapa, a0 + porCapa])

    return malla(nombre, verts, faces, mat)


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
        # La seccion va en ejes FIJOS: el ancho en X y el grosor en Y, siempre.
        #
        # Antes giraba siguiendo el camino, y como el camino baja y se abre, la
        # solapa se abria en abanico: parecian alas de murcielago. Una oreja de
        # trapo no hace eso. Es un panel plano que cuelga, y su cara mira
        # siempre al mismo sitio por mucho que la punta se curve.
        for k in range(lados):
            a = 2.0 * math.pi * k / lados
            q = (p
                 + Vector((math.cos(a) * ancho * 0.5 * escala, 0.0, 0.0))
                 + Vector((0.0, math.sin(a) * grosor * 0.5 * escala, 0.0)))
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


PERFIL_CABEZA = [
    (0.000, -0.078), (0.030, -0.073), (0.052, -0.061), (0.068, -0.043),
    (0.077, -0.021), (0.080, 0.003), (0.077, 0.027), (0.068, 0.049),
    (0.051, 0.066), (0.029, 0.076), (0.000, 0.080),
]

CABEZA_Z = 0.196


def radio_cabeza(z):
    """Radio de la cabeza a una altura dada, interpolando su perfil."""
    zl = z - CABEZA_Z

    if zl <= PERFIL_CABEZA[0][1] or zl >= PERFIL_CABEZA[-1][1]:
        return 0.0

    for i in range(len(PERFIL_CABEZA) - 1):
        r0, z0 = PERFIL_CABEZA[i]
        r1, z1 = PERFIL_CABEZA[i + 1]

        if z0 <= zl <= z1:
            t = (zl - z0) / max(1e-6, z1 - z0)
            return r0 + (r1 - r0) * t

    return 0.0


def oreja(nombre, sx, mat, ancho=0.058, grosor=0.017):
    """Panel plano colgando del lado de la cabeza.

    Se cose POR DENTRO de la silueta a proposito: el arranque queda
    enterrado en la cabeza, que es lo que hace una costura de verdad y
    ademas evita que se vea el canto de la tela por donde se une. Lo que
    asoma es de media oreja para abajo.

    Antes seguia el contorno del craneo y quedaba peor: al ir apartandose
    conforme bajaba, acababa a cuatro centimetros de la cabeza con aire en
    medio. Una oreja cuelga; no se separa.
    """
    arranque_z = 0.216
    largo = 0.164
    pasos = 16

    x_arriba = 0.066
    x_abajo = 0.078

    camino = []
    base = None

    for i in range(pasos + 1):
        t = i / pasos

        # Se abre un poco al bajar y la punta se vuelve hacia dentro, que
        # es lo que hace el peso de la tela.
        x = x_arriba + (x_abajo - x_arriba) * math.sin(math.pi * min(1.0, t * 1.15))
        atras = -0.014 + t * 0.008
        z = arranque_z - largo * t

        escala = 1.0 - 0.34 * t * t

        p = Vector((sx * x, atras, z))
        if base is None:
            base = p

        camino.append((p - base, escala))

    return solapa(nombre, (base.x, base.y, base.z), camino, ancho, grosor, mat)


def sonrisa(nombre, mat):
    """La boca: un hilo. En la foto es una costura fina y ancha."""
    # Ancha y poco honda. Antes abarcaba 116 grados y bajaba mucho: salia una U
    # en vez de una sonrisa.
    radio = 0.036
    grosor = 0.0018
    centro = Vector((0.0, -0.074, 0.178 + Z0))

    pasos, lados = 22, 8
    camino = []

    for i in range(pasos + 1):
        a = math.radians(208.0 + 124.0 * i / pasos)
        camino.append(Vector((math.cos(a) * radio, 0.0, math.sin(a) * radio * 0.52)))

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

        for k in range(lados):
            a = 2 * math.pi * k / lados
            q = p + nor * (math.cos(a) * grosor) + Vector((0.0, math.sin(a) * grosor, 0.0))
            verts.append((q.x + centro.x, q.y + centro.y, q.z + centro.z))

    for s in range(len(camino) - 1):
        for k in range(lados):
            a0 = s * lados + k
            a1 = s * lados + (k + 1) % lados
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
    piezas.append(torneado("Cuerpo", perfil_cuerpo, blanco, aplanado=0.92))

    # --- cabeza: mas ancha que el cuerpo y algo achatada --------------------
    piezas.append(torneado("Cabeza", PERFIL_CABEZA, blanco,
                           centro=(0.0, -0.004, 0.196), aplanado=0.94))

    # --- orejas: solapas planas que arrancan EN la cabeza -------------------
    piezas.append(oreja("Oreja_Izq", -1, negro))
    piezas.append(oreja("Oreja_Der", 1, negro))

    # --- mancha del ojo -----------------------------------------------------
    # UNA sola, sobre un ojo. Antes habia dos y ademas eran bultos pegados: la
    # cabeza parecia tener chichones.
    piezas.append(parche("Mancha_Ojo", PERFIL_CABEZA, (0.0, -0.004, 0.196), 0.94,
                         naranja, 0.194, 0.240, math.radians(-52.0),
                         math.radians(29.0)))

    # --- cara: ojos y nariz pequenos ----------------------------------------
    piezas.append(bulto("Ojo_Izq", (0.013, 0.010, 0.014),
                        (-0.029, -0.073, 0.214), negro, segmentos=16))
    piezas.append(bulto("Ojo_Der", (0.013, 0.010, 0.014),
                        (0.038, -0.070, 0.216), negro, segmentos=16))

    piezas.append(bulto("Nariz", (0.038, 0.032, 0.030),
                        (0.0, -0.078, 0.202), negro, segmentos=20))

    piezas.append(sonrisa("Boca", negro))

    # --- barriga y ombligo ---------------------------------------------------
    # La barriga tambien es una mancha cosida, no un bulto: se superponia al
    # cuerpo y quedaba como una pelota metida por delante.
    piezas.append(parche("Barriga", PERFIL_CUERPO, (0.0, 0.0, 0.0), 0.92,
                         naranja, 0.026, 0.094, math.radians(-90.0),
                         math.radians(46.0)))

    piezas.append(parche("Ombligo", PERFIL_CUERPO, (0.0, 0.0, 0.0), 0.92,
                         naranja_osc, 0.052, 0.064, math.radians(-90.0),
                         math.radians(7.0), sep=0.0028, filas=6, columnas=8))

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

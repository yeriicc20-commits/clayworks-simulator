"""
Maquina de gancho estilo "LA PINZA XL", sobre el esqueleto apto para fisica.

Manda la fisica: las piezas que articulan (Cabeza y Dedo_1..3) conservan su
origen en el punto de giro y su escala (1,1,1). Todo lo decorativo se anade
alrededor sin tocarlas.
"""

import bpy
import math
from mathutils import Matrix, Vector

# --- Medidas ----------------------------------------------------------------

# Maquina grande de centro comercial: ancha de frente y menos honda, que es la
# proporcion real. Cuadrada no la hace nadie: el escaparate se mira de frente y
# el fondo hay que poder verlo.
ANCHO = 1.30
FONDO = 0.95

BASE_Z = 0.10          # ruedas y zocalo
# Con 1,30 m de ancho la maquina se veia achaparrada. El escaparate sube a
# 1,07 m de alto, que es donde caben tres alturas de peluche y donde la vista
# del jugador cae a media altura en vez de por encima de todo.
MUEBLE_Z = 0.98        # arriba del mueble bajo
JUEGO_Z = 2.05         # arriba del cristal
CARTEL_Z = 2.45        # punta del arco

GROSOR_MARCO = 0.045
GROSOR_CRISTAL = 0.008
GROSOR_PANEL = 0.02

HUECO = 0.30           # por 20 cm no pasa comodo un oso de 25
ALTO_BOCA = 0.11        # paredes de cristal que impiden que se cuelen peluches
TOLVA_SUELO = 0.195     # fondo del conducto: su cara de arriba queda a ras
                        # del borde de abajo de la trampilla, para que el hueco
                        # que se ve por la puerta sea el suelo y no una repisa
TOLVA_PARED = 0.010
CHAPA = 0.02            # grosor de la carroceria del mueble

# Donde cae la boca del premio, en la esquina delantera izquierda del hueco
# de juego. Todo lo de abajo se coloca respecto a esto.
INTERIOR = ANCHO - GROSOR_MARCO * 2.0
INTERIOR_Y = FONDO - GROSOR_MARCO * 2.0
MITAD = INTERIOR * 0.5
MITAD_Y = INTERIOR_Y * 0.5
HX = -MITAD + HUECO * 0.5
HY = -MITAD_Y + HUECO * 0.5

# Hueco vaciado dentro del mueble por donde baja el peluche. El mueble NO
# puede ser un bloque macizo: si lo es, el peluche cae dentro del conducto
# y se queda clavado en el relleno.
#
# Se calcula, no se escribe a mano: antes eran numeros atados a los 0,80 que
# medía la maquina, y al ensancharla se habrian quedado descolocados en
# silencio.
VX0 = -ANCHO * 0.5 + CHAPA
VY0 = -FONDO * 0.5 + CHAPA
VX1 = HX + HUECO * 0.5 + 0.075
VY1 = HY + HUECO * 0.5 + 0.115
VZ0 = 0.140

# Boca de la trampilla, recortada en la chapa frontal
PX0 = HX - 0.115
PX1 = HX + 0.115
PZ1 = 0.615
RIEL_Z = JUEGO_Z - 0.05    # el portico, justo bajo el techo
CABLE_Z = RIEL_Z - 0.10    # de donde cuelga la garra en reposo
# --- Garra XL, medidas del fabricante ---------------------------------------
# 24 cm de alto, 21,5 cm de envergadura, brazo de 9 cm. Es la talla para
# premios de 200-300 mm, que es el tamano de estos peluches.

MOTOR_RADIO = 0.028
MOTOR_ALTO = 0.075
COLLAR_ALTO = 0.018
CONO_ALTO = 0.048
CONO_RADIO = 0.040

CABEZA_ALTO = MOTOR_ALTO + COLLAR_ALTO + CONO_ALTO

BISAGRA_RADIO = 0.030
BRAZO_RADIO = 0.106          # radio de curvatura del brazo
BRAZO_ARCO = 96.0            # grados que abre
GANCHO_RADIO = 0.038         # el gancho de la punta
GANCHO_ARCO = 70.0

# Seccion del brazo. Los brazos reales no son chapa plana: son pletinas
# gruesas, redondeadas por los cantos, y se afinan hacia la punta.
ANCHO_DEDO = 0.026
GRUESO_DEDO = 0.017
AFILADO_DEDO = 0.42          # cuanto adelgaza de la bisagra a la punta

# --- Paleta, sacada de la foto ----------------------------------------------

COLORES = {
    "Turquesa":  (0.13, 0.72, 0.80, 1.0),
    "Verde":     (0.42, 0.85, 0.45, 1.0),
    "VerdeOsc":  (0.10, 0.45, 0.38, 1.0),
    "Morado":    (0.55, 0.22, 0.72, 1.0),
    "Rosa":      (0.92, 0.28, 0.55, 1.0),
    "Amarillo":  (0.99, 0.82, 0.18, 1.0),
    "Blanco":    (0.95, 0.96, 0.96, 1.0),
    "Negro":     (0.06, 0.07, 0.09, 1.0),
    "Metal":     (0.72, 0.74, 0.78, 1.0),
    "MetalOsc":  (0.22, 0.23, 0.26, 1.0),
    "Dorado":    (0.85, 0.66, 0.16, 1.0),
    "Goma":      (0.10, 0.10, 0.11, 1.0),
    "Azul":      (0.10, 0.35, 0.85, 1.0),
}


def limpiar():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

    for grupo in (bpy.data.meshes, bpy.data.materials, bpy.data.curves):
        for dato in list(grupo):
            if getattr(dato, "users", 0) == 0:
                grupo.remove(dato)


def material(nombre, color, metalico=0.0, rugosidad=0.45, emision=0.0, alfa=1.0):
    if nombre in bpy.data.materials:
        return bpy.data.materials[nombre]

    mat = bpy.data.materials.new(nombre)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]

    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metalico
    bsdf.inputs["Roughness"].default_value = rugosidad

    if emision > 0.0:
        bsdf.inputs["Emission Color"].default_value = color
        bsdf.inputs["Emission Strength"].default_value = emision

    if alfa < 1.0:
        bsdf.inputs["Alpha"].default_value = alfa
        mat.blend_method = "BLEND"

    # Se guarda tambien en el color de vista, que es lo que ve Unity al importar
    mat.diffuse_color = color

    return mat


def pintar(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    return ob


def caja(nombre, tam, centro, origen=None, mat=None, giro=None):
    """Caja con escala aplicada y el origen exactamente donde se pida.

    El giro, si se pide, se aplica a la MALLA alrededor del origen, no al
    objeto. Asi la pieza puede quedar inclinada (una rampa, por ejemplo) y
    seguir teniendo rotacion cero y escala (1,1,1), que es lo que PhysX
    necesita para no deformar anclajes ni sesgar el tensor de inercia.
    """
    if origen is None:
        origen = centro

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.0))
    ob = bpy.context.object
    ob.name = nombre

    ob.scale = Vector(tam)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    ob.data.transform(Matrix.Translation(Vector(centro) - Vector(origen)))

    if giro:
        rot = (Matrix.Rotation(math.radians(giro[0]), 4, "X")
               @ Matrix.Rotation(math.radians(giro[1]), 4, "Y")
               @ Matrix.Rotation(math.radians(giro[2]), 4, "Z"))
        ob.data.transform(rot)

    ob.location = Vector(origen)

    if mat:
        pintar(ob, mat)

    return ob


def cilindro(nombre, radio, alto, centro, eje="Z", mat=None, lados=24):
    bpy.ops.mesh.primitive_cylinder_add(radius=radio, depth=alto, vertices=lados,
                                        location=(0.0, 0.0, 0.0))
    ob = bpy.context.object
    ob.name = nombre

    if eje == "X":
        ob.rotation_euler = (0.0, math.radians(90), 0.0)
    elif eje == "Y":
        ob.rotation_euler = (math.radians(90), 0.0, 0.0)

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

    ob.location = Vector(centro)

    if mat:
        pintar(ob, mat)

    return ob


def arco(nombre, ancho, grosor, base_z, alto_recto, flecha, centro_y, mat=None, pasos=22):
    """Panel con la parte de arriba curvada, como el cartel de la foto.

    El contorno se recorre entero y en orden: esquina inferior izquierda, sube
    por el lado, describe la curva de izquierda a derecha, y baja al otro lado.
    La version anterior lo cerraba con UNA sola esquina de abajo y salia un
    triangulo, que es lo que se veia en el render: una tienda de campana.
    """
    mitad = ancho * 0.5
    arriba = base_z + alto_recto

    contorno = [(-mitad, base_z), (-mitad, arriba)]

    for i in range(pasos + 1):
        t = i / pasos
        contorno.append((-mitad + ancho * t, arriba + math.sin(math.pi * t) * flecha))

    contorno.append((mitad, arriba))
    contorno.append((mitad, base_z))

    n = len(contorno)

    verts = []
    faces = []

    for signo in (-1, 1):
        y = centro_y + signo * grosor * 0.5
        for (x, z) in contorno:
            verts.append((x, y, z))

    # Caras laterales
    for i in range(n):
        j = (i + 1) % n
        faces.append([i, j, j + n, i + n])

    # Tapas
    faces.append(list(range(n - 1, -1, -1)))
    faces.append(list(range(n, n + n)))

    malla = bpy.data.meshes.new(nombre)
    malla.from_pydata(verts, [], faces)
    malla.validate()
    malla.update()

    ob = bpy.data.objects.new(nombre, malla)
    bpy.context.collection.objects.link(ob)

    if mat:
        pintar(ob, mat)

    return ob


def techo_arco(nombre, ancho, base_z, alto_recto, flecha, y0, y1, grosor,
               mat=None, pasos=22):
    """Tapa curva que sigue la cupula del cartel, de delante a atras.

    Antes aqui habia un panel plano a media altura: el arco le sobresalia por
    encima y la marquesina parecia una caja con una aleta pegada. Una
    marquesina real es un cajon de luz cuyo techo acompana la curva.
    """
    mitad = ancho * 0.5
    arriba = base_z + alto_recto

    puntos = []
    for i in range(pasos + 1):
        t = i / pasos
        puntos.append(Vector((-mitad + ancho * t, 0.0,
                              arriba + math.sin(math.pi * t) * flecha)))

    n = len(puntos)
    verts, faces = [], []

    for i, p in enumerate(puntos):
        if i == 0:
            tg = puntos[1] - puntos[0]
        elif i == n - 1:
            tg = puntos[-1] - puntos[-2]
        else:
            tg = puntos[i + 1] - puntos[i - 1]

        tg.normalize()
        nor = Vector((-tg.z, 0.0, tg.x))
        if nor.z < 0.0:
            nor = -nor

        fuera = p + nor * grosor

        # Cuatro vertices por punto: dentro y fuera, en cada cara del cajon.
        verts.append((p.x, y0, p.z))
        verts.append((fuera.x, y0, fuera.z))
        verts.append((p.x, y1, p.z))
        verts.append((fuera.x, y1, fuera.z))

    for i in range(n - 1):
        a = i * 4
        b = (i + 1) * 4
        faces.append([a + 1, a + 3, b + 3, b + 1])   # cara de fuera
        faces.append([a + 0, b + 0, b + 2, a + 2])   # cara de dentro
        faces.append([a + 0, a + 1, b + 1, b + 0])   # canto delantero
        faces.append([a + 2, b + 2, b + 3, a + 3])   # canto trasero

    u = (n - 1) * 4
    faces.append([0, 1, 3, 2])
    faces.append([u + 0, u + 2, u + 3, u + 1])

    malla = bpy.data.meshes.new(nombre)
    malla.from_pydata(verts, [], faces)
    malla.validate()
    malla.update()

    ob = bpy.data.objects.new(nombre, malla)
    bpy.context.collection.objects.link(ob)

    if mat:
        pintar(ob, mat)

    return ob


def dedo(indice, angulo_grados, mat):
    """Brazo curvo de garra XL: sale abierto y termina en gancho hacia dentro.

    Medidas de la tabla del fabricante para prender premios de 200-300 mm:
    24 cm de alto total, 21,5 cm de envergadura, brazo de 9 cm.

    La seccion se barre siguiendo la tangente del camino, con perfil octogonal
    y afilandose hacia la punta. La version anterior desplazaba los vertices en
    X fijo, asi que en cuanto el brazo se inclinaba la seccion se aplastaba y
    parecia recortada en carton. Un brazo con volumen no es solo estetica: es
    lo que luego permite darle un collider decente en vez de una lamina.

    El origen queda EN LA BISAGRA, que es el punto por el que pivota contra la
    carcasa. Con eso, en Unity la bisagra es anchor = Vector3.zero.

    Ojo con el eje: el arco se dibuja en el plano X-Z local, asi que el eje de
    giro del dedo es su Y LOCAL, no el X.
    """
    a = math.radians(angulo_grados)
    fuera = Vector((math.cos(a), math.sin(a), 0.0))
    bisagra = fuera * BISAGRA_RADIO + Vector((0.0, 0.0, CABLE_Z - CABEZA_ALTO))

    # Camino del brazo, en coordenadas locales a la bisagra. Dos tramos: uno
    # que abre hacia fuera y hacia abajo, y el gancho final que cierra hacia
    # dentro, que es lo que engancha al peluche por debajo.
    camino = []
    pasos = 16

    for i in range(pasos + 1):
        t = i / pasos
        ang = math.radians(BRAZO_ARCO) * t
        camino.append(Vector((BRAZO_RADIO * math.sin(ang), 0.0,
                              -BRAZO_RADIO * (1.0 - math.cos(ang)))))

    base = camino[-1]
    tangente = (camino[-1] - camino[-2]).normalized()
    # Hacia DENTRO: es el gancho que prende al peluche por debajo. Con el signo
    # contrario la punta se abre hacia fuera y no engancharia nada.
    normal = Vector((tangente.z, 0.0, -tangente.x))

    for i in range(1, 7):
        t = i / 6.0
        ang = math.radians(GANCHO_ARCO) * t
        camino.append(base + tangente * (GANCHO_RADIO * math.sin(ang))
                      + normal * (GANCHO_RADIO * (1.0 - math.cos(ang))))

    # --- Barrido de la seccion a lo largo del camino ------------------------
    lados = 8
    perfil = [(math.cos(2 * math.pi * k / lados), math.sin(2 * math.pi * k / lados))
              for k in range(lados)]

    verts, faces = [], []
    ultimo = len(camino) - 1

    for i, p in enumerate(camino):
        # Tangente centrada: en los extremos se usa el vecino que hay.
        if i == 0:
            tang = (camino[1] - camino[0]).normalized()
        elif i == ultimo:
            tang = (camino[-1] - camino[-2]).normalized()
        else:
            tang = (camino[i + 1] - camino[i - 1]).normalized()

        # El camino vive en X-Z, asi que la seccion se abre en la normal (que
        # tambien esta en X-Z) y en Y, y queda siempre perpendicular al brazo.
        nor = Vector((tang.z, 0.0, -tang.x))

        k = 1.0 - AFILADO_DEDO * (i / ultimo)
        grueso = GRUESO_DEDO * 0.5 * k
        ancho = ANCHO_DEDO * 0.5 * k

        for (cx, cy) in perfil:
            q = p + nor * (cx * grueso) + Vector((0.0, cy * ancho, 0.0))
            verts.append((q.x, q.y, q.z))

    for s in range(ultimo):
        for k in range(lados):
            a0 = s * lados + k
            a1 = s * lados + (k + 1) % lados
            faces.append([a0, a1, a1 + lados, a0 + lados])

    faces.append(list(range(lados - 1, -1, -1)))
    faces.append(list(range(ultimo * lados, ultimo * lados + lados)))

    # --- Manguito de la bisagra ---------------------------------------------
    # El nudillo grueso que se ve en la foto donde el brazo entra en la
    # carcasa. Va en el mismo mesh y centrado en el origen, asi que no mueve
    # el pivote.
    r = GRUESO_DEDO * 0.70
    h = ANCHO_DEDO * 1.25
    desde = len(verts)

    for signo in (-1, 1):
        for k in range(lados):
            ang = 2 * math.pi * k / lados
            verts.append((r * math.cos(ang), signo * h * 0.5, r * math.sin(ang)))

    for k in range(lados):
        k2 = (k + 1) % lados
        faces.append([desde + k, desde + k2, desde + lados + k2, desde + lados + k])

    faces.append([desde + k for k in range(lados - 1, -1, -1)])
    faces.append([desde + lados + k for k in range(lados)])

    malla = bpy.data.meshes.new("Dedo_%d" % indice)
    malla.from_pydata(verts, [], faces)
    malla.validate()
    malla.update()

    for poligono in malla.polygons:
        poligono.use_smooth = True

    ob = bpy.data.objects.new("Dedo_%d" % indice, malla)
    bpy.context.collection.objects.link(ob)

    ob.rotation_euler = (0.0, 0.0, a)
    ob.location = bisagra

    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    ob.select_set(False)

    if mat:
        pintar(ob, mat)

    return ob


def esfera(nombre, radio, centro, mat=None, segmentos=20):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radio, segments=segmentos,
                                         ring_count=segmentos // 2,
                                         location=(0.0, 0.0, 0.0))
    ob = bpy.context.object
    ob.name = nombre
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ob.location = Vector(centro)

    for poligono in ob.data.polygons:
        poligono.use_smooth = True

    if mat:
        pintar(ob, mat)

    return ob


def unir(principal, extras, origen):
    """Funde varias piezas en una y deja el origen donde se pida."""
    bpy.ops.object.select_all(action="DESELECT")

    for parte in [principal] + list(extras):
        parte.select_set(True)

    bpy.context.view_layer.objects.active = principal
    bpy.ops.object.join()

    principal.data.transform(Matrix.Translation(principal.location - Vector(origen)))
    principal.location = Vector(origen)

    bpy.ops.object.select_all(action="DESELECT")
    return principal


def construir():
    limpiar()

    m = {k: material(k, v) for k, v in COLORES.items()}
    m["Cristal"] = material("Cristal", (0.75, 0.85, 0.90, 1.0), 0.0, 0.05, 0.0, 0.14)
    m["LED"] = material("LED", (0.85, 0.45, 0.95, 1.0), 0.0, 0.3, 4.0)
    m["LEDAzul"] = material("LEDAzul", (0.06, 0.35, 1.00, 1.0), 0.0, 0.25, 1.4)
    # La bola va en rojo y el boton en azul. El PARPADEO no puede vivir aqui:
    # el FBX no lleva animacion de emision, asi que en Unity lo mueve el
    # script ArcadeBlink. Estos son los colores en reposo.
    m["BolaJoystick"] = material("BolaJoystick", (1.00, 0.07, 0.10, 1.0), 0.0, 0.08, 1.2, 0.88)
    m["BotonAzul"] = material("BotonAzul", (0.06, 0.35, 1.00, 1.0), 0.0, 0.20, 1.2)
    m["Cromo"] = material("Cromo", (0.90, 0.91, 0.94, 1.0), 0.5, 0.22)
    m["Marquesina"] = material("Marquesina", COLORES["Verde"], 0.0, 0.35, 0.6)

    piezas = []
    medio_juego = (MUEBLE_Z + JUEGO_Z) * 0.5
    alto_juego = JUEGO_Z - MUEBLE_Z
    interior = ANCHO - GROSOR_MARCO * 2
    interior_y = FONDO - GROSOR_MARCO * 2

    # --- Zocalo y ruedas ----------------------------------------------------
    piezas.append(caja("Zocalo", (ANCHO, FONDO, BASE_Z), (0, 0, BASE_Z * 0.5), mat=m["Negro"]))

    for i, (sx, sy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)], start=1):
        x = sx * (ANCHO * 0.5 - 0.07)
        y = sy * (FONDO * 0.5 - 0.07)
        piezas.append(cilindro("Rueda_%d" % i, 0.035, 0.025, (x, y, 0.035), "X", m["Goma"], 16))

    # --- Mueble bajo --------------------------------------------------------
    # El mueble va en trozos, dejando vacia la esquina delantera izquierda por
    # donde baja el peluche hasta el cajon. Antes era una caja maciza: se veia
    # perfecta por fuera, pero el peluche no tenia por donde pasar. Cada trozo
    # puede llevar su collider tal cual, sin excepciones que recordar luego.
    mz0, mz1 = BASE_Z, MUEBLE_Z
    ax = ANCHO * 0.5
    ay = FONDO * 0.5

    for nombre, x0, x1, y0, y1, z0, z1 in [
        ("Mueble_Base",     VX1,  ax,  -ay,  ay,  mz0, mz1),   # todo lo de la derecha
        ("Mueble_Fondo",    -ax,  VX1,  VY1, ay,  mz0, mz1),   # lo de detras del hueco
        ("Mueble_Suelo",    -ax,  VX1, -ay,  VY1, mz0, VZ0),   # por debajo del cajon
        ("Mueble_Lateral",  -ax,  VX0, -ay,  VY1, VZ0, mz1),   # chapa del costado
        ("Mueble_Frente_A", VX0,  PX0, -ay, VY0,  VZ0, mz1),   # chapa a un lado de la boca
        ("Mueble_Frente_B", PX1,  VX1, -ay, VY0,  VZ0, mz1),   # y al otro
        ("Mueble_Frente_C", PX0,  PX1, -ay, VY0,  PZ1, mz1),   # y por encima
    ]:
        piezas.append(caja(nombre, (x1 - x0, y1 - y0, z1 - z0),
                           ((x0 + x1) * 0.5, (y0 + y1) * 0.5, (z0 + z1) * 0.5),
                           mat=m["Turquesa"]))

    # Ondas del mueble. Arrancan a la derecha de la trampilla: en un mueble de
    # verdad el vinilo se corta en el recorte de la puerta, no lo cruza.
    for i in range(3):
        z = BASE_Z + 0.14 + i * 0.19
        piezas.append(caja("Onda_%d" % (i + 1), (ax - VX1 - 0.01, 0.007, 0.055),
                           ((VX1 + ax) * 0.5, -ay - 0.0015, z),
                           mat=m["Verde"] if i % 2 == 0 else m["Blanco"]))

    # --- Trampilla del premio -----------------------------------------------
    # Abre hacia arriba, asi que el origen va en su BISAGRA SUPERIOR y no en el
    # centro: en Unity basta con girarla sobre su X local para abrirla, sin
    # inventarse un objeto vacio de apoyo.
    puerta_x = (PX0 + PX1) * 0.5
    puerta_ancho = PX1 - PX0
    # El borde de ABAJO de la puerta tiene que ser la cara del suelo del
    # conducto, no quedarse por debajo. Si no, por el hueco se ve una repisa a
    # media altura y el peluche parece estar sobre un escalon.
    suelo_conducto = TOLVA_SUELO + 0.045
    bisagra_z = 0.600
    puerta_alto = bisagra_z - suelo_conducto
    cara_puerta = -FONDO * 0.5 - 0.004

    # El marco va en CUATRO BARRAS, no en una plancha.
    #
    # Era una plancha maciza del tamano de la puerta mas cinco centimetros, o
    # sea que tapaba la abertura entera. Por fuera no se notaba, porque la
    # puerta la cubre; pero al abrirla detras no habia hueco, habia marco. Y
    # ademas su collider es una caja: aunque la malla hubiera tenido agujero, la
    # caja lo habria rellenado igual y el rayo de recoger el peluche habria
    # chocado ahi.
    #
    # Comprobado con rayos desde donde se pone el jugador agachado: con la
    # plancha, cero de nueve llegaban al premio.
    marco_grueso = 0.025
    marco_z0 = bisagra_z - puerta_alto
    marco_y = cara_puerta + 0.002

    for nombre, ancho, alto, cx, cz in [
        ("Trampilla_Marco_Sup", puerta_ancho + marco_grueso * 2, marco_grueso,
         puerta_x, bisagra_z + marco_grueso * 0.5),
        ("Trampilla_Marco_Inf", puerta_ancho + marco_grueso * 2, marco_grueso,
         puerta_x, marco_z0 - marco_grueso * 0.5),
        ("Trampilla_Marco_Izq", marco_grueso, puerta_alto,
         puerta_x - puerta_ancho * 0.5 - marco_grueso * 0.5, bisagra_z - puerta_alto * 0.5),
        ("Trampilla_Marco_Der", marco_grueso, puerta_alto,
         puerta_x + puerta_ancho * 0.5 + marco_grueso * 0.5, bisagra_z - puerta_alto * 0.5),
    ]:
        piezas.append(caja(nombre, (ancho, 0.014, alto),
                           (cx, marco_y, cz), mat=m["Negro"]))

    piezas.append(caja("Trampilla", (puerta_ancho, 0.012, puerta_alto),
                       (puerta_x, cara_puerta - 0.008, bisagra_z - puerta_alto * 0.5),
                       origen=(puerta_x, cara_puerta - 0.008, bisagra_z),
                       mat=m["Cristal"]))

    piezas.append(caja("Trampilla_Tirador", (0.09, 0.016, 0.018),
                       (puerta_x, cara_puerta - 0.020, bisagra_z - puerta_alto + 0.03),
                       mat=m["Cromo"]))

    piezas.append(caja("Cartel_WIN", (0.15, 0.008, 0.055),
                       (puerta_x, cara_puerta - 0.014, bisagra_z + 0.048), mat=m["Amarillo"]))

    # --- Consola de mandos --------------------------------------------------
    # Cajon negro que sobresale del frontal: encima el joystick y el boton, y
    # en la cara vertical los dos monederos. El joystick se deja como pieza
    # SUELTA y con el origen en su rotula, porque mas adelante tiene que
    # inclinarse cuando el jugador mueva la garra.

    CONS_ANCHO = 0.38
    CONS_FONDO = 0.15
    CONS_ALTO = 0.30

    cons_z = MUEBLE_Z - 0.02              # la tapa, justo bajo el cristal
    cons_y = -FONDO * 0.5 - CONS_FONDO * 0.5
    cara_y = -FONDO * 0.5 - CONS_FONDO    # cara vertical de delante

    piezas.append(caja("Consola_Cuerpo", (CONS_ANCHO, CONS_FONDO, CONS_ALTO),
                       (0, cons_y, cons_z - CONS_ALTO * 0.5), mat=m["Negro"]))

    piezas.append(caja("Consola_Tapa", (CONS_ANCHO + 0.02, CONS_FONDO + 0.02, 0.018),
                       (0, cons_y, cons_z), mat=m["Negro"]))

    # Filo azul retroiluminado del canto, que es lo que mas se ve de noche
    piezas.append(caja("Consola_Filo", (CONS_ANCHO + 0.024, 0.006, 0.010),
                       (0, cara_y - 0.012, cons_z - 0.012), mat=m["LEDAzul"]))

    tapa_z = cons_z + 0.009

    # --- Joystick -----------------------------------------------------------
    piezas.append(cilindro("Joystick_Aro", 0.036, 0.006, (-0.075, cons_y, tapa_z + 0.002),
                           "Z", m["LEDAzul"], 24))
    piezas.append(cilindro("Joystick_Base", 0.028, 0.016, (-0.075, cons_y, tapa_z + 0.010),
                           "Z", m["MetalOsc"], 24))

    # Pieza movil: vastago + bola, con el origen en la rotula.
    rotula = Vector((-0.075, cons_y, tapa_z + 0.016))

    vastago = cilindro("Joystick", 0.009, 0.052, rotula + Vector((0, 0, 0.026)),
                       "Z", m["Metal"], 16)
    bola = esfera("_bola", 0.024, rotula + Vector((0, 0, 0.064)), m["BolaJoystick"])

    piezas.append(unir(vastago, [bola], rotula))

    # --- Boton de jugar -----------------------------------------------------
    piezas.append(cilindro("Boton_Aro", 0.038, 0.006, (0.085, cons_y, tapa_z + 0.002),
                           "Z", m["LEDAzul"], 24))
    piezas.append(cilindro("Boton_Jugar", 0.032, 0.014, (0.085, cons_y, tapa_z + 0.011),
                           "Z", m["BotonAzul"], 24))

    # --- Monederos, en la cara vertical -------------------------------------
    # Van por capas, de fuera hacia dentro: placa cromada, bisel oscuro hundido,
    # y encima los herrajes. Es lo que hace que se lean como mecanismos y no
    # como una pegatina: cada borde que sobresale coge su propia luz.
    for i, sx in enumerate((-1, 1), start=1):
        x = sx * 0.075
        z0 = cons_z - 0.16

        # Placa cromada con su marco
        piezas.append(caja("Monedero_%d" % i, (0.108, 0.012, 0.185),
                           (x, cara_y - 0.006, z0), mat=m["Cromo"]))

        piezas.append(caja("Monedero_Bisel_%d" % i, (0.082, 0.010, 0.155),
                           (x, cara_y - 0.014, z0), mat=m["MetalOsc"]))

        # Ranura de la moneda, con labio cromado encima
        piezas.append(caja("Ranura_%d" % i, (0.007, 0.012, 0.048),
                           (x, cara_y - 0.019, z0 + 0.048), mat=m["Negro"]))

        piezas.append(caja("Ranura_Labio_%d" % i, (0.034, 0.010, 0.011),
                           (x, cara_y - 0.023, z0 + 0.079), mat=m["Cromo"]))

        # Boton iluminado, con su aro
        piezas.append(cilindro("Boton_Moneda_Aro_%d" % i, 0.023, 0.010,
                               (x, cara_y - 0.019, z0 - 0.008), "Y", m["Cromo"], 20))

        piezas.append(cilindro("Boton_Moneda_%d" % i, 0.017, 0.014,
                               (x, cara_y - 0.023, z0 - 0.008), "Y", m["LEDAzul"], 20))

        # Cazoleta de devolucion, abajo
        piezas.append(caja("Devolucion_%d" % i, (0.048, 0.016, 0.026),
                           (x, cara_y - 0.020, z0 - 0.062), mat=m["Negro"]))

        piezas.append(caja("Devolucion_Labio_%d" % i, (0.054, 0.009, 0.008),
                           (x, cara_y - 0.021, z0 - 0.077), mat=m["Cromo"]))

        # Tornillos en las cuatro esquinas
        for j, (ex, ez) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)], start=1):
            piezas.append(cilindro("Tornillo_%d_%d" % (i, j), 0.0045, 0.010,
                                   (x + ex * 0.045, cara_y - 0.012, z0 + ez * 0.080),
                                   "Y", m["MetalOsc"], 10))

    # --- Panel de fuerza, en la trasera -------------------------------------
    # El cuadro de servicio: en una maquina de verdad va detras, mirando a la
    # pared, porque es del dueno y no del jugador. La AGUJA se deja como pieza
    # suelta y con el origen en el eje de la esfera, porque tiene que girar.

    tras_y = FONDO * 0.5
    fz_z = MUEBLE_Z - 0.22

    piezas.append(caja("Panel_Fuerza", (0.28, 0.022, 0.24),
                       (0, tras_y + 0.011, fz_z), mat=m["Negro"]))

    piezas.append(caja("Panel_Fuerza_Filo", (0.30, 0.008, 0.012),
                       (0, tras_y + 0.014, fz_z + 0.126), mat=m["LEDAzul"]))

    esfera_z = fz_z + 0.045

    piezas.append(cilindro("Fuerza_Esfera", 0.072, 0.010,
                           (0, tras_y + 0.027, esfera_z), "Y", m["Blanco"], 28))

    piezas.append(cilindro("Fuerza_Aro", 0.080, 0.008,
                           (0, tras_y + 0.024, esfera_z), "Y", m["Cromo"], 28))

    # Marcas de la escala, de flojo a fuerte
    for i in range(7):
        ang = math.radians(-120.0 + 240.0 * i / 6.0)
        rx = math.sin(ang) * 0.058
        rz = math.cos(ang) * 0.058
        largo = 0.014 if i % 3 == 0 else 0.009

        piezas.append(caja("Fuerza_Marca_%d" % (i + 1), (0.005, 0.006, largo),
                           (rx, tras_y + 0.033, esfera_z + rz),
                           mat=m["Negro"] if i < 4 else m["Rosa"]))

    # La aguja: origen EN EL EJE de la esfera para que gire sobre el.
    piezas.append(caja("Fuerza_Aguja", (0.006, 0.006, 0.052),
                       (0, tras_y + 0.036, esfera_z + 0.026),
                       origen=(0, tras_y + 0.036, esfera_z),
                       mat=m["Rosa"]))

    piezas.append(cilindro("Fuerza_Tuerca", 0.010, 0.012,
                           (0, tras_y + 0.038, esfera_z), "Y", m["Cromo"], 16))

    # El mando que se gira, debajo de la esfera
    piezas.append(cilindro("Fuerza_Mando", 0.026, 0.028,
                           (0, tras_y + 0.032, fz_z - 0.072), "Y", m["MetalOsc"], 20))

    piezas.append(caja("Fuerza_Mando_Muesca", (0.005, 0.008, 0.024),
                       (0, tras_y + 0.047, fz_z - 0.062), mat=m["Blanco"]))

    # --- Marco del cristal --------------------------------------------------
    for i, (sx, sy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)], start=1):
        x = sx * (ANCHO * 0.5 - GROSOR_MARCO * 0.5)
        y = sy * (FONDO * 0.5 - GROSOR_MARCO * 0.5)
        piezas.append(caja("Poste_%d" % i, (GROSOR_MARCO + 0.004, GROSOR_MARCO + 0.004, alto_juego),
                           (x, y, medio_juego), mat=m["Turquesa"]))

        # Tira de LED pegada al poste, hacia fuera
        piezas.append(caja("LED_%d" % i, (0.014, 0.014, alto_juego * 0.96),
                           (x + sx * 0.021, y + sy * 0.021, medio_juego), mat=m["LED"]))

    # --- Cristales ----------------------------------------------------------
    for nombre, centro, tam in [
        ("Cristal_Frente", (0, -FONDO * 0.5 + GROSOR_CRISTAL, medio_juego), (interior, GROSOR_CRISTAL, alto_juego)),
        ("Cristal_Atras", (0, FONDO * 0.5 - GROSOR_CRISTAL, medio_juego), (interior, GROSOR_CRISTAL, alto_juego)),
        ("Cristal_Izq", (-ANCHO * 0.5 + GROSOR_CRISTAL, 0, medio_juego), (GROSOR_CRISTAL, interior_y, alto_juego)),
        ("Cristal_Der", (ANCHO * 0.5 - GROSOR_CRISTAL, 0, medio_juego), (GROSOR_CRISTAL, interior_y, alto_juego)),
    ]:
        piezas.append(caja(nombre, tam, centro, mat=m["Cristal"]))

    # Techo de la zona de juego. Faltaba: la caja estaba abierta por arriba de
    # lado a lado. Ademas de sellar, es de lo que cuelga todo el portico.
    piezas.append(caja("Techo_Juego", (ANCHO - 0.004, FONDO - 0.004, 0.016),
                       (0, 0, JUEGO_Z - 0.004), mat=m["Negro"]))

    # --- Suelo de juego, sellado hasta el cristal ---------------------------
    mitad = interior * 0.5
    mitad_y = interior_y * 0.5

    # El suelo llega hasta el cristal por los cuatro lados. Antes se quedaba en
    # el borde de "interior" y dejaba una zanja de 33 mm contra el cristal en
    # la que un peluche se atasca de canto y ya no lo saca ni la garra.
    bx0, bx1 = -mitad, -mitad + HUECO
    by0, by1 = -mitad_y, -mitad_y + HUECO

    sx = ax - 0.002
    sy = ay - 0.002

    for nombre, x0, x1, y0, y1 in [
        ("Suelo_Juego_A", bx1,  sx,  -sy,  sy),
        ("Suelo_Juego_B", -sx,  bx1,  by1, sy),
        ("Suelo_Juego_C", -sx,  bx0, -sy,  by1),
        ("Suelo_Juego_D", bx0,  bx1, -sy,  by0),
    ]:
        piezas.append(caja(nombre, (x1 - x0, y1 - y0, 0.028),
                           ((x0 + x1) * 0.5, (y0 + y1) * 0.5, MUEBLE_Z + 0.006),
                           mat=m["Negro"]))
    # --- Conducto hasta la trampilla ----------------------------------------
    # Antes aqui habia una tapa plana llamada BocaPremio que TAPABA el agujero:
    # el peluche no podia caer, se quedaba encima. Ahora es un conducto de
    # verdad, con sus cuatro paredes, que lleva el peluche desde el suelo de
    # juego hasta el cajon de abajo.

    hx = -mitad + HUECO * 0.5
    hy = -mitad_y + HUECO * 0.5

    alto_tolva = MUEBLE_Z - TOLVA_SUELO + 0.025
    centro_tolva = (MUEBLE_Z + TOLVA_SUELO - 0.025) * 0.5
    ABRE_TOLVA = 0.003          # el conducto, un pelo mas ancho que la boca
    luz = HUECO + TOLVA_PARED + ABRE_TOLVA * 2.0

    for nombre, tam, centro in [
        ("Tolva_Izq", (TOLVA_PARED, luz, alto_tolva),
         (hx - HUECO * 0.5 - ABRE_TOLVA - TOLVA_PARED * 0.5, hy, centro_tolva)),
        ("Tolva_Der", (TOLVA_PARED, luz, alto_tolva),
         (hx + HUECO * 0.5 + ABRE_TOLVA + TOLVA_PARED * 0.5, hy, centro_tolva)),
        ("Tolva_Atras", (luz, TOLVA_PARED, alto_tolva),
         (hx, hy + HUECO * 0.5 + ABRE_TOLVA + TOLVA_PARED * 0.5, centro_tolva)),
    ]:
        piezas.append(caja(nombre, tam, centro, mat=m["MetalOsc"]))

    # La pared de DELANTE del conducto va en tres trozos, dejando libre el
    # recorte de la trampilla.
    #
    # Era una pared entera, y con ella el conducto quedaba cerrado por los
    # cuatro costados: el peluche caia dentro y no habia por donde sacarlo. La
    # puerta se abria y detras estaba esta chapa. Se veia bien en el render y no
    # se veia en absoluto que estuviera mal.
    frente_y = hy - HUECO * 0.5 - ABRE_TOLVA - TOLVA_PARED * 0.5
    tolva_x0 = hx - luz * 0.5
    tolva_x1 = hx + luz * 0.5
    tolva_z0 = centro_tolva - alto_tolva * 0.5
    tolva_z1 = centro_tolva + alto_tolva * 0.5

    for nombre, x0, x1, z0, z1 in [
        ("Tolva_Frente_Alto", tolva_x0, tolva_x1, PZ1, tolva_z1),
        ("Tolva_Frente_Izq", tolva_x0, PX0, tolva_z0, PZ1),
        ("Tolva_Frente_Der", PX1, tolva_x1, tolva_z0, PZ1),
    ]:
        if x1 - x0 < 0.001 or z1 - z0 < 0.001:
            continue

        piezas.append(caja(nombre, (x1 - x0, TOLVA_PARED, z1 - z0),
                           ((x0 + x1) * 0.5, frente_y, (z0 + z1) * 0.5),
                           mat=m["MetalOsc"]))

    # Aro que remata la boca a ras del suelo de juego. Ademas de rematar,
    # redondea el canto por el que pasa el peluche: un borde vivo ahi es donde
    # se quedan enganchados.
    for nombre, tam, centro in [
        ("Boca_Labio_Izq", (0.018, luz + 0.018, 0.016),
         (hx - HUECO * 0.5 - 0.005, hy, MUEBLE_Z + 0.008)),
        ("Boca_Labio_Der", (0.018, luz + 0.018, 0.016),
         (hx + HUECO * 0.5 + 0.005, hy, MUEBLE_Z + 0.008)),
        ("Boca_Labio_Frente", (luz + 0.018, 0.018, 0.016),
         (hx, hy - HUECO * 0.5 - 0.005, MUEBLE_Z + 0.008)),
        ("Boca_Labio_Atras", (luz + 0.018, 0.018, 0.016),
         (hx, hy + HUECO * 0.5 + 0.005, MUEBLE_Z + 0.008)),
    ]:
        piezas.append(caja(nombre, tam, centro, mat=m["Amarillo"]))

    # --- Cajon de recogida ---------------------------------------------------
    # Suelo inclinado 10 grados hacia la puerta, para que el peluche acabe
    # siempre pegado al cristal de la trampilla y no en un rincon donde no se
    # ve ni se alcanza.
    # El cajon se coloca respecto a la boca, no con numeros sueltos: si no, al
    # cambiar el ancho de la maquina se queda donde estaba y el peluche cae al
    # relleno.
    bin_ancho = HUECO + 0.10
    bin_fondo = HUECO + 0.14
    bin_x = hx + 0.015
    bin_y = hy + 0.055

    piezas.append(caja("Tolva_Suelo", (bin_ancho, bin_fondo, 0.014),
                       (bin_x, bin_y, TOLVA_SUELO), giro=(-10.0, 0.0, 0.0),
                       mat=m["MetalOsc"]))

    for nombre, tam, centro in [
        ("Cajon_Izq", (0.012, bin_fondo, 0.16),
         (bin_x - bin_ancho * 0.5, bin_y, TOLVA_SUELO + 0.08)),
        ("Cajon_Der", (0.012, bin_fondo, 0.16),
         (bin_x + bin_ancho * 0.5, bin_y, TOLVA_SUELO + 0.08)),
        ("Cajon_Atras", (bin_ancho, 0.012, 0.16),
         (bin_x, bin_y + bin_fondo * 0.5, TOLVA_SUELO + 0.08)),
    ]:
        piezas.append(caja(nombre, tam, centro, mat=m["MetalOsc"]))

    # Paredes de cristal alrededor de la boca.
    #
    # No son decoracion: sin ellas, un peluche que ande rodando por el suelo se
    # cuela en el agujero y se regala solo. Solo pueden entrar por arriba, que
    # es la unica forma en que la garra puede soltarlos ahi.
    borde_x = -mitad + HUECO
    borde_y = -mitad_y + HUECO

    piezas.append(caja("Cristal_Boca_1", (HUECO, GROSOR_CRISTAL, ALTO_BOCA),
                       (-mitad + HUECO * 0.5, borde_y + 0.006, MUEBLE_Z + ALTO_BOCA * 0.5 + 0.006),
                       mat=m["Cristal"]))

    piezas.append(caja("Cristal_Boca_2", (GROSOR_CRISTAL, HUECO, ALTO_BOCA),
                       (borde_x + 0.006, -mitad_y + HUECO * 0.5, MUEBLE_Z + ALTO_BOCA * 0.5 + 0.006),
                       mat=m["Cristal"]))

    # --- Marquesina ---------------------------------------------------------
    # Cajon de luz: dos paneles con la parte de arriba en arco (delante y
    # detras), dos costados BAJOS que solo llegan al arranque de la curva, y un
    # techo que sigue la cupula. Los costados iban antes a 30 cm de alto, o sea
    # por encima del arranque del arco, y lo tapaban: se veia un cajon recto
    # con una punta asomando.

    cart_recto = 0.16
    cart_flecha = CARTEL_Z - JUEGO_Z - cart_recto
    cart_y = FONDO * 0.5 - 0.009

    piezas.append(arco("Cartel_Frente", ANCHO - 0.006, 0.03, JUEGO_Z, cart_recto - 0.004, cart_flecha,
                       -FONDO * 0.5 + 0.02, m["Marquesina"]))

    piezas.append(arco("Cartel_Atras", ANCHO - 0.006, 0.03, JUEGO_Z, cart_recto - 0.004, cart_flecha,
                       FONDO * 0.5 - 0.02, m["VerdeOsc"]))

    for nombre, sx in (("Cartel_Lateral_Izq", -1), ("Cartel_Lateral_Der", 1)):
        piezas.append(caja(nombre, (0.02, cart_y * 2.0, cart_recto),
                           (sx * (ANCHO * 0.5 - 0.01), 0, JUEGO_Z + cart_recto * 0.5 + 0.003),
                           mat=m["Morado"]))

    piezas.append(techo_arco("Cartel_Techo", ANCHO - 0.006, JUEGO_Z, cart_recto, cart_flecha,
                             -cart_y, cart_y, 0.016, m["Morado"]))

    # Moldura que remata el arranque del arco por los cuatro lados, que es
    # donde se juntan los paneles y donde mas canta una junta abierta.
    VUELO = 0.004        # cuanto sobresale la moldura de la carroceria
    zoc_z = JUEGO_Z + 0.008
    zoc_alto = 0.024
    zoc_fuera_x = ANCHO * 0.5 + VUELO
    zoc_fuera_y = FONDO * 0.5 + VUELO
    zoc_grueso = VUELO + 0.018

    for nombre, tam, centro in [
        ("Cartel_Zocalo_F", ((zoc_fuera_x - zoc_grueso) * 2.0, zoc_grueso, zoc_alto),
         (0, -zoc_fuera_y + zoc_grueso * 0.5, zoc_z)),
        ("Cartel_Zocalo_A", ((zoc_fuera_x - zoc_grueso) * 2.0, zoc_grueso, zoc_alto),
         (0, zoc_fuera_y - zoc_grueso * 0.5, zoc_z)),
        ("Cartel_Zocalo_I", (zoc_grueso, zoc_fuera_y * 2.0, zoc_alto),
         (-zoc_fuera_x + zoc_grueso * 0.5, 0, zoc_z)),
        ("Cartel_Zocalo_D", (zoc_grueso, zoc_fuera_y * 2.0, zoc_alto),
         (zoc_fuera_x - zoc_grueso * 0.5, 0, zoc_z)),
    ]:
        piezas.append(caja(nombre, tam, centro, mat=m["Morado"]))

    # --- Portico de dos ejes ------------------------------------------------
    # Dos guias independientes, una por eje:
    #
    #   EJE X  Riel_Largo_1/2, fijos al techo y corriendo de lado a lado. Sobre
    #          ellos viaja el PUENTE entero.
    #   EJE Z  las barras del propio puente, que van de delante a atras. Sobre
    #          ellas corre el CARRO.
    #
    # El puente se mueve en X y el carro en Z, y el carro cuelga del puente. Es
    # el orden que espera ClawController: rail largo fuera, carro dentro. Se
    # monto asi a proposito, girando el portico 90 grados en el modelo, en vez
    # de retocar el controlador, que ya funciona.

    riel_y = interior_y * 0.5 - 0.03     # separacion de las guias al centro
    riel_z = RIEL_Z + 0.034              # altura de las guias fijas
    riel_largo = ANCHO * 0.80            # recorrido del puente
    barra_z = RIEL_Z                     # altura de las barras del puente

    for nombre, sy in (("Riel_Largo_1", -1), ("Riel_Largo_2", 1)):
        piezas.append(caja(nombre, (riel_largo, 0.020, 0.020),
                           (0, sy * riel_y, riel_z), mat=m["Metal"]))

    # Topes: marcan hasta donde llega el puente y evitan que se salga.
    for i, (sx, sy) in enumerate([(-1, -1), (1, -1), (-1, 1), (1, 1)], start=1):
        piezas.append(caja("Riel_Tope_%d" % i, (0.016, 0.032, 0.036),
                           (sx * (riel_largo * 0.5 - 0.015), sy * riel_y, riel_z - 0.002),
                           mat=m["MetalOsc"]))

    # Motor del eje X, anclado al bastidor por fuera de los topes para que el
    # puente no se lo lleve por delante al final del recorrido.
    piezas.append(caja("Motor_Puente", (0.056, 0.056, 0.056),
                       (riel_largo * 0.5 + 0.045, riel_y, riel_z - 0.006), mat=m["MetalOsc"]))

    piezas.append(cilindro("Motor_Puente_Polea", 0.020, 0.016,
                           (riel_largo * 0.5 + 0.004, riel_y, riel_z - 0.006), "X",
                           m["Dorado"], 16))

    # --- PUENTE: recorre el eje X -------------------------------------------
    # Una sola pieza con el origen en su centro. Los dos carros de los extremos
    # envuelven las guias fijas, y entre ellos van las barras del eje Z.
    truck_z = (barra_z - 0.010 + riel_z + 0.012) * 0.5
    truck_alto = (riel_z + 0.012) - (barra_z - 0.010)

    puente = caja("Puente", (0.092, 0.050, truck_alto),
                  (0, -riel_y, truck_z), mat=m["Blanco"])

    trozos = [caja("_p2", (0.092, 0.050, truck_alto), (0, riel_y, truck_z), mat=m["Blanco"])]

    for sx in (-1, 1):
        trozos.append(cilindro("_barra", 0.008, riel_y * 2.0,
                               (sx * 0.026, 0, barra_z), "Y", m["Metal"], 14))

    # Traviesa de union: sin ella los dos extremos parecen piezas sueltas.
    trozos.append(caja("_traviesa", (0.016, riel_y * 2.0, 0.012),
                       (0.054, 0, barra_z + 0.004), mat=m["Blanco"]))

    # Motor del eje Z: va MONTADO EN EL PUENTE, asi que viaja con el. Es la
    # diferencia que se ve a simple vista entre una guia y la otra.
    trozos.append(caja("_motorz", (0.056, 0.052, 0.050),
                       (-0.078, -riel_y, barra_z + 0.004), mat=m["MetalOsc"]))

    piezas.append(unir(puente, trozos, (0, 0, barra_z)))

    # --- CARRO: recorre el eje Z sobre las barras del puente ----------------
    cuerpo_alto = 0.050
    cuerpo_z = barra_z - 0.008 - cuerpo_alto * 0.5

    carro = caja("Carro", (0.096, 0.086, cuerpo_alto), (0, 0, cuerpo_z), mat=m["Blanco"])

    partes = []

    # Rodillos a la altura de las barras y asomando por encima del cuerpo, que
    # es lo que hace que se vea que el carro va agarrado a ellas.
    for sx in (-1, 1):
        partes.append(cilindro("_rodillo", 0.016, 0.024, (sx * 0.026, 0, barra_z),
                               "Y", m["MetalOsc"], 16))

    # Tambor del cable, sacado por delante para que se vea de donde sale.
    partes.append(cilindro("_tambor", 0.017, 0.030, (0, -0.052, cuerpo_z), "X",
                           m["Dorado"], 16))

    piezas.append(unir(carro, partes, (0, 0, cuerpo_z)))

    largo_cable = (cuerpo_z - cuerpo_alto * 0.5) - CABLE_Z
    piezas.append(cilindro("Cable", 0.0035, largo_cable,
                           (0, 0, CABLE_Z + largo_cable * 0.5), "Z", m["Metal"], 10))

    # --- Cabeza de la garra -------------------------------------------------
    # Se construye como en la foto: cuerpo del motor, collar dorado y carcasa
    # conica. Todo se une en una sola pieza cuyo origen queda EN EL CABLE, que
    # es el punto del que cuelga. Ese detalle es el que evita el cable de tres
    # metros que tenia el modelo anterior.
    motor = cilindro("Cabeza", MOTOR_RADIO, MOTOR_ALTO,
                     (0, 0, CABLE_Z - MOTOR_ALTO * 0.5), "Z", m["Metal"], 20)

    collar = cilindro("_collar", MOTOR_RADIO * 1.15, COLLAR_ALTO,
                      (0, 0, CABLE_Z - MOTOR_ALTO - COLLAR_ALTO * 0.5), "Z", m["Dorado"], 20)

    bpy.ops.mesh.primitive_cone_add(radius1=CONO_RADIO, radius2=MOTOR_RADIO * 0.9,
                                    depth=CONO_ALTO, vertices=20,
                                    location=(0, 0, CABLE_Z - MOTOR_ALTO - COLLAR_ALTO - CONO_ALTO * 0.5))
    cono = bpy.context.object
    cono.name = "_cono"
    cono.rotation_euler = (math.radians(180), 0, 0)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    pintar(cono, m["Metal"])

    # Se funden en la cabeza y se recoloca el origen en el cable
    bpy.ops.object.select_all(action="DESELECT")
    for parte in (motor, collar, cono):
        parte.select_set(True)
    bpy.context.view_layer.objects.active = motor
    bpy.ops.object.join()

    motor.data.transform(Matrix.Translation(-Vector((0, 0, CABLE_Z)) + motor.location))
    motor.location = Vector((0, 0, CABLE_Z))
    bpy.ops.object.select_all(action="DESELECT")

    piezas.append(motor)

    for i, ang in enumerate([90, 210, 330], start=1):
        piezas.append(dedo(i, ang, m["Cromo"]))

    return piezas


def verificar(piezas):
    fisicas = ("Cabeza", "Dedo_1", "Dedo_2", "Dedo_3", "Puente", "Carro",
               "Joystick", "Trampilla")

    print("\n" + "=" * 70)
    print("VERIFICACION")
    print("=" * 70)

    malas = [o.name for o in piezas if any(abs(v - 1.0) > 1e-4 for v in o.scale)]
    print("Piezas totales: %d" % len(piezas))
    print("Con escala distinta de 1: %s" % (", ".join(malas) if malas else "ninguna"))

    print("\nPiezas de fisica (origen y escala tienen que estar intactos):")
    for ob in piezas:
        if ob.name in fisicas:
            print("  %-8s escala (%.3f %.3f %.3f)  origen (%6.3f %6.3f %6.3f)" % (
                ob.name, ob.scale.x, ob.scale.y, ob.scale.z,
                ob.location.x, ob.location.y, ob.location.z))

    minimo = Vector((1e9,) * 3)
    maximo = Vector((-1e9,) * 3)
    for ob in piezas:
        for esquina in ob.bound_box:
            p = ob.matrix_world @ Vector(esquina)
            minimo = Vector(tuple(min(minimo[i], p[i]) for i in range(3)))
            maximo = Vector(tuple(max(maximo[i], p[i]) for i in range(3)))

    t = maximo - minimo
    print("\nMedidas: %.2f x %.2f x %.2f m" % (t.x, t.y, t.z))
    print("Materiales: %d" % len(bpy.data.materials))
    print("=" * 70 + "\n")

    return len(malas)


if __name__ == "__main__":
    import sys

    piezas = construir()
    verificar(piezas)

    if "--salida" in sys.argv:
        destino = sys.argv[sys.argv.index("--salida") + 1]
        bpy.ops.wm.save_as_mainfile(filepath=destino)
        print("GUARDADO: %s" % destino)

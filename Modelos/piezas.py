# -*- coding: utf-8 -*-
# Los cuatro ladrillos que usan todos los modelos pequenos.
#
# Vive aparte para no importar pinza.py, que al importarlo construye la maquina
# entera: son varios segundos y un monton de objetos que despues hay que borrar.
import math
import os

import bpy
from mathutils import Vector

# Donde van los .fbx que lee Unity.
MODELOS = "D:/APLHA ClayWorks simulator/Assets/_Project/Models/"
BLENDS = "D:/APLHA ClayWorks simulator/Modelos/"


def limpiar():
    bpy.ops.wm.read_factory_settings(use_empty=True)


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

    # Tambien en el color de vista, que es lo que ve Unity al importar.
    mat.diffuse_color = color

    return mat


def pintar(ob, mat):
    ob.data.materials.clear()
    ob.data.materials.append(mat)
    return ob


def caja(nombre, tam, centro, mat=None, bisel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=centro)

    ob = bpy.context.object
    ob.name = nombre
    ob.scale = Vector(tam)

    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    if bisel > 0.0:
        m = ob.modifiers.new("Bisel", "BEVEL")
        m.width = bisel
        m.segments = 3
        m.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=m.name)

    if mat is not None:
        pintar(ob, mat)

    return ob


def cilindro(nombre, radio, alto, centro, eje="Z", mat=None, lados=24):
    bpy.ops.mesh.primitive_cylinder_add(radius=radio, depth=alto,
                                        location=centro, vertices=lados)

    ob = bpy.context.object
    ob.name = nombre

    if eje == "X":
        ob.rotation_euler = (0.0, math.radians(90.0), 0.0)
    elif eje == "Y":
        ob.rotation_euler = (math.radians(90.0), 0.0, 0.0)

    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    if mat is not None:
        pintar(ob, mat)

    return ob


def cono(nombre, radio1, radio2, alto, centro, mat=None, lados=24):
    bpy.ops.mesh.primitive_cone_add(radius1=radio1, radius2=radio2, depth=alto,
                                    location=centro, vertices=lados)

    ob = bpy.context.object
    ob.name = nombre

    if mat is not None:
        pintar(ob, mat)

    return ob


def esfera(nombre, radio, centro, mat=None, segmentos=24, achatado=1.0):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radio, location=centro,
                                         segments=segmentos,
                                         ring_count=max(8, segmentos // 2))

    ob = bpy.context.object
    ob.name = nombre

    if achatado != 1.0:
        ob.scale = (1.0, 1.0, achatado)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    bpy.ops.object.shade_smooth()

    if mat is not None:
        pintar(ob, mat)

    return ob


def unir(piezas, nombre):
    """Une todo en una sola malla, que es como lo quiere Unity."""
    for ob in bpy.data.objects:
        ob.select_set(False)

    for ob in piezas:
        ob.select_set(True)

    bpy.context.view_layer.objects.active = piezas[0]
    bpy.ops.object.join()

    ob = bpy.context.object
    ob.name = nombre

    return ob


def exportar(nombre):
    """Guarda el .blend y saca el .fbx a la carpeta de Unity.

    Las opciones de exportacion son las mismas que las del resto del proyecto,
    y no son opcionales: con otras, el modelo llega a Unity girado o del reves
    y luego no hay quien cuadre las medidas con las del codigo.
    """
    for ob in bpy.data.objects:
        ob.select_set(True)

    ruta = MODELOS + nombre + ".fbx"

    bpy.ops.export_scene.fbx(
        filepath=ruta,
        use_selection=True,
        apply_unit_scale=True,
        global_scale=1.0,
        apply_scale_options="FBX_SCALE_NONE",
        bake_space_transform=True,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
    )

    bpy.ops.wm.save_as_mainfile(filepath=BLENDS + nombre + ".blend")

    print("  exportado: " + ruta)
    return ruta


def foto(nombre, mirando, distancia, angulo=0.6, alto=0.35, ancho=560, altoPx=560):
    """Un render rapido para poder mirar lo que se acaba de hacer.

    Con transformada Standard y luces flojas a proposito: con la de pelicula y
    focos fuertes los colores salen lavados y se acaba 'arreglando' un color que
    ya estaba bien.
    """
    esc = bpy.context.scene
    esc.render.engine = "BLENDER_EEVEE"
    esc.render.resolution_x = ancho
    esc.render.resolution_y = altoPx
    esc.view_settings.view_transform = "Standard"

    centro = Vector(mirando)

    esc.world = bpy.data.worlds.new("W")
    esc.world.use_nodes = True
    esc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.15, 0.16, 0.19, 1)

    for pos, energia in (((1.4, -1.8, 1.6), 60.0), ((-1.6, -1.2, 0.8), 22.0)):
        luz = bpy.data.lights.new("L", "AREA")
        luz.energy = energia
        luz.size = 2.0

        ob = bpy.data.objects.new("L", luz)
        ob.location = centro + Vector(pos)
        esc.collection.objects.link(ob)

    cam_dat = bpy.data.cameras.new("Cam")
    cam_dat.lens = 70
    cam = bpy.data.objects.new("Cam", cam_dat)
    esc.collection.objects.link(cam)
    esc.camera = cam

    cam.location = centro + Vector((math.sin(angulo) * distancia,
                                    -math.cos(angulo) * distancia,
                                    alto * distancia))

    d = centro - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    salida = ("C:/Users/xveli/AppData/Local/Temp/claude/"
              "D--APLHA-ClayWorks-simulator/"
              "8063a11e-76f8-430c-907f-f8b14ac42312/scratchpad/" + nombre)

    esc.render.filepath = salida
    bpy.ops.render.render(write_still=True)

    print("  foto: " + salida + ".png")

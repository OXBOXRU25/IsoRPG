# Примерка оружия в руке героя.
#
# Запуск:
#   "H:/Blender Foundation/Blender 5.2/blender.exe" --background --python "D:/GAME Ai/tools/weapon_fit.py" -- --weapon "путь\к\оружию.fbx"
#
# Необязательные ключи:
#   --hero human2        какой герой (по умолчанию human2)
#   --out кадр.png       куда сохранить (по умолчанию рядом со скриптом)
#   --grip 0.22          где у оружия точка хвата, доля длины от торца рукояти
#   --both               вложить оружие в обе руки
#   --roll -47           прокрутить лезвие вокруг оси клинка, градусы
#
# Что делает: ставит героя в боевую стойку, сжимает кисть в хват, сажает оружие
# в руку по принятым числам и снимает четыре ракурса одним кадром.

import bpy, os, sys, math, glob
import numpy as np
from mathutils import Vector, Matrix

A = r"D:\GAME Ai\IsoRPG\Assets"
TOOLS = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------- пресеты
# Числа подобраны 02.09.2026 щупами и приняты Павлоном.
HEROES = {
    "human2": {
        "model": r"C:\Temp\claude\D--GAME-Ai\a6fb687e-b755-48e7-9b6a-e7dec645a40a\scratchpad\hero\hero.glb",
        "texture": r"C:\Temp\claude\D--GAME-Ai\a6fb687e-b755-48e7-9b6a-e7dec645a40a\scratchpad\hero\T_Human-Custom2ColorMap.png",
        "arm_drop": -72,          # опускание плеча из T-позы, ось Y
        "grip": (38, 44, 28),     # фаланги четырёх пальцев, ось Z
        "thumb": (110, 40, -20),  # большой палец, только первая фаланга
        "thumb_falloff": (1.0, 0.18, 0.18),
        "wrist": (25, 0, 0),      # доворот кисти
        "bone_r": "hand_r",
        "bone_l": "hand_l",
        # положение оружия в мировых координатах при этой позе
        "weapon_pos": (-0.33946, 0.035115, 0.7740),
        "weapon_rot": (-3.1, -12.9, 179.7),
    },
}

# доля длины от торца рукояти, где оружие лежит в кулаке
DEFAULT_GRIP = 0.22
FINGERS = ["index", "middle", "ring", "pinky"]
TILE_W, TILE_H = 620, 900
VIEWS = ((0, "спереди"), (35, "три четверти"), (90, "сбоку"), (180, "сзади"))


def arg(name, default=None):
    """Читает ключ из командной строки после --."""
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if name in argv:
        i = argv.index(name)
        return argv[i + 1] if i + 1 < len(argv) else True
    return default


def has_flag(name):
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return name in argv


def make_mat(name, tex):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.85
    if tex and os.path.isfile(tex):
        n = m.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = bpy.data.images.load(tex)
        n.interpolation = 'Closest'
        m.node_tree.links.new(n.outputs["Color"], b.inputs["Base Color"])
    else:
        b.inputs["Base Color"].default_value = (0.58, 0.59, 0.62, 1)
        if "Metallic" in b.inputs:
            b.inputs["Metallic"].default_value = 0.6
    return m


def bbox(objs):
    mn = Vector((1e9,) * 3); mx = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c)
            for i in range(3):
                mn[i] = min(mn[i], w[i]); mx[i] = max(mx[i], w[i])
    return mn, mx


# куда смотрит клинок у эталонного кинжала после посадки в руку —
# к этому направлению приводится любое другое оружие
BLADE_REF = Vector((-0.095, -0.971, -0.221)).normalized()


def blade_direction(meshes, root):
    """Направление клинка: от опоры к дальнему от неё концу габарита."""
    mn, mx = bbox(meshes)
    pivot = root.matrix_world.translation
    corners = [Vector((x, y, z))
               for x in (mn.x, mx.x) for y in (mn.y, mx.y) for z in (mn.z, mx.z)]
    far = max(corners, key=lambda c: (c - pivot).length)
    d = far - pivot
    return d.normalized() if d.length > 1e-6 else None


def find_texture(weapon_path):
    """Ищет текстуру рядом с оружием: сначала в его папке, потом в Textures набора."""
    d = os.path.dirname(weapon_path)
    for pattern in ("*_d.tga", "*Diffuse*.png", "*_A.png", "*.png"):
        for cand in sorted(glob.glob(os.path.join(d, pattern))):
            low = cand.lower()
            if not any(k in low for k in ("normal", "metallic", "roughness", "occlusion", "mask", "emiss")):
                return cand
    # у Synty текстуры лежат уровнем выше, в папке Textures набора
    up = os.path.dirname(d)
    for sub in ("Textures", os.path.join("Textures", "Alts")):
        for cand in sorted(glob.glob(os.path.join(up, sub, "*.png"))):
            low = cand.lower()
            if not any(k in low for k in ("normal", "emiss", "mask", "icon")):
                return cand
    return None


def build(hero, weapon_path, grip_ratio, both, roll):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=hero["model"])
    arm = [o for o in bpy.context.scene.objects if o.type == 'ARMATURE'][0]
    hero_meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    hm = make_mat("hero", hero["texture"])
    for o in hero_meshes:
        o.data.materials.clear(); o.data.materials.append(hm)

    # поза: руки опущены, кисти сжаты
    for side, sign in (("r", 1), ("l", -1)):
        pb = arm.pose.bones["upperarm_" + side]
        pb.rotation_mode = 'XYZ'
        pb.rotation_euler = (0, math.radians(hero["arm_drop"] * sign), 0)
    for side in ("r", "l"):
        for f in FINGERS:
            for j, ang in enumerate(hero["grip"], start=1):
                pb = arm.pose.bones["%s_0%d_%s" % (f, j, side)]
                pb.rotation_mode = 'XYZ'
                pb.rotation_euler = (0, 0, math.radians(ang))
        for j, k in enumerate(hero["thumb_falloff"], start=1):
            pb = arm.pose.bones["thumb_0%d_%s" % (j, side)]
            pb.rotation_mode = 'XYZ'
            pb.rotation_euler = tuple(math.radians(a * k) for a in hero["thumb"])
    bpy.context.view_layer.update()

    wm = make_mat("weapon", find_texture(weapon_path))

    def put(bone_name, mirror):
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.fbx(filepath=weapon_path)
        objs = [o for o in bpy.context.scene.objects if o not in before]
        meshes = [o for o in objs if o.type == 'MESH']
        for o in meshes:
            o.data.materials.clear(); o.data.materials.append(wm)
        roots = [o for o in objs if o.parent is None]

        # ставим в принятое положение (масштаб не трогаем: импортёр уже задал)
        for o in roots:
            o.location = hero["weapon_pos"]
            o.rotation_mode = 'XYZ'
            o.rotation_euler = [math.radians(a) for a in hero["weapon_rot"]]
        bpy.context.view_layer.update()

        # Разное оружие лежит в своих файлах по-разному: у одних клинок по +Z,
        # у других по -Z или вдоль X. Поэтому доворачиваем каждое так, чтобы
        # клинок смотрел туда же, куда у эталонного кинжала.
        blade = blade_direction(meshes, roots[0])
        print("   направление клинка: (%.3f, %.3f, %.3f)" % (blade.x, blade.y, blade.z) if blade else "   направление не определено")
        if blade is not None:
            axis = blade.cross(BLADE_REF)
            dot = max(-1.0, min(1.0, blade.dot(BLADE_REF)))
            angle = math.acos(dot)
            if axis.length > 1e-6 and angle > math.radians(2):
                fix = Matrix.Rotation(angle, 4, axis.normalized())
                for o in roots:
                    loc, rot, _ = (fix @ o.matrix_world).decompose()
                    o.location = Vector(hero["weapon_pos"])
                    o.rotation_euler = rot.to_euler('XYZ')
                bpy.context.view_layer.update()
                print("   клинок довёрнут на %.0f°" % math.degrees(angle))

        # поправка на то, где у этого оружия опора: если она в торце рукояти,
        # сдвигаем клинок так, чтобы в кулаке оказалась точка хвата
        # меряем вдоль клинка, а не по углам габарита: проецируем все углы
        # бокса на направление клинка и смотрим, где на этом отрезке опора
        direction = blade_direction(meshes, roots[0]) or BLADE_REF
        mn, mx = bbox(meshes)
        corners = [Vector((x, y, z))
                   for x in (mn.x, mx.x) for y in (mn.y, mx.y) for z in (mn.z, mx.z)]
        proj = [c.dot(direction) for c in corners]
        lo, hi = min(proj), max(proj)
        length = hi - lo
        pivot = roots[0].matrix_world.translation
        along = (pivot.dot(direction) - lo) / max(length, 1e-6)
        shift = (grip_ratio - along) * length
        # двигаем оружие так, чтобы в кулаке оказалась точка хвата, а не опора:
        # если опора ближе к торцу, чем хват, оружие подтягиваем назад по клинку
        if abs(shift) > 0.005:
            for o in roots:
                o.location = Vector(o.location) - direction * shift
            bpy.context.view_layer.update()

        # прокрутка вокруг оси клинка: разворачивает лезвие плашмя или ребром
        if abs(roll) > 0.5:
            spin = Matrix.Rotation(math.radians(roll), 4, direction)
            for o in roots:
                keep = Vector(o.location)
                _, rot, _ = (spin @ o.matrix_world).decompose()
                o.location = keep
                o.rotation_euler = rot.to_euler('XYZ')
            bpy.context.view_layer.update()
            print("   лезвие прокручено на %.0f°" % roll)
        print("   опора на %.0f%% длины, поправка %.1f см" % (along * 100, shift * 100))

        if mirror:
            MIRROR = Matrix.Diagonal((-1, 1, 1, 1))
            br = arm.matrix_world @ arm.pose.bones[hero["bone_r"]].matrix
            bl = arm.matrix_world @ arm.pose.bones[bone_name].matrix
            local = br.inverted() @ roots[0].matrix_world
            loc, rot, _ = (bl @ (MIRROR @ local @ MIRROR)).decompose()
            for o in roots:
                o.location = loc
                o.rotation_euler = rot.to_euler('XYZ')
            bpy.context.view_layer.update()
        return meshes

    weapons = put(hero["bone_r"], mirror=False)
    if both:
        weapons += put(hero["bone_l"], mirror=True)
    return hero_meshes, weapons, arm


def shoot(objs, out_path, ang, pad):
    mn, mx = bbox(objs)
    c = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, (mn.z + mx.z) / 2))
    size = max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z) + pad
    cd = bpy.data.cameras.new("c"); cd.type = 'ORTHO'; cd.ortho_scale = size
    cam = bpy.data.objects.new("c", cd)
    bpy.context.scene.collection.objects.link(cam)
    a = math.radians(ang)
    cam.location = (c.x + 30 * math.sin(a), c.y - 30 * math.cos(a), c.z)
    cam.rotation_euler = (math.radians(90), 0, a)
    bpy.context.scene.camera = cam
    s = bpy.data.lights.new("s", type='SUN'); s.energy = 3.4
    so = bpy.data.objects.new("s", s)
    so.rotation_euler = (math.radians(55), math.radians(10), a + math.radians(35))
    bpy.context.scene.collection.objects.link(so)
    w_ = bpy.data.worlds.new("w"); bpy.context.scene.world = w_; w_.use_nodes = True
    w_.node_tree.nodes["Background"].inputs[0].default_value = (0.14, 0.15, 0.17, 1)
    w_.node_tree.nodes["Background"].inputs[1].default_value = 1.3
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE'
    sc.render.resolution_x = TILE_W
    sc.render.resolution_y = TILE_H
    sc.render.filepath = out_path
    sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    for o in list(bpy.context.scene.objects):
        if o.type in ('CAMERA', 'LIGHT'):
            bpy.data.objects.remove(o, do_unlink=True)


def main():
    weapon = arg("--weapon")
    if not weapon or not os.path.isfile(weapon):
        print("ОШИБКА: укажи --weapon <путь к .fbx>")
        return
    hero_name = arg("--hero", "human2")
    hero = HEROES.get(hero_name)
    if hero is None:
        print("ОШИБКА: неизвестный герой '%s'. Есть: %s" % (hero_name, ", ".join(HEROES)))
        return
    grip_ratio = float(arg("--grip", DEFAULT_GRIP))
    both = has_flag("--both")
    roll = float(arg("--roll", 0))
    name = os.path.splitext(os.path.basename(weapon))[0]
    out = arg("--out", os.path.join(TOOLS, "fit_%s.png" % name))

    print("ПРИМЕРКА: %s -> герой %s" % (name, hero_name))
    hero_meshes, weapons, arm = build(hero, weapon, grip_ratio, both, roll)
    mn, mx = bbox(weapons)
    print("   длина оружия %.3f м" % max(mx.x - mn.x, mx.y - mn.y, mx.z - mn.z))

    tiles = []
    for i, (ang, label) in enumerate(VIEWS):
        p = os.path.join(TOOLS, "_tile_%d.png" % i)
        # первые два кадра — кисть крупно, вторые два — фигура целиком
        shoot(weapons if i < 2 else hero_meshes + weapons, p, ang, 0.22 if i < 2 else 0.30)
        tiles.append(p)
        print("   снят вид: %s" % label)

    row = []
    for p in tiles:
        im = bpy.data.images.load(p)
        w, h = im.size
        a = np.empty(w * h * 4, dtype=np.float32)
        im.pixels.foreach_get(a)
        row.append(a.reshape(h, w, 4))
    big = np.concatenate(row, axis=1)
    H, W = big.shape[0], big.shape[1]
    img = bpy.data.images.new("fit", W, H, alpha=True)
    img.pixels.foreach_set(big.reshape(-1))
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()
    for p in tiles:
        try:
            os.remove(p)
        except OSError:
            pass
    print("ГОТОВО: %s" % out)


main()

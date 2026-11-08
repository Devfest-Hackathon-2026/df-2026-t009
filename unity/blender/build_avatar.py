"""Blender 4.5: onsite 用のオリジナル結晶トロフィーを生成する。"""

import argparse
import json
import math
import random
import struct
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


OUT = None
MATERIALS = {}
WRITTEN = []
PEDESTAL_TOP = 0.220
TRIANGLE_LIMIT = 35000
BODY_BUDGET = 25000
VOXEL_SIZE = 0.004
TYPES = ("gentleman", "lady", "gown", "goblin")
POSES = ("stand", "wave", "pose")


def clear_scene():
    # データ API で消し、画面や選択状態に依存しない。
    scene = bpy.context.scene
    scene.camera = None
    scene.world = None
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in (bpy.data.meshes, bpy.data.curves, bpy.data.cameras,
                       bpy.data.lights, bpy.data.materials, bpy.data.worlds):
        for block in list(collection):
            if block.users == 0:
                collection.remove(block)
    MATERIALS.clear()


def linear_color(hex_color):
    rgb = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple(c / 12.92 if c <= 0.04045
                 else ((c + 0.055) / 1.055) ** 2.4 for c in rgb)


def make_materials():
    settings = (("Body", "d9a441", 1.0, 0.18, 0.0, 0.22),
                ("Crystal", "ffcf6b", 0.2, 0.05, 0.6, 0.18),
                ("Base", "0b0d10", 0.3, 0.25, 0.0, 0.40))
    for name, color, metal, rough, transmission, coat in settings:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        mat.diffuse_color = (*linear_color(color), 1.0)
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = mat.diffuse_color
        shader.inputs["Metallic"].default_value = metal
        shader.inputs["Roughness"].default_value = rough
        shader.inputs["Transmission Weight"].default_value = transmission
        shader.inputs["IOR"].default_value = 1.46
        shader.inputs["Coat Weight"].default_value = coat
        shader.inputs["Coat Roughness"].default_value = 0.12
        MATERIALS[name] = mat


def mesh_object(name, vertices, faces, material="Body", smooth=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    # 閉じた体積の法線を揃える。
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(MATERIALS[material])
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    return obj


def ellipsoid(name, center, radii, rotation=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0)
    transform = Matrix.LocRotScale(Vector(center), rotation, Vector(radii))
    bmesh.ops.transform(bm, matrix=transform, verts=list(bm.verts))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(MATERIALS["Body"])
    return obj


def limb(name, start, end, radius, depth=None):
    # 関節を球で継がず、端まで太さが続く断面で接続する。
    a, b = Vector(start), Vector(end)
    axis = (b - a).normalized()
    depth = depth or radius
    return sweep(name, [(a - axis * radius * 0.35, radius * 0.85, depth * 0.85),
                        (a.lerp(b, 0.25), radius, depth),
                        (a.lerp(b, 0.75), radius * 0.93, depth * 0.93),
                        (b + axis * radius * 0.35, radius * 0.80, depth * 0.80)])


def axis_frame(axis):
    helper = Vector((0, 0, 1)) if abs(axis.z) < 0.92 else Vector((0, 1, 0))
    u = axis.cross(helper).normalized()
    return u, axis.cross(u).normalized()


def sweep(name, sections, segments=16):
    # 連続する楕円断面から、閉じた手足や衣服の稜線を作る。
    centers = [Vector(section[0]) for section in sections]
    vertices = []
    previous_u = None
    for i, (_, width, depth) in enumerate(sections):
        tangent = (centers[min(i + 1, len(centers) - 1)]
                   - centers[max(0, i - 1)]).normalized()
        # 前の断面軸を運び、曲がった膝でも輪が反転しないようにする。
        u = previous_u - tangent * previous_u.dot(tangent) if previous_u is not None else None
        if u is None or u.length < 1e-7:
            u, _ = axis_frame(tangent)
        u = u.normalized()
        v = tangent.cross(u).normalized()
        previous_u = u
        for j in range(segments):
            angle = math.tau * j / segments
            vertices.append(centers[i] + u * width * math.cos(angle)
                            + v * depth * math.sin(angle))
    faces = [tuple(reversed(range(segments)))]
    for row in range(len(sections) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            faces.append((row * segments + j, row * segments + k,
                          (row + 1) * segments + k, (row + 1) * segments + j))
    faces.append(tuple((len(sections) - 1) * segments + j for j in range(segments)))
    return mesh_object(name, vertices, faces)


def figure_point(point):
    # 造形用の解剖座標を、台座 22%・人物 78% の高さへ移す。
    x, y, z = point
    return Vector((x, y, PEDESTAL_TOP + (z - 0.320) * (0.780 / 0.680)))


def loft(name, rings, segments=32, folds=0.0):
    # 各段は (高さ, 横半径, 奥行半径, 中心 X, 中心 Y)。
    vertices = []
    for z, rx, ry, cx, cy in rings:
        for j in range(segments):
            angle = math.tau * j / segments
            ripple = 1.0 + folds * math.cos(8 * angle + 0.35) * (0.78 - z) / 0.46
            vertices.append((cx + rx * math.cos(angle) * ripple,
                             cy + ry * math.sin(angle) * ripple, z))
    faces = [tuple(reversed(range(segments)))]
    for row in range(len(rings) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            faces.append((row * segments + j, row * segments + k,
                          (row + 1) * segments + k, (row + 1) * segments + j))
    faces.append(tuple((len(rings) - 1) * segments + j for j in range(segments)))
    return mesh_object(name, vertices, faces)


def bake_modifier(obj, modifier):
    # 評価済みメッシュに置き換え、モディファイアを確実に焼き込む。
    modifier_name = modifier.name
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
    old_mesh = obj.data
    obj.modifiers.clear()
    obj.data = mesh
    if old_mesh.users == 0:
        bpy.data.meshes.remove(old_mesh)
    if not mesh.vertices or not mesh.polygons:
        raise RuntimeError(f"{obj.name}: empty result from {modifier_name}")


def subdivide(obj, levels=2):
    mod = obj.modifiers.new("Soft silhouette", "SUBSURF")
    mod.levels = levels
    mod.render_levels = levels
    bake_modifier(obj, mod)
    return obj


def combine(parts, name, material="Body"):
    # 座標は初めからワールド座標。全オブジェクトの変換は単位行列。
    bm = bmesh.new()
    for obj in parts:
        bm.from_mesh(obj.data)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for obj in parts:
        old_mesh = obj.data
        bpy.data.objects.remove(obj, do_unlink=True)
        if old_mesh.users == 0:
            bpy.data.meshes.remove(old_mesh)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.clear()
    mesh.materials.append(MATERIALS[material])
    for poly in mesh.polygons:
        poly.material_index = 0
    return obj


def triangulate(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def triangle_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def decimate_to(obj, budget):
    for _ in range(5):
        count = triangle_count(obj)
        if count <= budget:
            return
        mod = obj.modifiers.new("Triangle budget", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = min(0.98, budget / count * 0.96)
        mod.use_collapse_triangulate = True
        bake_modifier(obj, mod)
    if triangle_count(obj) > budget:
        raise RuntimeError(f"{obj.name}: cannot meet triangle budget {budget}")


def check_continuous(obj):
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        remaining = set(bm.verts)
        sizes = []
        while remaining:
            stack = [remaining.pop()]
            size = 0
            while stack:
                vertex = stack.pop()
                size += 1
                for edge in vertex.link_edges:
                    neighbor = edge.other_vert(vertex)
                    if neighbor in remaining:
                        remaining.remove(neighbor)
                        stack.append(neighbor)
            sizes.append(size)
        if len(sizes) != 1:
            print(f"警告: {obj.name}: つながっていない部分があります {sizes}（そのまま続けます）")
        if any(not edge.is_manifold for edge in bm.edges):
            raise RuntimeError(f"{obj.name}: sculpt is not a closed manifold")
    finally:
        bm.free()


def sculpt(parts, name):
    obj = combine(parts, name)
    for vertex in obj.data.vertices:
        vertex.co = figure_point(vertex.co)
    obj.data.update()
    mod = obj.modifiers.new("Fuse sculpt volumes", "REMESH")
    mod.mode = "VOXEL"
    mod.voxel_size = VOXEL_SIZE
    mod.adaptivity = 0.0
    mod.use_smooth_shade = True
    bake_modifier(obj, mod)
    mod = obj.modifiers.new("Polish transitions", "SMOOTH")
    mod.factor = 0.48
    mod.iterations = 3
    bake_modifier(obj, mod)
    decimate_to(obj, BODY_BUDGET)
    triangulate(obj)
    check_continuous(obj)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def make_pedestal():
    # 薄い二段。段の間だけに細い金帯を入れる。
    profile = ((0.000, 0.182), (0.004, 0.190), (0.012, 0.194),
               (0.094, 0.194), (0.102, 0.190), (0.104, 0.187),
               (0.104, 0.181), (0.106, 0.184), (0.113, 0.184),
               (0.116, 0.179), (0.116, 0.164), (0.120, 0.170),
               (0.210, 0.170), (0.217, 0.167), (PEDESTAL_TOP, 0.160))
    segments = 64
    vertices = [(r * math.cos(math.tau * j / segments),
                 r * math.sin(math.tau * j / segments), z)
                for z, r in profile for j in range(segments)]
    faces = [tuple(reversed(range(segments)))]
    bands = [0]
    smooth = [False]
    for row in range(len(profile) - 1):
        for j in range(segments):
            k = (j + 1) % segments
            faces.append((row * segments + j, row * segments + k,
                          (row + 1) * segments + k, (row + 1) * segments + j))
            bands.append(1 if row in (6, 7, 8) else 0)
            smooth.append(profile[row][0] != profile[row + 1][0])
    faces.append(tuple((len(profile) - 1) * segments + j for j in range(segments)))
    bands.append(0)
    smooth.append(False)
    obj = mesh_object("Pedestal", vertices, faces, "Base")
    obj.data.materials.append(MATERIALS["Body"])
    for poly, band, shading in zip(obj.data.polygons, bands, smooth):
        poly.material_index = band
        poly.use_smooth = shading
    return obj


def head_and_neck(parts, feminine=False):
    parts.append(limb("Neck", (0, 0.001, 0.866), (0, 0, 0.930), 0.016, 0.015))
    parts.append(ellipsoid("Head", (0, -0.001, 0.958),
                           (0.027 if feminine else 0.030, 0.026, 0.040)))
    parts.append(ellipsoid("Jaw", (0, -0.004, 0.936), (0.022, 0.023, 0.023)))
    parts.append(ellipsoid("Nose", (0, -0.026, 0.956), (0.005, 0.009, 0.010)))
    if feminine:
        parts.append(ellipsoid("Swept hair", (0, 0.008, 0.976), (0.028, 0.025, 0.023)))
        parts.append(ellipsoid("Hair bun", (0, 0.033, 0.975), (0.022, 0.023, 0.023)))
    else:
        parts.append(ellipsoid("Hair crest", (0, 0.005, 0.981), (0.029, 0.025, 0.019)))


def hand(parts, name, wrist, tip, radius=0.011):
    # 指は分けず、手首から先細りになるミトン形にする。
    wrist, tip = Vector(wrist), Vector(tip)
    axis = (tip - wrist).normalized()
    parts.append(sweep(name, [
        (wrist - axis * radius * 0.4, radius * 0.78, radius * 0.60),
        (wrist.lerp(tip, 0.33), radius * 1.16, radius * 0.72),
        (wrist.lerp(tip, 0.75), radius * 0.94, radius * 0.57),
        (tip, radius * 0.42, radius * 0.36)]))
    center = wrist.lerp(tip, 0.36)
    center.x += radius * (0.75 if wrist.x < 0 else -0.75)
    parts.append(ellipsoid(name + " thumb", center,
                           (radius * 0.63, radius * 0.62, radius * 0.85)))


def human_arm_points(pose, sign, feminine=False):
    shoulder = (sign * (0.057 if feminine else 0.066), 0, 0.855)
    if pose == "wave" and sign == -1:
        elbow = (-0.120, 0.001, 0.912)
        wrist = (-0.155, -0.012, 0.953)
        tip = (-0.159, -0.013, 0.980)
    elif pose == "pose":
        elbow = (sign * 0.094, -0.015, 0.772)
        wrist = (sign * 0.022, -0.067, 0.818)
        tip = (sign * 0.010, -0.069, 0.833)
    else:
        elbow = (sign * 0.083, -0.003, 0.771)
        wrist = (sign * 0.079, -0.017, 0.698)
        tip = (sign * 0.076, -0.020, 0.672)
    return tuple(Vector(point) for point in (shoulder, elbow, wrist, tip))


def continuous_arm(parts, name, points, radius):
    shoulder, elbow, wrist, tip = points
    # 肩・肘の球をなくし、肘の前後にも体積を残して一体化する。
    parts.append(sweep(name, [
        (shoulder, radius * 1.18, radius * 1.03),
        (shoulder.lerp(elbow, 0.35), radius * 1.06, radius * 0.96),
        (elbow, radius * 0.82, radius * 0.80),
        (elbow.lerp(wrist, 0.38), radius * 0.87, radius * 0.77),
        (wrist, radius * 0.60, radius * 0.58)]))
    hand(parts, name + " mitten", wrist, tip, radius * 0.74)


def human_arms(parts, pose, feminine=False):
    radius = 0.0155 if feminine else 0.019
    for sign in (-1, 1):
        continuous_arm(parts, "Sculpted arm",
                       human_arm_points(pose, sign, feminine), radius)


def lapel(sign):
    # 胸の前面へ食い込ませた、厚みのある折り返し。
    front = [(sign * 0.016, -0.042, 0.865),
             (sign * 0.051, -0.034, 0.844),
             (sign * 0.018, -0.046, 0.783)]
    back = [(x, y + 0.022, z) for x, y, z in front]
    return mesh_object("Lapel", front + back,
                       [(0, 1, 2), (5, 4, 3), (0, 3, 4, 1),
                        (1, 4, 5, 2), (2, 5, 3, 0)])


def make_gentleman(pose):
    parts = []
    for sign in (-1, 1):
        x = sign * 0.033
        parts.append(ellipsoid("Shoe", (x, -0.019, 0.337), (0.025, 0.043, 0.020)))
        parts.append(sweep("Continuous trousers", [
            ((x, 0, 0.346), 0.018, 0.021),
            ((x, 0.001, 0.420), 0.021, 0.023),
            ((x, 0.004, 0.510), 0.019, 0.023),
            ((sign * 0.031, 0.003, 0.585), 0.025, 0.027),
            ((sign * 0.028, 0, 0.703), 0.029, 0.032)]))
        # 稜線の半分をズボンへ埋め、リメッシュ後も折り目を残す。
        parts.append(sweep("Pressed trouser crease", [
            ((x, -0.021, 0.370), 0.0055, 0.006),
            ((x, -0.022, 0.470), 0.006, 0.006),
            ((x, -0.020, 0.515), 0.006, 0.006),
            ((sign * 0.029, -0.028, 0.651), 0.006, 0.006)], segments=12))
        parts.append(subdivide(loft("Jacket tail", [
            (0.626, 0.020, 0.014, sign * 0.026, 0.030),
            (0.638, 0.024, 0.016, sign * 0.026, 0.030),
            (0.713, 0.025, 0.018, sign * 0.023, 0.024),
            (0.746, 0.021, 0.018, sign * 0.021, 0.022)])))
        parts.append(lapel(sign))
        parts.append(limb("Lapel ridge", (sign * 0.039, -0.037, 0.849),
                          (sign * 0.018, -0.039, 0.788), 0.0065, 0.007))
    parts.append(subdivide(loft("Flared tailored jacket", [
        (0.645, 0.064, 0.037, 0, 0.003), (0.657, 0.067, 0.039, 0, 0.003),
        (0.700, 0.054, 0.035, 0, 0.002), (0.748, 0.041, 0.029, 0, 0),
        (0.784, 0.046, 0.033, 0, 0), (0.840, 0.067, 0.039, 0, 0),
        (0.860, 0.062, 0.035, 0, 0), (0.877, 0.025, 0.024, 0, 0)])))
    head_and_neck(parts)
    human_arms(parts, pose)
    return sculpt(parts, "Gentleman sculpt")


def make_lady(pose):
    parts = []
    for sign in (-1, 1):
        x = sign * 0.026
        parts.append(ellipsoid("Pump", (x, -0.016, 0.333), (0.018, 0.034, 0.016)))
        parts.append(sweep("Streamlined leg", [
            ((x, 0, 0.341), 0.012, 0.015),
            ((x, 0.003, 0.411), 0.018, 0.020),
            ((x, 0.005, 0.493), 0.015, 0.018),
            ((sign * 0.025, 0.002, 0.568), 0.022, 0.025),
            ((sign * 0.024, 0, 0.679), 0.026, 0.029)]))
    # 膝の高さまでの裾と、絞った腰で控えめな A ラインを作る。
    parts.append(subdivide(loft("Knee length A-line dress", [
        (0.514, 0.064, 0.037, 0, 0), (0.525, 0.065, 0.038, 0, 0),
        (0.601, 0.057, 0.035, 0, 0), (0.686, 0.050, 0.034, 0, 0.002),
        (0.746, 0.033, 0.026, 0, 0), (0.775, 0.034, 0.026, 0, 0),
        (0.827, 0.049, 0.033, 0, -0.002), (0.855, 0.053, 0.029, 0, 0),
        (0.875, 0.023, 0.020, 0, 0)])))
    head_and_neck(parts, feminine=True)
    human_arms(parts, pose, feminine=True)
    return sculpt(parts, "Lady sculpt")


def make_gown(pose):
    # 後方へ張り出す裾を台座上へ寝かせ、胴から続くトレーンにする。
    rings = [
        (0.318, 0.108, 0.128, 0, 0.030),
        (0.326, 0.111, 0.130, 0, 0.030),
        (0.348, 0.106, 0.113, 0, 0.025),
        (0.400, 0.091, 0.088, 0, 0.018),
        (0.470, 0.077, 0.066, 0, 0.010),
        (0.560, 0.061, 0.048, 0, 0.004),
        (0.665, 0.051, 0.035, 0, 0.002),
        (0.747, 0.032, 0.025, 0, 0),
        (0.779, 0.034, 0.027, 0, 0),
        (0.830, 0.050, 0.034, 0, -0.001),
        (0.855, 0.054, 0.029, 0, 0),
        (0.876, 0.024, 0.021, 0, 0)]
    parts = [subdivide(loft("Flowing gown and swept train", rings,
                            segments=64, folds=0.050))]
    parts.append(sweep("Train resting on pedestal", [
        ((0, 0.035, 0.365), 0.073, 0.018),
        ((0, 0.085, 0.333), 0.082, 0.012),
        ((0, 0.133, 0.327), 0.067, 0.009),
        ((0, 0.157, 0.326), 0.031, 0.008)]))
    # 八本の縦ひだ。太い根を本体へ重ね、丸めても消えない高さを持たせる。
    for j in range(8):
        angle = math.tau * j / 8 + 0.12
        sections = []
        for z, rx, ry, cx, cy in rings[1:7]:
            strength = (0.748 - z) / 0.430
            ripple = 1.0 + 0.050 * math.cos(8 * angle + 0.35) * (0.78 - z) / 0.46
            center = (cx + (rx * ripple - 0.003) * math.cos(angle),
                      cy + (ry * ripple - 0.003) * math.sin(angle), z)
            radius = 0.005 + 0.006 * strength
            sections.append((center, radius, radius))
        parts.append(sweep("Vertical gown pleat", sections, segments=12))
    head_and_neck(parts, feminine=True)
    human_arms(parts, pose, feminine=True)
    return sculpt(parts, "Gown sculpt")


def goblin_ear(sign):
    # 耳の根は頭の内部、先端は上外側へ伸ばす。
    start = Vector((sign * 0.043, -0.038, 0.874))
    end = Vector((sign * 0.150, -0.010, 0.963))
    axis = (end - start).normalized()
    u = axis.cross(Vector((0, 1, 0))).normalized()
    v = axis.cross(u).normalized()
    vertices = []
    for t, width, depth in ((0, 0.026, 0.018), (0.35, 0.029, 0.013),
                             (0.77, 0.013, 0.006), (0.99, 0.0015, 0.0015)):
        center = start.lerp(end, t)
        for j in range(12):
            angle = math.tau * j / 12
            vertices.append(center + u * (width * math.cos(angle))
                            + v * (depth * math.sin(angle)))
    faces = [tuple(reversed(range(12)))]
    for row in range(3):
        for j in range(12):
            k = (j + 1) % 12
            faces.append((row * 12 + j, row * 12 + k,
                          (row + 1) * 12 + k, (row + 1) * 12 + j))
    faces.append(tuple(range(36, 48)))
    return mesh_object("Pointed ear", vertices, faces)


def goblin_arm_points(pose, sign):
    shoulder = (sign * 0.073, -0.014, 0.766)
    if pose == "wave" and sign == -1:
        elbow = (-0.139, -0.020, 0.845)
        wrist = (-0.177, -0.036, 0.914)
        tip = (-0.180, -0.037, 0.946)
    elif pose == "pose":
        elbow = (sign * 0.116, -0.041, 0.663)
        wrist = (sign * 0.022, -0.112, 0.748)
        tip = (sign * 0.010, -0.113, 0.768)
    elif pose == "stand":
        elbow = (sign * 0.121, -0.036, 0.653)
        wrist = (sign * 0.022, -0.078, 0.650)
        tip = (sign * 0.008, -0.078, 0.669)
    else:
        elbow = (sign * 0.119, -0.030, 0.647)
        wrist = (sign * 0.112, -0.060, 0.557)
        tip = (sign * 0.107, -0.063, 0.525)
    return tuple(Vector(point) for point in (shoulder, elbow, wrist, tip))


def make_goblin(pose):
    parts = []
    for sign in (-1, 1):
        parts.append(ellipsoid("Oversized foot", (sign * 0.055, -0.030, 0.340),
                               (0.043, 0.070, 0.024)))
        # 膝を前、腰を後ろへ引き、短い脚で明確にしゃがませる。
        parts.append(sweep("Crouched leg", [
            ((sign * 0.052, 0.010, 0.354), 0.024, 0.026),
            ((sign * 0.067, -0.018, 0.412), 0.027, 0.028),
            ((sign * 0.085, -0.055, 0.466), 0.029, 0.030),
            ((sign * 0.062, -0.005, 0.526), 0.036, 0.035),
            ((sign * 0.043, 0.054, 0.585), 0.040, 0.038)]))
    parts.append(ellipsoid("Pelvis", (0, 0.050, 0.579), (0.066, 0.050, 0.055)))
    parts.append(subdivide(loft("Forward hunched torso", [
        (0.555, 0.057, 0.045, 0, 0.050),
        (0.625, 0.062, 0.050, 0, 0.043),
        (0.700, 0.067, 0.054, 0, 0.025),
        (0.764, 0.081, 0.055, 0, -0.006),
        (0.800, 0.067, 0.048, 0, -0.021),
        (0.822, 0.027, 0.026, 0, -0.035)])))
    # 肩から背へ重なる小さなケープ。裾は胴の輪郭より外へ広げる。
    parts.append(subdivide(loft("Small draped cape", [
        (0.604, 0.079, 0.014, 0, 0.090),
        (0.616, 0.085, 0.017, 0, 0.087),
        (0.706, 0.089, 0.022, 0, 0.067),
        (0.773, 0.075, 0.026, 0, 0.034),
        (0.797, 0.055, 0.025, 0, 0.018)])))
    parts.append(limb("Forward neck", (0, -0.026, 0.794),
                      (0, -0.048, 0.849), 0.028))
    parts.append(ellipsoid("Goblin head", (0, -0.052, 0.867), (0.057, 0.045, 0.061)))
    parts.append(ellipsoid("Muzzle", (0, -0.084, 0.840), (0.041, 0.028, 0.024)))
    parts.append(ellipsoid("Big hooked nose", (0, -0.095, 0.866), (0.018, 0.035, 0.032)))
    parts.append(ellipsoid("Nose tip", (0, -0.119, 0.850), (0.019, 0.018, 0.017)))
    for sign in (-1, 1):
        parts.append(goblin_ear(sign))
        parts.append(limb("Knowing brow", (sign * 0.010, -0.092, 0.888),
                          (sign * 0.040, -0.080, 0.896 - sign * 0.004), 0.008, 0.007))
        parts.append(ellipsoid("Cheek", (sign * 0.035, -0.080, 0.854),
                               (0.021, 0.018, 0.021)))
        parts.append(limb("Smile corner", (sign * 0.013, -0.105, 0.834),
                          (sign * 0.033, -0.094, 0.840 + sign * 0.003), 0.0055))
        continuous_arm(parts, "Long goblin arm", goblin_arm_points(pose, sign), 0.025)
    return sculpt(parts, "Goblin sculpt")


def crystal_shard(name, root, direction, length, radius, twist=0.0):
    root, axis = Vector(root), Vector(direction).normalized()
    helper = Vector((0, 0, 1)) if abs(axis.z) < 0.92 else Vector((0, 1, 0))
    u = axis.cross(helper).normalized()
    v = axis.cross(u).normalized()
    vertices = []
    # 六角柱の肩から一点の先端へ。面は平滑化しない。
    for t, scale in ((0.0, 0.72), (0.18, 1.0), (0.72, 0.77)):
        for j in range(6):
            angle = twist + math.tau * j / 6
            vertices.append(root + axis * (length * t)
                            + (u * math.cos(angle) + v * math.sin(angle)) * radius * scale)
    vertices.append(root + axis * length)
    faces = [tuple(reversed(range(6)))]
    for row in range(2):
        for j in range(6):
            k = (j + 1) % 6
            faces.append((row * 6 + j, row * 6 + k, (row + 1) * 6 + k, (row + 1) * 6 + j))
    for j in range(6):
        faces.append((12 + j, 12 + (j + 1) % 6, 18))
    return mesh_object(name, vertices, faces, "Crystal", smooth=False)


def make_crystals(seed, anchors):
    rng = random.Random(seed)
    shards = []
    for index, anchor in enumerate(anchors):
        root = Vector(anchor["point"])
        normal = Vector(anchor["normal"]).normalized()
        surface = anchor["surface"]
        if surface is not None:
            nearest, surface_normal, _, _ = surface.find_nearest(root)
            if nearest is None:
                raise RuntimeError("Cannot find sculpt surface for crystal growth")
            root, normal = nearest, surface_normal.normalized()
        u, v = axis_frame(normal)
        parents = []
        for j in range(anchor["count"]):
            # 約半数を親から 20〜40 度で分枝させ、群れの輪郭を密にする。
            if j >= 5 and j % 2 == 1:
                parent_root, parent_axis, parent_length, parent_width = parents[
                    rng.randrange(min(len(parents), 8))]
                pu, pv = axis_frame(parent_axis)
                angle = rng.uniform(0, math.tau)
                lateral = pu * math.cos(angle) + pv * math.sin(angle)
                tilt = math.radians(rng.uniform(20, 40))
                axis = parent_axis * math.cos(tilt) + lateral * math.sin(tilt)
                base = (parent_root + parent_axis * parent_length * rng.uniform(0.25, 0.43)
                        + lateral * parent_width * 0.18)
                length = parent_length * rng.uniform(0.34, 0.55)
                width = parent_width * rng.uniform(0.40, 0.66)
                label = "Branch"
            else:
                angle = rng.uniform(0, math.tau)
                distance = anchor["spread"] * math.sqrt(rng.random()) if j else 0.0
                query = root + (u * math.cos(angle) + v * math.sin(angle)) * distance
                local_normal = normal
                if surface is not None:
                    nearest, surface_normal, _, _ = surface.find_nearest(query)
                    if nearest is None:
                        raise RuntimeError("Cannot seat crystal root in sculpt")
                    query, local_normal = nearest, surface_normal.normalized()
                lu, lv = axis_frame(local_normal)
                jitter_angle = rng.uniform(0, math.tau)
                jitter = 0.0 if j == 0 else rng.uniform(0.10, 0.42)
                axis = (local_normal + (lu * math.cos(jitter_angle)
                                       + lv * math.sin(jitter_angle)) * jitter).normalized()
                length = anchor["length"] * (1.0 if j == 0 else rng.uniform(0.43, 0.92))
                width = anchor["radius"] * (1.0 if j == 0 else rng.uniform(0.40, 1.06))
                # 台座と皮膚の内側へ根を埋め、貼り付けた印象をなくす。
                base = query - local_normal * max(0.004, width * 0.65)
                parents.append((base, axis, length, width))
                label = "Growth"
            shards.append(crystal_shard(f"{label} {index:02d}.{j:02d}", base, axis,
                                        length, width, rng.uniform(0, math.tau)))
    obj = combine(shards, "Six dense crystal colonies", "Crystal")
    for poly in obj.data.polygons:
        poly.use_smooth = False
    print("Crystal clusters: " + ", ".join(str(a["count"]) for a in anchors)
          + f" shards ({sum(a['count'] for a in anchors)} total)", flush=True)
    return obj


def crystal_anchors(body_type, pose, body):
    # 完成した彫像の表面へ投影し、各ポーズで前腕にも確実に根付かせる。
    surface = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in body.data.vertices],
        [tuple(poly.vertices) for poly in body.data.polygons], all_triangles=True)

    def site(point, normal, length, radius, count, spread, on_body=True):
        return {"point": figure_point(point) if on_body else Vector(point),
                "normal": normal, "length": length, "radius": radius,
                "count": count, "spread": spread, "surface": surface if on_body else None}

    if body_type == "goblin":
        leg = (-0.103, -0.044, 0.447)
        shoulder = (-0.077, -0.008, 0.782)
        back = (-0.030, 0.083, 0.727)
        crown = (0, -0.046, 0.923)
        arm = goblin_arm_points(pose, 1)
    else:
        leg = ((-0.077, -0.043, 0.455) if body_type == "gown"
               else (-0.050, -0.012, 0.443))
        shoulder = (-0.060, 0.004, 0.858)
        back = (-0.023, 0.038, 0.815)
        crown = (-0.014, 0.005, 0.987)
        arm = human_arm_points(pose, 1, feminine=body_type != "gentleman")
    forearm = arm[1].lerp(arm[2], 0.52) + Vector((0.009, -0.006, 0))
    return [
        site((-0.092, -0.010, PEDESTAL_TOP), (0, 0, 1),
             0.220, 0.020, 40, 0.052, on_body=False),
        site(leg, (-0.85, -0.3, 0.2), 0.135, 0.014, 18, 0.045),
        site(shoulder, (-0.55, 0.2, 0.8), 0.155, 0.018, 22, 0.033),
        site(back, (-0.15, 0.85, 0.5), 0.132, 0.014, 18, 0.030),
        site(forearm, (0.75, -0.35, 0.3), 0.065, 0.0075, 10, 0.020),
        site(crown, (0, 0, 1), 0.100 if body_type == "goblin" else 0.036,
             0.008 if body_type == "goblin" else 0.006,
             11 if body_type == "goblin" else 9, 0.033 if body_type == "goblin" else 0.015)]


def held_crystal(body_type):
    if body_type == "goblin":
        root, length = (0, -0.108, 0.741), 0.078
    else:
        root, length = (0, -0.064, 0.807), 0.076
    return crystal_shard("Held heart crystal", figure_point(root),
                         (0, -0.15, 1), length, 0.016, 0.2)


def model_meshes():
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]


def prepare_export(name):
    objects = model_meshes()
    # 台座底中央を原点にし、全メッシュの高さを 1 に揃える。
    lower = min(vertex.co.z for obj in objects for vertex in obj.data.vertices)
    upper = max(vertex.co.z for obj in objects for vertex in obj.data.vertices)
    scale = 1.0 / (upper - lower)
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co.z -= lower
            vertex.co *= scale
        obj.data.update()
        triangulate(obj)
    total = sum(triangle_count(obj) for obj in objects)
    if total > TRIANGLE_LIMIT:
        raise RuntimeError(f"{name}: {total} triangles exceeds {TRIANGLE_LIMIT}")
    actual_materials = {mat.name for obj in objects for mat in obj.data.materials}
    if actual_materials != {"Body", "Crystal", "Base"}:
        raise RuntimeError(f"{name}: invalid materials {actual_materials}")
    for obj in objects:
        if obj.modifiers or any(abs(obj.matrix_world[r][c] - float(r == c)) > 1e-7
                                for r in range(4) for c in range(4)):
            raise RuntimeError(f"{name}: unapplied transform/modifier on {obj.name}")
        if any(len(poly.vertices) != 3 for poly in obj.data.polygons):
            raise RuntimeError(f"{name}: non-triangular polygon")
    print(f"{name}: {total:,} triangles; height 1.000; origin at pedestal bottom", flush=True)
    return objects


def validate_glb(path):
    # 実ファイルでも三角形数、材質、不要ノードを確かめる。
    with path.open("rb") as stream:
        magic, version, length = struct.unpack("<4sII", stream.read(12))
        chunk_length, chunk_type = struct.unpack("<II", stream.read(8))
        if magic != b"glTF" or version != 2 or chunk_type != 0x4E4F534A:
            raise RuntimeError(f"{path.name}: invalid GLB header")
        doc = json.loads(stream.read(chunk_length).decode("utf8"))
    if length != path.stat().st_size:
        raise RuntimeError(f"{path.name}: truncated GLB")
    triangles = 0
    for mesh in doc.get("meshes", []):
        for primitive in mesh["primitives"]:
            if primitive.get("mode", 4) != 4:
                raise RuntimeError(f"{path.name}: non-triangle primitive")
            accessor = doc["accessors"][primitive["indices"]]
            if accessor["count"] % 3:
                raise RuntimeError(f"{path.name}: invalid triangle index count")
            triangles += accessor["count"] // 3
    if not 0 < triangles <= TRIANGLE_LIMIT:
        raise RuntimeError(f"{path.name}: exported triangle count {triangles}")
    if {mat["name"] for mat in doc.get("materials", [])} != {"Body", "Crystal", "Base"}:
        raise RuntimeError(f"{path.name}: exported material names changed")
    if doc.get("cameras") or "KHR_lights_punctual" in doc.get("extensions", {}):
        raise RuntimeError(f"{path.name}: unwanted camera/light")


def export(name):
    objects = prepare_export(name)
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    path = OUT / f"{name}.glb"
    with bpy.context.temp_override(active_object=objects[0], object=objects[0],
                                   selected_objects=objects, selected_editable_objects=objects):
        result = bpy.ops.export_scene.gltf(
            filepath=str(path), check_existing=False, export_format="GLB",
            use_selection=True, export_yup=True, export_apply=True,
            export_materials="EXPORT", export_normals=True,
            export_texcoords=False, export_animations=False,
            export_cameras=False, export_lights=False)
    if "FINISHED" not in result:
        raise RuntimeError(f"Export failed: {path}")
    validate_glb(path)
    WRITTEN.append(path)


def area_light(name, position, target, energy, color, size, size_y=None):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "RECTANGLE"
    data.size = size
    data.size_y = size_y if size_y is not None else size
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = position
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_preview(name):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = False
    scene.render.use_file_extension = True
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.exposure = 0.0
    scene.view_settings.gamma = 1.0
    if hasattr(scene.eevee, "taa_render_samples"):
        scene.eevee.taa_render_samples = 96
    if hasattr(scene.eevee, "use_raytracing"):
        scene.eevee.use_raytracing = True
    world = bpy.data.worlds.new("Dark studio")
    world.use_nodes = True
    scene.world = world
    background = world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (*linear_color("05080a"), 1.0)
    background.inputs["Strength"].default_value = 1.0
    camera_data = bpy.data.cameras.new("Preview camera")
    camera = bpy.data.objects.new("Preview camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (1.10, -2.60, 1.06)
    target = Vector((0, 0, 0.50))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera_data.type = "ORTHO"
    # 投影した全頂点から余白を決め、結晶を含む像を画面の約 80% に収める。
    rotation = camera.rotation_euler.to_matrix()
    inverse = rotation.transposed()
    projected = [inverse @ vertex.co for obj in model_meshes()
                 for vertex in obj.data.vertices]
    left, right = min(p.x for p in projected), max(p.x for p in projected)
    bottom, top = min(p.y for p in projected), max(p.y for p in projected)
    center = rotation @ Vector(((left + right) * 0.5, (bottom + top) * 0.5,
                               (inverse @ target).z))
    camera.location += center - target
    camera_data.ortho_scale = max(top - bottom, right - left) / 0.80
    camera_data.lens = 70
    camera_data.clip_start = 0.01
    camera_data.clip_end = 100
    scene.camera = camera
    area_light("Warm key", (-0.90, -1.30, 1.70), (0, 0, 0.65),
               100, (1.0, 0.77, 0.48), 1.0, 1.4)
    area_light("Cyan rim behind left", (-0.95, 0.70, 1.35), (0, 0, 0.65),
               125, (0.22, 0.80, 1.0), 0.55, 1.05)
    area_light("Broad soft fill", (0.80, -1.65, 0.95), (0, 0, 0.60),
               55, (1.0, 0.94, 0.82), 1.2, 1.4)
    # Eevee の旧 Bloom に依存せず、背景実行でも合成で輝きを足す。
    scene.use_nodes = True
    scene.render.use_compositing = True
    nodes, links = scene.node_tree.nodes, scene.node_tree.links
    nodes.clear()
    layers = nodes.new("CompositorNodeRLayers")
    glare = nodes.new("CompositorNodeGlare")
    glare.glare_type = "FOG_GLOW"
    glare.quality = "HIGH"
    if glare.inputs.get("Threshold") is not None:
        glare.inputs["Threshold"].default_value = 1.5
        glare.inputs["Size"].default_value = 0.18
        glare.inputs["Strength"].default_value = 0.20
    else:
        glare.threshold = 1.5
        glare.size = 7
        glare.mix = -0.80
    composite = nodes.new("CompositorNodeComposite")
    links.new(layers.outputs["Image"], glare.inputs["Image"])
    links.new(glare.outputs["Image"], composite.inputs["Image"])
    path = OUT / f"{name}_preview.png"
    scene.render.filepath = str(path)
    result = bpy.ops.render.render(write_still=True)
    if "FINISHED" not in result or not path.is_file():
        raise RuntimeError(f"Preview render failed: {path}")
    WRITTEN.append(path)


def main():
    global OUT
    # Blender 自身の引数は解析しない。
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    options = parser.parse_args(args)
    OUT = options.out.expanduser().resolve()
    OUT.mkdir(parents=True, exist_ok=True)
    WRITTEN.clear()
    builders = {"gentleman": make_gentleman, "lady": make_lady,
                "gown": make_gown, "goblin": make_goblin}
    clear_scene()
    for type_index, body_type in enumerate(TYPES):
        for pose in POSES:
            clear_scene()
            make_materials()
            make_pedestal()
            body = builders[body_type](pose)
            # 同じ乱数列を使い、成長位置だけを実際のポーズへ追従させる。
            make_crystals(8107 + type_index * 101, crystal_anchors(body_type, pose, body))
            if pose == "pose":
                held_crystal(body_type)
            name = f"avatar_{body_type}_{pose}"
            export(name)
            if pose == "stand":
                render_preview(f"avatar_{body_type}")
    clear_scene()
    if len(WRITTEN) != 16:
        raise RuntimeError(f"Expected 16 outputs, got {len(WRITTEN)}")
    print("\nFiles written:", flush=True)
    for path in WRITTEN:
        print(str(path), flush=True)
    print("\nChanges: six dense, surface-rooted crystal clusters (40-shard pedestal fan, "
          "22-shard shoulder burst, leg/back/forearm/head growth); 20-40 degree branches; "
          "0.004-voxel polished continuous sculpts and tapered mittens; "
          "tailored suit creases, knee-length dress, eight gown pleats and swept train, "
          "crouched long-armed goblin with cape and crystal crown; "
          "two-tier 22% pedestal; lower 80% framing, warm key/cyan rear-left rim/soft fill "
          "and compositor glow; maximum 35,000 triangles per GLB.", flush=True)


if __name__ == "__main__":
    main()

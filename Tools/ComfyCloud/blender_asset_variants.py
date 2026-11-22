#!/usr/bin/env python3
"""Local authored variants of reviewed Cloud output: a body-rig pilot and calibrated view inputs.

Run inside Blender. Rigging and view rendering are local work, never advertised as Cloud nodes.
The body rig is a deformation pilot on the generated triangle topology; face rigging is separate.
"""
import argparse
import json
import math
import shutil
import sys
import time
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Quaternion

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_postprocess as pipeline


def fresh_output(path):
    path.mkdir(parents=True, exist_ok=True)
    if any(path.iterdir()):
        raise ValueError("Use a new version/output directory; do not overwrite reviewed assets")


def new_bone(armature, name, head, tail, parent=None, deform=True):
    bone = armature.data.edit_bones.new(name)
    bone.head, bone.tail = head, tail
    bone.use_deform = deform
    if parent:
        bone.parent = armature.data.edit_bones[parent]
    return bone


def segment_distance(point, head, tail):
    line = tail - head
    t = max(0, min(1, (point - head).dot(line) / max(1e-12, line.length_squared)))
    return (point - head - line * t).length


def vertex_luminance(mesh):
    """Sample the generated base colour to separate the dark apron from white sleeves."""
    import numpy as np
    result = [1.0] * len(mesh.data.vertices)
    if not mesh.data.materials or mesh.data.uv_layers.active is None: return result
    material = mesh.data.materials[0]
    shader = material.node_tree.nodes.get("Principled BSDF") if material.use_nodes else None
    links = shader.inputs["Base Color"].links if shader else []
    image = links[0].from_node.image if links and links[0].from_node.type == "TEX_IMAGE" else None
    if image is None: return result
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    uv = mesh.data.uv_layers.active.data
    for loop in mesh.data.loops:
        value = uv[loop.index].uv
        x = min(width - 1, max(0, int(value.x * width)))
        y = min(height - 1, max(0, int(value.y * height)))
        start = (y * width + x) * 4
        result[loop.vertex_index] = float(max(pixels[start:start + 3]))
    return result


def body_rig(meshes, output, descriptor):
    """An explicitly authored 20-bone Generic rig for this approved 1.75m character."""
    # glTF splits vertices at UV seams on every import. Weld positions again
    # before skinning, preserving loop UVs so a colour boundary cannot tear open.
    rig_cleanup = pipeline.clean_source(meshes, {"cleanup": {"weld_coincident_vertices": True}})
    bpy.ops.object.armature_add(enter_editmode=True)
    rig = bpy.context.object
    rig.name = "KitchenBodyRig"
    rig.data.edit_bones.remove(rig.data.edit_bones[0])
    new_bone(rig, "Root", (0, 0, 0), (0, 0, .12), deform=False)
    for name, head, tail, parent in (
        ("Hips", (0, 0, .94), (0, 0, 1.06), "Root"),
        ("Spine", (0, 0, 1.06), (0, 0, 1.25), "Hips"),
        ("Chest", (0, 0, 1.25), (0, 0, 1.43), "Spine"),
        ("Neck", (0, 0, 1.43), (0, 0, 1.53), "Chest"),
        ("Head", (0, 0, 1.53), (0, 0, 1.74), "Neck"),
    ):
        new_bone(rig, name, head, tail, parent)
    for side, sign in (("Left", 1), ("Right", -1)):
        for name, head, tail, parent in (
            ("Shoulder", (.035, 0, 1.42), (.17, 0, 1.42), "Chest"),
            ("UpperArm", (.17, 0, 1.42), (.285, 0, 1.16), side + "Shoulder"),
            ("LowerArm", (.285, 0, 1.16), (.365, -.005, .94), side + "UpperArm"),
            ("Hand", (.365, -.005, .94), (.405, -.005, .86), side + "LowerArm"),
            ("UpperLeg", (.105, 0, .94), (.105, -.012, .53), "Hips"),
            ("LowerLeg", (.105, -.012, .53), (.105, .005, .12), side + "UpperLeg"),
            ("Foot", (.105, .005, .12), (.105, -.15, .065), side + "LowerLeg"),
        ):
            new_bone(rig, side + name, (head[0] * sign, head[1], head[2]),
                     (tail[0] * sign, tail[1], tail[2]), parent)
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.show_in_front = True
    pipeline.select_only(meshes + [rig])
    bpy.context.view_layer.objects.active = rig
    bones = [b for b in rig.data.bones if b.use_deform]
    validated_meshes = []
    apron_vertices = 0
    for mesh in meshes:
        validated_meshes.append(mesh.data.validate(verbose=True, clean_customdata=False))
        mesh.modifiers.new("BodyDeformation", "ARMATURE").object = rig
        mesh.parent = rig
        mesh.vertex_groups.clear()
        luminance = vertex_luminance(mesh)
        groups = {bone.name: mesh.vertex_groups.get(bone.name) or mesh.vertex_groups.new(name=bone.name) for bone in bones}
        for vertex in mesh.data.vertices:
            x, y, z = vertex.co
            side = "Left" if x >= 0 else "Right"
            # Garment geometry needs semantic weights: the apron is attached to
            # the pelvis, not the knees behind it. This is sample-specific skinning.
            if .68 < z < 1.43 and abs(x) < .30 and y < 0 and luminance[vertex.index] < .4:
                allowed = ["Hips", "Spine", "Chest"]
                apron_vertices += 1
            else:
                opposite = "Right" if side == "Left" else "Left"
                allowed = [bone.name for bone in bones if not bone.name.startswith(opposite)]
            nearest = sorted(((segment_distance(vertex.co, bone.head_local, bone.tail_local), bone.name)
                              for bone in bones if bone.name in allowed))[:3]
            weights = [(1 / (distance + .02)**4, name) for distance, name in nearest]
            denominator = sum(weight for weight, _ in weights)
            for weight, name in weights:
                groups[name].add([vertex.index], weight / denominator, "REPLACE")
        # Smooth on welded topology, then keep the three largest influences.
        # This removes abrupt sleeve/apron boundaries without changing UVs.
        import numpy as np
        weights = np.zeros((len(mesh.data.vertices), len(bones)), dtype=np.float32)
        for vertex in mesh.data.vertices:
            for group in vertex.groups: weights[vertex.index, group.group] = group.weight
        edges = np.array([tuple(edge.vertices) for edge in mesh.data.edges], dtype=np.int32)
        degree = np.zeros(len(weights), dtype=np.float32)
        np.add.at(degree, edges[:, 0], 1); np.add.at(degree, edges[:, 1], 1)
        for _ in range(6):
            neighbours = np.zeros_like(weights)
            np.add.at(neighbours, edges[:, 0], weights[edges[:, 1]])
            np.add.at(neighbours, edges[:, 1], weights[edges[:, 0]])
            weights = .6 * weights + .4 * neighbours / np.maximum(1, degree)[:, None]
        all_vertices = list(range(len(weights)))
        for group in mesh.vertex_groups: group.remove(all_vertices)
        for index, row in enumerate(weights):
            chosen = row.argsort()[-3:]
            denominator = sum(row[j] for j in chosen)
            for j in chosen: mesh.vertex_groups[int(j)].add([index], float(row[j] / denominator), "REPLACE")
    unweighted = sum(not any(g.weight > 1e-6 for g in v.groups) for obj in meshes for v in obj.data.vertices)
    if unweighted:
        raise ValueError("Unweighted vertices after skinning: " + str(unweighted))

    scene = bpy.context.scene
    scene.render.fps = 24
    scene.frame_start, scene.frame_end = 1, 49
    rig.animation_data_create()
    actions = []
    for motion in ("Stand", "Walk", "RaiseArm", "BendKnee"):
        action = bpy.data.actions.new(motion)
        action.use_fake_user = True
        rig.animation_data.action = action
        for frame in (1, 13, 25, 37, 49):
            phase = (frame - 1) * 2 * math.pi / 48
            for pose in rig.pose.bones:
                pose.rotation_mode = "XYZ"
                pose.rotation_euler = (0, 0, 0)
            if motion == "Stand":
                rig.pose.bones["Chest"].rotation_euler.x = math.sin(phase) * .012
            elif motion == "Walk":
                for side, sign in (("Left", 1), ("Right", -1)):
                    rig.pose.bones[side + "UpperLeg"].rotation_euler.x = math.sin(phase) * .32 * sign
                    rig.pose.bones[side + "LowerLeg"].rotation_euler.x = min(0, math.sin(phase) * sign) * .42
                    rig.pose.bones[side + "UpperArm"].rotation_euler.x = -math.sin(phase) * .16 * sign
            elif motion == "RaiseArm":
                pose = rig.pose.bones["LeftUpperArm"]
                basis = pose.bone.matrix_local.to_quaternion()
                pose.rotation_euler = (basis.inverted() @ Quaternion((0, 1, 0), -math.sin(phase / 2)**2 * .9) @ basis).to_euler("XYZ")
            else:
                rig.pose.bones["LeftUpperLeg"].rotation_euler.x = math.sin(phase / 2)**2 * .3
                rig.pose.bones["LeftLowerLeg"].rotation_euler.x = math.sin(phase / 2)**2 * -.55
            for pose in rig.pose.bones:
                pose.keyframe_insert(data_path="rotation_euler", frame=frame, group=pose.name)
        actions.append(action)

    # Render real deformations, not a static mesh with an unrelated animated skeleton.
    for action in actions:
        rig.animation_data.action = action
        scene.frame_set(13 if action.name == "Walk" else 25)
        for obj in list(scene.objects):
            if obj.type in {"LIGHT", "CAMERA"}:
                bpy.data.objects.remove(obj, do_unlink=True)
        pipeline.preview(meshes, output / ("body_" + action.name + ".png"), 1.75)
    rig.animation_data.action = actions[0]
    scene.frame_set(1)
    pipeline.select_only(meshes + [rig])
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "kitchen_character_rig.blend"))
    pipeline.export_glb(output / "kitchen_character_rig.glb", meshes + [rig])
    pipeline.select_only(meshes + [rig])
    bpy.ops.export_scene.fbx(filepath=str(output / "kitchen_character_rig.fbx"), use_selection=True,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, add_leaf_bones=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, path_mode="RELATIVE", embed_textures=False)
    descriptor.update({"intact_fbx": "kitchen_character_rig.fbx", "special_authoring_completed": True,
        "character_actions": [a.name for a in actions], "body_rig": {
            "type": "Generic", "bones": len(rig.data.bones), "unweighted_vertices": unweighted,
            "maximum_influences": 3, "mesh_validation_applied": validated_meshes,
            "rig_mesh_cleanup": rig_cleanup, "weight_smoothing_iterations": 6,
            "apron_weighted_vertices": apron_vertices,
            "weighting_method": "Authored anatomical nearest-segment weights with an apron/pelvis mask; not automatic production skinning",
            "topology": "Generated triangle topology with welded UV seams; production joint retopology and cloth cleanup remain manual",
            "facial_lipsync": "not_implemented_separate_acceptance"}})


def views(meshes, output, descriptor):
    lo, hi = pipeline.bounds(meshes)
    centre = (lo + hi) / 2
    for obj in meshes:
        for vertex in obj.data.vertices:
            vertex.co -= centre
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = True
    scene.world.color = (.7, .7, .7)
    span = max(hi - lo)
    distance = span * 1.15 / (2 * math.tan(math.radians(10)))
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "PERSP"
    camera.data.angle = math.radians(20)
    scene.camera = camera
    for position in ((2, -3, 4), (-3, 1, 2)):
        bpy.ops.object.light_add(type="AREA", location=tuple(v * span for v in position))
        lamp = bpy.context.object
        lamp.data.energy = 150 * span**2
        lamp.data.size = 4 * span
        lamp.rotation_euler = (-lamp.location).to_track_quat("-Z", "Y").to_euler()
    cameras = {"front": (0, -distance, 0), "left": (distance, 0, 0),
               "back": (0, distance, 0), "right": (-distance, 0, 0)}
    for name, position in cameras.items():
        camera.location = position
        camera.rotation_euler = (-camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(output / (name + ".png"))
        bpy.ops.render.render(write_still=True)
    descriptor.update({"view_input_provenance": "Four renders of the same reviewed single-image Cloud master; controlled multiview pipeline test, not four independent photographs",
                       "fov_degrees": 20, "resolution": [1024, 1024], "camera_positions_z_up": cameras,
                       "shared_object_centre": list(centre), "shared_crop": [0, 0, 1024, 1024]})


def cube(name, centre, size, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        vertex.co = Vector(centre) + Vector((vertex.co.x * size[0], vertex.co.y * size[1], vertex.co.z * size[2]))
    if material:
        obj.data.materials.append(material)
    return obj


def module(meshes, output, descriptor):
    """Dimension the carcass locally; reuse the Cloud facade UVs and wood worktop."""
    if len(meshes) != 1:
        raise ValueError("Review/join the cabinet proxy before modular authoring")
    source = meshes[0]
    lo, hi = pipeline.bounds(meshes)
    dimensions = Vector((.6, .6, 1.0))
    for vertex in source.data.vertices:
        vertex.co = Vector(((vertex.co.x - lo.x) / (hi.x - lo.x) * .6 - .3,
                            (vertex.co.y - lo.y) / (hi.y - lo.y) * .6 - .3,
                            (vertex.co.z - lo.z) / (hi.z - lo.z)))
    source.data.update()
    interior = pipeline.interior_material("Cabinet core")
    interior.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.82, .81, .76, 1)
    visuals, colliders = [], []
    for name, centre, size in (
        ("LeftSide", (-.288, .01, .51), (.024, .54, .92)),
        ("RightSide", (.288, .01, .51), (.024, .54, .92)),
        ("Back", (0, .268, .51), (.552, .024, .92)),
        ("Base", (0, .01, .062), (.552, .54, .024)),
        ("Shelf", (0, .01, .48), (.552, .54, .018)),
        ("TopSupport", (0, .01, .957), (.552, .54, .024)),
    ):
        visuals.append(cube("VIS_" + name, centre, size, interior))
        colliders.append(cube("COL_" + name, centre, size, None))
    top = pipeline.duplicate(source, "VIS_CloudWorktop")
    bm = bmesh.new(); bm.from_mesh(top.data)
    remove = [face for face in bm.faces if face.normal.z < .5 or face.calc_center_median().z < .75]
    bmesh.ops.delete(bm, geom=remove, context="FACES")
    loose = [v for v in bm.verts if not v.link_faces]
    if loose: bmesh.ops.delete(bm, geom=loose, context="VERTS")
    bm.to_mesh(top.data); bm.free()
    if len(top.data.polygons) < 20: raise ValueError("Review the source worktop orientation")
    lo_top, hi_top = pipeline.bounds([top])
    for vertex in top.data.vertices:
        vertex.co.x = (vertex.co.x - lo_top.x) / (hi_top.x - lo_top.x) * .6 - .3
        vertex.co.y = (vertex.co.y - lo_top.y) / (hi_top.y - lo_top.y) * .6 - .3
        vertex.co.z = 1.0
    bm = bmesh.new(); bm.from_mesh(top.data)
    for face in bm.faces:
        face.normal_update()
        if face.normal.z < 0: face.normal_flip()
    bm.to_mesh(top.data); bm.free(); top.data.update()
    pipeline.decimate(top, 1600)
    wood = pipeline.interior_material("Worktop edge")
    wood.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.5, .31, .14, 1)
    visuals.extend([top, cube("VIS_WorktopEdge", (0, 0, .983), (.6, .6, .03), wood)])
    colliders.append(cube("COL_Worktop", (0, 0, .985), (.6, .6, .03), None))

    # The generated facade looked wrinkled. A rigid module uses an authored flat
    # finish and closed backing; preserve the rejected Cloud proxy as provenance.
    facade = cube("VIS_Door", (0, -.283, .515), (.564, .024, .85), interior)
    handle_material = pipeline.interior_material("Cabinet hardware")
    principled = handle_material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (.32, .34, .36, 1)
    principled.inputs["Metallic"].default_value = .8
    handle = cube("VIS_Handle", (.12, -.32, .85), (.14, .018, .012), handle_material)
    pipeline.select_only([facade, handle]); bpy.context.view_layer.objects.active = facade
    bpy.ops.object.join(); facade.name = "VIS_Door"
    visuals.append(facade)
    door_collider = cube("COL_Door", (0, -.295, .515), (.564, .06, .85), None)
    colliders.append(door_collider)
    for obj in visuals + colliders:
        pipeline.decimate(obj, 1000000)
    triangles = sum(pipeline.mesh_stats(obj)["triangles"] for obj in visuals)
    if triangles > descriptor["triangle_budget"]:
        raise ValueError("Authored module exceeds its game budget")
    asset = descriptor["asset_name"]
    pipeline.export_glb(output / (asset + "_game.glb"), visuals)
    pipeline.export_fbx(output / (asset + "_game.fbx"), visuals + colliders)
    bpy.ops.wm.save_as_mainfile(filepath=str(output / (asset + "_module.blend")))
    pipeline.preview(visuals, output / "module_closed.png", 1)
    # Render the actual door assembly around the same hinge exported to Unity.
    from mathutils import Matrix
    hinge = Vector((-.282, -.297, .09))
    turn = Matrix.Translation(hinge) @ Matrix.Rotation(math.radians(-100), 4, "Z") @ Matrix.Translation(-hinge)
    original = [v.co.copy() for v in facade.data.vertices]
    facade.data.transform(turn); facade.data.update()
    for obj in list(bpy.context.scene.objects):
        if obj.type in {"LIGHT", "CAMERA"}: bpy.data.objects.remove(obj, do_unlink=True)
    pipeline.preview(visuals, output / "module_open.png", 1)
    for vertex, position in zip(facade.data.vertices, original): vertex.co = position
    facade.data.update()
    descriptor.update({"intact_fbx": asset + "_game.fbx", "game_triangles": triangles,
        "special_authoring_completed": True, "door_visual": "VIS_Door", "door_collider": "COL_Door",
        "hinge_unity_xyz": [-.282, .09, .297], "size_blender_xyz": list(dimensions),
        "module_authoring": {"nominal_carcass_dimensions_metres": list(dimensions),
            "handle_projection_metres": .035, "enterable_building": False,
            "source_geometry_reused": ["Cloud wood worktop UV surface projected flat"],
            "locally_authored": ["dimensioned hollow carcass and shelf", "flat rigid door and finish replacing wrinkled generated facade", "handle", "separate box colliders"],
            "note": "Cloud cabinet is an appearance proxy; its original dimensions and solid interior are not used as spatial evidence"}})


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", choices=("character", "module", "views"), required=True)
    parser.add_argument("--prepared", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
    started = time.monotonic()
    source = args.prepared.resolve().parent
    output = args.output.resolve()
    descriptor = json.loads(args.prepared.read_text())
    if not descriptor["design_approved"] or descriptor["diagnostic_fixture"]:
        raise ValueError("Use a reviewed production asset as the source")
    fresh_output(output)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    meshes = pipeline.load_source(source / (descriptor["asset_name"] + "_game.glb"))
    if args.mode in {"character", "module"}:
        shutil.copytree(source / "Textures", output / "Textures")
    if args.mode == "character":
        if descriptor["asset_class"] != "character":
            raise ValueError("Character mode requires a character descriptor")
        body_rig(meshes, output, descriptor)
    elif args.mode == "module":
        if descriptor["asset_class"] != "building": raise ValueError("Module mode requires a building descriptor")
        module(meshes, output, descriptor)
    else:
        views(meshes, output, descriptor)
    descriptor["local_authoring_seconds"] = round(time.monotonic() - started, 3)
    (output / ("view_provenance.json" if args.mode == "views" else "unity_asset.json")).write_text(json.dumps(descriptor, indent=2) + "\n")
    print("KITCHEN_VARIANT " + str(output))


if __name__ == "__main__":
    main()

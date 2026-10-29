"""Fit reviewed Pixal3D cookware to the recipe's physical proxies.

No network requests. Preserve downloaded/processed masters; repair floating
fridge debris and author a straight liner/shelves. Blender 5.2, metres, Z-up.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import blender_postprocess as core
import blender_recipe_assets as mechanical

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource/KitchenAssets/Recipe/v003_cookware"
NAMES = ["fridge_body", "fridge_door", "frying_pan", "stockpot", "spatula", "ladle", "gas_hob", "whisk"]


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def source(name):
    asset = "kitchen_" + name
    objects = core.load_source(ROOT / f"ArtSource/KitchenAssets/{asset}/v001/prepared/{asset}_game.glb")
    visual = next(o for o in objects if o.name.startswith("VIS_"))
    for obj in objects:
        if obj != visual:
            bpy.data.objects.remove(obj, do_unlink=True)
    for material in visual.data.materials:
        material.name = "MAT_Cookware_" + name
    visual.name = "VIS_" + name
    return visual


def fit(obj, low, high):
    lo, hi = core.bounds([obj]); low, high = Vector(low), Vector(high)
    scale = Matrix.Diagonal(Vector(tuple((high[i] - low[i]) / (hi[i] - lo[i]) for i in range(3)) + (1,)))
    obj.data.transform(Matrix.Translation(low) @ scale @ Matrix.Translation(-lo)); obj.data.update()


def discard_floating_debris(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    unseen = set(bm.verts); groups = []
    while unseen:
        group = {unseen.pop()}; todo = list(group)
        while todo:
            for edge in todo.pop().link_edges:
                for neighbour in edge.verts:
                    if neighbour in unseen:
                        unseen.remove(neighbour); group.add(neighbour); todo.append(neighbour)
        groups.append(group)
    largest = max(groups, key=len)
    lo = Vector(tuple(min(v.co[i] for v in largest) for i in range(3)))
    hi = Vector(tuple(max(v.co[i] for v in largest) for i in range(3)))
    deleted = []
    for group in groups:
        centre = sum((v.co for v in group), Vector()) / len(group)
        if any(centre[i] < lo[i] - .045 or centre[i] > hi[i] + .045 for i in range(3)):
            deleted.extend(group)
    count = len(deleted)
    if deleted: bmesh.ops.delete(bm, geom=deleted, context="VERTS")
    bm.to_mesh(obj.data); bm.free(); obj.data.update()
    return count


def align_panel(obj):
    coordinates = np.array([tuple(v.co) for v in obj.data.vertices])
    coordinates -= coordinates.mean(axis=0)
    _, axes = np.linalg.eigh(coordinates.T @ coordinates)
    normal = Vector(axes[:, 0].tolist())
    if normal.y < 0: normal.negate()
    yaw = math.atan2(normal.x, normal.y)
    obj.data.transform(Matrix.Rotation(yaw, 4, "Z")); obj.data.update()


def export(name, objects):
    core.export_fbx(OUT / (name + ".fbx"), objects)
    core.export_glb(OUT / (name + ".glb"), objects)
    return {"meshes": [{"name": o.name, **core.mesh_stats(o)} for o in objects],
            "bounds_blender": [list(p) for p in core.bounds(objects)]}


def prepare(name):
    clear(); obj = source(name); notes = []
    if name in ("fridge_body", "fridge_door"):
        notes.append({"removed_floating_vertices": discard_floating_debris(obj)})
    if name == "fridge_door":
        align_panel(obj)
        fit(obj, (0, -.115, .015), (.69, .02, 1.745))
        obj.data.transform(Matrix(((-1,0,0,.69),(0,1,0,0),(0,0,1,0),(0,0,0,1))))
        bm = bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.reverse_faces(bm, faces=list(bm.faces)); bm.to_mesh(obj.data); bm.free(); obj.data.update()
        # The generated rear panel has baked-in dark smears; replace its inner
        # face with a clean, moulded liner while retaining the generated leaf.
        liner = mechanical.material("RealLiner",(.85,.85,.80),0,.36)
        rubber = mechanical.material("RealGasket",(.035,.039,.037),0,.6)
        parts = [obj,mechanical.box("VIS_DoorInnerPanel",(.345,.026,.88),(.64,.014,1.66),liner,.012)]
        for x in (.026,.664): parts.append(mechanical.box("VIS_DoorSeal",(x,.035,.88),(.010,.009,1.66),rubber,.003))
        for z in (.055,1.705): parts.append(mechanical.box("VIS_DoorSeal",(.345,.035,z),(.64,.009,.010),rubber,.003))
        result = export(name,parts); result["cleanup"] = notes+["Generated front/handle retained; clean local rear liner and gasket cover unreconstructable backside texture."]
        return result
    elif name == "fridge_body":
        fit(obj, (-.36, -.33, 0), (.36, .33, 1.81))
        # The generated shelf spacing is illustrative. Replace the cavity with
        # explicit geometry supporting the recipe's registered food positions.
        bm = bmesh.new(); bm.from_mesh(obj.data)
        faces = [f for f in bm.faces if abs(f.calc_center_median().x) < .313 and
                 -.34 < f.calc_center_median().y < .249 and .14 < f.calc_center_median().z < 1.69]
        bmesh.ops.delete(bm, geom=faces, context="FACES")
        bm.to_mesh(obj.data); bm.free(); obj.data.update()
        liner = mechanical.material("RealLiner", (.85, .85, .80), 0, .36)
        shelf = mechanical.material("RealShelf", (.60, .72, .70), .10, .20)
        # Image-to-3D leaves warped broad sheet-metal faces. Retopologise these
        # manufactured planes; keep the generated master as the design source.
        enamel = mechanical.material("RealEnamel", (.80,.81,.77), .12,.27)
        rubber = mechanical.material("RealGasket", (.035,.039,.037), 0,.6)
        steel = mechanical.material("RealVentSteel", (.25,.27,.26), .7,.35)
        bpy.data.objects.remove(obj, do_unlink=True)
        parts = [mechanical.box("VIS_EnamelLeft", (-.34,0,.92), (.04,.66,1.76), enamel,.012),
                 mechanical.box("VIS_EnamelRight", (.34,0,.92), (.04,.66,1.76), enamel,.012),
                 mechanical.box("VIS_EnamelTop", (0,0,1.79), (.68,.66,.04), enamel,.012),
                 mechanical.box("VIS_EnamelBase", (0,0,.09), (.68,.66,.10), enamel,.012),
                 mechanical.box("VIS_EnamelBack", (0,.31,.92), (.64,.04,1.76), enamel,.01),
                 mechanical.box("VIS_LinerBack", (0, .255, .93), (.626, .022, 1.58), liner, .01),
                 mechanical.box("VIS_LinerLeft", (-.317, -.01, .93), (.018, .55, 1.58), liner, .008),
                 mechanical.box("VIS_LinerRight", (.317, -.01, .93), (.018, .55, 1.58), liner, .008),
                 mechanical.box("VIS_LinerBase", (0, -.01, .147), (.626, .55, .018), liner, .008)]
        for x in (-.31,.31): parts.append(mechanical.box("VIS_FrameGasket", (x,-.325,.93),(.012,.01,1.6),rubber,.003))
        for z in (.13,1.73): parts.append(mechanical.box("VIS_FrameGasket",(0,-.325,z),(.62,.01,.012),rubber,.003))
        for index in range(6): parts.append(mechanical.box("VIS_VentSlat",(0,-.337,.045+index*.011),(.59,.012,.005),steel,.002))
        for x in (-.28,.28):
            for y in (-.24,.24): parts.append(mechanical.box("VIS_AdjustableFoot",(x,y,.023),(.055,.055,.046),rubber,.008))
        parts.append(mechanical.box("VIS_InteriorLamp",(0,-.10,1.72),(.12,.08,.009),liner,.004))
        for index, z in enumerate((.62, 1.0, 1.32)):
            parts.append(mechanical.box(f"VIS_Shelf_{index}", (0, -.005, z), (.625, .55, .018), shelf, .003))
            parts.append(mechanical.box(f"VIS_ShelfLip_{index}", (0, -.281, z), (.625, .012, .026), liner, .003))
        notes.append("Generated broad casing faces failed straightness review; rebuilt hard-surface retopology from approved generated design, open liner and three registered shelves. Cloud master retained; runtime casing is locally authored, door is generated mesh.")
        result = export(name, parts); result["cleanup"] = notes; return result
    elif name == "gas_hob":
        obj.data.transform(Matrix.Rotation(-math.pi / 2, 4, "X")); obj.data.update()
        fit(obj, (-.16, -.18, 0), (.16, .18, .055))
    elif name == "frying_pan":
        lo, hi = core.bounds([obj]); diameter = hi.y - lo.y
        # Use the bowl's leftmost tangent plus half its diameter, excluding handle.
        centre = Vector((lo.x + diameter / 2, (lo.y + hi.y) / 2, lo.z))
        scale = .218 / diameter
        obj.data.transform(Matrix.Translation((0,0,.004)) @ Matrix.Diagonal(Vector((scale, scale, .036 / (hi.z-lo.z), 1))) @ Matrix.Translation(-centre))
        obj.data.transform(Matrix.Rotation(math.pi,4,"Z"))
        obj.data.update()
    elif name == "stockpot":
        lo, hi = core.bounds([obj]); scale = .18 / (hi.y - lo.y)
        centre = Vector(((lo.x+hi.x)/2, (lo.y+hi.y)/2, lo.z))
        obj.data.transform(Matrix.Translation((0,0,-.060)) @ Matrix.Diagonal(Vector((scale,scale,.128/(hi.z-lo.z),1))) @ Matrix.Translation(-centre)); obj.data.update()
    elif name in ("spatula", "ladle"):
        lo, hi = core.bounds([obj]); head = .0415 if name == "spatula" else .035
        # Head-centred pivot, handle along Blender -Y / Unity +Z.
        obj.data.transform(Matrix.Rotation(-math.pi/2,4,"Z") @ Matrix.Translation((-(lo.x+head), -(lo.y+hi.y)/2, -lo.z)))
        obj.data.update()
        if name == "spatula":
            lo, hi = core.bounds([obj]); fit(obj, (-.033, -.282, 0), (.033, .0415, .033))
    elif name == "whisk":
        obj.data.transform(Matrix.Rotation(-math.pi/2,4,"Y")); obj.data.update()
        fit(obj, (-.014, -.014, .014), (.014, .014, .29))
    result = export(name, [obj]); result["cleanup"] = notes
    if name in ("fridge_door", "gas_hob", "frying_pan"):
        core.preview([obj], OUT/(name+"_review.png"), 1.81 if name.startswith("fridge") else .4)
    return result


def plumbing():
    clear()
    steel = mechanical.material("RealSinkSteel",(.45,.48,.49),.8,.28)
    chrome = mechanical.material("RealChrome",(.62,.65,.66),.9,.2)
    dark = mechanical.material("RealDrain",(.055,.06,.06),.7,.4)
    # Rolled rim and a curved, open basin. The inside matches the existing
    # registered circular receiver; the renderer is independent of physics.
    profile = [(.016,.008),(.14,.008),(.159,.015),(.175,.06),(.18,.083),(.185,.087),(.192,.083),(.19,.075),(.172,.0),(.016,0)]
    vertices = [(r*math.cos(i*math.tau/80),r*math.sin(i*math.tau/80),z) for r,z in profile for i in range(80)]
    faces = [(j*80+i,(j+1)*80+i,(j+1)*80+(i+1)%80,j*80+(i+1)%80) for j in range(len(profile)-1) for i in range(80)]
    mesh = bpy.data.meshes.new("Basin rolled steel"); mesh.from_pydata(vertices,[],faces); mesh.update()
    obj = bpy.data.objects.new("VIS_RolledSink",mesh); bpy.context.collection.objects.link(obj); mesh.materials.append(steel)
    for face in mesh.polygons: face.use_smooth=True
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.017,depth=.002,location=(0,0,.008))
    drain=bpy.context.object;drain.name="VIS_DrainStrainer";drain.data.materials.append(dark)
    basin = export("sink_basin",[obj,drain])
    clear()
    curve=bpy.data.curves.new("Gooseneck faucet","CURVE");curve.dimensions="3D";curve.resolution_u=16;curve.bevel_depth=.012;curve.bevel_resolution=4
    spline=curve.splines.new("BEZIER");points=[(0,.16,.01),(0,.16,.19),(0,.14,.26),(0,.07,.28),(0,-.02,.28),(0,-.02,.25)]
    spline.bezier_points.add(len(points)-1)
    for point,co in zip(spline.bezier_points,points): point.co=co;point.handle_left_type="AUTO";point.handle_right_type="AUTO"
    faucet=bpy.data.objects.new("VIS_GooseneckTap",curve);bpy.context.collection.objects.link(faucet);curve.materials.append(chrome)
    core.select_only([faucet]);bpy.ops.object.convert(target="MESH")
    bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.026,depth=.009,location=(0,.16,.0045))
    base=bpy.context.object;base.name="VIS_TapBase";base.data.materials.append(chrome)
    tap=export("sink_tap",[faucet,base])
    clear()
    lever=mechanical.box("VIS_TapLever",(0,0,0),(.09,.035,.025),chrome,.01)
    handle=export("tap_lever",[lever])
    return {"sink_basin":basin,"sink_tap":tap,"tap_lever":handle}


def main():
    if OUT.exists() and any(OUT.iterdir()): raise ValueError("Use a fresh source revision")
    OUT.mkdir(parents=True)
    report = {"schema_version":1, "assets":{}}
    for name in NAMES:
        report["assets"][name] = prepare(name)
        asset = "kitchen_"+name
        manifest = json.loads((ROOT/f"ArtSource/KitchenAssets/Manifests/{asset}_v001.json").read_text())
        report["assets"][name]["job_id"] = manifest["generation"]["job_id"]
        report["assets"][name]["master_sha256"] = hashlib.sha256((ROOT/f"ArtSource/KitchenAssets/{asset}/v001/download/master.glb").read_bytes()).hexdigest()
    report["locally_authored_plumbing"] = plumbing()
    (OUT/"visual_provenance.json").write_text(json.dumps(report,indent=2)+"\n")
    print("COOKWARE_VISUALS_READY",len(report["assets"]))


if __name__ == "__main__": main()

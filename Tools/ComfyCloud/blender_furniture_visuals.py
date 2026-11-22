"""Review and fit Pixal3D furniture; retain masters and author mechanical parts.

Cloud table/chair geometry and the lamp's oak collar are retained. Manufactured
fridge panels and the hollow lamp shade are local hard-surface reconstruction
from the user-approved references, not further Cloud generation. Blender Z-up.
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
OUT = ROOT / "ArtSource/KitchenAssets/Recipe/v004_furniture"


def clear():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)


def source(name):
    meshes = core.load_source(ROOT / f"ArtSource/KitchenAssets/furniture_{name}/v001/prepared/furniture_{name}_game.glb")
    visual = next(m for m in meshes if m.name.startswith("VIS_"))
    for mesh in meshes:
        if mesh != visual:
            bpy.data.objects.remove(mesh, do_unlink=True)
    visual.name = "VIS_Cloud_" + name
    for material in visual.data.materials:
        material.name = "MAT_Furniture_Cloud_" + name
    return visual


def fit(obj, low, high):
    lo, hi = core.bounds([obj]); low, high = Vector(low), Vector(high)
    scale = Matrix.Diagonal(Vector(tuple((high[i]-low[i])/(hi[i]-lo[i]) for i in range(3))+(1,)))
    obj.data.transform(Matrix.Translation(low) @ scale @ Matrix.Translation(-lo))
    obj.data.update()


def material(name, colour, metal=0, rough=.45):
    result = mechanical.material(name, colour, metal, rough)
    result.name = "MAT_Furniture_" + name
    return result


def cylinder(name, centre, radius, depth, mat, vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=centre)
    obj=bpy.context.object; obj.name=name; obj.data.materials.append(mat)
    bevel=obj.modifiers.new("Machined edge", "BEVEL"); bevel.width=min(.0015,radius*.15); bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    for face in obj.data.polygons: face.use_smooth=abs(face.normal.z)<.9
    return obj


def lathe(name, profile, mat, segments=96):
    vertices=[(r*math.cos(i*math.tau/segments),r*math.sin(i*math.tau/segments),z)
              for r,z in profile for i in range(segments)]
    faces=[(j*segments+i,j*segments+(i+1)%segments,(j+1)*segments+(i+1)%segments,(j+1)*segments+i)
           for j in range(len(profile)-1) for i in range(segments)]
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces);mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(mat)
    for face in mesh.polygons:face.use_smooth=True
    return obj


def export(name, objects, lod_budget=None):
    original=objects
    # Keep distinct materials while avoiding one draw per screw, rail or slat.
    groups={}
    for obj in objects:
        key=tuple(obj.data.materials)
        groups.setdefault(key,[]).append(obj)
    objects=[]
    for materials,group in groups.items():
        core.select_only(group)
        if len(group)>1:bpy.ops.object.join()
        merged=bpy.context.object
        merged.name="VIS_"+(materials[0].name.replace("MAT_Furniture_","") if materials else name)
        objects.append(merged)
    original[:]=objects
    core.export_fbx(OUT/(name+".fbx"),objects)
    core.export_glb(OUT/(name+".glb"),objects)
    result={"meshes":[{"name":o.name,**core.mesh_stats(o)} for o in objects],
            "bounds_blender":[list(p) for p in core.bounds(objects)]}
    if lod_budget:
        low=[core.duplicate(o,o.name+"_LOD1") for o in objects]
        total=sum(core.mesh_stats(o)["triangles"] for o in low)
        for obj in low:core.decimate(obj,max(12,round(lod_budget*core.mesh_stats(obj)["triangles"]/total)))
        if name.startswith("dining_"):
            for obj in low:
                for v in obj.data.vertices:v.co.z=max(0,v.co.z)
                obj.data.update()
        core.export_fbx(OUT/(name+"_lod1.fbx"),low)
        result["lod1_triangles"]=sum(core.mesh_stats(o)["triangles"] for o in low)
        for obj in low:bpy.data.objects.remove(obj,do_unlink=True)
    return result


def dining(name):
    clear();obj=source(name)
    if name=="dining_table":
        # Image-to-3D retained a camera-like 15 degree pitch. Infer the broad
        # tabletop plane from upward-facing triangle area before calibrating.
        lo,hi=core.bounds([obj])
        candidates=[f for f in obj.data.polygons if f.normal.z>.85 and f.center.z>lo.z+(hi.z-lo.z)*.6]
        normal=sum((f.normal*f.area for f in candidates),Vector()).normalized()
        obj.data.transform(normal.rotation_difference(Vector((0,0,1))).to_matrix().to_4x4())
        obj.data.update()
    points=np.array([tuple(v.co) for v in obj.data.vertices])
    lo,hi=points.min(0),points.max(0)
    top=points[points[:,2]>lo[2]+(hi[2]-lo[2])*.80]
    if name=="dining_table":
        xy=top[:,:2]-top[:,:2].mean(0);_,axes=np.linalg.eigh(xy.T@xy)
        long=axes[:,1];yaw=-math.atan2(long[1],long[0])
    else:
        seat=points[(points[:,2]>.43*(hi[2]-lo[2])+lo[2])&(points[:,2]<.62*(hi[2]-lo[2])+lo[2])]
        back=top[:,:2].mean(0)-seat[:,:2].mean(0)
        yaw=math.pi/2-math.atan2(back[1],back[0])
    obj.data.transform(Matrix.Rotation(yaw,4,"Z"));obj.data.update()
    if name=="dining_table":
        fit(obj,(-.75,-.425,0),(.75,.425,.76))
        # Flatten the usable middle of the generated top without changing the
        # source UV islands, rounded perimeter, apron or leg silhouette.
        flat=[]
        for v in obj.data.vertices:
            if v.co.z>.742 and abs(v.co.x)<.70 and abs(v.co.y)<.375:
                v.co.z=.758;flat.append(v)
        obj.data.update()
    else:
        fit(obj,(-.245,-.265,0),(.245,.265,.83));flat=[]
    # Calibrate all four contacts, not just the single lowest generated foot.
    # The adjustment blends out below the apron/seat, retaining their joinery.
    cap=.64 if name=="dining_table" else .39
    feet={(sx,sy):min(v.co.z for v in obj.data.vertices if v.co.x*sx>0 and v.co.y*sy>0)
          for sx in (-1,1) for sy in (-1,1)}
    for v in obj.data.vertices:
        if v.co.z<cap:
            foot=feet[(1 if v.co.x>0 else -1,1 if v.co.y>0 else -1)]
            v.co.z=max(0,(v.co.z-foot)*cap/(cap-foot))
    obj.data.update()
    result=export(name,[obj],3000)
    result["cleanup"]={"yaw_radians":yaw,"calibrated_size_m":[1.5,.85,.76] if name=="dining_table" else [.49,.53,.83],
                       "flattened_table_centre_vertices":len(flat),"four_foot_contacts_corrected":True,
                       "geometry":"Pixal3D, aligned and calibrated; source loop UV retained"}
    core.preview([obj],OUT/(name+"_review.png"),1.5 if name=="dining_table" else .83)
    return result


def pendant():
    clear();collar=source("pendant_shade")
    lo,hi=core.bounds([collar])
    bm=bmesh.new();bm.from_mesh(collar.data)
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                         plane_co=(0,0,lo.z+(hi.z-lo.z)*.82),plane_no=(0,0,1),clear_inner=True)
    bm.to_mesh(collar.data);bm.free();collar.data.update()
    fit(collar,(-.025,-.025,.165),(.025,.025,.220))
    ivory=material("IvoryEnamel",(.88,.85,.77),.10,.31)
    brass=material("Brass",(.53,.36,.13),.78,.3)
    cable=material("Cable",(.075,.071,.061),0,.8)
    # Hollow spun shade: outer surface, rolled lip, inner surface; opening faces
    # down. Cable, canopy and bulb remain independent from the generated collar.
    outer=[(.029,.171),(.043,.166),(.065,.158),(.09,.147),(.12,.128),(.148,.102),(.17,.075),(.187,.045),(.198,.014),(.2,.004)]
    profile=outer+[(.198,0),(.196,.003)]+[(max(.001,r-.0025),z) for r,z in reversed(outer[:-1])]
    shade=lathe("VIS_SpunIvoryShade",profile,ivory)
    parts=[shade,collar,cylinder("VIS_BrassCollar",(0,0,.166),.029,.008,brass),
           cylinder("VIS_PendantCord",(0,0,.548),.003,.656,cable,16),
           cylinder("VIS_CeilingCanopy",(0,0,.888),.049,.024,ivory)]
    result=export("pendant",parts,2400)
    result["cleanup"]="Raw shade rejected: irregular rim and hallucinated brass loop. Generated oak collar retained; smooth hollow shade, cord and canopy locally reconstructed from approved reference."
    core.preview(parts,OUT/"pendant_review.png",.92)
    return result


def fridge():
    clear()
    enamel=material("FridgeEnamel",(.84,.845,.81),.12,.28)
    liner=material("FridgeLiner",(.88,.89,.85),0,.38)
    steel=material("BrushedSteel",(.47,.50,.51),.85,.28)
    rubber=material("DoorGasket",(.034,.038,.036),0,.65)
    shelf=material("ShelfGlass",(.59,.75,.73),0,.14)
    frost=material("Crisper",(.78,.82,.79),0,.36)
    lamp=material("OpalLamp",(.94,.94,.85),0,.24)
    b=mechanical.box
    body=[b("VIS_EnamelLeft",(-.34,0,.92),(.04,.66,1.76),enamel,.011),
          b("VIS_EnamelRight",(.34,0,.92),(.04,.66,1.76),enamel,.011),
          b("VIS_EnamelTop",(0,0,1.79),(.68,.66,.04),enamel,.009),
          b("VIS_EnamelBase",(0,0,.09),(.68,.66,.10),enamel,.009),
          b("VIS_EnamelBack",(0,.31,.92),(.64,.04,1.76),enamel,.007),
          b("VIS_LinerBack",(0,.255,.93),(.626,.022,1.58),liner,.011),
          b("VIS_LinerLeft",(-.317,-.01,.93),(.018,.55,1.58),liner,.007),
          b("VIS_LinerRight",(.317,-.01,.93),(.018,.55,1.58),liner,.007),
          b("VIS_LinerBase",(0,-.01,.147),(.626,.55,.018),liner,.007),
          b("VIS_InteriorLamp",(0,-.10,1.72),(.23,.08,.012),lamp,.005),
          b("VIS_ThermostatHousing",(.21,.228,1.59),(.15,.028,.10),liner,.012)]
    for z in (.62,1,1.32):
        body += [b("VIS_GlassShelf",(0,-.005,z),(.625,.55,.018),shelf,.003),
                 b("VIS_ShelfFrontTrim",(0,-.281,z),(.625,.012,.026),steel,.003)]
        for x in (-.312,.312):body.append(b("VIS_ShelfRail",(x,-.012,z-.017),(.016,.51,.015),liner,.004))
    # Decorative lower crisper. The existing food storage and supports stay at
    # their registered heights; no new drawer interaction is implied.
    body += [b("VIS_CrisperFloor",(0,-.015,.18),(.59,.48,.012),frost,.004),
             b("VIS_CrisperFront",(0,-.25,.32),(.59,.014,.27),frost,.009),
             b("VIS_CrisperGrip",(0,-.267,.448),(.39,.016,.014),liner,.005)]
    for x in (-.289,.289):body.append(b("VIS_CrisperSide",(x,-.015,.32),(.012,.48,.27),frost,.005))
    for x in (-.31,.31):body.append(b("VIS_FrameGasket",(x,-.325,.93),(.012,.01,1.6),rubber,.003))
    for z in (.13,1.73):body.append(b("VIS_FrameGasket",(0,-.325,z),(.62,.01,.012),rubber,.003))
    body.append(b("VIS_VentRecess",(0,-.331,.06),(.625,.015,.091),rubber,.004))
    for i in range(6):body.append(b("VIS_VentSlat",(0,-.343,.021+i*.014),(.60,.008,.005),steel,.0015))
    for x in (-.28,.28):
        for y in (-.24,.24):body.append(cylinder("VIS_AdjustableFoot",(x,y,.023),.024,.046,rubber,32))
    for z in (.185,.235,.285):
        body.append(b("VIS_LinerVent",(0,.240,z+1.15),(.23,.003,.008),rubber,.002))
    body_result=export("fridge_body",body,3500)
    clear()
    # The existing Unity hinge extends in -X; the FBX bridge mirrors Blender X.
    door=[b("VIS_PrecisionDoor",(.345,-.024,.88),(.686,.078,1.73),enamel,.014),
          b("VIS_DoorLiner",(.345,.023,.88),(.634,.018,1.65),liner,.014)]
    for x in (.028,.662):door.append(b("VIS_AccordionSeal",(x,.041,.88),(.014,.016,1.65),rubber,.004))
    for z in (.058,1.702):door.append(b("VIS_AccordionSeal",(.345,.041,z),(.64,.016,.014),rubber,.004))
    for z in (.37,.90,1.39):
        door += [b("VIS_DoorBinFloor",(.345,.075,z),(.52,.09,.012),liner,.005),
                 b("VIS_DoorBinFront",(.345,.124,z+.043),(.52,.009,.086),frost,.005)]
        for x in (.089,.601):door.append(b("VIS_DoorBinSide",(x,.075,z+.043),(.009,.09,.086),liner,.004))
    handle=cylinder("VIS_TubularHandle",(.571,-.105,1),.012,.65,steel)
    door.append(handle)
    for z in (.708,1.292):
        mount=cylinder("VIS_HandleMount",(.571,-.083,z),.015,.039,steel)
        mount.rotation_euler.x=math.pi/2;door.append(mount)
    for z in (.022,1.733):door.append(b("VIS_HingeCover",(.014,-.025,z),(.033,.06,.025),steel,.006))
    door_result=export("fridge_door",door,2200)
    core.preview(door,OUT/"fridge_door_review.png",1.81)
    return {"body":body_result,"door":door_result,"cleanup":"Cloud closed refrigerator rejected for warped planes and streaked baked shading. All precision casing, door, handle, liner, shelves, trim and bins locally reconstructed from approved reference; original Cloud model/PBR retained for traceability. Existing interaction proxies unchanged."}


def assemble_fridge():
    clear()
    core.load_source(OUT/"fridge_body.glb")
    body=set(bpy.context.scene.objects)
    meshes=core.load_source(OUT/"fridge_door.glb")
    door=[o for o in meshes if o not in body]
    hinge=bpy.data.objects.new("PIVOT_FridgeDoor",None);bpy.context.collection.objects.link(hinge)
    hinge.location=(-.345,-.37,.04)
    for obj in door:obj.parent=hinge
    core.export_glb(OUT/"refrigerator.glb",meshes+[hinge])
    return {"file":"refrigerator.glb","door_hinge_blender":[-.345,-.37,.04],
            "sha256":hashlib.sha256((OUT/"refrigerator.glb").read_bytes()).hexdigest()}


def main():
    if OUT.exists() and any(OUT.iterdir()):raise ValueError("Use a new derivative revision")
    OUT.mkdir(parents=True)
    report={"schema_version":1,"assets":{}}
    for n in ("dining_table","dining_chair"):report["assets"][n]=dining(n)
    report["assets"]["pendant_shade"]=pendant()
    report["assets"]["refrigerator"]=fridge()
    report["assets"]["refrigerator"]["assembled_glb"]=assemble_fridge()
    for name,entry in report["assets"].items():
        root=ROOT/f"ArtSource/KitchenAssets/furniture_{name}/v001"
        manifest=json.loads((ROOT/f"ArtSource/KitchenAssets/Manifests/furniture_{name}_v001.json").read_text())
        entry["job_id"]=manifest["generation"]["job_id"]
        entry["master_sha256"]=hashlib.sha256((root/"download/master.glb").read_bytes()).hexdigest()
    (OUT/"visual_provenance.json").write_text(json.dumps(report,indent=2)+"\n")
    print("FURNITURE_VISUALS_READY",len(report["assets"]))


if __name__=="__main__":main()

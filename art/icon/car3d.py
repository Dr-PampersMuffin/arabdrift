import bpy, json, math, sys, os
from mathutils import Vector, Euler
argv = sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
CAR = argv[0] if argv else "shaheen"
OUT = argv[1] if len(argv) > 1 else "car.png"
YAW = float(argv[2]) if len(argv) > 2 else -35
CAM = argv[3] if len(argv) > 3 else "front"
R = "/home/claude/arabdrift/client/Assets/DriftKuwait/Resources/DriftKuwait/Cars/" + CAR + "/"
meta = json.load(open(R + "car.json"))

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 48; sc.cycles.use_denoising = True
sc.render.resolution_x = sc.render.resolution_y = 1024
sc.render.film_transparent = True
sc.view_settings.view_transform = "AgX"; sc.view_settings.look = "AgX - Medium High Contrast"

bpy.ops.wm.obj_import(filepath=R + "body.obj")
body = bpy.context.selected_objects[0]
bpy.ops.object.shade_smooth()

def img(n, cs="sRGB"):
    i = bpy.data.images.load(R + n + ".png"); i.colorspace_settings.name = cs; return i
m = bpy.data.materials.new("paint"); m.use_nodes = True; nt = m.node_tree
bs = nt.nodes["Principled BSDF"]
ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = img("albedo")
PAINT = tuple(float(x) for x in (argv[4].split(",") if len(argv) > 4 else "0.86,0.85,0.82".split(",")))
tk = nt.nodes.new("ShaderNodeTexImage"); tk.image = img("paintmask", "Non-Color")
sk = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(tk.outputs["Color"], sk.inputs["Color"])
mk = nt.nodes.new("ShaderNodeMath"); mk.operation = "MULTIPLY"; mk.inputs[1].default_value = 0.9; nt.links.new(sk.outputs["Red"], mk.inputs[0])
mixn = nt.nodes.new("ShaderNodeMix"); mixn.data_type = "RGBA"
nt.links.new(mk.outputs[0], mixn.inputs["Factor"]); nt.links.new(ta.outputs["Color"], mixn.inputs[6]); mixn.inputs[7].default_value = PAINT + (1,)
nt.links.new(mixn.outputs[2], bs.inputs["Base Color"])
tm = nt.nodes.new("ShaderNodeTexImage"); tm.image = img("metallic", "Non-Color")
sep = nt.nodes.new("ShaderNodeSeparateColor"); nt.links.new(tm.outputs["Color"], sep.inputs["Color"])
mm = nt.nodes.new("ShaderNodeMath"); mm.operation = "MULTIPLY"; mm.inputs[1].default_value = 0.35
nt.links.new(sep.outputs["Red"], mm.inputs[0]); nt.links.new(mm.outputs[0], bs.inputs["Metallic"])
inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
nt.links.new(tm.outputs["Alpha"], inv.inputs[1]); 
mx = nt.nodes.new("ShaderNodeMath"); mx.operation = "MULTIPLY"; mx.inputs[1].default_value = 0.6
nt.links.new(inv.outputs[0], mx.inputs[0]); nt.links.new(mx.outputs[0], bs.inputs["Roughness"])
tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img("normal", "Non-Color")
nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs["Normal"], bs.inputs["Normal"])
bs.inputs["Coat Weight"].default_value = 0.6; bs.inputs["Coat Roughness"].default_value = 0.05
body.data.materials.clear(); body.data.materials.append(m)

# file (x,y,z) -> blender (x,-z,y)
def P(v): return Vector((v[0], -v[2], v[1]))
car = bpy.data.objects.new("car", None); sc.collection.objects.link(car); body.parent = car

rub = bpy.data.materials.new("rubber"); rub.use_nodes = True
rb = rub.node_tree.nodes["Principled BSDF"]; rb.inputs["Base Color"].default_value = (0.02, 0.02, 0.02, 1); rb.inputs["Roughness"].default_value = 0.8
rim = bpy.data.materials.new("rim"); rim.use_nodes = True
rr = rim.node_tree.nodes["Principled BSDF"]; rr.inputs["Base Color"].default_value = (0.75, 0.75, 0.78, 1); rr.inputs["Metallic"].default_value = 1; rr.inputs["Roughness"].default_value = 0.22
dk = bpy.data.materials.new("rimdark"); dk.use_nodes = True
dd = dk.node_tree.nodes["Principled BSDF"]; dd.inputs["Base Color"].default_value = (0.04, 0.04, 0.045, 1); dd.inputs["Metallic"].default_value = 0.6; dd.inputs["Roughness"].default_value = 0.4
wr, ww = meta["wheel_radius"], meta["wheel_width"]
steer = math.radians(22)
for k, v in meta["wheels"].items():
    w = bpy.data.objects.new("w" + k, None); sc.collection.objects.link(w); w.parent = car
    w.location = P(v)
    if k.startswith("F"): w.rotation_euler = Euler((0, 0, steer))
    side = 1 if v[0] > 0 else -1
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=wr, depth=ww, rotation=(0, math.pi/2, 0))
    t = bpy.context.active_object; t.parent = w; t.location = (0, 0, 0); t.data.materials.append(rub)
    bev = t.modifiers.new("b", "BEVEL"); bev.width = 0.05; bev.segments = 4
    bpy.ops.object.shade_smooth()
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=wr*0.66, depth=0.03, rotation=(0, math.pi/2, 0))
    d = bpy.context.active_object; d.parent = w; d.location = (side*(ww/2+0.004), 0, 0); d.data.materials.append(dk)
    bpy.ops.mesh.primitive_torus_add(major_radius=wr*0.66, minor_radius=0.018, major_segments=64, rotation=(0, math.pi/2, 0))
    lp = bpy.context.active_object; lp.parent = w; lp.location = (side*(ww/2+0.01), 0, 0); lp.data.materials.append(rim)
    for s in range(5):
        a = s * 2*math.pi/5
        bpy.ops.mesh.primitive_cube_add(size=1)
        sp = bpy.context.active_object; sp.parent = w
        sp.scale = (0.03, 0.045, wr*0.62); sp.location = (side*(ww/2+0.02), math.sin(a)*wr*0.3, math.cos(a)*wr*0.3)
        sp.rotation_euler = Euler((-a, 0, 0)); sp.data.materials.append(rim)
    bpy.ops.mesh.primitive_cylinder_add(vertices=24, radius=wr*0.12, depth=0.06, rotation=(0, math.pi/2, 0))
    h = bpy.context.active_object; h.parent = w; h.location = (side*(ww/2+0.03), 0, 0); h.data.materials.append(rim)

car.rotation_euler = Euler((0, math.radians(-2.5), math.radians(YAW)))   # body roll toward the outside

bpy.ops.mesh.primitive_plane_add(size=60); g = bpy.context.active_object; g.is_shadow_catcher = True

w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.42, 0.38, 1); w.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
def light(kind, loc, energy, color, size=1.0, target=(0, 0, 0.6)):
    L = bpy.data.lights.new(kind + str(loc), kind); L.energy = energy; L.color = color
    if kind == "AREA": L.size = size
    if kind == "SUN": L.angle = math.radians(3)
    o = bpy.data.objects.new(L.name, L); sc.collection.objects.link(o); o.location = loc
    d = Vector(target) - Vector(loc); o.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
light("SUN", (6, 9, 4), 4.5, (1.0, 0.72, 0.45))       # low sunset from behind
light("AREA", (-6, -8, 3), 900, (0.75, 0.85, 1.0), 6)  # cool fill from the camera side
light("AREA", (0, 0, 7), 500, (1, 0.95, 0.9), 8)       # top softbox for the roof and bonnet

cam = bpy.data.cameras.new("c"); cam.lens = 34
co = bpy.data.objects.new("c", cam); sc.collection.objects.link(co); sc.camera = co
co.location = (-3.6, -4.0, 1.05) if CAM == "front" else (3.5, -4.1, 0.62)
d = Vector((0.15, 0.0, 0.62)) - co.location; co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
sc.render.filepath = os.path.abspath(OUT)
bpy.ops.render.render(write_still=True)

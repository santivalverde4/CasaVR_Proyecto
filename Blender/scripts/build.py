import bpy, sys, os, json, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import arch
SRC, OUT = sys.argv[1], sys.argv[2]
stage = sys.argv[3] if len(sys.argv) > 3 else "all"
t0 = time.time()
bpy.ops.wm.open_mainfile(filepath=SRC)
M = arch.make_materials()
colls = arch.run(M)
print("arch done", round(time.time()-t0,1))
json.dump(arch.INFO, open(os.path.join(OUT, "arch_info.json"), "w"), indent=1)
if stage in ("all", "furn"):
    import furniture
    furniture.run(M, arch)
    print("furniture done", round(time.time()-t0,1))
    arch.INFO["markers"] = furniture.INFO["markers"]
    json.dump(arch.INFO, open(os.path.join(OUT, "arch_info.json"), "w"), indent=1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "CasaVR_proc.blend"))

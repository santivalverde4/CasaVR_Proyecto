# Plano de verificación: secciones del .blend procesado + arcos de puertas + extras
import bpy, sys, json, math, bmesh
import numpy as np
import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
from matplotlib.patches import Arc, Rectangle, Polygon
bpy.ops.wm.open_mainfile(filepath=sys.argv[1]); out=sys.argv[2]; info=json.load(open(sys.argv[3]))
level=int(sys.argv[4]); zc = 1.0 if level==1 else 4.0; z0 = 0.0 if level==1 else 3.0
dg=bpy.context.evaluated_depsgraph_get()
fig, ax = plt.subplots(figsize=(13,14))
def col(o):
    n=o.name
    for c in o.users_collection:
        if c.name.startswith("Mueble"): return "tab:orange"
    if n.startswith("Muro"): return "black"
    if n.startswith("Puerta"): return "saddlebrown"
    if n.startswith(("Ventana","Moldura")): return "royalblue"
    if n.startswith(("Escalera","Pasillo")): return "green"
    return "gray"
for o in bpy.data.objects:
    if o.type!='MESH': continue
    me=o.evaluated_get(dg).to_mesh(); M=o.matrix_world
    V=np.array([tuple(M@v.co) for v in me.vertices]);
    me.calc_loop_triangles()
    T=np.array([tuple(t.vertices) for t in me.loop_triangles])
    if len(T)==0: continue
    tri=V[T]; c=col(o)
    # furniture: draw footprint (top projection) of items within this level
    isf = c=="tab:orange"
    if isf:
        zmin=V[:,2].min(); 
        if not (z0-0.2 < zmin < z0+2.5): continue
    for t in tri:
        d=t[:,2]-zc
        if isf:
            ax.fill(t[:,0],t[:,1],color=c,alpha=0.15,lw=0)
            continue
        pts=[]
        for i in range(3):
            a,b=t[i],t[(i+1)%3]; da,db=d[i],d[(i+1)%3]
            if (da<0)!=(db<0): s=da/(da-db); pts.append(a+s*(b-a))
        if len(pts)==2: ax.plot([pts[0][0],pts[1][0]],[pts[0][1],pts[1][1]],color=c,lw=0.8)
for dd in info["doors"]:
    h=dd["hinge"]
    if abs(h[2]-z0)>0.5: continue
    op=dd["open"]; lm=dd["leaf_min"]; lx=dd["leaf_max"]
    # closed free edge
    w=dd["w_ax"]; fe=list(h); fe[w]= lm[w] if abs(h[w]-lx[w])<1e-3 else lx[w]
    a0=math.degrees(math.atan2(fe[1]-h[1],fe[0]-h[0])); a1=math.degrees(math.atan2(op[1]-h[1],op[0]-h[0]))
    r=dd["width"]; lo,hi=sorted([a0,a1]); 
    if hi-lo>180: lo,hi=hi,lo+360
    ax.add_patch(Arc((h[0],h[1]),2*r,2*r,theta1=lo,theta2=hi,color="red",lw=1))
    ax.plot([h[0],op[0]],[h[1],op[1]],color="red",lw=1.5)
for e in info.get("markers",[]):
    if abs(e["p"][2]-z0) > 2.6: continue
    ax.plot(e["p"][0],e["p"][1],marker=e.get("m","o"),color=e.get("c","m"),ms=6)
    if e.get("label"): ax.text(e["p"][0]+0.05,e["p"][1]+0.05,e["label"],fontsize=6,color=e.get("c","m"))
ax.set_aspect('equal'); ax.grid(True,alpha=.25); ax.set_xticks(np.arange(-8,4,0.5)); ax.set_yticks(np.arange(-5,7.5,0.5)); ax.tick_params(labelsize=6)
ax.set_xlim(-7.6,3.0); ax.set_ylim(-4.8,7.0); ax.set_title(f"Planta nivel {level} (corte z={zc})")
fig.savefig(out,dpi=85,bbox_inches='tight')

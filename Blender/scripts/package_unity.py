# Genera .meta (GUID deterministas), el .unitypackage de Assets/CasaVR y la carpeta del proyecto completo.
import os, sys, hashlib, tarfile, io, shutil, zipfile

PROJ = sys.argv[1]          # carpeta del proyecto Unity (contiene Assets/)
OUTDIR = sys.argv[2]
ROOT = "Assets/CasaVR"

def guid(path):
    return hashlib.md5(("CasaVR:" + path).encode()).hexdigest()

INPUT_META = """ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData:
  assetBundleName:
  assetBundleVariant:
  script: {fileID: 11500000, guid: 8404be70184654265930450def6a9037, type: 3}
  generateWrapperCode: 0
  wrapperCodePath:
  wrapperClassName:
  wrapperCodeNamespace:
"""
MONO_META = """MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"""
FOLDER_META = """folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def meta_text(rel, is_dir):
    head = f"fileFormatVersion: 2\nguid: {guid(rel)}\n"
    if is_dir:
        return head + FOLDER_META
    if rel.endswith(".cs"):
        return head + MONO_META
    if rel.endswith(".inputactions"):
        return head + INPUT_META
    return head

def write_metas():
    entries = []
    base = os.path.join(PROJ, ROOT)
    for dirpath, dirnames, filenames in os.walk(base):
        dirnames.sort()
        reld = os.path.relpath(dirpath, PROJ).replace(os.sep, "/")
        entries.append((reld, True))
        for f in sorted(filenames):
            if f.endswith(".meta"):
                continue
            entries.append((reld + "/" + f, False))
    # carpeta Assets/CasaVR también necesita meta (está dentro de Assets)
    for rel, is_dir in entries:
        with open(os.path.join(PROJ, rel) + ".meta", "w", newline="\n") as fh:
            fh.write(meta_text(rel, is_dir))
    return entries

def build_unitypackage(entries, out):
    """Mismo formato que los .unitypackage que genera Unity: tar ustar + gzip, y por cada asset una
    entrada de carpeta <guid>/ seguida de asset, asset.meta y pathname (sin la carpeta Unity no lista nada)."""
    import time
    now = int(time.time())
    with tarfile.open(out, "w:gz", format=tarfile.USTAR_FORMAT) as tar:
        def add_dir(name):
            ti = tarfile.TarInfo(name); ti.type = tarfile.DIRTYPE; ti.mode = 0o755; ti.mtime = now
            tar.addfile(ti)
        def add_bytes(name, data):
            ti = tarfile.TarInfo(name); ti.size = len(data); ti.mode = 0o644; ti.mtime = now
            tar.addfile(ti, io.BytesIO(data))
        for rel, is_dir in entries:
            g = guid(rel)
            add_dir(g)
            if not is_dir:
                add_bytes(f"{g}/asset", open(os.path.join(PROJ, rel), "rb").read())
            add_bytes(f"{g}/asset.meta", open(os.path.join(PROJ, rel) + ".meta", "rb").read())
            add_bytes(f"{g}/pathname", rel.encode("utf-8"))

entries = write_metas()
os.makedirs(OUTDIR, exist_ok=True)
pkg = os.path.join(OUTDIR, "CasaVR.unitypackage")
build_unitypackage(entries, pkg)
print("unitypackage:", pkg, len(entries), "entradas", os.path.getsize(pkg) // 1024, "KB")

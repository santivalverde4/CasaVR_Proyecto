import json, uuid, sys
def U(): return str(uuid.uuid4())
def act(name, typ, ctrl=""):
    return {"name": name, "type": typ, "id": U(), "expectedControlType": ctrl, "processors": "", "interactions": "", "initialStateCheck": typ != "Button"}
def b(action, path, proc="", inter=""):
    return {"name": "", "id": U(), "path": path, "interactions": inter, "processors": proc, "groups": "", "action": action, "isComposite": False, "isPartOfComposite": False}
def comp(action, kind, parts, name=None):
    out = [{"name": name or kind, "id": U(), "path": kind, "interactions": "", "processors": "", "groups": "", "action": action, "isComposite": True, "isPartOfComposite": False}]
    for pn, path in parts:
        out.append({"name": pn, "id": U(), "path": path, "interactions": "", "processors": "", "groups": "", "action": action, "isComposite": False, "isPartOfComposite": True})
    return out
def hand(side):
    H = "{%s}" % side
    m = {"name": f"CasaVR {'Izquierda' if side=='LeftHand' else 'Derecha'}", "id": U(), "actions": [], "bindings": []}
    A, B = m["actions"], m["bindings"]
    A += [act("Position", "Value", "Vector3"), act("Rotation", "Value", "Quaternion"), act("Tracking State", "Value", "Integer"),
          act("Select", "Button"), act("Select Value", "Value", "Axis"), act("Activate", "Button"), act("Activate Value", "Value", "Axis"),
          act("UI Press", "Button"), act("UI Press Value", "Value", "Axis"), act("Thumbstick", "Value", "Vector2"), act("Menu", "Button")]
    B += comp("Position", "Vector3Fallback", [("first", f"<XRController>{H}/pointerPosition"), ("second", f"<XRController>{H}/devicePosition")])
    B += comp("Rotation", "QuaternionFallback", [("first", f"<XRController>{H}/pointerRotation"), ("second", f"<XRController>{H}/deviceRotation")])
    B += [b("Tracking State", f"<XRController>{H}/trackingState"),
          b("Select", f"<XRController>{H}/{{GripButton}}"), b("Select", f"<XRController>{H}/{{TriggerButton}}"),
          b("Select Value", f"<XRController>{H}/{{Grip}}"), b("Select Value", f"<XRController>{H}/{{Trigger}}"),
          b("Activate", f"<XRController>{H}/{{TriggerButton}}"), b("Activate Value", f"<XRController>{H}/{{Trigger}}"),
          b("UI Press", f"<XRController>{H}/{{TriggerButton}}"), b("UI Press Value", f"<XRController>{H}/{{Trigger}}"),
          b("Thumbstick", f"<XRController>{H}/{{Primary2DAxis}}", proc="StickDeadzone"),
          b("Menu", f"<XRController>{H}/{{SecondaryButton}}")]
    if side == "LeftHand":
        B.append(b("Menu", f"<XRController>{H}/{{MenuButton}}"))
    return m
head = {"name": "CasaVR Cabeza", "id": U(), "actions": [act("Position", "Value", "Vector3"), act("Rotation", "Value", "Quaternion"), act("Tracking State", "Value", "Integer")],
        "bindings": [b("Position", "<XRHMD>/centerEyePosition"), b("Rotation", "<XRHMD>/centerEyeRotation"), b("Tracking State", "<XRHMD>/trackingState")]}
desk = {"name": "CasaVR Escritorio", "id": U(), "actions": [act("Move", "Value", "Vector2"), act("Look", "Value", "Vector2"), act("Interact", "Button"),
        act("Menu", "Button"), act("Run", "Button"), act("Release Cursor", "Button")], "bindings": []}
desk["actions"][1]["initialStateCheck"] = False
desk["bindings"] += comp("Move", "2DVector", [("up", "<Keyboard>/w"), ("down", "<Keyboard>/s"), ("left", "<Keyboard>/a"), ("right", "<Keyboard>/d")], name="WASD")
desk["bindings"] += comp("Move", "2DVector", [("up", "<Keyboard>/upArrow"), ("down", "<Keyboard>/downArrow"), ("left", "<Keyboard>/leftArrow"), ("right", "<Keyboard>/rightArrow")], name="Flechas")
desk["bindings"] += [b("Look", "<Mouse>/delta"), b("Interact", "<Keyboard>/e"), b("Interact", "<Mouse>/leftButton"),
                     b("Menu", "<Keyboard>/tab"), b("Menu", "<Keyboard>/m"), b("Run", "<Keyboard>/leftShift"), b("Release Cursor", "<Keyboard>/escape")]
asset = {"version": 1, "name": "CasaVR_Controles", "maps": [head, hand("LeftHand"), hand("RightHand"), desk], "controlSchemes": []}
json.dump(asset, open(sys.argv[1], "w"), indent=4)
print("ok")

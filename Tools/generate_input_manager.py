#!/usr/bin/env python3
"""Regenerates ProjectSettings/InputManager.asset with Unity's standard axes plus
per-joystick axes named Spheres_J{joystick}_A{axis} (joysticks 1-8, axes 1-10)
used by Spheres's gamepad input. Run from the repository root."""
import os

def axis(name, type_=0, axis_=0, joy=0, pos="", neg="", altpos="", altneg="",
         gravity=0, dead=0, sens=1, snap=0, invert=0, desc=""):
    return f"""  - serializedVersion: 3
    m_Name: {name}
    descriptiveName: {desc}
    descriptiveNegativeName: 
    negativeButton: {neg}
    positiveButton: {pos}
    altNegativeButton: {altneg}
    altPositiveButton: {altpos}
    gravity: {gravity}
    dead: {dead}
    sensitivity: {sens}
    snap: {snap}
    invert: {invert}
    type: {type_}
    axis: {axis_}
    joyNum: {joy}
"""

axes = [
    axis("Horizontal", neg="left", pos="right", altneg="a", altpos="d", gravity=3, dead=0.001, sens=3, snap=1),
    axis("Vertical", neg="down", pos="up", altneg="s", altpos="w", gravity=3, dead=0.001, sens=3, snap=1),
    axis("Fire1", pos="left ctrl", altpos="mouse 0", gravity=1000, dead=0.001, sens=1000),
    axis("Fire2", pos="left alt", altpos="mouse 1", gravity=1000, dead=0.001, sens=1000),
    axis("Jump", pos="space", gravity=1000, dead=0.001, sens=1000),
    axis("Mouse X", type_=1, axis_=0, sens=0.1),
    axis("Mouse Y", type_=1, axis_=1, sens=0.1),
    axis("Mouse ScrollWheel", type_=1, axis_=2, sens=0.1),
    axis("Submit", pos="return", altpos="joystick button 0", gravity=1000, dead=0.001, sens=1000),
    axis("Cancel", pos="escape", altpos="joystick button 1", gravity=1000, dead=0.001, sens=1000),
]
for j in range(1, 9):
    for a in range(1, 11):
        axes.append(axis(f"Spheres_J{j}_A{a}", type_=2, axis_=a - 1, joy=j, dead=0.05, sens=1))

text = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!13 &1
InputManager:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Axes:
""" + "".join(axes) + "  m_UsePhysicalKeys: 0\n"

path = os.path.join("ProjectSettings", "InputManager.asset")
with open(path, "w", newline="\n") as f:
    f.write(text)
print("wrote", path, len(axes), "axes")

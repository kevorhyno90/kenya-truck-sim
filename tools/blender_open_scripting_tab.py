"""Tiny Blender GUI startup helper: switches the active window straight to the Scripting tab with Material Preview."""
import bpy


def _switch_to_scripting():
    ws = bpy.data.workspaces.get("Scripting")
    if ws is not None:
        for wm in bpy.data.window_managers:
            for win in wm.windows:
                win.workspace = ws
                for area in win.screen.areas:
                    if area.type == 'VIEW_3D':
                        for space in area.spaces:
                            if space.type == 'VIEW_3D':
                                space.shading.type = 'MATERIAL'
                                space.region_3d.view_perspective = 'CAMERA'
    return None  # run once


bpy.app.timers.register(_switch_to_scripting, first_interval=0.2)

import re

file_path = "src/Kst.Acoustics.App/Controls/GeometryPreviewControl.cs"
with open(file_path, "r", encoding="utf-8") as f:
    lines = f.readlines()

def get_block(start_line_idx):
    # returns end_line_idx (inclusive)
    stack = 0
    started = False
    for i in range(start_line_idx, len(lines)):
        line = lines[i]
        if '{' in line:
            stack += line.count('{')
            started = True
        if '}' in line:
            stack -= line.count('}')
        if started and stack == 0:
            return i
    return len(lines) - 1

def extract_methods(method_names):
    extracted_blocks = []
    i = 0
    while i < len(lines):
        line = lines[i]
        matched = False
        for name in method_names:
            # simple match for " name("
            if re.search(r'\b' + name + r'\(', line) and ("private" in line or "protected" in line or "public" in line):
                # find where the method starts (might have attributes or summary above)
                start_idx = i
                while start_idx > 0 and (lines[start_idx-1].strip().startswith("///") or lines[start_idx-1].strip().startswith("[")):
                    start_idx -= 1
                end_idx = get_block(i)
                extracted_blocks.append((start_idx, end_idx))
                matched = True
                break
        if matched:
            i = end_idx + 1
        else:
            i += 1
    
    # Sort and merge blocks if necessary
    extracted_blocks.sort()
    merged = []
    for b in extracted_blocks:
        if not merged:
            merged.append(b)
        else:
            prev = merged[-1]
            if b[0] <= prev[1] + 1:
                merged[-1] = (prev[0], max(prev[1], b[1]))
            else:
                merged.append(b)
    
    # Extract code and delete from lines (in reverse to not mess up indices)
    extracted_code = []
    for start, end in reversed(merged):
        extracted_code.insert(0, "".join(lines[start:end+1]))
        del lines[start:end+1]
        
    return "".join(extracted_code)

input_methods = [
    "OnPointerPressed", "OnPointerExited", "OnPointerMoved", "OnPointerReleased", "OnPointerWheelChanged",
    "SnapPointToBackground", "SnapEdgeDeltaToBackground"
]

render_methods = [
    "Render", "DrawEmptyState", "DrawModeIndicator", "DrawMetricGrid", "DrawNeighborSegments",
    "DrawFlankingPaths", "DrawSelectedWall", "DrawAcikliklar", "DrawBackgroundSegments",
    "DrawKabloSegmentleri", "DrawLeakTestPath", "DrawReceiverHeatMap", "DrawRayPaths",
    "DrawAcousticPoints", "HeatMapColor", "HeatMapStrokeColor", "LerpColor", "GetCategoryColor",
    "ColorFromHsv", "DrawAimingLine", "DrawDiamond", "DrawPointLabel"
]

overlay_methods = [
    "DrawEditableHandles", "DrawOdaEklePreview", "DrawManuelAciklikPreview", "DrawOdaEkleBanner",
    "DrawManuelAciklikBanner", "DrawPlacementOverlay", "DrawColorLegend", "DrawRuler",
    "GetNiceLength", "FormatNumber"
]

input_code = extract_methods(input_methods)
render_code = extract_methods(render_methods)
overlay_code = extract_methods(overlay_methods)

# Make main class partial
for i in range(len(lines)):
    if "public sealed class GeometryPreviewControl" in lines[i]:
        lines[i] = lines[i].replace("public sealed class", "public sealed partial class")
        break

# Write main file
with open(file_path, "w", encoding="utf-8") as f:
    f.writelines(lines)

# Write partial files
header = """using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Kst.Acoustics.App.ViewModels;
using Kst.Acoustics.Core.Electric.BasitProjeCiz.Models;
using Kst.Acoustics.Core.Geometry;
using Kst.Acoustics.Core.Projects;
using Kst.Acoustics.Core.Simulation;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Kst.Acoustics.App.Controls;

public sealed partial class GeometryPreviewControl
{
"""
footer = "\n}\n"

with open("src/Kst.Acoustics.App/Controls/GeometryPreviewControl.Input.cs", "w", encoding="utf-8") as f:
    f.write(header + input_code + footer)

with open("src/Kst.Acoustics.App/Controls/GeometryPreviewControl.Render.cs", "w", encoding="utf-8") as f:
    f.write(header + render_code + footer)

with open("src/Kst.Acoustics.App/Controls/GeometryPreviewControl.Overlay.cs", "w", encoding="utf-8") as f:
    f.write(header + overlay_code + footer)

print("Split completed successfully.")

#!/usr/bin/env python3
"""
STL/3MF Thumbnail Generator for PrintVault 3D
Generates transparent PNG thumbnails from STL and 3MF files.
"""

import sys
import json
import os
import zipfile
import io
from pathlib import Path

try:
    import numpy as np
    from stl import mesh
    import matplotlib
    matplotlib.use('Agg')  # Non-interactive backend
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d import Axes3D
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
except ImportError as e:
    print(json.dumps({
        "success": False,
        "error": f"Missing dependency: {e}. Run: pip install numpy-stl matplotlib"
    }))
    sys.exit(1)


def load_3mf_mesh(file_path: str):
    """
    Load a mesh from a 3MF file.
    3MF files are ZIP archives containing XML and 3D model data.
    Handles multiple namespace formats for compatibility.
    """
    try:
        import xml.etree.ElementTree as ET
        
        # 3MF files are ZIP archives
        with zipfile.ZipFile(file_path, 'r') as zip_file:
            # Look for the 3D model file (usually 3D/3dmodel.model)
            model_files = [f for f in zip_file.namelist() if f.endswith('.model')]
            
            if not model_files:
                raise Exception("No .model file found in 3MF archive")
            
            # Read the model XML
            with zip_file.open(model_files[0]) as xml_file:
                content = xml_file.read()
                root = ET.fromstring(content)
                
                # Extract namespace from root tag
                ns_uri = None
                if root.tag.startswith('{'):
                    ns_uri = root.tag.split('}')[0].strip('{')
                
                # Common 3MF namespaces to try
                possible_namespaces = [
                    ns_uri,
                    'http://schemas.microsoft.com/3dmanufacturing/core/2015/02',
                    'http://schemas.openxmlformats.org/3dmanufacturing/core/2015/02',
                    None  # No namespace
                ]
                
                def find_element(parent, tag, namespaces):
                    """Try to find element with different namespace possibilities."""
                    for ns in namespaces:
                        if ns:
                            elem = parent.find(f'{{{ns}}}{tag}')
                            if elem is not None:
                                return elem
                        else:
                            elem = parent.find(tag)
                            if elem is not None:
                                return elem
                    return None
                
                def find_all_elements(parent, tag, namespaces):
                    """Try to find all elements with different namespace possibilities."""
                    for ns in namespaces:
                        if ns:
                            elems = parent.findall(f'{{{ns}}}{tag}')
                            if elems:
                                return elems
                        else:
                            elems = parent.findall(tag)
                            if elems:
                                return elems
                    return []
                
                # Find resources (objects)
                resources = find_element(root, 'resources', possible_namespaces)
                
                if resources is None:
                    raise Exception("No resources found in 3MF file")

                # Find objects
                objects = find_all_elements(resources, 'object', possible_namespaces)
                
                if not objects:
                    raise Exception("No object found in 3MF file")

                # Find the first object with a mesh (prefer type='model')
                object_node = None
                for obj in objects:
                    if obj.get('type', 'model') == 'model':
                        mesh_check = find_element(obj, 'mesh', possible_namespaces)
                        if mesh_check is not None:
                            object_node = obj
                            break
                
                # Fallback to any object with a mesh
                if object_node is None:
                    for obj in objects:
                        mesh_check = find_element(obj, 'mesh', possible_namespaces)
                        if mesh_check is not None:
                            object_node = obj
                            break
                
                if object_node is None:
                    raise Exception("No object with mesh found in 3MF file")

                mesh_node = find_element(object_node, 'mesh', possible_namespaces)
                if mesh_node is None:
                    raise Exception("No mesh found in 3MF object")

                # Parse vertices
                vertices_node = find_element(mesh_node, 'vertices', possible_namespaces)
                vertices = []
                if vertices_node is not None:
                    vertex_elements = find_all_elements(vertices_node, 'vertex', possible_namespaces)
                    for v in vertex_elements:
                        x = float(v.get('x', 0))
                        y = float(v.get('y', 0))
                        z = float(v.get('z', 0))
                        vertices.append([x, y, z])
                
                if len(vertices) == 0:
                    raise Exception("No vertices found in 3MF mesh")
                
                np_vertices = np.array(vertices)

                # Parse triangles
                triangles_node = find_element(mesh_node, 'triangles', possible_namespaces)
                faces = []
                if triangles_node is not None:
                    triangle_elements = find_all_elements(triangles_node, 'triangle', possible_namespaces)
                    for t in triangle_elements:
                        v1 = int(t.get('v1'))
                        v2 = int(t.get('v2'))
                        v3 = int(t.get('v3'))
                        faces.append([v1, v2, v3])
                
                np_faces = np.array(faces)
                
                if len(np_faces) == 0:
                    raise Exception("No triangles found in 3MF mesh")

                # Create the mesh object
                model_mesh = mesh.Mesh(np.zeros(np_faces.shape[0], dtype=mesh.Mesh.dtype))
                for i, f in enumerate(np_faces):
                    for j in range(3):
                        model_mesh.vectors[i][j] = np_vertices[f[j], :]
                
                return model_mesh

    except zipfile.BadZipFile:
        raise Exception("Invalid 3MF file (not a valid ZIP)")
    except Exception as e:
        raise Exception(f"Failed to parse 3MF file: {str(e)}")




def generate_thumbnail(file_path: str, output_path: str, size: int = 256) -> dict:
    """
    Generate a transparent PNG thumbnail from an STL or 3MF file.
    
    Args:
        file_path: Path to the STL or 3MF file
        output_path: Path for the output PNG file
        size: Size of the thumbnail (width and height)
    
    Returns:
        dict with success status and metadata
    """
    result = {
        "success": False,
        "file_path": file_path,
        "output_path": output_path,
        "error": None,
        "metadata": {}
    }
    
    try:
        # Detect file type
        file_ext = Path(file_path).suffix.lower()
        
        # Load mesh based on file type
        if file_ext == '.stl':
            stl_mesh = mesh.Mesh.from_file(file_path)
        elif file_ext == '.3mf':
            stl_mesh = load_3mf_mesh(file_path)
        else:
            raise Exception(f"Unsupported file type: {file_ext}. Only .stl and .3mf are supported.")
        
        # Get mesh statistics
        min_coords = stl_mesh.min_
        max_coords = stl_mesh.max_
        dimensions = max_coords - min_coords
        
        result["metadata"] = {
            "dimensions": {
                "x": float(dimensions[0]),
                "y": float(dimensions[1]),
                "z": float(dimensions[2])
            },
            "volume": float(stl_mesh.get_mass_properties()[0]) if hasattr(stl_mesh, 'get_mass_properties') else None,
            "triangles": len(stl_mesh.vectors)
        }
        
        # Create figure with transparent background
        fig = plt.figure(figsize=(size/100, size/100), dpi=100)
        ax = fig.add_subplot(111, projection='3d')
        
        # Set transparent background
        fig.patch.set_alpha(0)
        ax.set_facecolor((0, 0, 0, 0))
        ax.patch.set_alpha(0)
        
        # Create polygon collection from mesh
        vectors = stl_mesh.vectors
        
        # Normalize vertices to center the model
        center = (min_coords + max_coords) / 2
        vectors_centered = vectors - center
        
        # Scale to fit
        max_range = np.max(dimensions) / 2
        if max_range > 0:
            vectors_normalized = vectors_centered / max_range
        else:
            vectors_normalized = vectors_centered
        
        # Create the 3D polygon collection
        poly_collection = Poly3DCollection(
            vectors_normalized,
            alpha=0.9,
            facecolor='#39D0D8',  # Cyan accent color matching theme
            edgecolor='#1a6b6e',
            linewidth=0.1
        )
        ax.add_collection3d(poly_collection)
        
        # Set axis limits
        ax.set_xlim([-1, 1])
        ax.set_ylim([-1, 1])
        ax.set_zlim([-1, 1])
        
        # Set viewing angle (isometric-like)
        ax.view_init(elev=25, azim=45)
        
        # Remove axes and grid for clean look
        ax.set_axis_off()
        ax.grid(False)
        
        # Remove margins
        plt.tight_layout(pad=0)
        plt.subplots_adjust(left=0, right=1, top=1, bottom=0)
        
        # Ensure output directory exists
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        
        # Save with transparency
        plt.savefig(
            output_path,
            format='png',
            transparent=True,
            dpi=100,
            bbox_inches='tight',
            pad_inches=0
        )
        plt.close(fig)
        
        result["success"] = True
        
    except Exception as e:
        result["error"] = str(e)
        result["success"] = False
    
    return result


def main():
    """Main entry point for command-line usage."""
    try:
        if len(sys.argv) < 3:
            print(json.dumps({
                "success": False,
                "error": "Usage: python stl_thumbnail.py <file_path> <output_path> [size]"
            }))
            sys.exit(1)
        
        file_path = sys.argv[1]
        output_path = sys.argv[2]
        size = int(sys.argv[3]) if len(sys.argv) > 3 else 256
        
        # Validate input file exists
        if not os.path.exists(file_path):
            print(json.dumps({
                "success": False,
                "error": f"File not found: {file_path}"
            }))
            sys.exit(1)
        
        result = generate_thumbnail(file_path, output_path, size)
        print(json.dumps(result, indent=2))
        
        sys.exit(0 if result["success"] else 1)

    except Exception as e:
        # Critical catch-all
        print(json.dumps({
            "success": False,
            "error": f"Critical script error: {str(e)}",
            "trace": str(e) # simplified trace
        }))
        sys.exit(1)

if __name__ == "__main__":
    main()


#!/usr/bin/env python3
"""
STL/3MF Batch Thumbnail Generator for PrintVault 3D
Processes multiple STL and 3MF files in a single Python process invocation.
Much faster than spawning a new process for each file.
"""

import sys
import json
import os
import zipfile
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor, as_completed

try:
    import numpy as np
    from stl import mesh
    import matplotlib
    matplotlib.use('Agg')  # Non-interactive backend
    import matplotlib.pyplot as plt
    from mpl_toolkits.mplot3d.art3d import Poly3DCollection
except ImportError as e:
    print(json.dumps({
        "success": False,
        "error": f"Missing dependency: {e}. Run: pip install numpy-stl matplotlib",
        "results": []
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
        
        with zipfile.ZipFile(file_path, 'r') as zip_file:
            model_files = [f for f in zip_file.namelist() if f.endswith('.model')]
            
            if not model_files:
                raise Exception("No .model file found in 3MF archive")
            
            with zip_file.open(model_files[0]) as xml_file:
                content = xml_file.read()
                root = ET.fromstring(content)
                
                ns_uri = None
                if root.tag.startswith('{'):
                    ns_uri = root.tag.split('}')[0].strip('{')
                
                possible_namespaces = [
                    ns_uri,
                    'http://schemas.microsoft.com/3dmanufacturing/core/2015/02',
                    'http://schemas.openxmlformats.org/3dmanufacturing/core/2015/02',
                    None
                ]
                
                def find_element(parent, tag, namespaces):
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
                
                resources = find_element(root, 'resources', possible_namespaces)
                if resources is None:
                    raise Exception("No resources found in 3MF file")

                objects = find_all_elements(resources, 'object', possible_namespaces)
                if not objects:
                    raise Exception("No object found in 3MF file")

                object_node = None
                for obj in objects:
                    if obj.get('type', 'model') == 'model':
                        mesh_check = find_element(obj, 'mesh', possible_namespaces)
                        if mesh_check is not None:
                            object_node = obj
                            break
                
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

                model_mesh = mesh.Mesh(np.zeros(np_faces.shape[0], dtype=mesh.Mesh.dtype))
                for i, f in enumerate(np_faces):
                    for j in range(3):
                        model_mesh.vectors[i][j] = np_vertices[f[j], :]
                
                return model_mesh

    except zipfile.BadZipFile:
        raise Exception("Invalid 3MF file (not a valid ZIP)")
    except Exception as e:
        raise Exception(f"Failed to parse 3MF file: {str(e)}")


def generate_single_thumbnail(file_path: str, output_path: str, size: int = 256) -> dict:
    """Generate a single thumbnail from STL or 3MF file."""
    result = {
        "file_path": file_path,
        "output_path": output_path,
        "success": False,
        "error": None
    }
    
    try:
        print(f"Processing: {os.path.basename(file_path)}", file=sys.stderr, flush=True)
        
        # Detect file type and load mesh
        file_ext = Path(file_path).suffix.lower()
        
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
            "triangles": len(stl_mesh.vectors)
        }
        
        # Create figure
        fig = plt.figure(figsize=(size/100, size/100), dpi=100)
        ax = fig.add_subplot(111, projection='3d')
        
        # Set transparent background
        fig.patch.set_alpha(0)
        ax.set_facecolor((0, 0, 0, 0))
        ax.patch.set_alpha(0)
        
        # Normalize and center
        vectors = stl_mesh.vectors
        center = (min_coords + max_coords) / 2
        vectors_centered = vectors - center
        max_range = np.max(dimensions) / 2
        
        if max_range > 0:
            vectors_normalized = vectors_centered / max_range
        else:
            vectors_normalized = vectors_centered
        
        # Create 3D polygon collection
        poly_collection = Poly3DCollection(
            vectors_normalized,
            alpha=0.9,
            facecolor='#39D0D8',
            edgecolor='#1a6b6e',
            linewidth=0.1
        )
        ax.add_collection3d(poly_collection)
        
        # Set axis limits and view
        ax.set_xlim([-1, 1])
        ax.set_ylim([-1, 1])
        ax.set_zlim([-1, 1])
        ax.view_init(elev=25, azim=45)
        ax.set_axis_off()
        ax.grid(False)
        
        plt.tight_layout(pad=0)
        plt.subplots_adjust(left=0, right=1, top=1, bottom=0)
        
        # Ensure output directory exists
        os.makedirs(os.path.dirname(output_path), exist_ok=True)
        
        # Save thumbnail
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
        # Ensure figure is closed on error
        plt.close('all')
    
    return result



def process_batch(jobs: list, max_workers: int = 4, stream: bool = False) -> dict:
    """
    Process multiple thumbnail jobs in parallel.
    
    Args:
        jobs: List of dicts with 'input', 'output', 'size' keys
        max_workers: Number of parallel workers
        stream: If True, prints individual results to stdout as they complete
    
    Returns:
        dict with overall success and list of individual results
    """
    results = []
    success_count = 0
    
    with ProcessPoolExecutor(max_workers=max_workers) as executor:
        future_to_job = {
            executor.submit(
                generate_single_thumbnail,
                job['input'],
                job['output'],
                job.get('size', 256)
            ): job
            for job in jobs
        }
        
        for future in as_completed(future_to_job):
            job = future_to_job[future]
            try:
                result = future.result()
                results.append(result)
                
                if result.get('success', False):
                    success_count += 1
                
                # In stream mode, allow real-time feedback
                if stream:
                    # Print result immediately as a single-line JSON
                    print(json.dumps({"type": "result", "data": result}), flush=True)
                    
            except Exception as e:
                # Handle unexpected future errors with correct context
                err_result = {
                    "success": False, 
                    "error": str(e), 
                    "file_path": job['input'], 
                    "output_path": job['output']
                }
                results.append(err_result)
                if stream:
                    print(json.dumps({"type": "result", "data": err_result}), flush=True)

    summary = {
        "success": True,
        "total": len(jobs),
        "succeeded": success_count,
        "failed": len(jobs) - success_count,
        "results": results
    }
    
    if stream:
        print(json.dumps({"type": "summary", "data": summary}), flush=True)
        
    return summary


def main():
    """
    Main entry point for batch processing.
    """
    
    stream_mode = '--stream' in sys.argv
    # Filter out --stream arg to not confuse other parsers
    argv_clean = [a for a in sys.argv if a != '--stream']
    sys.argv = argv_clean
    
    if len(sys.argv) < 2:
        print(json.dumps({
            "success": False,
            "error": "Usage: python stl_thumbnail_batch.py <input> <output> [size] OR --batch <jobs.json> OR --batch-stdin [--stream]"
        }))
        sys.exit(1)
    
    # Batch mode via JSON file
    if sys.argv[1] == '--batch' and len(sys.argv) >= 3:
        jobs_file = sys.argv[2]
        max_workers = int(sys.argv[3]) if len(sys.argv) > 3 else 4
        
        with open(jobs_file, 'r') as f:
            data = json.load(f)
        
        jobs = data.get('jobs', [])
        result = process_batch(jobs, max_workers, stream_mode)
        # Only print final result if NOT in stream mode (to avoid double printing)
        if not stream_mode:
            print(json.dumps(result))
        sys.exit(0 if result['success'] else 1)
    
    # Batch mode via stdin
    elif sys.argv[1] == '--batch-stdin':
        max_workers = int(sys.argv[2]) if len(sys.argv) > 2 else 4
        
        data = json.load(sys.stdin)
        jobs = data.get('jobs', [])
        result = process_batch(jobs, max_workers, stream_mode)
        if not stream_mode:
            print(json.dumps(result))
        sys.exit(0 if result['success'] else 1)
    
    # Single file mode (backward compatible)
    else:
        if len(sys.argv) < 3:
            print(json.dumps({
                "success": False,
                "error": "Usage: python stl_thumbnail_batch.py <input> <output> [size]"
            }))
            sys.exit(1)
        
        file_path = sys.argv[1]
        output_path = sys.argv[2]
        size = int(sys.argv[3]) if len(sys.argv) > 3 else 256
        
        if not os.path.exists(file_path):
            print(json.dumps({
                "success": False,
                "error": f"File not found: {file_path}"
            }))
            sys.exit(1)
        
        result = generate_single_thumbnail(file_path, output_path, size)
        print(json.dumps(result, indent=2))
        sys.exit(0 if result["success"] else 1)


if __name__ == "__main__":
    main()

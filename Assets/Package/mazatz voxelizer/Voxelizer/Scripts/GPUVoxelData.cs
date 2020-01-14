using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using UnityEngine;

/***************************************************************************************
*    Title: unity-voxel
*    Author: masatatsu nakamura
*    Date: 06 Jan 2019
*    Availability: https://github.com/mattatz/unity-voxel
***************************************************************************************/

namespace VoxelSystem {

	public class GPUVoxelData : System.IDisposable {

		public ComputeBuffer Buffer { get { return buffer; } }

		public int Width { get { return width; } }
		public int Height { get { return height; } }
		public int Depth { get { return depth; } }
		public float UnitLength { get { return unitLength; } }

		int width, height, depth;
		float unitLength;

		ComputeBuffer buffer;
        Voxel_t[] voxels;

		public GPUVoxelData(ComputeBuffer buf, int w, int h, int d, float u) {
			buffer = buf;
			width = w;
			height = h;
			depth = d;
			unitLength = u;
		}

		public Voxel_t[] GetData() {
            // cache
            if(voxels == null) {
			    voxels = new Voxel_t[Buffer.count];
			    Buffer.GetData(voxels);
            }
			return voxels;
		}

		public void Dispose() {
			buffer.Release();
		}

	}

}


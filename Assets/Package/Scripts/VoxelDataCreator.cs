using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;
using System.IO;

public class VoxelDataCreator : MonoBehaviour
{
    [SerializeField]
    ComputeShader voxelizer;

    [SerializeField]
    bool useUV;

    [SerializeField, Range(1, 256)]
    int resolution = 64;

    [SerializeField]
    GameObject _GameObject;

    private Mesh _Mesh;

    //[SerializeField]
    //Texture2D texture;

    private void Start()
    {
        _Mesh = _GameObject.GetComponent<MeshFilter>().sharedMesh;
        //texture = new Texture2D(256, 256);
        //Color[] colors = new Color[256 * 256];
        //texture.SetPixels(colors);

        int maxLengthPow2 = Mathf.NextPowerOfTwo((int)Mathf.Max(_Mesh.bounds.size.x, Mathf.Max(_Mesh.bounds.size.y, _Mesh.bounds.size.z)));
        Vector3 boundsSize = new Vector3(maxLengthPow2, maxLengthPow2, maxLengthPow2);
        Bounds bounds = new Bounds(Vector3.zero, boundsSize);

        //make sure the unit isn't float
        if (resolution > maxLengthPow2) resolution = (int)maxLengthPow2;

        //create voxelData
        VoxelSystem.GPUVoxelData data = VoxelSystem.GPUVoxelizer.Voxelize(
        voxelizer,  // ComputeShader (Voxelizer.compute)
        _Mesh,       // a target mesh
        bounds,     // custom bounds
        resolution, // # of voxels for largest AABB bounds
        false,      // flag to fill in volume or not; if set flag to false, sample a surface only
        true
        );

        VoxelSystem.Voxel_t[] voxels = data.GetData();

        //Make the voxeldata start from 0 instead of -extents
        for(int i = 0; i < voxels.Length; ++i)
        {
            voxels[i].position += bounds.extents;
        }

        //TODO: when constructing drawing the svo on a certain level, the positions are not corrent
        //This is because the position(morton) is the first child that has data right now, while it should be the leftbottomfromt position of the entire voxel
        //So change it so that you don't just have an ignore morton or 0's, but actually calculate the position of each voxel

        List<ulong> mortons;
        EncodeVoxels(voxels, (int)bounds.extents.x, out mortons);

        // need to release a voxel buffer
        data.Dispose();

        //put the data in a svo
        SVOConstructor svoc = GetComponent<SVOConstructor>();
        svoc.Construct(_GameObject.name, voxels.Length, (uint)bounds.extents.x, mortons.ToArray());
    }

    private void EncodeVoxels(VoxelSystem.Voxel_t[] voxels, int boundExtent, out List<ulong> mortons)
    {
        //write mortons to temp array
        ulong m = 0;
        mortons = new List<ulong>();
        foreach (VoxelSystem.Voxel_t v in voxels)
        {
            //can only send 21 bits to the encryptor, so make sure the positions aren't exeeding limit
            if (((uint)v.position.x > 0x1FFFFF) ||
                ((uint)v.position.y > 0x1FFFFF) ||
                ((uint)v.position.z > 0x1FFFFF))
            {
                Debug.LogAssertion("one of the positions is larger than 2097152, try reducing the resolution");
            }

            //ignore defaults
            if (v.position == new Vector3(boundExtent, boundExtent, boundExtent)) continue;

            //Encode the position
            m = Morton.morton3DEncode((uint)v.position.x, (uint)v.position.y, (uint)v.position.z);

            //only use the 63 first bits, the rest is signs and fill indicator
            m &= 0x7FFFFFFFFFFFFFFF;

            //ulong idx = m;

            //set last bit to indicate fill
            m |= (ulong)1 << 63;

            //write 64-bits
            //mortons[idx] = m;
            mortons.Add(m);
        }

        //sort array
        mortons.Sort();
    }

    private void TestReadMortons(int voxelAmount)
    {
        //read mortons, decrypt mortons and compare to original file
        Stream s = File.Open("Assets/VoxelData.bin", FileMode.Open);
        BinaryReader br = new BinaryReader(s);
        List<ulong> mortons = new List<ulong>();
        ulong morton = 1;
        for(int i = 0; i < voxelAmount; ++i)
        {
            morton = br.ReadUInt64();
            mortons.Add(morton);
        }
        br.Dispose();

        StreamWriter w = new StreamWriter("Assets/DecryptedMortons.txt");
        for(int i = 0; i < mortons.Capacity; ++i)
        {
            uint x, y, z;
            Morton.morton3DDecode(mortons[i], out x, out y, out z);
            w.Write($"{x}, {y}, {z}");
            w.Write('\n');
        }
        w.Dispose();

    }
}

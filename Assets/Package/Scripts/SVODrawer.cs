using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.Runtime.InteropServices;

[ImageEffectAllowedInSceneView]
public class SVODrawer : MonoBehaviour
{
    struct voxelPosition
    {
        public voxelPosition(uint x, uint y, uint z)
        {
            _x = x;
            _y = y;
            _z = z;
        }
        public uint _x, _y, _z;
    }

    //The computeshader
    [SerializeField]
    private ComputeShader _Shader;

    //the geometryshader that will create the faces
    [SerializeField]
    private Material _GeomMaterial;

    [SerializeField]
    private string _GameObjectName;

    //buffer for all the positions in the voxel grid (decoded mortons, so 3 * uint)
    private ComputeBuffer _VoxelPositionsBuffer;
    //integers for setting flags to draw faces
    private ComputeBuffer _VoxelFaceFlagsBuffer;
    //buffer with the returnArgs (amount of nodes, )
    private ComputeBuffer _ArgsBuffer;
    //buffer that will be filled with voxels that need to be rendered
    private ComputeBuffer _VoxelsInFrustum;
    //buffer for the frustum vectors
    private ComputeBuffer _FrustumVectorsBuffer;

    //frustum variables
    private Vector3[] _UnitFrustumVectors;

    private SvoParser _Parser;
    private List<voxelPosition> _AllNodePositions;
    private List<uint> _VoxelFaceFlags;

    public void Start()
    {
        //Variables init
        _Parser = new SvoParser();
        _Parser.Init(_GameObjectName);

        _AllNodePositions = new List<voxelPosition>();
        _VoxelFaceFlags = new List<uint>();
        _UnitFrustumVectors = new Vector3[4];

        //Get all childeren and add mortons to list
        List<Node> nodes = new List<Node>();
        _Parser.GetAllNodes(ref nodes, _Parser.GetRootNode());
        foreach(Node n in nodes)
        {
            //if (n._morton == 0) continue;
            uint x, y, z;
            Morton.morton3DDecode(n._morton, out x, out y, out z);

            _AllNodePositions.Add(new voxelPosition(x, y, z));
        }

        CullFaces(nodes);
        //DEBUG(nodes);

        InitBuffers();

        _Parser.Dispose();
    }

    public void CullFaces(List<Node> nodes)
    {
        //for every voxel, set face flags
        //this is a static render, so only check this once on the cpu
        //TODO: do this on a computeShader when converting to dynamic

        foreach(Node node in nodes)
        {
            //check all neighbours
            uint flags = 0;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.North) == null) flags += 1;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.South) == null) flags += 2;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.West) == null) flags += 4;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.East) == null) flags += 8;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.Up) == null) flags += 16;
            if (_Parser.GetNeighbourGreaterEqual(node, SvoParser.Direction.Down) == null) flags += 32;
            _VoxelFaceFlags.Add(flags);
        }
    }

    public void Update()
    {
        //always update frustum bounds
        CalculateFrustumBounds();
        _FrustumVectorsBuffer.SetData(_UnitFrustumVectors);

        //fill the buffer with only voxels in the frustrum
        RunComputeBuffer();
    }

    public void OnApplicationQuit()
    {
        ReleaseBuffers();
    }

    private void OnPostRender()
    {
        if (_ArgsBuffer == null) return;

        _GeomMaterial.SetPass(0);
        _GeomMaterial.SetBuffer("_Verts", _VoxelsInFrustum);

        //copy the counter from amount of voxels to argsBuffer
        ComputeBuffer.CopyCount(_VoxelsInFrustum, _ArgsBuffer, sizeof(int));

        Graphics.DrawProceduralIndirectNow(MeshTopology.Points, _ArgsBuffer);
    }

    private void InitBuffers()
    {
        //create the buffer for all the positions of the voxelGrid
        _VoxelPositionsBuffer = new ComputeBuffer(_AllNodePositions.Count, sizeof(uint) * 3);
        _VoxelPositionsBuffer.SetData(_AllNodePositions.ToArray());

        //reate the buffer for all the voxel positions tht will be inside the frustum
        _VoxelsInFrustum = new ComputeBuffer(_AllNodePositions.Count, sizeof(uint) * 4, ComputeBufferType.Append);
        _VoxelsInFrustum.SetCounterValue(0);

        //create the buffer for the arguments
        _ArgsBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.IndirectArguments);

        //create buffer for shader face culling
        _VoxelFaceFlagsBuffer = new ComputeBuffer(_VoxelFaceFlags.Count, sizeof(uint));
        _VoxelFaceFlagsBuffer.SetData(_VoxelFaceFlags);

        //create the buffer for the frustum data
        _FrustumVectorsBuffer = new ComputeBuffer(4, sizeof(float) * 3);
    }

    private void RunComputeBuffer()
    {
        //set variables and run shader
        int kernelID = 0;
        int threadAmount = Mathf.CeilToInt(Mathf.Pow(_VoxelPositionsBuffer.count, 1.0f / 3.0f));

        _VoxelsInFrustum.SetCounterValue(0);

        _Shader.SetBuffer(kernelID, "_VoxelPositions", _VoxelPositionsBuffer);
        _Shader.SetBuffer(kernelID, "_VoxelsInFrustum", _VoxelsInFrustum);
        _Shader.SetBuffer(kernelID, "_Args", _ArgsBuffer);
        _Shader.SetBuffer(kernelID, "_UnitFrustumVectors", _FrustumVectorsBuffer);
        _Shader.SetBuffer(kernelID, "_FaceFlags", _VoxelFaceFlagsBuffer);

        _Shader.SetInt("_VoxelAmount", _VoxelPositionsBuffer.count);
        _Shader.SetInt("_ThreadAmount", threadAmount);

        _Shader.SetFloat("_FarClipDistance", Camera.main.farClipPlane);
        _Shader.SetFloat("_NearClipDistance", Camera.main.nearClipPlane);

        float[] pos = new float[3];
        pos[0] = Camera.main.transform.position.x;
        pos[1] = Camera.main.transform.position.y;
        pos[2] = Camera.main.transform.position.z;
        _Shader.SetFloats("_CamPos", pos);

        _Shader.Dispatch(kernelID, threadAmount, threadAmount, threadAmount);
    }

    private void ReleaseBuffers()
    {
        _VoxelPositionsBuffer.Release();
        _ArgsBuffer.Release();
        _VoxelsInFrustum.Release();
        _FrustumVectorsBuffer.Release();
    }

    private void CalculateFrustumBounds()
    {
        Camera.main.CalculateFrustumCorners(Camera.main.rect, Camera.main.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, _UnitFrustumVectors);
        for (int i = 0; i < 4; ++i)
        {
            //normalize vectors and make sure they are rotated correctly
            _UnitFrustumVectors[i].Normalize();
            _UnitFrustumVectors[i] = Camera.main.transform.rotation * _UnitFrustumVectors[i];
        }
    }
}

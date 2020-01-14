using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

/*
 * Authors: J. Baert, A. Lagae and Ph. Dutré
 * Source: http://graphics.cs.kuleuven.be/publications/BLD14OCCSVO/BLD14OCCSVO_paper.pdf
*/

public class Node
{
    public ulong _morton;
    public int[] _ChildrenOffsets; //the offset where the children are written in the file, -1 if no child
    public long _ChildrenBase = -1;

    //used in searching for neighbours, isn't passed to file
    public Node _Parent = null;
    public Node[] _Children;
    //public int _Id = 0;

    //public long _Data;

    public Node(ulong morton = 0)
    {
        _morton = morton;
        _ChildrenOffsets = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1};

        _Children = new Node[8];
    }
}

public class SVOConstructor : MonoBehaviour
{
    private List<Node>[] _Buffers;
    //private StreamWriter _Writer;
    private BinaryWriter _BWriter;

    public int _MaxDepth { get; set; }
    private ulong _CurrentMorton = 0;
    private long _CurrentNodePos = 0;

    private int _GridSize = 0;

    public void Construct(string objectName, int gridSize, uint boundExtent, ulong[] data)
    {
        _GridSize = gridSize;

        _MaxDepth = Mathf.CeilToInt(Mathf.Log(_GridSize, 8)) + 1;
        CreateTree(data, boundExtent, objectName);
    }

    private void CreateTree(ulong[] data, uint boundExtent, string objectName)
    {
        //Init buffers
        _Buffers = new List<Node>[_MaxDepth];
        for(int i = 0; i < _MaxDepth; ++i)
        {
            _Buffers[i] = new List<Node>();
        }

        //_Writer = new StreamWriter(File.Open("Assets/Data/TreeNoNewlines.txt", FileMode.OpenOrCreate, FileAccess.Write));
        _BWriter = new BinaryWriter(File.Open($"Assets/Data/{objectName}_Svo.bin", FileMode.OpenOrCreate, FileAccess.Write));

        //build tree, ignore certain morton (see VoxelDataCreator::WriteVoxelData())
        ulong ignoreMorton = Morton.morton3DEncode(boundExtent, boundExtent, boundExtent);
        for (int i = 0; i < data.Length; ++i)
        {
            //ignore empty morton
            if (data[i] == ignoreMorton || data[i] == 0) continue;

            //only get the 63-bits
            data[i] &= 0x7FFFFFFFFFFFFFFF;

            if(_CurrentMorton != data[i])
            {
                AddEmptyVoxels(data[i] - _CurrentMorton);
            }

            Node node = new Node(); //leaf node
            node._morton = data[i];
            _Buffers[_MaxDepth - 1].Add(node);

            RefineBuffers(_MaxDepth - 1);

            _CurrentMorton++;
        }

        //Finalize tree
        FinalizeTree();

        //write header
        WriteHeader(objectName);

        //_Writer.Dispose();
        _BWriter.Dispose();
    }

    private void AddEmptyVoxels(ulong amount)
    {
        ulong r_amount = amount;
        while(r_amount > 0)
        {
            int bufferIndex = (_MaxDepth - (int)Mathf.Log(r_amount, 8)) - 1;
            if (bufferIndex != _MaxDepth) bufferIndex = Mathf.Max(bufferIndex, GetHighestNonEmptyBuffer());
            AddEmptyVoxel(bufferIndex);
            int amount_hit = (int)Mathf.Pow(8, _MaxDepth - bufferIndex - 1);
            r_amount = r_amount - (ulong)amount_hit;
        }
    }

    private int GetHighestNonEmptyBuffer()
    {
        int highestFound = _MaxDepth - 1;
        for(int i = _MaxDepth - 1; i >= 0; --i)
        {
            if(_Buffers[i].Count == 0)
            {
                highestFound--;
            }
            else
            {
                return highestFound;
            }
        }
        return highestFound;
    }

    void AddEmptyVoxel(int bufferIndex)
    {
        _Buffers[bufferIndex].Add(new Node());
        RefineBuffers(bufferIndex);
        _CurrentMorton = _CurrentMorton + (ulong)Mathf.Pow(8, _MaxDepth - bufferIndex - 1);
    }

    private void RefineBuffers(int startDepth)
    {
        for(int d = startDepth; d > 0; d--)
        {
            if(_Buffers[d].Count == 8)
            {
                Debug.Assert(d - 1 >= 0);
                if(IsBufferEmpty(_Buffers[d]))
                {
                    _Buffers[d - 1].Add(new Node());
                }
                else
                {
                    _Buffers[d - 1].Add(GroupNodes(_Buffers[d], d));
                }
                _Buffers[d].Clear();
            }
            else
            {
                break;
            }
        }
    }

    private bool IsBufferEmpty(List<Node> buffer)
    {
        for(int i = 0; i < 8; ++i)
        {
            if (buffer[i]._morton != 0) return false;
        }
        return true;
    }

    private Node GroupNodes(List<Node> buffer, int depth)
    {
        //only get here when buffer at depth is full and the buffers contains at least one node with data

        Node parent = new Node();
        bool first_stored_child = true;

        for (int k = 0; k < 8; ++k)
        {
            //check if empty
            if(buffer[k]._morton != 0)
            {
                if(first_stored_child)
                {
                    parent._ChildrenBase = _CurrentNodePos;
                    _CurrentNodePos++;
                    parent._ChildrenOffsets[k] = 0;
                    first_stored_child = false;

                    //write the baseChild and the offsets
                    WriteNodeBin(buffer[k]);

                    parent._morton = buffer[k]._morton; //make sure morton > 0 so it won't be ignored in further tests
                }
                else
                {
                    parent._ChildrenOffsets[k] = (int)(_CurrentNodePos - parent._ChildrenBase);
                    _CurrentNodePos++;

                    WriteNodeBin(buffer[k]);
                }
            }
            else
            {
                //don't add reference to empty child
                parent._ChildrenOffsets[k] = -1;
            }
        }
        return parent;
    }

    private void WriteNodeBin(Node node)
    {
        _BWriter.Write(node._ChildrenBase); //64-bit child base
        for (int h = 0; h < node._ChildrenOffsets.Length; h++) //8 * 32-bit int offsets
        {
            _BWriter.Write(node._ChildrenOffsets[h]);
        }
        _BWriter.Write(node._morton); //64-bit morton code (needed for boundingbox)
    }

    private void FinalizeTree()
    {
        if(_CurrentMorton < (ulong)_GridSize)
        {
            //fill the rest of the array with ampty nodes
            AddEmptyVoxels(((ulong)_GridSize - _CurrentMorton) /*+ 1*/);
        }

        //write root node
        WriteNodeBin(_Buffers[0][0]);
    }

    private void WriteHeader(string objectName)
    {
        StreamWriter w = new StreamWriter($"Assets/Data/{objectName}_Header.txt");

        w.Write($"GridSize {_GridSize}");

        w.Dispose();
    }

}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;

public class SvoParser
{
    public int _MaxDepth { get; private set; }
    public long _GridSize { get; private set; }

    //get the root node
    private Node _Root;

    BinaryReader _BReader;

    //the size of the data of a node in bytes
    private int _NodeDataSizeBytes = 48;

    public void Init(string fileName)
    {
        //START READER
        _BReader = new BinaryReader(File.Open($"Assets/Data/{fileName}_Svo.bin", FileMode.Open, FileAccess.Read));

        //read the header
        StreamReader r = new StreamReader($"Assets/Data/{fileName}_Header.txt");
        string read = r.ReadLine();
        read = read.Substring(9);
        _GridSize = System.Convert.ToInt64(read);
        r.Dispose();

        _MaxDepth = Mathf.CeilToInt(Mathf.Log(_GridSize, 8)) + 1;

        //Init root
        _Root = new Node();

        FindRootNode();
    }
    public void Dispose()
    {
        _BReader.Dispose();
    }

    private void FindRootNode()
    {
        _BReader.BaseStream.Seek(-_NodeDataSizeBytes, SeekOrigin.End);

        _Root._ChildrenBase = _BReader.ReadInt64();
        for (int i = 0; i < 8; ++i)
        {
            _Root._ChildrenOffsets[i] = _BReader.ReadInt32();
        }
    }
    public Node GetRootNode()
    {
        return _Root;
    }

    //Get node from parent and index (helper functions)
    private long GetChildLocation(Node node, int idx)
    {
        if (idx >= 8)
        {
            Debug.LogError("TESTSVO::GetChild >> Index can't be larger than 7");
            return -1;
        }

        //if the node doesn't have a child at this index, return -1
        if (node._ChildrenOffsets[idx] == -1) return -1;

        //return the position of the child
        return node._ChildrenBase + node._ChildrenOffsets[idx];
    }
    private Node GetNodeLoc(long fileLocation)
    {
        Node newNode = null;
        if (fileLocation != -1)
        {
            //set reader position
            _BReader.BaseStream.Seek(fileLocation * (long)_NodeDataSizeBytes, SeekOrigin.Begin);
            ReadNode(ref newNode);
        }

        return newNode;
    }
    private void ReadNode(ref Node node)
    {
        node = new Node();
        node._ChildrenBase = _BReader.ReadInt64();
        for (int i = 0; i < 8; ++i)
        {
            node._ChildrenOffsets[i] = _BReader.ReadInt32();
        }
        node._morton = _BReader.ReadUInt64();
    }
    //Get node from parent and index
    public Node GetChild(Node parent, int idx)
    {
        long loc = GetChildLocation(parent, idx);
        return GetNodeLoc(loc);
    }

    //Get all the nodes
    public void GetAllNodes(ref List<Node> nodes, Node currentNode)
    {
        for(int i = 0; i < 8; ++i)
        {
            Node newNode = GetChild(currentNode, i);

            if (newNode == null) continue;

            newNode._Parent = currentNode;
            //newNode._Id = i;
            currentNode._Children[i] = newNode;

            if (newNode._ChildrenBase != -1)
            {
                //keep looking
                GetAllNodes(ref nodes, newNode);
            }
            else
            {
                //add
                nodes.Add(newNode);
            }

        }
    }

    public enum Direction
    {
        North,
        East,
        South,
        West,
        Up,
        Down
    }
    public Node GetNeighbourGreaterEqual(Node self, Direction dir)
    {
        Node parentNodeSWD = self._Parent._Children[0];
        Node parentNodeNWD = self._Parent._Children[1];
        Node parentNodeSWU = self._Parent._Children[2];
        Node parentNodeNWU = self._Parent._Children[3];
        Node parentNodeSED = self._Parent._Children[4];
        Node parentNodeNED = self._Parent._Children[5];
        Node parentNodeSEU = self._Parent._Children[6];
        Node parentNodeNEU = self._Parent._Children[7];

        //find neighbour north of node
        if (dir == Direction.North)
        {
            if(self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the south side, the northern neighbour has the same parent
            if (parentNodeSWD != null && parentNodeSWD == self) //down SW nodes
            {
                return parentNodeNWD; //return down NW node
            }
            else if(parentNodeSED != null && parentNodeSED == self) //down SE node
            {
                return parentNodeNED; //return down SE node
            }
            else if (parentNodeSWU != null && parentNodeSWU == self) //same as prev but upper nodes
            {
                return parentNodeNWU;
            }
            else if(parentNodeSEU != null && parentNodeSEU == self)
            {
                return parentNodeNEU;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if(newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            //node is north child, find south nodes from different parent
            if (parentNodeNWD != null && parentNodeNWD == self) //down NW nodes
            {
                return GetChild(newNode, 0);
            }
            else if (parentNodeNED != null && parentNodeNED == self) //down NE node
            {
                return GetChild(newNode, 4);
            }
            else if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return GetChild(newNode, 2);
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return GetChild(newNode, 6);
            }
        }
        else if(dir == Direction.East)
        {
            //same implementation as North direction

            if (self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the west side, the eastern neighbour has the same parent
            if (parentNodeSWD != null && parentNodeSWD == self)
            {
                return parentNodeSED;
            }
            else if (parentNodeNWD != null && parentNodeNWD == self)
            {
                return parentNodeNED;
            }
            else if (parentNodeSWU != null && parentNodeSWU == self)
            {
                return parentNodeSEU;
            }
            else if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return parentNodeNEU;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if (newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            if (parentNodeNED != null && parentNodeNED == self)
            {
                return GetChild(newNode, 1);
            }
            else if (parentNodeSED != null && parentNodeSED == self)
            {
                return GetChild(newNode, 0);
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return GetChild(newNode, 3);
            }
            else if (parentNodeSEU != null && parentNodeSEU == self)
            {
                return GetChild(newNode, 2);
            }
        }
        else if(dir == Direction.South)
        {
            //same implementation as North direction

            if (self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the west side, the eastern neighbour has the same parent
            if (parentNodeNWD != null && parentNodeNWD == self)
            {
                return parentNodeSWD;
            }
            else if (parentNodeNED != null && parentNodeNED == self)
            {
                return parentNodeSED;
            }
            else if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return parentNodeSWU;
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return parentNodeSEU;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if (newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            if (parentNodeSED != null && parentNodeSED == self)
            {
                return GetChild(newNode, 5);
            }
            else if (parentNodeSWD != null && parentNodeSWD == self)
            {
                return GetChild(newNode, 0);
            }
            else if (parentNodeSEU != null && parentNodeSEU == self)
            {
                return GetChild(newNode, 7);
            }
            else if (parentNodeSWU != null && parentNodeSWU == self)
            {
                return GetChild(newNode, 3);
            }
        }
        else if(dir == Direction.West)
        {
            //same implementation as North direction

            if (self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the west side, the eastern neighbour has the same parent
            if (parentNodeSED != null && parentNodeSED == self)
            {
                return parentNodeSWD;
            }
            else if (parentNodeNED != null && parentNodeNED == self)
            {
                return parentNodeNWD;
            }
            else if (parentNodeSEU != null && parentNodeSEU == self)
            {
                return parentNodeSWU;
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return parentNodeNWU;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if (newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            if (parentNodeNWD != null && parentNodeNWD == self)
            {
                return GetChild(newNode, 5);
            }
            else if (parentNodeSWD != null && parentNodeSWD == self)
            {
                return GetChild(newNode, 4);
            }
            else if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return GetChild(newNode, 7);
            }
            else if (parentNodeSWU != null && parentNodeSWU == self)
            {
                return GetChild(newNode, 6);
            }
        }
        else if(dir == Direction.Up)
        {
            //same implementation as North direction

            if (self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the west side, the eastern neighbour has the same parent
            if (parentNodeSED != null && parentNodeSED == self)
            {
                return parentNodeSEU;
            }
            else if (parentNodeNED != null && parentNodeNED == self)
            {
                return parentNodeNEU;
            }
            else if (parentNodeSWD != null && parentNodeSWD == self)
            {
                return parentNodeSWU;
            }
            else if (parentNodeNWD != null && parentNodeNWD == self)
            {
                return parentNodeNWU;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if (newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return GetChild(newNode, 1);
            }
            else if (parentNodeSWU != null && parentNodeSWU == self)
            {
                return GetChild(newNode, 0);
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return GetChild(newNode, 5);
            }
            else if (parentNodeSEU != null && parentNodeSEU == self)
            {
                return GetChild(newNode, 4);
            }
        }
        else if(dir == Direction.Down)
        {
            //same implementation as North direction

            if (self._Parent == null) //reached root
            {
                return null;
            }

            //if this node is on the west side, the eastern neighbour has the same parent
            if (parentNodeSEU != null && parentNodeSEU == self)
            {
                return parentNodeSED;
            }
            else if (parentNodeNEU != null && parentNodeNEU == self)
            {
                return parentNodeNED;
            }
            else if (parentNodeSWU != null && parentNodeSWU == self)
            {
                return parentNodeSWD;
            }
            else if (parentNodeNWU != null && parentNodeNWU == self)
            {
                return parentNodeNWD;
            }

            Node newNode = GetNeighbourGreaterEqual(self._Parent, dir);
            if (newNode == null || newNode._ChildrenBase == -1)
            {
                return newNode;
            }

            if (parentNodeNWD != null && parentNodeNWD == self)
            {
                return GetChild(newNode, 3);
            }
            else if (parentNodeSWD != null && parentNodeSWD == self)
            {
                return GetChild(newNode, 2);
            }
            else if (parentNodeNED != null && parentNodeNED == self)
            {
                return GetChild(newNode, 7);
            }
            else if (parentNodeSED != null && parentNodeSED == self)
            {
                return GetChild(newNode, 6);
            }
        }

        return null;
    }

}
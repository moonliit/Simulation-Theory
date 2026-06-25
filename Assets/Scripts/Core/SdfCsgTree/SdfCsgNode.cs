using UnityEngine;
using System.Collections.Generic;

public enum CSGGroupOp { Union, Intersection, Subtraction }

[ExecuteAlways]
public class SdfCsgNode : MonoBehaviour
{
    [Header("CSG Structural Settings")]
    public bool isGroupNode = false;
    public CSGGroupOp groupOperation = CSGGroupOp.Union;

    [Header("Primitive Reference")]
    public SdfPrimitiveSubscriber primitiveSubscriber;

    private void Reset()
    {
        // Auto-hook up subscriber if it exists on the same object
        if (!isGroupNode && primitiveSubscriber == null)
        {
            primitiveSubscriber = GetComponent<SdfPrimitiveSubscriber>();
        }
    }

    public void GetFlattenedSubtree(List<SdfCsgNode> flatList)
    {
        // Start a deep, recursive search from this transform down through ALL child objects
        TraverseAndCollect(transform, flatList);
    }

    private void TraverseAndCollect(Transform currentTransform, List<SdfCsgNode> flatList)
    {
        // Step 1: Check every immediate child of this transform bone/container
        for (int i = 0; i < currentTransform.childCount; i++)
        {
            Transform child = currentTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            // Step 2: See if this child is actually part of our math tree
            var csgNode = child.GetComponent<SdfCsgNode>();

            if (csgNode != null)
            {
                // If it IS a group node (like a boolean operation block), 
                // let it explore its children first before adding itself (Post-order traversal)
                if (csgNode.isGroupNode)
                {
                    csgNode.TraverseAndCollect(child, flatList);
                    flatList.Add(csgNode);
                }
                else // It's a leaf primitive shape (Cube, Sphere, Capsule)
                {
                    if (csgNode.primitiveSubscriber != null)
                    {
                        flatList.Add(csgNode);
                    }
                }
            }
            else
            {
                // 🚀 THE FIX: This child doesn't have a CSG node component (it's just a raw bone/container).
                // Do not stop! Keep diving deeper down this branch to find nested primitives.
                TraverseAndCollect(child, flatList);
            }
        }
    }
}
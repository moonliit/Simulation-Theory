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
        if (!isGroupNode && primitiveSubscriber == null)
        {
            primitiveSubscriber = GetComponent<SdfPrimitiveSubscriber>();
        }
    }

    public void GetFlattenedSubtree(List<SdfCsgNode> flatList)
    {
        TraverseAndCollect(transform, flatList);
    }

    private void TraverseAndCollect(Transform currentTransform, List<SdfCsgNode> flatList)
    {
        for (int i = 0; i < currentTransform.childCount; i++)
        {
            Transform child = currentTransform.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;

            var csgNode = child.GetComponent<SdfCsgNode>();

            if (csgNode != null)
            {
                if (csgNode.isGroupNode)
                {
                    csgNode.TraverseAndCollect(child, flatList);
                    flatList.Add(csgNode);
                }
                else
                {
                    if (csgNode.primitiveSubscriber != null)
                    {
                        flatList.Add(csgNode);
                    }
                }
            }
            else
            {
                TraverseAndCollect(child, flatList);
            }
        }
    }
}
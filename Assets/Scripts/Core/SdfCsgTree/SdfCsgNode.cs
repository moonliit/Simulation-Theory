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
        if (isGroupNode)
        {
            int validChildrenCount = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var childNode = transform.GetChild(i).GetComponent<SdfCsgNode>();
                if (childNode != null && childNode.gameObject.activeInHierarchy)
                {
                    childNode.GetFlattenedSubtree(flatList);
                    validChildrenCount++;
                }
            }
            if (validChildrenCount > 0) flatList.Add(this);
        }
        else
        {
            if (primitiveSubscriber != null) flatList.Add(this);
        }
    }
}
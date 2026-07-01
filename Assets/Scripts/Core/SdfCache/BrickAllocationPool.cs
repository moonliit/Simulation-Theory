using System.Collections.Generic;
using UnityEngine;

public class BrickAllocationPool
{
    // The explicit global signal indicating a completely empty or solid unallocated brick slice
    public const uint INVALID_BRICK_INDEX = 0;

    private int maxBricks;
    private Queue<uint> freeBrickIndices;
    private HashSet<uint> activeAllocations;

    public BrickAllocationPool(int maxAtlasBricks)
    {
        maxBricks = maxAtlasBricks;
        freeBrickIndices = new Queue<uint>();
        activeAllocations = new HashSet<uint>();

        ResetPool();
    }

    /// <summary>
    /// Flushes all allocations and marks all atlas slots as free.
    /// </summary>
    public void ResetPool()
    {
        freeBrickIndices.Clear();
        activeAllocations.Clear();

        // Index 0 is strictly reserved as our "Dead Voxel / Empty Space" pointer.
        // Any indirection node mapping to 0 will instantly short-circuit out.
        for (uint i = INVALID_BRICK_INDEX + 1; i < maxBricks; i++)
        {
            freeBrickIndices.Enqueue(i);
        }
    }

    /// <summary>
    /// Claims an available 8x8x8 physical brick slot from our texture atlas index chain.
    /// </summary>
    public bool TryAllocate(out uint brickIndex)
    {
        if (freeBrickIndices.Count > 0)
        {
            brickIndex = freeBrickIndices.Dequeue();
            activeAllocations.Add(brickIndex);
            return true;
        }

        // Out of available VRAM texture atlas allocations! 
        brickIndex = INVALID_BRICK_INDEX; 
        return false;
    }

    /// <summary>
    /// Returns a physical block slice index back to the pool, making it available for reuse.
    /// </summary>
    public void Free(uint brickIndex)
    {
        if (brickIndex == INVALID_BRICK_INDEX   ) return; // Never free the empty space pointer

        if (activeAllocations.Contains(brickIndex))
        {
            activeAllocations.Remove(brickIndex);
            freeBrickIndices.Enqueue(brickIndex);
        }
    }

    public int GetAllocatedCount() => activeAllocations.Count;
    public int GetAvailableCount() => freeBrickIndices.Count;
}
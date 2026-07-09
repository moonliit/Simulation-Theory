using UnityEngine;
using System.Collections.Generic;

public class ScarLimiter : MonoBehaviour
{
    public static List<GameObject> activeScars = new List<GameObject>();
    public static int maxScars = 12; 

    void Start()
    {
        activeScars.Add(gameObject);

        if (activeScars.Count > maxScars)
        {
            if (activeScars[0] != null)
                Destroy(activeScars[0]);
            activeScars.RemoveAt(0);
        }
    }

    void OnDestroy()
    {
        activeScars.Remove(gameObject);
    }
}
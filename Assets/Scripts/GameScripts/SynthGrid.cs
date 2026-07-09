using UnityEngine;

public class SynthGrid : MonoBehaviour
{
    [Header("Material de la Cuadrícula")]
    public Material gridMaterial;
    public float scrollSpeed = 0.5f;
    public float colorSpeed = 1f;

    private Color[] synthColors;
    private float timeIndex = 0f;

    void Start()
    {
        // Paleta Synthwave pura
        synthColors = new Color[] { 
            Color.cyan, 
            Color.magenta, 
            new Color(0.5f, 0f, 1f), 
            new Color(1f, 0.4f, 0f) 
        };
    }

    void Update()
    {
        if (gridMaterial == null) return;

        Vector2 offset = gridMaterial.GetTextureOffset("_BaseMap");
        offset.y -= scrollSpeed * Time.deltaTime; 
        gridMaterial.SetTextureOffset("_BaseMap", offset);

        timeIndex += Time.deltaTime * colorSpeed;
        int currentIndex = Mathf.FloorToInt(timeIndex) % synthColors.Length;
        int nextIndex = (currentIndex + 1) % synthColors.Length;
        float transition = timeIndex - Mathf.Floor(timeIndex);

        Color baseColor = Color.Lerp(synthColors[currentIndex], synthColors[nextIndex], transition);
        float neonIntensity = 5f;

        gridMaterial.SetColor("_BaseColor", baseColor * neonIntensity);
    }
}
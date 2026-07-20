using UnityEngine;

public class LightningBeam : MonoBehaviour
{
    [Header("Points")]
    public Transform origin;
    public Transform target;
    
    [SerializeField] private Vector3[] multipliers;
    
    [Header("Shape")]
    [Range(8, 100)]
    public int segmentCount = 16;
    [Range(0f, 1f)]
    public float noiseStrength = 0.25f;
    [Range(1f, 20f)]
    public float noiseSpeed = 10f;

    LineRenderer _lr;
    float _seed;
    
    // [Header("Glow Layer")]
    // public LineRenderer lrGlow;  // drag сюда второй LR
    // public float glowWidth = 0.25f;
    // public float glowAlpha = 0.35f;

    void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = segmentCount;
        _lr.useWorldSpace = true;
        
        _seed = Random.value * 100f;

        _lr.alignment = LineAlignment.TransformZ;

        if (multipliers.Length == 0)
        {
            multipliers = new Vector3[1];
            multipliers[0] = Vector3.one;
        }
        
        // lrGlow.positionCount = segmentCount;
        // lrGlow.useWorldSpace = true;
        // lrGlow.alignment = LineAlignment.TransformZ;
        //
        // // Шире и прозрачнее
        // lrGlow.startWidth = glowWidth;
        // lrGlow.endWidth = glowWidth;
        //
        // lrGlow.startColor = new Color(1f, 0.85f, 0.1f, glowAlpha);
        // lrGlow.endColor   = new Color(1f, 0.85f, 0.1f, glowAlpha);

    }

    void Update()
    {
        if (origin == null || target == null) return;

        // float t = Time.time * noiseSpeed + _seed;
        var positions = new Vector3[segmentCount];

        for (int i = 0; i < segmentCount; i++)
        {
            float frac = i / (float)(segmentCount - 1);
            Vector3 basePos = Vector3.Lerp(origin.position, target.position, frac);
        
            if (i > 0 && i < segmentCount - 1)
            {
                // float envelope = Mathf.Sin(frac * Mathf.PI);
        
                // Синусоида движущаяся во времени
                float wave = 0;
                for (int j = 0; j < multipliers.Length; j++)
                {
                    wave += Mathf.Sin(frac * Mathf.PI * multipliers[j].x - Time.time * noiseSpeed * multipliers[j].y) * multipliers[j].z;
                }
                basePos.y += wave * noiseStrength;
            }
        
            positions[i] = basePos;
        }

        _lr.SetPositions(positions);
        // lrGlow.SetPositions(positions);
    }

    // Вызывай это из своей системы разрушения
    public void Fire(Transform from, Transform to)
    {
        origin = from;
        target = to;
        gameObject.SetActive(true);
    }

    public void Stop()
    {
        gameObject.SetActive(false);
    }
}

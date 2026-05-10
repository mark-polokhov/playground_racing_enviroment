using UnityEngine;

public class CarSplineStats : MonoBehaviour
{
    [SerializeField] private SplineCalculator splineCalculator;
    [SerializeField] private Transform carTransform;
    [SerializeField] private Rigidbody rb;
    private float[] cumulativeLengths;

    void Start()
    {
        splineCalculator = FindObjectOfType<SplineCalculator>();
        carTransform = GetComponent<Transform>();
        rb = GetComponent<Rigidbody>();
        RebuildCumulativeLengths();
    }

    void Update()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null)
            return;
        if (cumulativeLengths == null || cumulativeLengths.Length != splineCalculator.splinePoints.Length)
            RebuildCumulativeLengths();
    }

    /// <summary>
    /// Минимальное расстояние от машины до сплайна
    /// </summary>
    public float GetDistanceToSpline()
    {
        return splineCalculator.GetDistanceToSpline(carTransform.position);
    }

    /// <summary>
    /// Индекс ближайшей точки сплайна
    /// </summary>
    public int GetClosestSplineIndex()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null || splineCalculator.splinePoints.Length == 0)
            return 0;
        float minDistance = float.MaxValue;
        int closestIndex = 0;

        for (int i = 0; i < splineCalculator.splinePoints.Length; i++)
        {
            float distance = Vector3.Distance(carTransform.position, splineCalculator.splinePoints[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    /// <summary>
    /// Процент прогресса вдоль сплайна
    /// </summary>
    public float GetProgressAlongSpline()
    {
        float totalLength = GetTrackTotalLength();
        if (totalLength <= 0f)
            return 0f;
        return GetCumulativeDistanceAlongSpline() / totalLength;
    }

    public float GetCumulativeDistanceAlongSpline()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null || splineCalculator.splinePoints.Length < 2)
            return 0f;
        if (cumulativeLengths == null || cumulativeLengths.Length != splineCalculator.splinePoints.Length)
            RebuildCumulativeLengths();

        int index = GetClosestSplineIndex();
        if (index <= 0)
            return 0f;
        if (index >= splineCalculator.splinePoints.Length - 1)
            return cumulativeLengths[splineCalculator.splinePoints.Length - 1];

        Vector3 a = splineCalculator.splinePoints[index];
        Vector3 b = splineCalculator.splinePoints[index + 1];
        Vector3 ab = b - a;
        float segLen = ab.magnitude;
        if (segLen <= 1e-6f)
            return cumulativeLengths[index];

        float proj = Vector3.Dot(carTransform.position - a, ab.normalized);
        proj = Mathf.Clamp(proj, 0f, segLen);
        return cumulativeLengths[index] + proj;
    }

    /// <summary>
    /// Угол между направлением машины и направлением сплайна
    /// </summary>
    public float GetAngleToSplineDirection()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null || splineCalculator.splinePoints.Length < 2)
            return 0f;
        int index = GetClosestSplineIndex();
        if (index < splineCalculator.splinePoints.Length - 1)
        {
            Vector3 splineDir = (splineCalculator.splinePoints[index + 1] - splineCalculator.splinePoints[index]).normalized;
            float angle = Vector3.SignedAngle(carTransform.forward, splineDir, Vector3.up);
            return angle;
        }
        Vector3 splineDirLast = (splineCalculator.splinePoints[index] - splineCalculator.splinePoints[index - 1]).normalized;
        return Vector3.SignedAngle(carTransform.forward, splineDirLast, Vector3.up);
    }

    public float GetForwardDotSplineTangent()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null)
            return 0f;
        Vector3[] pts = splineCalculator.splinePoints;
        if (pts.Length < 2)
            return 0f;
        int index = GetClosestSplineIndex();
        Vector3 splineDir;
        if (index < pts.Length - 1)
            splineDir = (pts[index + 1] - pts[index]).normalized;
        else if (index > 0)
            splineDir = (pts[index] - pts[index - 1]).normalized;
        else
            return 0f;
        return Vector3.Dot(carTransform.forward, splineDir);
    }

    /// <summary>
    /// Кривизна в ближайшей точке — чем больше угол поворота, тем выше значение
    /// </summary>
    public float GetLocalCurvature()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null || splineCalculator.splinePoints.Length < 3)
            return 0f;
        int i = GetClosestSplineIndex();
        if (i > 0 && i < splineCalculator.splinePoints.Length - 1)
        {
            Vector3 a = splineCalculator.splinePoints[i - 1];
            Vector3 b = splineCalculator.splinePoints[i];
            Vector3 c = splineCalculator.splinePoints[i + 1];
            Vector3 ab = (b - a).normalized;
            Vector3 bc = (c - b).normalized;
            float angleRad = Vector3.SignedAngle(ab, bc, Vector3.up) * Mathf.Deg2Rad;
            float arcLen = Vector3.Distance(a, b) + Vector3.Distance(b, c);
            if (arcLen <= 1e-6f)
                return 0f;
            return angleRad / arcLen;
        }
        return 0f;
    }

    public float GetTrackTotalLength()
    {
        if (cumulativeLengths == null || cumulativeLengths.Length == 0)
            return 0f;
        return cumulativeLengths[cumulativeLengths.Length - 1];
    }

    public Vector2 GetLocalVelocity()
    {
        if (rb == null)
            return Vector2.zero;
        Vector3 localVelocity = carTransform.InverseTransformDirection(rb.velocity);
        return new Vector2(localVelocity.x, localVelocity.z);
    }

    private void RebuildCumulativeLengths()
    {
        if (splineCalculator == null || splineCalculator.splinePoints == null || splineCalculator.splinePoints.Length == 0)
        {
            cumulativeLengths = new float[0];
            return;
        }

        int n = splineCalculator.splinePoints.Length;
        cumulativeLengths = new float[n];
        cumulativeLengths[0] = 0f;
        for (int i = 1; i < n; i++)
        {
            cumulativeLengths[i] = cumulativeLengths[i - 1] + Vector3.Distance(splineCalculator.splinePoints[i - 1], splineCalculator.splinePoints[i]);
        }
    }
}